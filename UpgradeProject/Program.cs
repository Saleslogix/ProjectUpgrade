using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.OleDb;
using System.Linq;
using System.Reflection;
using log4net;
using log4net.Config;
using Sage.Platform.Application;
using Sage.Platform.Orm.Services;
using UpgradeProject.Properties;
using Sage.Platform.FileSystem;
using Sage.Platform.FileSystem.Interfaces;
using Sage.Platform.Orm.Entities;
using Sage.Platform.Upgrade;
using Sage.Platform.Projects;
using Sage.Platform.Projects.Interfaces;
using Sage.Platform.Utility;
using Sage.Platform.VirtualFileSystem;
using IProjectUpgradeService = Sage.Platform.Upgrade.IProjectUpgradeService;
using Sage.SalesLogix.SchemaSupport;

namespace UpgradeProject
{
    class Program
    {
        private static CommandLineArgs _commandLineArgs;
        private static string _connectionString;
        private static IProjectUpgradeService _upgradeService;

        static int Main(string[] args)
        {
            try
            {
                XmlConfigurator.Configure();
                _commandLineArgs = new CommandLineArgs();

                if (args.Length == 0)
                {
                    Console.WriteLine(Resources.Intro + Environment.NewLine);
                }

                if (Parser.ParseArgumentsWithUsage(args, _commandLineArgs, true))
                {
                    Console.WriteLine(Resources.ValidatingArguments);
                    List<string> msgs;

                    InitializeVfsIfNecessary();
                    EnsureUsableFileSystemConfiguration();

                    if (!_commandLineArgs.ValidateArgs(out msgs))
                    {
                        foreach (string msg in msgs)
                        {
                            Console.WriteLine(msg);
                        }
                    }
                    else
                    {
                        PerformOperation();
                        return 0;
                    }
                }
            }
            catch (Exception ex)
            {
                DisplayExceptionAtConsole(ex);
            }
            finally
            {
                Console.WriteLine(Environment.NewLine + "Done.");
            }

            //Console.ReadLine();

            return -1;
        }

        private static void PerformOperation()
        {
            _upgradeService = new ProjectUpgradeService();
            if (_commandLineArgs.Operation == UpgradeOperation.AddProject)
                PerformAddProjectOperation();
            if (_commandLineArgs.Operation == UpgradeOperation.AddBundle)
                PerformAddBundleOperation();
            else if (_commandLineArgs.Operation == UpgradeOperation.IdentifyProjectVersion)
                PerformIdentifyProjectVersionOperation();
            else if (_commandLineArgs.Operation == UpgradeOperation.UpgradeReport)
                PerformUpgradeReport();
            else if (_commandLineArgs.Operation == UpgradeOperation.Upgrade)
                PerformUpgrade();
            else if (_commandLineArgs.Operation == UpgradeOperation.BuildBaseProject)
                PerformBuildBaseProject();
        }

        private static void InitializeVfsIfNecessary()
        {
            if (!_commandLineArgs.SourcePath.StartsWith("VFS:", StringComparison.InvariantCultureIgnoreCase))
                return;

            Console.WriteLine("Retrieving connection string...");
            _connectionString = TryGettingNativeConnectionString();
            if (string.IsNullOrEmpty(_connectionString))
            {
                _connectionString = TryGettingConnectionStringFromConfig();
            }

            if (string.IsNullOrEmpty(_connectionString))
            {
                Console.WriteLine("ConnectionString must be set in .config file or Server, Database, UserName, and Password must be passed on command line.");
                Environment.Exit(-1);
            }

            VFSQuery.ConnectToVFS(_connectionString);
        }

        private static string TryGettingConnectionStringFromConfig()
        {
            var conStrings = ConfigurationManager.ConnectionStrings;
            ConnectionStringSettings firstConnection = null;
            if (conStrings != null)
                firstConnection = conStrings.Cast<ConnectionStringSettings>().FirstOrDefault();

            if (firstConnection != null)
            {
                Console.WriteLine("Using explicit connection string: " + firstConnection.ConnectionString);
                return firstConnection.ConnectionString;
            }

            return null;
        }

        private static string TryGettingNativeConnectionString()
        {
            if (string.IsNullOrEmpty(_commandLineArgs.Password))
                return null;

            string conStringTemplate = ConfigurationManager.AppSettings["NativeConStringTemplate"];
            if (string.IsNullOrEmpty(conStringTemplate))
                return null;

            string connectionString = string.Format(conStringTemplate,
                                                                    _commandLineArgs.Server,
                                                                    _commandLineArgs.Database,
                                                                    _commandLineArgs.UserName,
                                                                    _commandLineArgs.Password);
            Console.WriteLine("Using native connection string: " + connectionString);
            return connectionString;
        }

        /// <summary>
        /// Changes FileSystemConfiguration to get filesystemtypes from current assembly in case assemblies are ILMerged.
        /// </summary>
        private static void EnsureUsableFileSystemConfiguration()
        {
            try
            {
                Assembly.Load("Sage.Platform.VirtualFileSystem");
            }
            catch (Exception)
            {
                //Assume all assemblies have been ILMerged together so a different filesystem config must be used
                var fileSystemTypeNames = new List<string>
                                              {
                                                  "Sage.Platform.FileSystem.Disk.DiskFileSystem",
                                                  "Sage.Platform.FileSystem.ZIP.ZipFileSystem",
                                                  "Sage.Platform.VirtualFileSystem.VFS.VFSFileSystem"
                                              };

                //Call private static method through reflection to initialize file system.
                //This is to avoid formally exposing a method to initialize the file system api just for this case.
                try
                {
                    Type fsType = Type.GetType("Sage.Platform.FileSystem.FileSystem");
                    MethodInfo initMethod = fsType.GetMethod("InitializeFileSystem", BindingFlags.Static | BindingFlags.NonPublic);
                    initMethod.Invoke(null, new object[] { fileSystemTypeNames });
                }
                catch (Exception e)
                {
                    throw new Exception("Could not initialize file system api.", e);
                }
            }
        }

        private static void PerformAddProjectOperation()
        {
            Console.WriteLine("Adding files from {0} to repository as version {1}", _commandLineArgs.SourcePath, _commandLineArgs.Version);
            _upgradeService.AddFileProgress += (sender, e) =>
            {
                Console.Write("\r{0} percent uploaded...   ", e.PercentComplete);
            };
            _upgradeService.AddProjectToRepository(_commandLineArgs.SourcePath,
                                                      new Version(_commandLineArgs.Version),
                                                      _commandLineArgs.ProjectName);
        }

        private static void PerformAddBundleOperation()
        {
            Console.WriteLine("Adding files from {0} to repository as version {1}", _commandLineArgs.SourcePath, _commandLineArgs.Version);
            _upgradeService.AddFileProgress += (sender, e) =>
            {
                Console.Write("\r{0} percent uploaded...   ", e.PercentComplete);
            };
            _upgradeService.AddBundleToRepository(_commandLineArgs.SourcePath, new Version(_commandLineArgs.Version));
        }

        private static IEnumerable<IModelUpgradeService> InitializeModelUpgradeServices(IEnumerable<IModel> models)
        {
            return new List<IModelUpgradeService>
                       {
                           new OrmModelUpgradeService(),
                           new PortalModelUpgradeService(),
                           new QuickFormModelUpgradeService()
                       };
        }

        private static void DisplayExceptionAtConsole(Exception ex)
        {
            Exception baseEx = ex.GetBaseException();
            Console.WriteLine(Resources.ErrorSource, baseEx.Source);
            Console.WriteLine(Resources.ErrorMessage, ex.Message);
            Console.WriteLine(Resources.ErrorMessage, baseEx.Message);
            Console.WriteLine(baseEx.StackTrace);
        }

        //////
        
        private static void PerformIdentifyProjectVersionOperation()
        {
            string dbVersion = GetDbVersion();
            if (!string.IsNullOrEmpty(dbVersion))
                _upgradeService.Log.Info(string.Format("Database Version: {0}", dbVersion));
            
            ProjectInstallInfo versionInfo = GetProjectVersionInfo();                                 
        }

        private static ProjectInstallInfo GetProjectVersionInfo()
        {
            ProjectInstallInfo versionInfo = null;
            try
            {
                versionInfo = _upgradeService.IdentifyProjectVersion(_commandLineArgs.SourcePath);
            }
            catch (InvalidProjectException exc)
            {
                _upgradeService.Log.Error(exc.Message);
                _upgradeService.Log.Error("Confirm that your source path is correct.");
                Environment.Exit(-1);
            }
            return versionInfo;
        }

        private static IProject CreateProjectFromPath(string path)
        {
            IDriveInfo projectDrive = FileSystem.GetDriveFromPath(path);
            return new Project(projectDrive, new ModelTypeCollection { new ModelType(typeof(OrmModel)) });
        }

        private static void PerformUpgradeReport()
        {
            IProject baseProject = CreateProjectFromPath(_commandLineArgs.BasePath);
            IProject sourceProject = CreateProjectFromPath(_commandLineArgs.SourcePath);

            ((ProjectUpgradeService) _upgradeService).GetModelUpgradeServices = InitializeModelUpgradeServices;
            
            UpgradeReport report = _upgradeService.AnalyzeForUpgrade(sourceProject, baseProject);
        }

        private static void PerformUpgrade()
        {
            IProject baseProject = CreateProjectFromPath(_commandLineArgs.BasePath);
            IProject sourceProject = CreateProjectFromPath(_commandLineArgs.SourcePath);
            IProject targetProject = CreateProjectFromPath(_commandLineArgs.TargetPath);

            ((ProjectUpgradeService)_upgradeService).GetModelUpgradeServices = InitializeModelUpgradeServices;

            UpgradeReport report = _upgradeService.Upgrade(sourceProject, baseProject, targetProject);
        }

        private static void PerformBuildBaseProject()
        {
            _upgradeService.Log.Info("Identifying project version and bundles installed.");

            ApplicationContext.Initialize("SalesLogix");
            var dummySchemaService = new DbSchemaCreationService(null, null);
            ApplicationContext.Current.Services.Add(typeof(IDbSchemaCreationService), dummySchemaService);

            IDirectoryInfo repoDir = GetRepositoryDirectory();

            string basePath = _commandLineArgs.BasePath;
            if (!basePath.EndsWith("\\Model", StringComparison.OrdinalIgnoreCase))
                basePath = System.IO.Path.Combine(basePath, "Model");

            ProjectInstallInfo versionInfo = GetProjectVersionInfo();

            
            _upgradeService.BuildBaseProject(versionInfo, repoDir, basePath);
        }

        private static IDirectoryInfo GetRepositoryDirectory()
        {
            string repoPath = ConfigurationManager.AppSettings["ReleaseRepositoryPath"];
            if (string.IsNullOrEmpty(repoPath))
            {
                _upgradeService.Log.Error("ReleaseRepositoryPath is not set in the .config file.");
                Environment.Exit(-1);
            }

            IDirectoryInfo repoDir = CommandLineArgs.GetDirectoryInfo(repoPath);
            if (repoDir == null)
            {
                _upgradeService.Log.Error("ReleaseRepositoryPath is not a valid directory.");
                Environment.Exit(-1);
            }
            if (!repoDir.Exists)
            {
                _upgradeService.Log.Error("ReleaseRepositoryPath does not exist.");
                Environment.Exit(-1);
            }

            return repoDir;
        }

        private static string GetDbVersion()
        {
            if (string.IsNullOrEmpty(_connectionString))
                return null;

            using (var con = new OleDbConnection(_connectionString))
            using (var cmd = con.CreateCommand())
            {
                con.Open();
                cmd.CommandText = "SELECT DBVERSION FROM SYSTEMINFO WHERE SYSTEMINFOID = 'PRIMARY'";
                return (string)cmd.ExecuteScalar();
            }
        }        
    }
}
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Data.SQLite;
using System.Xml.Linq;
using log4net;
using Sage.Platform.BundleModel;
using Sage.Platform.Data;
using Sage.Platform.FileSystem.Interfaces;
using Sage.Platform.Projects;
using Sage.Platform.Projects.Interfaces;
using Sage.Platform.Application;

namespace Sage.Platform.Upgrade
{
    public class ProjectUpgradeService : IProjectUpgradeService
    {
        internal static string DefaultDbFileName = "ProjectReleaseInfo.db";
        internal static string FileReleaseDbConnectionString = "Data Source=\"{0}\";Version=3;FailIfMissing=False;";
        private Regex _fileIgnoreRegex;
        protected Dictionary<int, BundleInfo> _registeredBundles;
        protected Dictionary<int, RegisteredProjectInfo> _registeredProjects;
        private readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public ILog Log
        {
            get { return _log; }
        }

        public ProjectUpgradeService()
        {
            var pathsToIgnore = new string[] //as regular expressions
                                    {
                                        @"\\modelindex\.xml",
                                        @"\\project\.info\.xml",
                                        @"\\Bundle Manifests\\",
                                        @"\\deployment\\",
                                        @"\\bundleData\.xml",
                                        @"\\manifest\.xml",
                                        @"^.*\\\.svn.*",
                                        @"^.*\\\.git\\.*"
                                    };
            _fileIgnoreRegex = new Regex(string.Join("|", pathsToIgnore));
        }

        internal virtual Dictionary<int, BundleInfo> RegisteredBundles
        {
            get
            {
                if (_registeredBundles == null)
                    _registeredBundles = LoadRegisteredBundles();

                return _registeredBundles;
            }
        }

        internal virtual Dictionary<int, RegisteredProjectInfo> RegisteredProjects
        {
            get
            {
                if (_registeredProjects == null)
                    _registeredProjects = LoadRegisteredProjects();

                return _registeredProjects;
            }
        }

        internal Dictionary<int, BundleInfo> LoadRegisteredBundles()
        {
            using (var connection = GetOpenConnection())
            {
                const string selectSQl = "SELECT ID, FILENAME, NAME, MAJORVERSION, MINORVERSION, BUILD, REVISION FROM BUNDLE";
                var command = connection.CreateCommand();
                command.CommandText = selectSQl;
                var reader = command.ExecuteReader();

                return reader.ReadRows().Select(row => new
                {
                    Id = Convert.ToInt32(row[0]),
                    Bundle = new BundleInfo(
                            row[2].ToString(),
                            row[1].ToString(),
                            new Version(Convert.ToInt32(row[3]),
                            Convert.ToInt32(row[4]),
                            Convert.ToInt32(row[5]),
                            Convert.ToInt32(row[6])))
                })
                    .ToDictionary(item => item.Id, item => item.Bundle);
            }
        }

        internal Dictionary<int, RegisteredProjectInfo> LoadRegisteredProjects()
        {
            using (var connection = GetOpenConnection())
            {
                const string selectSQl = "SELECT ID, NAME, BACKUPFILENAME, MAJORVERSION, MINORVERSION, BUILD, REVISION FROM PROJECT";
                var command = connection.CreateCommand();
                command.CommandText = selectSQl;
                var reader = command.ExecuteReader();

                return reader.ReadRows().Select(row => new
                {
                    Id = Convert.ToInt32(row[0]),
                    Project = new RegisteredProjectInfo(
                            row[1].ToString(),
                            row[2].ToString(),
                            new Version(Convert.ToInt32(row[3]),
                            Convert.ToInt32(row[4]),
                            Convert.ToInt32(row[5]),
                            Convert.ToInt32(row[6])))
                })
                    .ToDictionary(item => item.Id, item => item.Project);
            }
        }

        public void AddProjectToRepository(string projectPath, Version version, string projectName)
        {
            IDriveInfo drive = FileSystem.FileSystem.GetDriveFromPath(projectPath);
            string backupFileName = GetBackupFileNameFromProjectPath(projectPath);
            CreateReleaseDbSchemaIfNecessary();
            int projectId = (int)InsertProjectRecord(new RegisteredProjectInfo(projectName, backupFileName, version));
            AddDirectoryFilesToRepository(drive.RootDirectory, version, null, projectId);
        }

        public void AddBundleToRepository(string bundlePath, Version version)
        {
            IDriveInfo drive = FileSystem.FileSystem.GetDriveFromPath(@"ZIP:\" + bundlePath);
            CreateReleaseDbSchemaIfNecessary();

            var bundleFileName = Path.GetFileName(bundlePath);
            IFileInfo manifestFile = drive.GetFileInfo("\\manifest.xml");
            if (!manifestFile.Exists)
                throw new ApplicationException("Invalid zip file.  Could not find manifest.xml");

            BundleInfo bundleInfo = ExtractBundleInfoFromBundleManifest(manifestFile, bundleFileName);
            int bundleId = (int)InsertBundleRecord(bundleInfo, version);
            AddDirectoryFilesToRepository(drive.RootDirectory, version, bundleId, null);
        }

        internal string GetBackupFileNameFromProjectPath(string projectPath)
        {
            if (projectPath.StartsWith("ZIP:\\", StringComparison.OrdinalIgnoreCase))
                projectPath = projectPath.Substring(5);
            else
                return null;

            int zipNameEndPos = projectPath.IndexOf(".zip", StringComparison.OrdinalIgnoreCase);
            if (zipNameEndPos > -1)
                projectPath = projectPath.Substring(0, zipNameEndPos + 4);

            return Path.GetFileName(projectPath);
        }

        internal long InsertProjectRecord(RegisteredProjectInfo project)
        {
            const string PROJECT_INSERT = "INSERT INTO PROJECT " +
                "(NAME, BACKUPFILENAME, MAJORVERSION, MINORVERSION, BUILD, REVISION) " +
                "VALUES('{0}', '{1}', {2}, {3}, {4}, {5})";

            using (var connection = GetOpenConnection())
            {
                ExecuteNonQuery(string.Format(PROJECT_INSERT,
                    project.Name,
                    project.BackupFileName,
                    project.Version.Major,
                    project.Version.Minor,
                    project.Version.Build,
                    project.Version.Revision),
                    connection);

                return GetLastInsertId(connection);
            }
        }

        internal long GetLastInsertId(IDbConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT last_insert_rowid()";
                return (long)command.ExecuteScalar();
            }
        }

        internal void CreateReleaseDbSchemaIfNecessary()
        {
            using (var con = GetOpenConnection())
            {
                CreateReleaseDbSchemaIfNecessary(con);
            }
        }

        internal void CreateReleaseDbSchemaIfNecessary(IDbConnection connection)
        {
            const string CREATE_PATH_INDEX = "CREATE INDEX IF NOT EXISTS IDX_FILERELEASE_PATH ON FILERELEASE (PATH ASC)";
            const string CREATE_RELEASE_TABLE =
                @"CREATE TABLE IF NOT EXISTS FILERELEASE
                (
	                ID INTEGER PRIMARY KEY ASC NOT NULL,
                    BUNDLE_ID INTEGER NULL,
                    PROJECT_ID INTEGER NULL,
	                PATH VARCHAR(255) NOT NULL,
	                FILENAME VARCHAR(255) NOT NULL,
	                MAJORVERSION INTEGER NOT NULL,
	                MINORVERSION INTEGER NOT NULL,
	                BUILD INTEGER NOT NULL,
	                REVISION INTEGER NOT NULL,
	                HASH BLOB NOT NULL
                )";

            const string CREATE_BUNDLE_TABLE =
                @"CREATE TABLE IF NOT EXISTS BUNDLE
                (
	                ID INTEGER PRIMARY KEY ASC NOT NULL,
	                FILENAME VARCHAR(255) NOT NULL,
	                NAME VARCHAR(255) NOT NULL,
	                MAJORVERSION INTEGER NOT NULL,
	                MINORVERSION INTEGER NOT NULL,
	                BUILD INTEGER NOT NULL,
	                REVISION INTEGER NOT NULL
                )";

            const string CREATE_PROJECT_TABLE =
                @"CREATE TABLE IF NOT EXISTS PROJECT
                (
	                ID INTEGER PRIMARY KEY ASC NOT NULL,
	                NAME VARCHAR(255) NOT NULL,
                    BACKUPFILENAME VARCHAR(255) NULL,
	                MAJORVERSION INTEGER NOT NULL,
	                MINORVERSION INTEGER NOT NULL,
	                BUILD INTEGER NOT NULL,
	                REVISION INTEGER NOT NULL
                )";

            ExecuteNonQuery(CREATE_RELEASE_TABLE, connection);
            ExecuteNonQuery(CREATE_PATH_INDEX, connection);
            ExecuteNonQuery(CREATE_BUNDLE_TABLE, connection);
            ExecuteNonQuery(CREATE_PROJECT_TABLE, connection);
        }

        internal void ExecuteNonQuery(string sql, IDbConnection connection)
        {
            if (connection.State == ConnectionState.Closed)
                connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        protected virtual IDbConnection GetOpenConnection()
        {
            var dbPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), DefaultDbFileName);
            var connection = new SQLiteConnection(string.Format(FileReleaseDbConnectionString, dbPath));
            connection.Open();
            return connection;
        }

        internal void AddDirectoryFilesToRepository(IDirectoryInfo directory, Version version, int? bundleId = null,
            int? projectId = null)
        {
            IFileInfo[] files = directory.GetFiles("*.*", SearchOption.AllDirectories);
            var totalFiles = files.Length;

            if (totalFiles == 0)
                return;

            using (var connection = GetOpenConnection())
            {
                IDbTransaction trans = null;
                for (int i = 0; i < totalFiles; i++)
                {
                    if (trans == null)
                        trans = connection.BeginTransaction();

                    if (FileShouldBeTracked(files[i].Url))
                        AddFileToReleaseDb(files[i], version, connection, bundleId, projectId);

                    if (i % 100 == 0)
                    {
                        trans.Commit();
                        trans.Dispose();
                        trans = null;
                    }

                    //notify progress every 100 files
                    if ((i % 100 == 0) && (AddFileProgress != null))
                    {
                        var percentComplete = (int)((i / (float)totalFiles) * 100);
                        AddFileProgress(this, new FileProgressEventArgs(percentComplete));
                    }
                }
                if (trans != null)
                {
                    trans.Commit();
                    trans.Dispose();
                }

                if (AddFileProgress != null)
                    AddFileProgress(this, new FileProgressEventArgs(100));
            }
        }

        internal bool FileShouldBeTracked(string path)
        {
            return !_fileIgnoreRegex.IsMatch(path);
        }

        internal virtual byte[] CalculateHashCode(IFileInfo file)
        {
            using (Stream stream = file.Open(FileMode.Open, FileAccess.Read))
            {
                var hashProvider = SHA1.Create();
                return hashProvider.ComputeHash(stream);
            }
        }

        internal long InsertBundleRecord(BundleInfo bundleInfo, Version version)
        {
            const string BUNDLE_INSERT = "INSERT INTO BUNDLE " +
                "(FILENAME, NAME, MAJORVERSION, MINORVERSION, BUILD, REVISION) " +
                "VALUES('{0}', '{1}', {2}, {3}, {4}, {5})";

            using (var connection = GetOpenConnection())
            {
                ExecuteNonQuery(string.Format(BUNDLE_INSERT,
                    bundleInfo.FileName,
                    bundleInfo.Name,
                    version.Major,
                    version.Minor,
                    version.Build,
                    version.Revision),
                    connection);

                return GetLastInsertId(connection);
            }
        }

        internal void AddFileToReleaseDb(IFileInfo file, Version version, IDbConnection connection, int? bundleId = null,
            int? projectId = null)
        {
            AddFileToReleaseDb(file.Url, version, connection, CalculateHashCode(file), bundleId, projectId);
        }

        internal void AddFileToReleaseDb(string url, Version version, IDbConnection connection, byte[] hash,
            int? bundleId = null, int? projectId = null)
        {
            const string INSERT_FILERELEASE =
                "INSERT INTO FILERELEASE " +
                "(PATH, FILENAME, MAJORVERSION, MINORVERSION, BUILD, REVISION, HASH, BUNDLE_ID, PROJECT_ID) " +
                "VALUES(@PATH, @FILENAME, @MAJORVERSION, @MINORVERSION, @BUILD, @REVISION, @HASH, @BUNDLE_ID, @PROJECT_ID)";

            var build = version.Build == -1 ? 0 : version.Build;
            var revision = version.Revision == -1 ? 0 : version.Revision;

            using (var command = connection.CreateCommand())
            {
                command.CommandText = INSERT_FILERELEASE;
                AddCommandParameter("@PATH", url, command);
                AddCommandParameter("@FILENAME", Path.GetFileName(url), command);
                AddCommandParameter("@MAJORVERSION", version.Major, command);
                AddCommandParameter("@MINORVERSION", version.Minor, command);
                AddCommandParameter("@BUILD", build, command);
                AddCommandParameter("@REVISION", revision, command);
                AddCommandParameter("@HASH", hash, command);
                AddCommandParameter("@BUNDLE_ID", bundleId, command);
                AddCommandParameter("@PROJECT_ID", projectId, command);
                command.ExecuteNonQuery();
            }
        }

        private void AddCommandParameter(string name, object value, IDbCommand command)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        public event EventHandler<FileProgressEventArgs> AddFileProgress;

        public UpgradeReport AnalyzeForUpgrade(IProject sourceProject, IProject baseProject)
        {
            _log.Info(string.Format("Analyzing project folder {0} for customizations...", sourceProject.Drive.RootDirectory.FullName));

            List<string> addedFiles = new List<string>();
            List<string> autoMergableFiles = new List<string>();
            List<string> filesToManuallyMerge = new List<string>();
            List<string> warnings = new List<string>();

            IEnumerable<IModelUpgradeService> modelUpgradeServices = GetModelUpgradeServices(sourceProject.Models.Values);
            
            foreach (var fileReleaseEntry in GetAllReleaseInfoFromProjectByFile(sourceProject.Drive.RootDirectory.FullName))
            {
                IFileInfo file = fileReleaseEntry.Key;
                List<FileReleaseInfo> releases = fileReleaseEntry.Value;

                if (!releases.Any()) //this url does not exist in the official repository
                {
                    if (FileIsAValidAdd(file, baseProject, sourceProject, warnings, modelUpgradeServices))
                        addedFiles.Add(file.Url);
                }
                else
                {
                    if (!FileHasReleaseMatch(file, releases))
                    {                        
                        if (MergeFileIsAValidDiff(file, releases, baseProject, sourceProject, warnings, modelUpgradeServices))
                        {
                            IFileInfo baseFile = baseProject.Drive.GetFileInfo(file.Url);

                            if (!baseFile.Exists)
                            {
                                warnings.Add(string.Format("{0} is a file that was originally written by SalesLogix, but could not be found in the base project.  This file will need to be manually merged.", file.Url));
                                filesToManuallyMerge.Add(file.Url);
                            }
                            else
                            {
                                if (FileIsAutoMergable(file.Url, modelUpgradeServices))
                                    autoMergableFiles.Add(file.Url);
                                else
                                    filesToManuallyMerge.Add(file.Url);
                            }
                        }
                    }
                }
            }

            _log.Info(Environment.NewLine + "The following files are customizations not originally authored by SalesLogix and can be safely copied to the target upgrade project:");
            addedFiles.ForEach(url => _log.Info(url));

            _log.Info(Environment.NewLine + "The following files are customizations originally authored by SalesLogix that can be automatically merged into the target upgrade project:");
            autoMergableFiles.ForEach(url => _log.Info(url));

            _log.Info(Environment.NewLine + "The following files are customizations originally authored by SalesLogix that will require manual merging into the target upgrade project:");
            filesToManuallyMerge.ForEach(url => _log.Info(url));

            _log.Info(Environment.NewLine + "Warnings:");
            if (!warnings.Any())
                _log.Info("None");
            warnings.ForEach(url => _log.Warn(url));

            return new UpgradeReport(addedFiles, autoMergableFiles, filesToManuallyMerge, warnings);
        }

        private bool FileHasReleaseMatch(IFileInfo file, List<FileReleaseInfo> releases)
        {
            byte[] fileHash = CalculateHashCode(file);
            return releases.Where(info => fileHash.SequenceEqual(info.Hash)).Any();
        }

        private bool FileIsAutoMergable(string url, IEnumerable<IModelUpgradeService> modelUpgradeServices)
        {
            var modelService = modelUpgradeServices.FirstOrDefault(svc => svc.CanMergeFile(url));
            return modelService != null;
        }

        public UpgradeReport Upgrade(IProject sourceProject, IProject baseProject, IProject targetProject)
        {
            IEnumerable<IModelUpgradeService> modelUpgradeServices = GetModelUpgradeServices(sourceProject.Models.Values);

            UpgradeReport report = AnalyzeForUpgrade(sourceProject, baseProject);
            foreach (string addedUrl in report.AddedFiles)
            {
                IFileInfo sourceFile = sourceProject.Drive.GetFileInfo(addedUrl);
                IFileInfo targetFile = targetProject.Drive.GetFileInfo(addedUrl);
                if (!targetFile.Directory.Exists)
                    targetFile.Directory.Create();
                FileSystem.FSFile.Copy(sourceFile, targetFile, true);
            }

            foreach (string urlToMerge in report.AutoMergeableFiles)
            {
                var modelService = modelUpgradeServices.First(svc => svc.CanMergeFile(urlToMerge));
                modelService.MergeFile(urlToMerge, baseProject, sourceProject, targetProject);
            }

            return report;
        }

        public bool BuildBaseProject(ProjectInstallInfo installedProjectInfo, IDirectoryInfo projectBackupDir, string baseProjectPath)
        {
            if (!projectBackupDir.Exists)
                throw new ArgumentException("Path does not exist.", "projectBackupPath");

            if (string.IsNullOrEmpty(installedProjectInfo.ProjectVersionInfo.BackupFileName))
                throw new ArgumentException("BackupFileName is not set.", "projectInfo");

            IFileInfo backupFile = projectBackupDir.GetFiles(installedProjectInfo.ProjectVersionInfo.BackupFileName).FirstOrDefault();
            if (backupFile == null)
            {
                throw new ApplicationException(string.Format("Project backup file {0} does not exist.",
                    Path.Combine(projectBackupDir.FullName, installedProjectInfo.ProjectVersionInfo.BackupFileName)));
            }

            IDirectoryInfo baseDir = FileSystem.FileSystem.GetDirectoryInfo(baseProjectPath);
            if (!baseDir.Exists)
                baseDir.Create();

            var bundlesToInstall = installedProjectInfo.BundlesApplied
                .OrderBy(b => b.Version.Major)
                .ThenBy(b => b.Version.Minor)
                .ThenBy(b => b.Version.Build)
                .ThenBy(b => b.Version.Revision);

            //ensure we can find all the bundle files
            foreach (BundleInfo bundleInfo in bundlesToInstall)
            {
                if (!BundleExistsInRepository(projectBackupDir, bundleInfo.FileName))
                {
                    _log.Error(string.Format("The bundle file \"{0}\" could not be found.", bundleInfo.FileName));
                    Environment.Exit(-1);
                }
            }

            var backup = new ProjectBackup(backupFile.FullName);
            var workspace = new ProjectWorkspace(baseProjectPath);
            workspace.RestoreProjectFromBackup(backup);
            IProject project = new Project(workspace);
            var pcs = new SimpleProjectContextService(project);
            ApplicationContext.Current.Services.Add(typeof(IProjectContextService), pcs);

            var installResults = bundlesToInstall.Select(bundleInfo => 
                InstallBundle(projectBackupDir.FullName, bundleInfo.FileName, project));
            return installResults.All(result => result);
        }

        private bool InstallBundle(string bundlePath, string bundleFileName, IProject project)
        {
            string bundleFullFileName = Path.Combine(bundlePath, bundleFileName);

            var bundle = new Bundle(bundleFullFileName, null, project);
            ApplyTweaksForAutoBundleInstall(bundle);
            Exception installException = null;

            using (bundle)
            {
                bundle.HandleException += (item, ex) =>
                {
                    installException = ex;
                    return false;
                };

                _log.Info(string.Format("Installing bundle: " + bundleFullFileName));
                bundle.Install(false);
            }

            if (installException != null)
            {
                //log exception here
                return false;
            }

            return true;
        }

        private static void ApplyTweaksForAutoBundleInstall(Bundle bundle)
        {
            foreach (DuplicateInstallItem item in bundle.DuplicateInstallItems.Values)
            {
                if (item.InstallAction == DuplicateItemInstallAction.CustomManualMerge ||
                    item.InstallAction == DuplicateItemInstallAction.DiffMerge)
                {
                    item.InstallAction = DuplicateItemInstallAction.Overwrite;
                }
            }
            bundle.SilentInstall = true;
        }

        private bool BundleExistsInRepository(IDirectoryInfo bundleDir, string bundleFileName)
        {
            IFileInfo bundleFile = bundleDir.GetFiles(bundleFileName).FirstOrDefault();
            if (bundleFile == null)
                return false;
            return bundleFile.Exists;
        }

        private bool FileIsAValidAdd(IFileInfo file, IProject baseProject, IProject sourceProject, List<string> warnings,
            IEnumerable<IModelUpgradeService> modelUpgradeServices)
        {
            return !modelUpgradeServices
                        .Where(filter => filter.FileBelongsToThisModel(file))
                        .Any(filter => !filter.IsFileAValidAddition(file, baseProject, sourceProject, warnings));
        }

        private bool MergeFileIsAValidDiff(IFileInfo file, List<FileReleaseInfo> releases, IProject baseProject, IProject sourceProject, List<string> warnings,
            IEnumerable<IModelUpgradeService> modelUpgradeServices)
        {
            return !modelUpgradeServices
                        .Where(filter => filter.FileBelongsToThisModel(file))
                        .Any(filter => !filter.IsFileAValidModification(file, baseProject, sourceProject, warnings, releases));
        }

        internal List<BundleInfo> GetBundlesApplied(IEnumerable<BundleInfo> matchedBundles,
            IEnumerable<BundleInfo> bundlesFromManifests)
        {
            return matchedBundles
                .Intersect(bundlesFromManifests)
                .OrderBy(bundle => bundle.Name)
                .ToList();
        }

        internal List<BundleInfo> GetPossibleBundlesApplied(IEnumerable<BundleInfo> matchedBundles,
            IEnumerable<BundleInfo> bundlesFromManifests)
        {
            return bundlesFromManifests
                .Intersect(RegisteredBundles.Values)
                .Except(matchedBundles)
                .OrderBy(bundle => bundle.Name)
                .ToList();
        }

        private const string BASIC_FILERELEASE_SELECT =
            "SELECT PATH, FILENAME, MAJORVERSION, MINORVERSION, BUILD, REVISION, HASH, BUNDLE_ID, PROJECT_ID " +
            "FROM FILERELEASE ";

        internal IDataReader GetReaderOfMatchesByUrl(string url, IDbConnection connection)
        {
            const string selectSQl = BASIC_FILERELEASE_SELECT + "WHERE PATH = @PATH";
            var command = connection.CreateCommand();
            command.CommandText = selectSQl;
            AddCommandParameter("@PATH", url, command);
            return command.ExecuteReader();
        }

        internal IDataReader GetReaderOfMatchesByFileName(string fileName, IDbConnection connection)
        {
            const string selectSQl = BASIC_FILERELEASE_SELECT + "WHERE FILENAME = @FILENAME";
            var command = connection.CreateCommand();
            command.CommandText = selectSQl;
            AddCommandParameter("@FILENAME", fileName, command);
            return command.ExecuteReader();
        }

        internal IEnumerable<FileReleaseInfo> CreateFileReleaseInfosFromReader(IDataReader reader)
        {
            return from row in reader.ReadRows()
                   let bundle = (row[7] != DBNull.Value) ? GetBundleInfoById(Convert.ToInt32(row[7])) : null
                   let project = (row[8] != DBNull.Value) ? GetProjectById(Convert.ToInt32(row[8])) : null
                   select new FileReleaseInfo(row[0].ToString(),
                                              row[1].ToString(),
                                              new Version(Convert.ToInt32(row[2]),
                                                          Convert.ToInt32(row[3]),
                                                          Convert.ToInt32(row[4]),
                                                          Convert.ToInt32(row[5])),
                                              (byte[])row[6],
                                              bundle,
                                              project
                                              );
        }

        public IEnumerable<BundleInfo> GetBundleInfosFromProject(IDriveInfo projectDrive)
        {
            var manifestsDir = projectDrive.RootDirectory.GetDirectories().FirstOrDefault(dir => dir.Name == "Bundle Manifests");
            if (manifestsDir == null)
                return new List<BundleInfo>();

            var manifests = manifestsDir.GetFiles("*.manifest.xml", SearchOption.AllDirectories);
            return manifests.Select(manifest => ExtractBundleInfoFromBundleManifest(manifest, null));
        }

        internal BundleInfo ExtractBundleInfoFromBundleManifest(IFileInfo bundleManifestFile, string bundleFileName)
        {
            using (var streamReader = bundleManifestFile.OpenText())
            {
                string manifestText = streamReader.ReadToEnd();
                XDocument doc = XDocument.Parse(manifestText);
                XElement bundleProps = doc.Root.Element("bundleProperties");
                Version version = new Version(
                    Convert.ToInt32(bundleProps.Element("majorVersion").Value),
                    Convert.ToInt32(bundleProps.Element("minorVersion").Value),
                    Convert.ToInt32(bundleProps.Element("revision").Value),
                    Convert.ToInt32(bundleProps.Element("build").Value)
                    );
                return new BundleInfo(bundleProps.Element("name").Value, bundleFileName, version);
            }
        }

        internal BundleInfo GetBundleInfoById(int id)
        {
            return RegisteredBundles[id];
        }

        internal RegisteredProjectInfo GetProjectById(int id)
        {
            return RegisteredProjects[id];
        }

        internal IEnumerable<KeyValuePair<IFileInfo, List<FileReleaseInfo>>> GetAllReleaseInfoFromProjectByFile(string projectPath)
        {
            foreach (var file in GetAllTrackedFilesInProject(projectPath))
                yield return new KeyValuePair<IFileInfo, List<FileReleaseInfo>>(file, GetReleaseInfoByPath(file.Url));
        }

        internal IEnumerable<IFileInfo> GetAllTrackedFilesInProject(string projectPath)
        {
            IDriveInfo drive = FileSystem.FileSystem.GetDriveFromPath(projectPath);
            IFileInfo[] files = drive.RootDirectory.GetFiles("*.*", SearchOption.AllDirectories);
            return files
                .Where(file => FileShouldBeTracked(file.Url))
                .OrderBy(file => file.Url);
        }

        public List<FileReleaseInfo> GetReleaseInfoByPath(string path)
        {
            using (var con = GetOpenConnection())
            using (var reader = GetReaderOfMatchesByUrl(path, con))
            {
                return CreateFileReleaseInfosFromReader(reader).ToList();
            }
        }

        public ProjectInstallInfo IdentifyProjectVersion(string projectPath)
        {
            _log.Info(string.Format("Analyzing files in {0} to determine versioning information...", projectPath));
            Dictionary<string, List<FileReleaseInfo>> projMatchInfo = FindAllMatchingReleasesInProject(projectPath);
            if (projMatchInfo == null || !projMatchInfo.Any())
                throw new InvalidProjectException("Project Version could not be determined.  No files were found.", projectPath);

            IDriveInfo projectDrive = FileSystem.FileSystem.GetDriveFromPath(projectPath);
            RegisteredProjectInfo projectInfo = DetermineProjectVersionFromMatches(projMatchInfo.Values);
            _log.Info("Project Version: " + projectInfo.Name);

            IEnumerable<BundleInfo> bundlesByMatches = FindInstalledBundlesByMatches(projMatchInfo.Values, projectInfo.MainVersion);
            IEnumerable<BundleInfo> bundlesFromManifests = GetBundleInfosFromProject(projectDrive);

            var installInfo = new ProjectInstallInfo(projectInfo,
                                          GetBundlesApplied(bundlesByMatches, bundlesFromManifests),
                                          GetPossibleBundlesApplied(bundlesByMatches, bundlesFromManifests));

            if (installInfo.BundlesApplied.Any())
            {
                _log.Info("Bundles installed:");
                installInfo.BundlesApplied.ForEach(bundle =>
                    _log.Info(string.Format("\t{0} {1}", bundle.Name, bundle.Version)));
                _log.Info("");
            }
            else
            {
                _log.Info("No SalesLogix bundles have been installed.");
            }

            if (installInfo.PossibleBundlesApplied.Any())
            {
                _log.Warn("The following bundles may have been applied, but the associated files were not found:");
                installInfo.PossibleBundlesApplied.ForEach(bundle =>
                    _log.Warn(string.Format("{0} {1}", bundle.Name, bundle.Version)));
                _log.Info("");
            }

            return installInfo;
        }

        internal IEnumerable<BundleInfo> FindInstalledBundlesByMatches(IEnumerable<List<FileReleaseInfo>> matchesByUrl,
            Version mainVersion)
        {
            return matchesByUrl
                .Where(matches => matches != null) //skip files with no repo matches
                .SelectMany(matches => matches) //flatten out to list of matches
                .Where(match => match.Bundle != null //skip matches that aren't part of a bundle and match the main version
                    && mainVersion.Major == match.Version.Major
                    && mainVersion.Minor == match.Version.Minor
                    && mainVersion.Build == match.Version.Build)
                .Select(match => match.Bundle) //select bundles
                .Distinct(); //get unique bundles
        }

        internal RegisteredProjectInfo DetermineProjectVersionFromMatches(IEnumerable<List<FileReleaseInfo>> matchesByUrl)
        {
            Version maxVersion = matchesByUrl
                .Where(matches => matches != null && matches.Count == 1)
                .Select(matches => matches.First())
                .Where(match => match != null && match.Project != null && match.FileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(match => match.Project)
                .Max(project => project.Version);

            if (maxVersion == null)
                throw new InvalidProjectException("No matching projects were found.");

            return RegisteredProjects.Values.First(project => project.Version == maxVersion);
        }

        internal Dictionary<string, List<FileReleaseInfo>> FindAllMatchingReleasesInProject(string projectPath)
        {
            IDriveInfo drive = FileSystem.FileSystem.GetDriveFromPath(projectPath);
            IFileInfo[] files = drive.RootDirectory.GetFiles("*.*", SearchOption.AllDirectories);
            return files.Where(file => FileShouldBeTracked(file.Url))
                .Select(file => new { Url = file.Url, Matches = FindAllMatchingReleaseInfo(file) })
                .ToDictionary(item => item.Url, item => item.Matches);
        }

        internal List<FileReleaseInfo> FindAllMatchingReleaseInfo(IFileInfo file)
        {
            byte[] fileHash = CalculateHashCode(file);
            return GetReleaseInfoByPath(file.Url)
                .Where(info => fileHash.SequenceEqual(info.Hash))
                .ToList();
        }

        /// <summary>
        /// This is a temporary measure to inject IModelUpgradeService instances until the implementations
        /// can be moved to the proper assemblies and accessed through IModel.GetModelService().
        /// This allows the project upgrade service to be a purely additive change for now
        /// that can be version controlled elsewhere.
        /// </summary>
        public Func<IEnumerable<IModel>, IEnumerable<IModelUpgradeService>> GetModelUpgradeServices;
    }
}
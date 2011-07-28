using System;
using System.Collections.Generic;
using System.Linq;
using Sage.Platform.Application;
using Sage.Platform.Utility;
using Sage.Platform.FileSystem.Interfaces;

namespace UpgradeProject
{
    public enum UpgradeOperation
    {
        AddProject,
        AddBundle,
        IdentifyProjectVersion,
        BuildBaseProject,
        UpgradeReport,
        Upgrade
    }

    public class CommandLineArgs
    {
        [Argument(ArgumentType.Required, ShortName = "O", HelpText = "Operation to perform.")]
        public UpgradeOperation Operation;

        [Argument(ArgumentType.Required, ShortName = "SP", HelpText = "Full Path of the project or bundle to add to the repository.")]
        public string SourcePath = string.Empty;

        [Argument(ArgumentType.AtMostOnce, ShortName = "TP", HelpText = "Full Path of the project to upgrade to.  Only used by UpgradeReport option.")]
        public string TargetPath = string.Empty;

        [Argument(ArgumentType.AtMostOnce, ShortName = "BP", HelpText = "Full Path of the project the source path is based on without customizations and including any SLX bundles that are also applied to source path.  Only used by UpgradeReport option.")]
        public string BasePath = string.Empty;

        [Argument(ArgumentType.AtMostOnce, ShortName = "V", HelpText = "Version of files to register files as in the repository.  This should bein the format X.X.X.X")]
        public string Version = string.Empty;

        [Argument(ArgumentType.AtMostOnce, ShortName = "S", HelpText = "Database server name when using a native connection to access a VFS project.  Default is localhost.")]
        public string Server = "localhost";

        [Argument(ArgumentType.AtMostOnce, ShortName = "D", HelpText = "Database name when using a native connection to access a VFS project.  Default is saleslogix.")]
        public string Database = "saleslogix";

        [Argument(ArgumentType.AtMostOnce, ShortName = "U", HelpText = "Username when using a native connection to access a VFS project.  Default is sysdba.")]
        public string UserName = "sysdba";

        [Argument(ArgumentType.AtMostOnce, ShortName = "P", HelpText = "Password when using a native connection to access a VFS project.")]
        public string Password = string.Empty;

        [Argument(ArgumentType.AtMostOnce, ShortName = "PN", HelpText = "Name to identify a project being added to the repository.")]
        public string ProjectName = string.Empty;

        public bool ValidateArgs(out List<string> badArgs)
        {
            badArgs = new List<string>();

            if (Operation != UpgradeOperation.AddBundle)
            {
                IDirectoryInfo projectDir = GetDirectoryInfo(SourcePath);
                if (projectDir == null)
                    badArgs.Add("SourcePath is not a valid project path.");
                else if (!projectDir.Exists)
                    badArgs.Add("SourcePath does not exist.");
            }
            else
            {
                IFileInfo bundleFile = GetFileInfo(SourcePath);
                if (bundleFile == null)
                    badArgs.Add("SourcePath is not a valid bundle file path.");
                else if (!bundleFile.Exists)
                    badArgs.Add("SourcePath does not exist.");
                else if (!bundleFile.Url.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                    badArgs.Add("SourcePath should be a full path to a bundle file that ends in .zip");
            }

            if (Operation == UpgradeOperation.AddProject && string.IsNullOrEmpty(ProjectName))
                badArgs.Add("ProjectName is required when the AddProject operation is used.");

            if (Operation == UpgradeOperation.AddProject || Operation == UpgradeOperation.AddBundle)
            {
                if (string.IsNullOrEmpty(Version))
                    badArgs.Add("Version is required for operations AddProject and AddBundle");
                else
                {
                    try
                    {
                        new Version(Version);
                    }
                    catch (Exception)
                    {
                        badArgs.Add("Invalid Version number.  Version numbers should be in format X.X.X.X");
                    }
                }
            }

            if (Operation == UpgradeOperation.UpgradeReport || Operation == UpgradeOperation.Upgrade)
            {
                if (string.IsNullOrEmpty(BasePath))
                {
                    badArgs.Add("BasePath is required for the UpgradeReport and Upgrade operations.");
                }
                else
                {
                    IDirectoryInfo baseProjectDir = GetDirectoryInfo(BasePath);
                    if (baseProjectDir == null)
                        badArgs.Add("BasePath is not a valid project path.");
                    else if (!baseProjectDir.Exists)
                        badArgs.Add("BasePath does not exist.");
                }
            }

            if (Operation == UpgradeOperation.Upgrade)
            {
                if (string.IsNullOrEmpty(TargetPath))
                {
                    badArgs.Add("TargetPath is required for the Upgrade operation.");
                }
                else
                {
                    IDirectoryInfo targetProjectDir = GetDirectoryInfo(TargetPath);
                    if (targetProjectDir == null)
                        badArgs.Add("TargetPath is not a valid project path.");
                    else if (!targetProjectDir.Exists)
                        badArgs.Add("TargetPath does not exist.");
                }
            }

            if (Operation == UpgradeOperation.BuildBaseProject)
            {
                if (string.IsNullOrEmpty(BasePath))
                {
                    badArgs.Add("BasePath is required for the BuildBaseProject operation.");
                }
                else
                {
                    IDirectoryInfo baseProjectDir = GetDirectoryInfo(BasePath);
                    if (baseProjectDir == null)
                        badArgs.Add("BasePath is not a valid project path.");
                    else if (baseProjectDir.Exists && baseProjectDir.GetFiles("*.*").Any())
                        badArgs.Add("When using BuildBaseProject, BasePath must not exist or it should be empty.");
                }
            }


            return badArgs.Count == 0;
        }

        internal static IDirectoryInfo GetDirectoryInfo(string path)
        {
            IDirectoryInfo inf = null;
            try
            {
                inf = Sage.Platform.FileSystem.FileSystem.GetDirectoryInfo(path);
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("An error occurred validating the path {0}", path), e);
            }
            return inf;
        }

        internal static IFileInfo GetFileInfo(string path)
        {
            IFileInfo inf = null;
            try
            {
                inf = Sage.Platform.FileSystem.FileSystem.GetFileInfo(path);
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("An error occurred validating the path {0}", path), e);
            }
            return inf;
        }
    }
}
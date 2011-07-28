using System.Collections.Generic;
using Sage.Platform.FileSystem.Interfaces;
using Sage.Platform.Projects.Interfaces;
using System;
using log4net;

namespace Sage.Platform.Upgrade
{
    public interface IProjectUpgradeService
    {
        /// <summary>
        /// Adds all files in a project to the release repository.
        /// </summary>
        /// <param name="projectPath">The folder to add files from.</param>
        /// <param name="version">The version number to register the files as in the repository.  Should be in the format X.X.X.X</param>
        void AddProjectToRepository(string projectPath, Version version, string projectName);

        /// <summary>
        /// Adds all files in a bundle to the release repository.
        /// </summary>
        /// <param name="bundlePath">The path to the bundle file.</param>
        /// <param name="version">The version number to register the files as in the repository.  Should be in the format X.X.X.X</param>
        void AddBundleToRepository(string bundlePath, Version version);        

        /// <summary>
        /// Identifies the product version of a project including the bundles installed (hotfixes, accelerators, etc.)
        /// </summary>
        /// <param name="projectPath">The path of the project to identify the version for.</param>
        /// <returns>The product version of the project and the bundles installed.</returns>
        ProjectInstallInfo IdentifyProjectVersion(string projectPath);

        /// <summary>
        /// Recreates a project starting point given a version and bundles to apply.
        /// </summary>
        /// <param name="installedProjectInfo">Describes the product version and bundles to apply.</param>
        /// <param name="projectBackupDir">Directory where all project backups and bundles reside.</param>
        /// <param name="baseProjectPath">path where project will be restored to.</param>
        /// <returns>True, if all bundles successfully installed, otherwise false</returns>
        bool BuildBaseProject(ProjectInstallInfo installedProjectInfo, IDirectoryInfo projectBackupDir, string baseProjectPath);

        /// <summary>
        /// Compares two projects to identify differences for assessing upgrade effort.
        /// </summary>
        /// <param name="sourceProject">Your customized project</param>
        /// <param name="baseProject">The uncustomized version of the source project.  These two should be based on the same product version.</param>
        /// <returns>A report describing the changes between two projects.</returns>
        UpgradeReport AnalyzeForUpgrade(IProject sourceProject, IProject baseProject);

        /// <summary>
        /// Analyzed the differences between the source and base projects and applies any changes possible to the target project.
        /// </summary>
        /// <param name="sourceProject">Your customized project</param>
        /// <param name="baseProject">The uncustomized version of the source project.  These two should be based on the same product version.</param>
        /// <param name="targetProject">The target project</param>        
        /// <returns>A report describing the changes between two projects.</returns>
        UpgradeReport Upgrade(IProject sourceProject, IProject baseProject, IProject targetProject);

        /// <summary>
        /// Notifies when a file from a bundle or project backup has been added to the release repository.
        /// </summary>
        event EventHandler<FileProgressEventArgs> AddFileProgress;

        ILog Log { get; }
    }

    public class FileReleaseInfo
    {
        public string Path { get; private set; }
        public string FileName { get; private set; }
        public Version Version { get; private set; }
        public byte[] Hash { get; private set; }
        public BundleInfo Bundle { get; private set; }
        public RegisteredProjectInfo Project { get; private set; }

        public FileReleaseInfo(string path, string fileName, Version version, byte[] hash, BundleInfo bundle,
            RegisteredProjectInfo project)
        {
            Path = path;
            FileName = fileName;
            Version = version;
            Hash = hash;
            Bundle = bundle;
            Project = project;
        }
    }

    public class BundleInfo
    {
        public string Name { get; private set; }
        public string FileName { get; private set; }
        public Version Version { get; private set; }

        public BundleInfo(string name, string fileName, Version version)
        {
            Name = name;
            FileName = fileName;
            Version = version;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            return GetHashCode() == obj.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class RegisteredProjectInfo
    {
        public string BackupFileName { get; private set; }
        public string Name { get; private set; }
        public Version Version { get; private set; }

        public Version MainVersion
        {
            get { return new Version(Version.Major, Version.Minor, Version.Build); }
        }

        public RegisteredProjectInfo(string name, string backupFileName, Version version)
        {
            Name = name;
            BackupFileName = backupFileName;
            Version = version;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode() ^ Version.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            return GetHashCode() == obj.GetHashCode();
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public class ProjectInstallInfo
    {
        public RegisteredProjectInfo ProjectVersionInfo { get; private set; }
        public List<BundleInfo> BundlesApplied { get; private set; }
        public List<BundleInfo> PossibleBundlesApplied { get; private set; }

        public ProjectInstallInfo(RegisteredProjectInfo projectInfo, List<BundleInfo> bundlesApplied, List<BundleInfo> possibleBundlesApplied)
        {
            ProjectVersionInfo = projectInfo;
            BundlesApplied = bundlesApplied;
            PossibleBundlesApplied = possibleBundlesApplied;
        }
    }

    public class UpgradeReport
    {
        public List<string> AddedFiles { get; private set; }
        public List<string> AutoMergeableFiles { get; private set; }
        public List<string> FilesToManuallyMerge { get; private set; }
        public List<string> Warnings { get; private set; }

        public UpgradeReport(List<string> addedFiles, List<string> autoMergeableFiles, List<string> filesToManuallyMerge, 
            List<string> warnings)
        {
            AddedFiles = addedFiles;
            AutoMergeableFiles = autoMergeableFiles;
            FilesToManuallyMerge = filesToManuallyMerge;
            Warnings = warnings;
        }
    }

    public class FileProgressEventArgs : EventArgs
    {
        public int PercentComplete { get; set; }

        public FileProgressEventArgs(int percentComplete)
        {
            PercentComplete = percentComplete;
        }
    }
}
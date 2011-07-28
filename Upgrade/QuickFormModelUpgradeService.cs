using System;
using System.Collections.Generic;
using Sage.Platform.FileSystem.Interfaces;
using Sage.Platform.Projects.Interfaces;

namespace Sage.Platform.Upgrade
{
    public class QuickFormModelUpgradeService : IModelUpgradeService
    {
        public bool FileBelongsToThisModel(IFileInfo file)
        {
            return false;
        }

        public bool IsFileAValidAddition(IFileInfo file, IProject baseProject, IProject sourceProject, List<string> warnings)
        {
            return true;
        }

        public bool IsFileAValidModification(IFileInfo file, IProject baseProject, IProject sourceProject, List<string> warnings, List<FileReleaseInfo> releases)
        {
            return true;
        }

        public bool CanMergeFile(string url)
        {
            if (url.EndsWith(".quickform.xml.resx", StringComparison.OrdinalIgnoreCase))
                return true;

            if (url.EndsWith("\\Localization\\Global_Images.resx", StringComparison.OrdinalIgnoreCase))
                return true;
            
            return false;
        }

        public void MergeFile(string url, IProject baseProject, IProject sourceProject, IProject targetProject)
        {
            if (url.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
                MergeResxFile(url, baseProject, sourceProject, targetProject);
        }

        public void MergeResxFile(string url, IProject baseProject, IProject sourceProject, IProject targetProject)
        {
            if (url.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
            {
                var baseFile = baseProject.Drive.GetFileInfo(url);
                var sourceFile = sourceProject.Drive.GetFileInfo(url);
                var targetFile = targetProject.Drive.GetFileInfo(url);
                ResxDifferences changes = ResxDiffMerge.CompareResxFiles(sourceFile, baseFile);
                ResxDiffMerge.MergeChangesIntoResx(changes, targetFile);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Sage.Platform.FileSystem.Interfaces;
using Sage.Platform.Projects.Interfaces;

namespace Sage.Platform.Upgrade
{
    public class PortalModelUpgradeService : IModelUpgradeService
    {
        private Regex _portalResxRegex;

        public PortalModelUpgradeService()
        {
            _portalResxRegex = new Regex(@"\\Portal\\.*\.resx");
        }

        public bool FileBelongsToThisModel(IFileInfo file)
        {
            return FileIsAssembly(file) 
                || FileIsOrderCollection(file)
                || FileIsSupportFilesDefinition(file);
        }

        public bool IsFileAValidAddition(IFileInfo file, IProject baseProject, IProject sourceProject, List<string> warnings)
        {
            if (FileIsOrderCollection(file))
                return OrderedCollectionIsAValidAddition(file, baseProject, sourceProject, warnings);

            return true;
        }

        private bool FileIsOrderCollection(IFileInfo file)
        {
            return file.Url.EndsWith(".order.xml", StringComparison.OrdinalIgnoreCase);
        }

        private bool FileIsAssembly(IFileInfo file)
        {
            return file.Url.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                   file.Url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        }

        private bool FileIsSupportFilesDefinition(IFileInfo file)
        {
            return file.Url.EndsWith("SupportFiles.def.xml", StringComparison.OrdinalIgnoreCase);
        }

        private bool OrderedCollectionIsAValidAddition(IFileInfo file, IProject baseProject, IProject sourceProject, 
            List<string> warnings)
        {
            //if the file is showing as an add, but exists in the base, it was created through a bundle install
            return !baseProject.Drive.GetFileInfo(file.Url).Exists;
        }

        public bool IsFileAValidModification(IFileInfo file, IProject baseProject, IProject sourceProject, List<string> warnings, List<FileReleaseInfo> releases)
        {
            if (FileIsAssembly(file))
            {
                warnings.Add(string.Format("{0} is an unknown version of a SalesLogix assembly.", file.Url));
                return false;
            }

            if (FileIsSupportFilesDefinition(file))
                return false;

            if (FileIsOrderCollection(file))
            {
                IFileInfo baseFile = baseProject.Drive.GetFileInfo(file.Url);
                byte[] currentHash = CalculateHashCode(file);
                byte[] baseHash = CalculateHashCode(baseFile);
                return !currentHash.SequenceEqual(baseHash);
            }

            return true;
        }

        internal virtual byte[] CalculateHashCode(IFileInfo file)
        {
            using (Stream stream = file.Open(FileMode.Open, FileAccess.Read))
            {
                var hashProvider = SHA1.Create();
                return hashProvider.ComputeHash(stream);
            }
        }

        public bool CanMergeFile(string url)
        {
            if (url.EndsWith(".order.xml", StringComparison.OrdinalIgnoreCase))
                return true;

            return _portalResxRegex.IsMatch(url);
        }

        public void MergeFile(string url, IProject baseProject, IProject sourceProject, IProject targetProject)
        {
            if (_portalResxRegex.IsMatch(url))
            {
                var baseFile = baseProject.Drive.GetFileInfo(url);
                var sourceFile = sourceProject.Drive.GetFileInfo(url);
                var targetFile = targetProject.Drive.GetFileInfo(url);
                ResxDifferences changes = ResxDiffMerge.CompareResxFiles(sourceFile, baseFile);
                ResxDiffMerge.MergeChangesIntoResx(changes, targetFile);
            }
            else if (url.EndsWith(".order.xml", StringComparison.OrdinalIgnoreCase))
            {
                var baseFile = baseProject.Drive.GetFileInfo(url);
                var sourceFile = sourceProject.Drive.GetFileInfo(url);
                var targetFile = targetProject.Drive.GetFileInfo(url);

                OrderedCollectionDiffMerge.MergeDifferencesIntoTargetOrderFile(baseFile, sourceFile, targetFile);
            }
        }
    }
}
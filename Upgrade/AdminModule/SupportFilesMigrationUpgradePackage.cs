using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Sage.Platform.FileSystem;
using Sage.Platform.FileSystem.Interfaces;
using Sage.Platform.Projects;
using Sage.Platform.Upgrade.AdminModule.Localization;
using Sage.Platform.Upgrade.AdminModule.Properties;

namespace Sage.Platform.Upgrade.AdminModule
{
    [UpgradeVersion("7.5.0.1333")]
    [SRDescription(SR.SupportFilesMigrationUpgradePackage_Description)]
    public class SupportFilesMigrationUpgradePackage : UpgradePackage
    {
        [UpgradeStep]
        [SRDescription(SR.SupportFilesMigrationUpgradePackage_CopySupportFilesIntoModel_Description)]
        public void CopySupportFilesIntoModel()
        {
            //see if any portals are pointing to support files outside the model
            Dictionary<string, string> externalSupportFilePaths = GetExternalSupportFilePaths();

            if (externalSupportFilePaths.Keys.Count == 0)
                return;

            //count files to copy
            Log.Info(Resources.SupportFilesMigrationUpgradePackage_Retrieving_support_files_to_copy);
            int totalFilesToCopy = 0;
            foreach (string supportFilesPath in externalSupportFilePaths.Values)
            {
                IDirectoryInfo supportFilesFolder = FileSystem.FileSystem.GetDirectoryInfo(supportFilesPath);
                if (supportFilesFolder.Exists)
                {
                    IFileInfo[] files = supportFilesFolder.GetFiles("*.*", SearchOption.AllDirectories);
                    totalFilesToCopy += files.Length;
                }                
            }

            int filesCopied = 0;
            foreach (string portalFileName in externalSupportFilePaths.Keys)
            {
                IDirectoryInfo sourceSupportDir = FileSystem.FileSystem.GetDirectoryInfo(externalSupportFilePaths[portalFileName]);

                if (sourceSupportDir.Exists)
                {
                    Log.InfoFormat(Resources.SupportFilesMigrationUpgradePackage_The_support_files_path_is_being_copied_into_the_model_for_portal,
                                                  sourceSupportDir.FullName, portalFileName);

                    string portalName = portalFileName.Split('.')[0];
                    string destinationFolder = "\\Portal\\" + portalName + "\\SupportFiles";
                    IDirectoryInfo destSupportDir = Drive.GetDirectoryInfo(destinationFolder);
                                    
                    CopyDirectory(sourceSupportDir, destSupportDir, totalFilesToCopy, ref filesCopied);

                    //update the portal's support files path reference
                    IFileInfo supportFilesDefFile = GetSupportFilesDefinitionFile(portalName);
                    ChangeSupportFilesPathToRelativePath(supportFilesDefFile);
                }
                else
                {
                    Log.InfoFormat(Resources.SupportFilesMigrationUpgradePackage_The_support_files_path_does_not_exist_for_portal, 
                                                  sourceSupportDir.FullName, portalFileName);
                }
            }
        }

        private IFileInfo GetSupportFilesDefinitionFile(string portalName)
        {
            return Drive.GetFileInfo("\\Portal\\" + portalName + "\\SupportFiles.def.xml");
        }

        private void CopyDirectory(IDirectoryInfo sourceDirectory, IDirectoryInfo targetDirectory, int totalFilesToCopy, ref int filesCopied)
        {
            if (targetDirectory.Exists)
                ClearDirectory(targetDirectory);
            else
                targetDirectory.Create();

            foreach (IFileInfo sourceFile in sourceDirectory.GetFiles())
            {
                filesCopied++;
                //if ((filesCopied % 20 == 0) && (ProjectFileCopyProgress != null))
                //{
                //    var percentComplete = (int)((filesCopied / (float)totalFilesToCopy) * 100);
                //    ProjectFileCopyProgress(percentComplete, string.Format(Resources.ProjectWorkspaceFolderCopy, sourceDirectory.Url));
                //}
                
                string targetFileName = Path.Combine(targetDirectory.Url, sourceFile.Name);
                IFileInfo targetFile = targetDirectory.DriveInfo.GetFileInfo(targetFileName);
                FSFile.Copy(sourceFile, targetFile, true);
            }

            //copy subdirectories
            foreach (IDirectoryInfo sourceSubdirectory in sourceDirectory.GetDirectories())
            {
                CopyDirectory(sourceSubdirectory,
                              targetDirectory.DriveInfo.GetDirectoryInfo(Path.Combine(targetDirectory.Url, sourceSubdirectory.Name)),
                              totalFilesToCopy,
                              ref filesCopied);
            }
        }

        private static void ClearDirectory(IDirectoryInfo directory)
        {
            foreach (IFileInfo file in directory.GetFiles())
                file.Delete();
            foreach (IDirectoryInfo subDirectory in directory.GetDirectories())
                subDirectory.Delete(true);
        }

        private Dictionary<string, string> GetExternalSupportFilePaths()
        {
            var paths = new Dictionary<string, string>();

            IDirectoryInfo portalDirectory = Drive.GetDirectoryInfo("\\Portal");
            IFileInfo[] portalAppFiles = portalDirectory.GetFiles("*.portal.xml", SearchOption.AllDirectories);
            foreach (IFileInfo portalAppFile in portalAppFiles)
            {
                IFileInfo[] supportFilesDefFiles = portalAppFile.Directory.GetFiles("SupportFiles.def.xml");
                if (supportFilesDefFiles.Length > 0)
                {
                    using (Stream stream = supportFilesDefFiles[0].Open(FileMode.Open, FileAccess.Read))
                    using (var reader = new XmlTextReader(stream))
                    {
                        reader.ReadToFollowing("LinkedFolderDefinition");
                        string supportFilesPathName = reader.GetAttribute("Source");
                        if (supportFilesPathName.Contains("\\"))
                            paths.Add(portalAppFile.Name, supportFilesPathName);
                    }
                }
            }

            return paths;
        }

        private static void CountFiles(IDirectoryInfo directory, ref int totalFiles)
        {
            IFileInfo[] files = directory.GetFiles("*.*", SearchOption.AllDirectories);
            totalFiles += files.Length;
        }

        private static void ChangeSupportFilesPathToRelativePath(IFileInfo file)
        {
            string modifiedContent;
            using (Stream stream = file.Open(FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(stream))
            {
                string content = reader.ReadToEnd();
                var regex = new Regex("Source=\".*\"");
                modifiedContent = regex.Replace(content, "Source=\"SupportFiles\"");
            }

            using (Stream stream = file.Open(FileMode.Open, FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                stream.SetLength(0);
                writer.Write(modifiedContent);
            }
        }
    }
}
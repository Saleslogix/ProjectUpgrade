using System.Collections.Generic;
using Sage.Platform.FileSystem.Interfaces;
using Sage.Platform.Projects.Interfaces;

namespace Sage.Platform.Upgrade
{
    public interface IModelUpgradeService
    {
        /// <summary>
        /// Determines whether or not a file is a member of the model type that this class instance services.
        /// </summary>
        /// <param name="file">The file to check membership for.</param>
        /// <returns>True, if the file belongs to the current model type; otherwise false.</returns>
        /// <remarks>
        /// This is currently only being used as a filter when checking for valid additions and modifications.  
        /// It only needs to be implemented for file types that are actually being validated for additions or modifications.
        /// </remarks>
        bool FileBelongsToThisModel(IFileInfo file);
        
        /// <summary>
        /// Determines whether or not a file was truly added or if it is a false positive.  An example of this is a renamed relationship file.
        /// </summary>
        /// <param name="file">The file to validate</param>
        /// <param name="baseProject">The starting project.  This may be searched to confirm the file did not exist before.</param>
        /// <param name="sourceProject">The project where the file to be validated currently resides.</param>
        /// <param name="warnings">A list of warnings that can be added to depending on findings of this function.</param>
        /// <returns>True, if the file was really a new addition, otherwise false.</returns>
        bool IsFileAValidAddition(IFileInfo file, IProject baseProject, IProject sourceProject, List<string> warnings);
        
        /// <summary>
        /// Determines whether or not a file was has actual modifying customizations that need to be merged 
        /// or if the file was just incidentally modified and does not need to be merged.
        /// </summary>
        /// <param name="file">The file to validate</param>
        /// <param name="baseProject"></param>
        /// <param name="sourceProject">The project where the file to be validated currently resides.</param>
        /// <param name="warnings">A list of warnings that can be added to depending on findings of this function.</param>
        /// <param name="releases"></param>
        /// <returns></returns>
        bool IsFileAValidModification(IFileInfo file, IProject baseProject, IProject sourceProject, List<string> warnings, 
            List<FileReleaseInfo> releases);
        
        /// <summary>
        /// Specifies whether or not changes to a file can be automatically merged.
        /// </summary>
        /// <param name="url">The url of the file to be merged.</param>
        /// <returns>True, if the file can be automatically merged; otherwise false.</returns>
        bool CanMergeFile(string url);
        
        /// <summary>
        /// Automatically merges the changes between a file in the baseProject and the sourceProject into the targetProject.
        /// </summary>
        /// <param name="url">The url of the file to be automatically merged.</param>
        /// <param name="baseProject">The base project containing the file to be compared against.</param>
        /// <param name="sourceProject">The project containing the customized file.</param>
        /// <param name="targetProject">The project where the merged file will reside.</param>
        void MergeFile(string url, IProject baseProject, IProject sourceProject, IProject targetProject);
    }
}
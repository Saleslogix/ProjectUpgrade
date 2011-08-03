using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Sage.Platform.Orm.Entities;
using Sage.Platform.Projects.Interfaces;
using Sage.Platform.FileSystem.Interfaces;
using System.Text.RegularExpressions;

namespace Sage.Platform.Upgrade
{
    public class OrmModelUpgradeService : IModelUpgradeService
    {
        public bool FileBelongsToThisModel(IFileInfo file)
        {
            return FileIsRelationship(file) || FileIsDeletedItem(file) || FileIsSharedResx(file);
        }

        private bool FileIsRelationship(IFileInfo file)
        {
            return file.Url.EndsWith(".relationship.xml", StringComparison.InvariantCultureIgnoreCase);
        }

        private bool FileIsDeletedItem(IFileInfo file)
        {
            return file.Url.StartsWith("\\Deleted Items\\", StringComparison.InvariantCultureIgnoreCase);            
        }

        private bool FileIsSharedResx(IFileInfo file)
        {
            return file.Url.EndsWith("EntityResources.resx", StringComparison.OrdinalIgnoreCase)
                   || file.Url.EndsWith("Global_Images.resx", StringComparison.OrdinalIgnoreCase);
        }

        public bool IsFileAValidAddition(IFileInfo file, IProject baseProject, IProject sourceProject, List<string> warnings)
        {
            Guid sourceId = GetModelItemIdFromFile(file);
            if (sourceId == Guid.Empty)
                return true;

            OrmRelationship baseRelationship = FindRelationshipInBaseById(sourceId, baseProject);
            if (baseRelationship != null)
            {
                var sourceRelationship = sourceProject.Get<OrmRelationship>(file.Url);
                var diffMerge = new ObjectDiffMerge();
                var changes = diffMerge.CompareObjects(sourceRelationship, baseRelationship);
                if (!changes.All(change => RelationshipChangeCanBeIgnored(change)))
                    warnings.Add(string.Format("{0} is an existing SalesLogix relationship that was renamed and also modified.  This file will need to be manually merged.", file.Url));
                
                return false;
            }

            return true;
        }

        private bool RelationshipChangeCanBeIgnored(PropertyChange relationshipChange)
        {
            if (relationshipChange.Name == "LastModifiedUtc")
                return true;
            var columnIdRegex = new Regex(@"^Columns\[[0-9]*\]\.Id$");
            
            if (columnIdRegex.IsMatch(relationshipChange.Name))
                return true;

            return false;
        }

        public bool IsFileAValidModification(IFileInfo file, IProject baseProject, IProject sourceProject, List<string> warnings, List<FileReleaseInfo> releases)
        {
            if (FileIsDeletedItem(file))
                return false;

            if (FileIsSharedResx(file))
            {
                var baseFile = baseProject.Drive.GetFileInfo(file.Url);
                var sourceFile = sourceProject.Drive.GetFileInfo(file.Url);
                ResxDifferences changes = ResxDiffMerge.CompareResxFiles(sourceFile, baseFile);
                if (changes.None)
                    return false;
            }

            if (FileIsRelationship(file))
            {
                Guid sourceId = GetModelItemIdFromFile(file);
                if (sourceId == Guid.Empty)
                    return true;

                OrmRelationship baseRelationship = FindRelationshipInBaseById(sourceId, baseProject);
                if (baseRelationship != null)
                {
                    var sourceRelationship = sourceProject.Get<OrmRelationship>(file.Url);
                    var diffMerge = new ObjectDiffMerge();
                    var changes = diffMerge.CompareObjects(sourceRelationship, baseRelationship);
                    if (!changes.All(change => RelationshipChangeCanBeIgnored(change)))
                        warnings.Add(string.Format("{0} is an existing SalesLogix relationship that was renamed and also modified.  This file will need to be manually merged.", file.Url));

                    return false;
                }           
            }

            return true;
        }

        private Guid GetModelItemIdFromFile(IFileInfo file)
        {
            using (var stream = file.Open(FileMode.Open, FileAccess.Read))
            {
                return GetModelItemIdFromStream(stream);
            }
        }

        private Guid GetModelItemIdFromStream(Stream stream)
        {
            using (var reader = XmlReader.Create(stream))
            {
                while (reader.Read() && reader.NodeType != XmlNodeType.Element)
                    reader.Read();

                string id = reader.GetAttribute("id");

                return string.IsNullOrEmpty(id) ? Guid.Empty : new Guid(id);
            }
        }

        private OrmRelationship FindRelationshipInBaseById(Guid sourceId, IProject baseProject)
        {
            return baseProject.Models.Get<OrmModel>().Relationships.FirstOrDefault(rel => rel.Id == sourceId);
        }

        public bool CanMergeFile(string url)
        {
            if (url.EndsWith("EntityResources.resx", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        public void MergeFile(string url, IProject baseProject, IProject sourceProject, IProject targetProject)
        {
            if (url.EndsWith("EntityResources.resx", StringComparison.OrdinalIgnoreCase))
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
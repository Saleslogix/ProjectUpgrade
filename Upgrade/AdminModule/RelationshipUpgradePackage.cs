using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Sage.Platform.FileSystem;
using Sage.Platform.FileSystem.Interfaces;
using Sage.Platform.Orm.Entities;
using Sage.Platform.Projects;
using Sage.Platform.Upgrade.AdminModule.Localization;
using Sage.Platform.Upgrade.AdminModule.Properties;

namespace Sage.Platform.Upgrade.AdminModule
{
    [UpgradeVersion("7.5.3.2173")]
    [SRDescription(SR.RelationshipUpgradePackage_Description)]
    public class RelationshipUpgradePackage : UpgradePackage
    {
        private Dictionary<string, string> _entityNameByIdCache = new Dictionary<string, string>();

        [UpgradeStep]
        [SRDescription(SR.RelationshipUpgradePackage_UpdateFileNameFormat_Description)]
        public void EnsureAllRelationshipsHaveACurrentFileNameFormat()
        {
            Log.Info(Resources.RelationshipUpgradePackageGettingAllRelationships);
            IFileInfo[] relationshipFiles = Drive.GetDirectoryInfo(
                Path.Combine(EntityModelUrlConstants.PATH_ENTITY_MODEL, "Relationships"))
                .GetFiles(EntityModelUrlConstants.QRY_ALL_RELATIONSHIPS);

            var relationshipFilesInOldNameFormat = relationshipFiles.Where(file => FileNameIsInOldFormat(file.Name));

            if (!relationshipFilesInOldNameFormat.Any())
                return;

            Log.Info(Resources.RelationshipUpgradePackage_DeletingModelIndexCache);
            var modelIndexFile = Drive.GetFileInfo(ProjectBase.INDEX_URL);
            modelIndexFile.Delete();

            Log.Info(Resources.RelationshipUpgradePackage_LookingUpEntityIds);
            RetrieveAllEntityIds();

            foreach (IFileInfo relationshipFile in relationshipFilesInOldNameFormat)
            {
                string oldFileName = relationshipFile.Name;

                try
                {
                    Log.InfoFormat(Resources.RelationshipUpgradePackage_RenamingRelationshipFile, oldFileName);
                    if (relationshipFile.IsReadOnly)
                    {
                        relationshipFile.IsReadOnly = false;
                    }

                    var relationInfo = GetRelationshipInfoFromFile(relationshipFile);
                    string newFileName = GenerateNewRelationshipFileName(relationInfo);
                    string newUrl = relationshipFile.Directory.Url + "\\" + newFileName;
                    relationshipFile.MoveTo(newUrl);
                    Log.InfoFormat(Resources.RelationshipUpgradePackage_FileWasRenamed, oldFileName, newFileName);
                }
                catch (Exception e)
                {
                    Log.ErrorFormat(Resources.RelationshipUpgradePackage_ErrorRenamingRelationship + "\n{1}\n{2}", 
                        oldFileName, e.Message, e.StackTrace);
                }
            }
        }

        private bool FileNameIsInOldFormat(string fileName)
        {
            const int NUM_FILENAME_PARTS_IN_CURRENT_FORMAT = 5;
            return fileName.Split('.').Length != NUM_FILENAME_PARTS_IN_CURRENT_FORMAT;
        }

        private RelationshipInfo GetRelationshipInfoFromFile(IFileInfo relationshipFile)
        {
            using (Stream stream = relationshipFile.Open(FileMode.Open, FileAccess.Read))
            using (XmlReader xmlReader = XmlReader.Create(stream))
            {
                xmlReader.ReadToFollowing("relationship");
                var relationInfo = new RelationshipInfo()
                                       {
                                           Id = xmlReader.GetAttribute("id"),
                                           ParentEntityId = xmlReader.GetAttribute("parentEntityId"),
                                           ChildEntityId = xmlReader.GetAttribute("childEntityId")
                                       };

                return relationInfo;
            }
        }

        private string GenerateNewRelationshipFileName(RelationshipInfo relationInfo)
        {
            const string SHORT_FILENAME = "{0}.{1}.{2}" + EntityModelUrlConstants.EXT_RELATIONSHIP;

            string parentEntityName = _entityNameByIdCache[relationInfo.ParentEntityId];
            string childEntityName = _entityNameByIdCache[relationInfo.ChildEntityId];
            return string.Format(SHORT_FILENAME, parentEntityName, childEntityName, new Guid(relationInfo.Id).ToString("N"));
        }

        private void RetrieveAllEntityIds()
        {
            var modelDir = Drive.GetDirectoryInfo(EntityModelUrlConstants.PATH_ENTITY_MODEL);
            IFileInfo[] entityFiles = Search.FindFiles(modelDir, EntityModelUrlConstants.QRY_ALL_ENTITIES, 2);
            foreach (IFileInfo entityFile in entityFiles)
            {
                using (Stream stream = entityFile.Open(FileMode.Open, FileAccess.Read))
                using (XmlReader xmlReader = XmlReader.Create(stream))
                {
                    xmlReader.ReadToFollowing("entity");
                    string id = xmlReader.GetAttribute("id");
                    string name = xmlReader.GetAttribute("name");
                    _entityNameByIdCache.Add(id, name);
                }    
            }
        }
    }

    internal class RelationshipInfo
    {
        public string Id { get; set; }
        public string ParentEntityId { get; set; }
        public string ChildEntityId { get; set; }
    }
}
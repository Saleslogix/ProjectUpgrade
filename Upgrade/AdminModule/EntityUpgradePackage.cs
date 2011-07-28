using System.IO;
using System.Xml.Serialization;
using Sage.Platform.FileSystem;
using Sage.Platform.Orm.Entities;
using Sage.Platform.Projects;
using Sage.Platform.Upgrade.AdminModule.Localization;
using Sage.Platform.Upgrade.AdminModule.Properties;

namespace Sage.Platform.Upgrade.AdminModule
{
    [UpgradeVersion("7.5.3.2173")]
    [SRDescription(SR.EntityUpgradePackage_Description)]
    public class EntityUpgradePackage : UpgradePackage
    {
        private readonly XmlSerializer _serializer = new XmlSerializer(typeof (OrmEntity));

        [UpgradeStep]
        [SRDescription(SR.EntityUpgradePackage_SetLastUpdatedProperty_Description)]
        public void SetLastUpdatedProperty()
        {
            var modelDir = Drive.GetDirectoryInfo(EntityModelUrlConstants.PATH_ENTITY_MODEL);
            var files = Search.FindFiles(modelDir, EntityModelUrlConstants.QRY_ALL_ENTITIES, 2);

            foreach (var file in files)
            {
                OrmEntity entity;

                using (var stream = file.Open(FileMode.Open))
                {
                    entity = (OrmEntity) _serializer.Deserialize(stream);
                }

                if (entity.GetLastUpdatedProperty() != null)
                {
                    Log.DebugFormat("Entity '{0}' already has a last updated property assigned.", entity.Name);
                    continue;
                }

                var prop = entity.Properties.GetFieldPropertyByFieldName("MODIFYDATE") ??
                           entity.Properties.GetFieldPropertyByFieldName("MODIFIEDDATE");

                if (prop == null)
                {
                    Log.WarnFormat(Resources.EntityUpgradePackage_Unable_to_find_an_appropriate_last_updated_property, entity.Name);
                    continue;
                }

                Log.DebugFormat("Assigning the last updated property of '{0}' to '{1}'", entity.Name, prop.PropertyName);
                entity.SetLastUpdatedProperty(prop);

                using (var stream = file.OpenWrite())
                {
                    _serializer.Serialize(stream, entity);
                    stream.SetLength(stream.Position);
                }
            }
        }
    }
}
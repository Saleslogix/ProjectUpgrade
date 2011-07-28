using System;
using System.Linq;
using System.Xml.Serialization;
using Sage.Platform.Application;
using Sage.Platform.Projects;
using Sage.Platform.Projects.Interfaces;
using Sage.Platform.Upgrade.AdminModule.Localization;
using Sage.Platform.Upgrade.AdminModule.Properties;

namespace Sage.Platform.Upgrade.AdminModule
{
    [UpgradeVersion("7.5.0.1321")]
    [SRDescription(SR.MashupUpgradePackage_Description)]
    public class MashupUpgradePackage : UpgradePackage
    {
        private const string _mashupModelTypeName = "Sage.Platform.Mashups.AdminModule.MashupModel, Sage.Platform.Mashups.AdminModule";

        [ServiceDependency]
        public IModelTypeEnumerator ModelTypeEnumerator { private get; set; }

        [UpgradeStep]
        [SRDescription(SR.MashupUpgradePackage_AddMashupModule_Description)]
        public void AddMashupModule()
        {
            var projectFile = Drive.GetFileInfo(ProjectBase.PROJECT_INFO_PATH);

            if (!projectFile.Exists)
            {
                return;
            }

            var ser = new XmlSerializer(typeof (ProjectInfo));
            ProjectInfo info;

            using (var reader = projectFile.OpenText())
            {
                info = (ProjectInfo) ser.Deserialize(reader);
            }

            if (info.ModelTypeNames == null)
            {
                info.ModelTypeNames = new ModelTypeCollection();
            }

            var changed = false;

            if (info.ModelTypeNames.Count == 0)
            {
#pragma warning disable 618,612
                var modelTypeNames = ProjectWorkspace.ModelTypeNames;
#pragma warning restore 618,612
                if (modelTypeNames != null && modelTypeNames.Count > 0)
                {
                    Log.Info(Resources.MashupUpgradePackage_Migrating_model_type_names_from_project_workspace);
                    modelTypeNames.ForEach(info.ModelTypeNames.Add);
                    changed = true;
                }
                else
                {
                    modelTypeNames = ModelTypeEnumerator.GetModelTypes();

                    if (modelTypeNames != null && modelTypeNames.Count > 0)
                    {
                        Log.Info(Resources.MashupUpgradePackage_Migrating_model_type_names_from_configuration);
                        info.ModelTypeNames = modelTypeNames;
                        changed = true;
                    }
                }
            }

            var mashupModelType = Type.GetType(_mashupModelTypeName);

            if (mashupModelType != null && info.ModelTypeNames.All(name => name.GetModelType() != mashupModelType))
            {
                Log.Info(Resources.MashupUpgradePackage_Appending_mashup_model_type_name);
                info.ModelTypeNames.Add(new ModelType(_mashupModelTypeName));
                changed = true;
            }

            if (changed)
            {
                using (var writer = projectFile.CreateText())
                {
                    ser.Serialize(writer, info);
                }
            }
        }
    }
}
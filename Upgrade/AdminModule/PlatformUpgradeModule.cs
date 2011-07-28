using Sage.Platform.Projects;
using Sage.Platform.Upgrade.AdminModule.Properties;

namespace Sage.Platform.Upgrade.AdminModule
{
    [UpgradePackage(typeof(MashupUpgradePackage))]
    [UpgradePackage(typeof(SupportFilesMigrationUpgradePackage))]
    [UpgradePackage(typeof(QuickFormUpgradePackage))]
    [UpgradePackage(typeof(EntityUpgradePackage))]
    [UpgradePackage(typeof(RelationshipUpgradePackage))]
    public class PlatformUpgradeModule : UpgradeModule
    {
        public override string ToString()
        {
            return Resources.UpgradeModuleFriendlyName;
        }
    }
}
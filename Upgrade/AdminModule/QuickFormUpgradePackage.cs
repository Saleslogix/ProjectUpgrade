using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Sage.Platform.FileSystem;
using Sage.Platform.FileSystem.Interfaces;
using Sage.Platform.Orm.Entities;
using Sage.Platform.Projects;
using Sage.Platform.Upgrade.AdminModule.Localization;
using Sage.Platform.Upgrade.AdminModule.Properties;

namespace Sage.Platform.Upgrade.AdminModule
{
    [UpgradeVersion("7.5.0.1332")]
    [SRDescription(SR.QuickFormUpgradePackage_Description)]
    public class QuickFormUpgradePackage : UpgradePackage
    {
        private readonly XmlSerializer _serializer = new XmlSerializer(typeof (OrmEntityMethod));

        [UpgradeStep]
        [SRDescription(SR.QuickFormUpgradePackage_MoveSnippetActionMethods_Description)]
        public void MoveSnippetActionMethods()
        {
            var modelDir = Drive.GetDirectoryInfo(EntityModelUrlConstants.PATH_ENTITY_MODEL);
            var query = new FileQuery(modelDir, EntityModelUrlConstants.QRY_ALL_METHODS, 2);
            var methodFiles = Search.FindInFiles(query, " methodType=\"Other\"", StringComparison.InvariantCultureIgnoreCase);
            methodFiles.ForEach(MoveMethodFile);
        }

        private void MoveMethodFile(IFileInfo methodFile)
        {
            var dirName = methodFile.Directory.Url;
            var qfDirName = Path.Combine(dirName, "QuickForms");
            var qfDir = Drive.GetDirectoryInfo(qfDirName);

            if (!qfDir.Exists)
            {
                qfDir.Create();
            }

            OrmEntityMethod method;

            using (var stream = methodFile.Open(FileMode.Open))
            {
                try
                {
                    method = (OrmEntityMethod) _serializer.Deserialize(stream);
                }
                catch
                {
                    Log.Warn(Resources.QuickFormUpgradePackage_Unable_to_parse_method_file + methodFile);
                    return;
                }
            }

            //var query = new FileQuery(qfDir, "*.main.quickform.xml", 0);
            //var qfFile = Search.FindFirstInFiles(query, string.Format(" methodId=\"{0}\"", method.Id.ToString("D")), StringComparison.InvariantCultureIgnoreCase);

            //if (qfFile != null && method.MethodParameters.Count > 0)
            //{
            //    var param = method.MethodParameters[0];
            //    param.ParamName = "form";
            //    param.ParamType = "I" + qfFile.Name.Substring(0, qfFile.Name.Length - 19);
            //}

            Log.Info(Resources.QuickFormUpgradePackage_Moving_snippet_action_method_file + methodFile);
            methodFile.MoveTo(Path.Combine(qfDirName, methodFile.Name));
            method.PreExecuteTargets
                .Union(method.MethodTargets)
                .Union(method.PostExecuteTargets)
                .Union(method.PostFlushTargets)
                .OfType<OrmMethodTargetSnippet>()
                .SelectMany(snippetMethod =>
                            new[]
                                {
                                    string.Format(
                                        "Sage.SnippetLibrary.CSharp.@.{0}{1}",
                                        snippetMethod.CodeSnippetId,
                                        EntityModelUrlConstants.EXT_CS_CODESNIPPET),
                                    string.Format(
                                        "Sage.SnippetLibrary.VB.@.{0}{1}",
                                        snippetMethod.CodeSnippetId,
                                        EntityModelUrlConstants.EXT_VB_CODESNIPPET)
                                })
                .Select(snippetFileName => Drive.GetFileInfo(Path.Combine(dirName, snippetFileName)))
                .Where(snippetFile => snippetFile.Exists)
                .ForEach(MoveSnippetFile);
        }

        private void MoveSnippetFile(IFileInfo snippetFile)
        {
            Log.Info(Resources.QuickFormUpgradePackage_Moving_method_target_snippet_file + snippetFile);
            Log.WarnFormat(Resources.QuickFormUpgradePackage_Snippet_will_need_to_be_manually_updated, snippetFile.Name);
            var qfDirName = Path.Combine(snippetFile.Directory.Url, "QuickForms");
            snippetFile.MoveTo(Path.Combine(qfDirName, snippetFile.Name));
        }
    }
}
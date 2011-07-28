using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rhino.Mocks;
using Sage.Platform.FileSystem.Interfaces;
using System;
using System.Data.SQLite;
using Sage.Platform.NUnit;

namespace Sage.Platform.Upgrade.Test
{
    [TestFixture]
    public class ProjectUpgradeServiceTest
    {
        private PersistentConnectionProjectUpgradeService _projectUpgradeService;

        [SetUp]
        public void Setup()
        {
            _projectUpgradeService = new PersistentConnectionProjectUpgradeService();
            _projectUpgradeService.CreateReleaseDbSchemaIfNecessary();
        }

        [TearDown]
        public void Teardown()
        {
            _projectUpgradeService.CloseConnection();
        }

        [Test]
        public void AddFileToReleaseDb_SetsVersionInDb()
        {
            var expectedVersion = new Version("7.5.3.1");
            var url = @"\SomeFolder\SomeFile.entity.xml";
            var connection = _projectUpgradeService.Connection;
            _projectUpgradeService.AddFileToReleaseDb(StubFile(url), expectedVersion, connection);

            using (IDataReader reader = _projectUpgradeService.GetReaderOfMatchesByUrl(url, connection))
            {
                Assert.IsTrue(reader.Read(), "Record not found.");
                Assert.AreEqual(expectedVersion.Major, reader["MAJORVERSION"]);
                Assert.AreEqual(expectedVersion.Minor, reader["MINORVERSION"]);
                Assert.AreEqual(expectedVersion.Build, reader["BUILD"]);
                Assert.AreEqual(expectedVersion.Revision, reader["REVISION"]);
            }
        }

        [Test]
        public void AddFileToReleaseDb_StripsOffFolderOnFileNameColumn()
        {
            var fileName = "SomeFile.entity.xml";
            var url = @"\SomeFolder\" + fileName;
            var connection = _projectUpgradeService.Connection;
            _projectUpgradeService.AddFileToReleaseDb(StubFile(url), new Version(), connection);

            using (IDataReader reader = _projectUpgradeService.GetReaderOfMatchesByUrl(url, connection))
            {
                Assert.IsTrue(reader.Read(), "Record not found.");
                Assert.AreEqual(fileName, reader["FILENAME"]);
            }
        }

        [Test]
        public void AddFileToReleaseDb_WithNonNullBundleId_SetsBundleOnFileReleaseInfo()
        {
            string bundleFileName = "SomeBundle.zip";
            var version = new Version("7.5.2.0");
            BundleInfo bundleInfo = new BundleInfo("SomeBundle", bundleFileName, version);
            _projectUpgradeService.InsertBundleRecord(bundleInfo, version);
            var url = @"\SomeFolder\SomeFile.entity.xml";
            var connection = _projectUpgradeService.Connection;
            int bundleId = _projectUpgradeService.RegisteredBundles.Keys.First();
            _projectUpgradeService.AddFileToReleaseDb(StubFile(url), new Version(), connection, bundleId);
            FileReleaseInfo matchingRelease = _projectUpgradeService.GetReleaseInfoByPath(url).First();
            Assert.NotNull(matchingRelease.Bundle);
            Assert.AreEqual(bundleFileName, matchingRelease.Bundle.FileName);
        }

        [Test]
        public void AddFileToReleaseDb_WithNonNullProjectId_SetsProjectOnFileReleaseInfo()
        {
            var projectInfo = new RegisteredProjectInfo("SomeProject", null, new Version("7.5.2.0"));
            int projectId = (int)_projectUpgradeService.InsertProjectRecord(projectInfo);
            var connection = _projectUpgradeService.Connection;
            var url = @"\SomeFolder\blah.txt";
            _projectUpgradeService.AddFileToReleaseDb(StubFile(url), new Version(), connection, null, projectId);
            FileReleaseInfo matchingRelease = _projectUpgradeService.GetReleaseInfoByPath(url).First();
            Assert.NotNull(matchingRelease.Project);
            Assert.AreEqual(projectInfo.Name, matchingRelease.Project.Name);
            Assert.AreEqual(projectInfo.Version, matchingRelease.Project.Version);
        }

        [Test]
        public void CreateFileReleaseInfosFromReader_PopulatesAllFileReleaseInfoFields()
        {
            var fileName = "SomeFile.entity.xml";
            var url = @"\SomeFolder\" + fileName;
            var expectedVersion = new Version("7.5.3.1");
            var connection = _projectUpgradeService.Connection;
            _projectUpgradeService.AddFileToReleaseDb(StubFile(url), expectedVersion, connection);
            using (IDataReader reader = _projectUpgradeService.GetReaderOfMatchesByUrl(url, connection))
            {
                var fileInfos = _projectUpgradeService.CreateFileReleaseInfosFromReader(reader);
                var file = fileInfos.FirstOrDefault();
                Assert.NotNull(file);
                Assert.AreEqual(url, file.Path);
                Assert.AreEqual(fileName, file.FileName);
                Assert.AreEqual(expectedVersion.Major, file.Version.Major);
                Assert.AreEqual(expectedVersion.Minor, file.Version.Minor);
                Assert.AreEqual(expectedVersion.Build, file.Version.Build);
                Assert.AreEqual(expectedVersion.Revision, file.Version.Revision);
            }
        }

        [Test]
        public void FindAllMatchingReleaseInfo_WithMatchingAndNonMatchingPaths_OnlyReturnsMatches()
        {
            var matchingFile = StubFile(@"\SomeFolder\File.txt");
            var nonMatchingFile = StubFile(@"\SomeFolder\OtherFile.txt");

            var connection = _projectUpgradeService.Connection;
            _projectUpgradeService.AddFileToReleaseDb(matchingFile, new Version(), connection);
            _projectUpgradeService.AddFileToReleaseDb(nonMatchingFile, new Version(), connection);

            var actualMatches = _projectUpgradeService.FindAllMatchingReleaseInfo(matchingFile).ToList();
            Assert.AreEqual(1, actualMatches.Count);
            Assert.AreEqual(matchingFile.Url, actualMatches.First().Path);
        }

        [Test]
        [TestCase(@"\modelindex.xml", Result = false)]
        [TestCase(@"\project.info.xml", Result = false)]
        [TestCase(@"\\Bundle Manifests\New Manifest\blah.txt", Result = false)]
        [TestCase(@"\\bundleData.xml", Result = false)]
        [TestCase(@"\\manifest.xml", Result = false)]
        [TestCase("validfile.txt", Result = true)]
        public bool FileShouldBeRecorded_OnlyReturnsFalseForExcludedPaths(string path)
        {
            return _projectUpgradeService.FileShouldBeTracked(path);
        }

        [Test]
        public void LoadRegisteredBundles_LoadsDbDataIntoBundleInfoDictionary()
        {
            string fileName = "file.txt";
            string name = "my name";
            Version version = new Version("7.5.3.2");
            BundleInfo bundleInfo = new BundleInfo(name, fileName, version);

            _projectUpgradeService.InsertBundleRecord(bundleInfo, version);

            Dictionary<int, BundleInfo> registeredBundles = _projectUpgradeService.LoadRegisteredBundles();
            Assert.AreEqual(1, registeredBundles.Count);

            var bundle = registeredBundles.Values.First();
            Assert.AreEqual(fileName, bundle.FileName);
            Assert.AreEqual(name, bundle.Name);
            Assert.AreEqual(version, bundle.Version);
        }

        [Test]
        public void ExtractBundleInfoFromBundleManifest_CreatesBundleInfoPopulatedFromManifestXml()
        {
            string bundleFileName = "bundle.zip";
            const string MANIFEST_TEXT =
                @"<?xml version=""1.0""?>
                <BundleManifest id=""ed1f9b38-8783-4a1d-9738-5fd8e0f4357c"">
                  <bundleProperties>
                    <name>TestName</name>
                    <description><![CDATA[]]></description>
                    <majorVersion>7</majorVersion>
                    <minorVersion>5</minorVersion>
                    <revision>3</revision>
                    <build>1</build>
                    <autoIncrement>false</autoIncrement>
                  </bundleProperties>
                </BundleManifest>";

            StreamReader reader = MockRepository.GenerateStub<StreamReader>();
            reader.Stub(r => r.ReadToEnd()).Return(MANIFEST_TEXT);
            IFileInfo manifestFile = MockRepository.GenerateStub<IFileInfo>();
            manifestFile.Stub(file => file.OpenText()).Return(reader);

            BundleInfo bundleInfo = _projectUpgradeService.ExtractBundleInfoFromBundleManifest(manifestFile, bundleFileName);
            Assert.NotNull(bundleInfo);
            Assert.AreEqual(bundleFileName, bundleInfo.FileName);
            Assert.AreEqual("TestName", bundleInfo.Name);
            Assert.AreEqual(new Version("7.5.3.1"), bundleInfo.Version);
        }

        [Test]
        public void DetermineProjectVersionFromMatches_WithMultipleMatchesWithProjects_ReturnsProjectWithHighestVersion()
        {
            var highestVersionProject = new RegisteredProjectInfo("project1", null, new Version("2.0"));
            var lowestVersionProject = new RegisteredProjectInfo("project2", null, new Version("1.0"));
            var match1 = new FileReleaseInfo("", "", new Version(), null, null, highestVersionProject);
            var match2 = new FileReleaseInfo("", "", new Version(), null, null, lowestVersionProject);
            var matches = new List<List<FileReleaseInfo>>
                              {
                                  new List<FileReleaseInfo> {match1},
                                  new List<FileReleaseInfo> {match2}
                              };
            _projectUpgradeService.RegisteredProjects.Add(0, highestVersionProject);
            _projectUpgradeService.RegisteredProjects.Add(1, lowestVersionProject);
            var matchedProject = _projectUpgradeService.DetermineProjectVersionFromMatches(matches);

            Assert.AreEqual(highestVersionProject, matchedProject);
        }

        [Test]
        public void DetermineProjectVersionFromMatches_WithElementWithMultipleMatches_DoesNotIncludeMultipleMatchEntriesInResultingCalcuation()
        {
            var highestVersionProject = new RegisteredProjectInfo("project1", null, new Version("2.0"));
            var lowestVersionProject = new RegisteredProjectInfo("project2", null, new Version("1.0"));
            var match1 = new FileReleaseInfo("", "", new Version(), null, null, highestVersionProject);
            var match2 = new FileReleaseInfo("", "", new Version(), null, null, highestVersionProject);
            var match3 = new FileReleaseInfo("", "", new Version(), null, null, lowestVersionProject);
            var matches = new List<List<FileReleaseInfo>>
                              {
                                  new List<FileReleaseInfo> {match1, match2},
                                  new List<FileReleaseInfo> {match3}
                              };
            _projectUpgradeService.RegisteredProjects.Add(0, highestVersionProject);
            _projectUpgradeService.RegisteredProjects.Add(1, lowestVersionProject);
            var matchedProject = _projectUpgradeService.DetermineProjectVersionFromMatches(matches);

            Assert.AreEqual(lowestVersionProject, matchedProject);
        }

        [Test]
        public void DetermineProjectVersionFromMatches_WithMatchWithoutProject_DoesNotIncludeProjectlessMatchInResultingCalcuation()
        {
            var expectedProject = new RegisteredProjectInfo("project", null, new Version("1.2.3.4"));
            var matchWithoutProject = new FileReleaseInfo("", "", new Version(), null, null, null);
            var matchWithProject = new FileReleaseInfo("", "", new Version(), null, null, expectedProject);
            var matches = new List<List<FileReleaseInfo>>
                              {
                                  new List<FileReleaseInfo> {matchWithProject},
                                  new List<FileReleaseInfo> {matchWithoutProject}
                              };
            _projectUpgradeService.RegisteredProjects.Add(0, expectedProject);
            var matchedProject = _projectUpgradeService.DetermineProjectVersionFromMatches(matches);

            Assert.AreEqual(expectedProject, matchedProject);
        }

        [Test]
        public void GetBundlesApplied_ReturnsOnlyBundlesFoundInBothMatchesAndManifests()
        {
            var bundleInBoth = new BundleInfo("Match", "", new Version());
            var bundleOnlyInMatches = new BundleInfo("OnlyInMatches", "", new Version());
            var bundleOnlyInManifests = new BundleInfo("OnlyInManifests", "", new Version());
            var matchedBundles = new List<BundleInfo> { bundleInBoth, bundleOnlyInMatches };
            var bundlesFromManifests = new List<BundleInfo> { bundleInBoth, bundleOnlyInManifests };
            List<BundleInfo> bundlesApplied = _projectUpgradeService.GetBundlesApplied(matchedBundles, bundlesFromManifests);

            Assert.AreEqual(1, bundlesApplied.Count);
            Assert.AreEqual(bundleInBoth, bundlesApplied.First());
        }

        [Test]
        public void GetPossibleBundlesApplied_WithBundlesNotInRegisteredList_DoesNotReturnUnregisteredBundles()
        {
            var registeredBundle = new BundleInfo("registered", null, new Version());
            var unregisteredBundle = new BundleInfo("unregistered", null, new Version());
            List<BundleInfo> bundlesFromManifests = new List<BundleInfo> { registeredBundle, unregisteredBundle };
            _projectUpgradeService.RegisteredBundles.Add(0, registeredBundle);
            List<BundleInfo> possibleMatches = _projectUpgradeService.GetPossibleBundlesApplied(new List<BundleInfo>(),
                                                                                                 bundlesFromManifests);

            Assert.AreEqual(1, possibleMatches.Count);
            Assert.AreEqual(registeredBundle, possibleMatches.First());
        }

        [Test]
        public void GetPossibleBundlesApplied_OnlyReturnsBundlesInManifestsButNotMatches()
        {
            var bundlefromManifest1 = new BundleInfo("manifest1 bundle", null, new Version());
            var bundlefromManifest2 = new BundleInfo("manifest2 bundle", null, new Version());
            List<BundleInfo> bundlesFromManifests = new List<BundleInfo> { bundlefromManifest1, bundlefromManifest2 };
            List<BundleInfo> bundlesFromMatches = new List<BundleInfo> { bundlefromManifest1 };
            _projectUpgradeService.RegisteredBundles.Add(0, bundlefromManifest1);
            _projectUpgradeService.RegisteredBundles.Add(1, bundlefromManifest2);
            List<BundleInfo> possibleMatches = _projectUpgradeService.GetPossibleBundlesApplied(bundlesFromMatches,
                                                                                                 bundlesFromManifests);

            Assert.AreEqual(1, possibleMatches.Count);
            Assert.AreEqual(bundlefromManifest2, possibleMatches.First());
        }

        [Test]
        public void FindInstalledBundlesByMatches_DoesNotReturnBundlesWhereMajorMinorBuildDiffersFromMatchVersion()
        {
            var bundleToMatch = new BundleInfo("bundle to match", "", new Version());
            var match1 = new FileReleaseInfo("match1", "", new Version("7.5.3.1"), null, bundleToMatch, null);
            var bundleToNotMatch = new BundleInfo("bundle to not match", "", new Version());
            var match2 = new FileReleaseInfo("match2", "", new Version("7.5.0.1"), null, bundleToNotMatch, null);

            var allMatches = new List<List<FileReleaseInfo>>
                                 {
                                     new List<FileReleaseInfo> { match1, match2}
                                 };
            var installedBundles = _projectUpgradeService.FindInstalledBundlesByMatches(allMatches, new Version("7.5.3"));
            Assert.AreEqual(1, installedBundles.Count());
            Assert.AreEqual(bundleToMatch, installedBundles.First());
        }

        [Test]
        public void FindInstalledBundlesByMatches_WithMultipleMatchesOnTheSameBundle_OnlyReturnsTheSameBundleOnce()
        {
            var bundle = new BundleInfo("test", "", new Version());
            var match1 = new FileReleaseInfo("match1", "", new Version("7.5.3.1"), null, bundle, null);
            var match2 = new FileReleaseInfo("match2", "", new Version("7.5.3.2"), null, bundle, null);

            var allMatches = new List<List<FileReleaseInfo>>
                                 {
                                     new List<FileReleaseInfo> { match1, match2}
                                 };

            var installedBundles = _projectUpgradeService.FindInstalledBundlesByMatches(allMatches, new Version("7.5.3"));

            Assert.AreEqual(1, installedBundles.Count());
            Assert.AreEqual(bundle, installedBundles.First());
        }

        //making sure this call returns an empty list and doesn't blow up or return null
        [Test]
        public void GetBundlesApplied_WithNoMatchedBundlesAndNoBundlesFromManifests_ReturnsEmptyList()
        {
            var bundlesApplied = _projectUpgradeService.GetBundlesApplied(new List<BundleInfo>(), new List<BundleInfo>());
            Assert.IsNotNull(bundlesApplied);
            Assert.AreEqual(0, bundlesApplied.Count);
        }

        [Test]
        public void GetBackupFileNameFromProjectPath_WithAZipPath_ReturnsTheEmbeddedZipFileName()
        {
            var pathWithZip = @"ZIP:\C:\Sage SalesLogix v7.5 Project.Backup.zip\Model";
            string zipFileName = _projectUpgradeService.GetBackupFileNameFromProjectPath(pathWithZip);
            Assert.AreEqual("Sage SalesLogix v7.5 Project.Backup.zip", zipFileName);
        }

        [Test]
        public void GetBackupFileNameFromProjectPath_WithoutAZipPath_ReturnsNull()
        {
            var pathWithoutZip = @"C:\Model";
            string zipFileName = _projectUpgradeService.GetBackupFileNameFromProjectPath(pathWithoutZip);
            Assert.IsNull(zipFileName);
        }

        //making sure this call returns an empty list and doesn't blow up or return null
        [Test]
        public void FindInstalledBundlesByMatches_WithNoMatches_ReturnsAnEmptyListOfBundles()
        {
            var installedBundles = _projectUpgradeService.FindInstalledBundlesByMatches(new List<List<FileReleaseInfo>>(),
                new Version());

            Assert.IsNotNull(installedBundles);
            Assert.AreEqual(0, installedBundles.Count());
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void BuildBaseProject_WithNonExistentProjectBackupDir_ThrowsArgumentException()
        {
            IDirectoryInfo projectBackupDir = MockRepository.GenerateStub<IDirectoryInfo>();
            projectBackupDir.Stub(dir => dir.Exists).Return(false);

            _projectUpgradeService.BuildBaseProject(null, projectBackupDir, null);
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void BuildBaseProject_WithNullBackupFileName_ThrowsArgumentException()
        {
            IDirectoryInfo projectBackupDir = MockRepository.GenerateStub<IDirectoryInfo>();
            projectBackupDir.Stub(dir => dir.Exists).Return(true);
            RegisteredProjectInfo projectInfo = new RegisteredProjectInfo(null, null, new Version());
            ProjectInstallInfo installInfo = new ProjectInstallInfo(projectInfo, null, null);

            _projectUpgradeService.BuildBaseProject(installInfo, projectBackupDir, null);
        }

        [Test]
        [ExpectedException(typeof(ApplicationException))]
        public void BuildBaseProject_WithNonExistentBackupFile_ThrowsArgumentException()
        {
            IDirectoryInfo projectBackupDir = MockRepository.GenerateStub<IDirectoryInfo>();
            projectBackupDir.Stub(dir => dir.Exists).Return(true);
            projectBackupDir.Stub(dir => dir.Exists).Return(true);
            projectBackupDir.Stub(dir => dir.GetFiles("")).IgnoreArguments().Return(new IFileInfo[] {});
            projectBackupDir.Stub(dir => dir.FullName).Return("C:\\");

            RegisteredProjectInfo projectInfo = new RegisteredProjectInfo(null, "dummy.zip", new Version());
            ProjectInstallInfo installInfo = new ProjectInstallInfo(projectInfo, null, null);

            _projectUpgradeService.BuildBaseProject(installInfo, projectBackupDir, null);
        }

        [ExpectedException(typeof(InvalidProjectException))]
        [Test]
        public void DetermineProjectVersionFromMatches_WithNoMatches_ThrowsException()
        {
            _projectUpgradeService.DetermineProjectVersionFromMatches(new List<List<FileReleaseInfo>>());
        }

        [ExpectedException(typeof(InvalidProjectException))]
        [Test]
        public void DetermineProjectVersionFromMatches_WithNoMatchesWithProjects_ThrowsException()
        {
            var match = new FileReleaseInfo("", "", new Version(), null, null, null);
            var allMatches = new List<List<FileReleaseInfo>> { new List<FileReleaseInfo> { match } };
            _projectUpgradeService.DetermineProjectVersionFromMatches(allMatches);
        }

        private IFileInfo StubFile(string url)
        {
            var file = MockRepository.GenerateStub<IFileInfo>();
            file.Stub(f => f.Url).Return(url);
            return file;
        }
    }

    public class PersistentConnectionProjectUpgradeService : ProjectUpgradeService
    {
        internal const string IN_MEMORY_CONNECTION_STRING = "Data Source=:memory:;Version=3;";

        private IDbConnection _innerConnection;
        private IDbConnection _persistentConnection;

        public IDbConnection Connection { get { return _persistentConnection; } }

        protected override IDbConnection GetOpenConnection()
        {
            if (_persistentConnection == null)
            {
                _innerConnection = new SQLiteConnection(IN_MEMORY_CONNECTION_STRING);
                _persistentConnection = new PersistentDbConnectionWrapper(_innerConnection);
            }

            if (_persistentConnection.State == ConnectionState.Closed)
                _persistentConnection.Open();

            return _persistentConnection;
        }

        internal override byte[] CalculateHashCode(IFileInfo file)
        {
            return new byte[] { 0 };
        }

        public void CloseConnection()
        {
            _persistentConnection.Dispose();
            _innerConnection.Dispose();
        }
    }
}
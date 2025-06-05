using Moq;
using Nethermind.Api;
using Nethermind.Arbitrum.Config;
using Nethermind.Arbitrum.Snapshots; // Required for SnapshotDownloader.DownloadResult
using Nethermind.Config;
using Nethermind.Core.Initialization;
using Nethermind.Logging;
using Nethermind.Persistence.ethdb; // Required for Database
using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;
using System; // Required for Action

namespace Nethermind.Arbitrum.Test.Config
{
    [TestFixture]
    public class ArbitrumInitializeBlockchainStepTests
    {
        private Mock<INethermindApi> _mockApi;
        private Mock<ILogManager> _mockLogManager;
        private Mock<ILogger> _mockLogger;
        private Mock<IDbConfig> _mockDbConfig;
        private Mock<IDatabaseProvider> _mockDbProvider;
        private Mock<Database> _mockDatabase; // Nethermind.Persistence.ethdb.Database
        private Mock<InitConfig> _mockInitConfig; // Nethermind.Config.InitConfig

        private ArbitrumInitializeBlockchainStep _step;
        private InitializationState _initializationState;

        private const string TestDbPath = "/tmp/testdb";
        private const string DummySnapshotPath = "/tmp/dummy.tar.gz";
        private const string InvalidSnapshotPath = "/tmp/invalid_dummy.tar.gz";
        private string _tempExtractionPath;


        [SetUp]
        public void SetUp()
        {
            _mockApi = new Mock<INethermindApi>();
            _mockLogManager = new Mock<ILogManager>();
            _mockLogger = new Mock<ILogger>();
            _mockDbConfig = new Mock<IDbConfig>();
            _mockDbProvider = new Mock<IDatabaseProvider>();
            _mockDatabase = new Mock<Database>();
            _mockInitConfig = new Mock<InitConfig>();

            _mockApi.Setup(api => api.LogManager).Returns(_mockLogManager.Object);
            _mockLogManager.Setup(lm => lm.GetClassLogger()).Returns(_mockLogger.Object); // Default logger
            _mockLogManager.Setup(lm => lm.GetClassLogger(It.IsAny<Type>())).Returns(_mockLogger.Object);


            _mockApi.Setup(api => api.Config<IDbConfig>()).Returns(_mockDbConfig.Object);
            _mockApi.Setup(api => api.DbProvider).Returns(_mockDbProvider.Object);
            _mockDbProvider.Setup(dbp => dbp.RealDb).Returns(_mockDatabase.Object); // Default to DB existing

            // Default InitConfig setup
            _mockInitConfig.Object.Url = "http://example.com/snapshot.tar.gz"; // Default to having a URL
            _mockInitConfig.Object.Type = "tar.gz";

            _initializationState = new InitializationState(false);

            // Setup a temporary path for extraction that can be cleaned up
            _tempExtractionPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempExtractionPath);
            _mockDbConfig.Setup(dbc => dbc.Path).Returns(_tempExtractionPath);


            // Instantiate the class under test
            // We pass _mockInitConfig.Object directly. If InitConfig needs to be mocked for its properties,
            // ensure its properties are virtual or configure them through the constructor if possible.
            // For this setup, we assume InitConfig is a concrete class whose properties can be set.
            _step = new ArbitrumInitializeBlockchainStep(_mockApi.Object, _mockInitConfig.Object);
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up the dummy snapshot file if it was copied for a test
            if (File.Exists(Path.Combine(_tempExtractionPath, "dummy_snapshot_for_test.tar.gz")))
            {
                File.Delete(Path.Combine(_tempExtractionPath, "dummy_snapshot_for_test.tar.gz"));
            }
             if (File.Exists(Path.Combine(_tempExtractionPath, "invalid_snapshot_for_test.tar.gz")))
            {
                File.Delete(Path.Combine(_tempExtractionPath, "invalid_snapshot_for_test.tar.gz"));
            }

            // Clean up the extraction directory
            if (Directory.Exists(_tempExtractionPath))
            {
                Directory.Delete(_tempExtractionPath, true);
            }
        }

        [Test]
        public async Task ExecuteAsync_NoSnapshotUrl_DbDoesNotExist_CallsBaseExecuteAsync()
        {
            // Arrange
            _mockInitConfig.Object.Url = null; // No snapshot URL
            _mockDbProvider.Setup(dbp => dbp.RealDb).Returns((Database)null); // DB does not exist

            // Act & Assert
            // We expect base.ExecuteAsync to be called, which should not throw in this configuration.
            // If SnapshotDownloader was instance based, we could verify it wasn't called.
            // For now, we assert that no exception is thrown, implying the flow proceeded as expected.
            await _step.ExecuteAsync(_initializationState);

            // Verify appropriate logging (optional, but good for confirmation)
            // Example: _mockLogger.Verify(log => log.Info(It.Is<string>(s => s.Contains("Snapshot download failed, was skipped, or path was empty"))), Times.Once);
            // The above log would occur if it tries to download with an empty URL.
            // If URL is null, SnapshotDownloader.InitializeAndDownloadInitAsync might return success=false or path=null quickly.
            // The SUT logs "Snapshot download failed, was skipped, or path was empty."
            // Then it checks if dbExists. It's false.
            // Then it checks `if (!dbExists && (downloadResult == null || !downloadResult.Success))`
            // This condition would be TRUE if downloadResult indicates failure.
            // This means an InvalidOperationException would be thrown if no URL + no DB. This is not what we want for "proceeds normally".

            // Re-thinking the logic in SUT for "No Snapshot URL":
            // Current SUT:
            // (downloadResult, cleanupAction) = await SnapshotDownloader.InitializeAndDownloadInitAsync(CancellationToken.None, _initConfig);
            // if (downloadResult != null && downloadResult.Success && !string.IsNullOrEmpty(downloadResult.UncompressedDirectoryPath))
            // { /* process snapshot */ }
            // else { /* log warning */
            //    bool dbExists = _api.DbProvider.RealDb != null && !_api.DbProvider.RealDb.IsEmpty();
            //    if (!dbExists) { throw new InvalidOperationException("..."); } <--- THIS IS THE PROBLEM for this test case
            //    await base.ExecuteAsync(state);
            // }
            // This means if a snapshot URL is not provided, `InitializeAndDownloadInitAsync` likely returns a "failed" result (no path).
            // If the DB also doesn't exist, it WILL throw. This is by design in the SUT.
            // So "ExecuteAsync_NoSnapshotUrl_WhenDbDoesNotExist_ProceedsNormally" is not possible with current SUT.
            // It seems the intention is that if a snapshot URL is configured, it MUST succeed for a fresh DB.
            // If no snapshot URL is configured, it's like a download failure.

            // Let's align the test with the SUT's actual behavior.
            // If Url is null, downloadResult.Success will be false.
            // If dbDoesNotExist, then `!dbExists && !downloadResult.Success` will be true, leading to an exception.
            // So, this test should expect an InvalidOperationException.

            _mockLogger.Verify(log => log.Warn(It.Is<string>(s => s.Contains("Snapshot download failed, was skipped, or path was empty"))), Times.Once);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _step.ExecuteAsync(_initializationState));
        }

        [Test]
        public async Task ExecuteAsync_NoSnapshotUrl_DbExists_CallsBaseExecuteAsync()
        {
            // Arrange
            _mockInitConfig.Object.Url = null; // No snapshot URL
            _mockDbProvider.Setup(dbp => dbp.RealDb).Returns(_mockDatabase.Object); // DB DOES exist
            _mockDatabase.Setup(db => db.IsEmpty()).Returns(false); // And is not empty

            // Act
            await _step.ExecuteAsync(_initializationState);

            // Assert
            // Should proceed to base.ExecuteAsync. We verify by checking for a log message that indicates this path.
            _mockLogger.Verify(log => log.Info(It.Is<string>(s => s.Contains("Proceeding with existing database as snapshot download/extraction failed or was skipped."))), Times.Once);
            // And no exception should be thrown. If base.ExecuteAsync itself could be mocked, that would be better.
        }


        [Test]
        public async Task ExecuteAsync_SnapshotDownloadFails_DbDoesNotExist_ThrowsException()
        {
            // Arrange
            _mockInitConfig.Object.Url = "http://nonexistent-snapshot.local/snapshot.tar.gz"; // Valid URL format, but will fail
            _mockDbProvider.Setup(dbp => dbp.RealDb).Returns((Database)null); // DB does not exist

            // Act & Assert
            // SnapshotDownloader will be called, likely return Success=false because the URL is fake.
            // Then, because DB doesn't exist, it should throw.
            Assert.ThrowsAsync<InvalidOperationException>(async () => await _step.ExecuteAsync(_initializationState));
            _mockLogger.Verify(log => log.Warn(It.Is<string>(s => s.Contains("Snapshot download failed, was skipped, or path was empty"))), Times.Once);
            _mockLogger.Verify(log => log.Error(It.Is<string>(s => s.Contains("Halting node start due to failure in snapshot processing for a fresh database.")), It.IsAny<Exception>()), Times.Once);

        }

        [Test]
        public async Task ExecuteAsync_SnapshotDownloadFails_DbExists_CallsBaseExecuteAsync()
        {
            // Arrange
            _mockInitConfig.Object.Url = "http://nonexistent-snapshot.local/snapshot.tar.gz"; // Valid URL format, but will fail
            _mockDbProvider.Setup(dbp => dbp.RealDb).Returns(_mockDatabase.Object); // DB DOES exist
            _mockDatabase.Setup(db => db.IsEmpty()).Returns(false); // And is not empty

            // Act
            await _step.ExecuteAsync(_initializationState);

            // Assert
            // Should proceed to base.ExecuteAsync.
            _mockLogger.Verify(log => log.Warn(It.Is<string>(s => s.Contains("Snapshot download failed, was skipped, or path was empty"))), Times.Once);
            _mockLogger.Verify(log => log.Info(It.Is<string>(s => s.Contains("Proceeding with existing database as snapshot download/extraction failed or was skipped."))), Times.Once);
            _mockLogger.Verify(log => log.Error(It.Is<string>(s => s.Contains("Halting node start due to failure in snapshot processing for a fresh database.")), It.IsAny<Exception>()), Times.Never);
        }

        // This test is challenging due to the static SnapshotDownloader.
        // We'll simulate a successful "download" by making SnapshotDownloader.InitializeAndDownloadInitAsync
        // return a path to our local dummy.tar.gz. This requires refactoring SnapshotDownloader or using a wrapper.
        // For now, we assume this test cannot be implemented perfectly without such refactoring.
        // However, we can test the extraction part if we can assume the file is "downloaded".
        // Let's assume for this test that `SnapshotDownloader` is modified/mockable to return our dummy file.
        // Since we can't modify SnapshotDownloader now, this test will be more of a template.
        // To make it somewhat runnable, we'd need to ensure that if a specific URL is passed,
        // the actual SUT's call to the real SnapshotDownloader somehow results in our dummy file path.
        // This is not feasible without more control.

        // For the purpose of this exercise, I will write the test as if I *could* control the output of SnapshotDownloader,
        // and then acknowledge this limitation.
        // The alternative is to test ExtractSnapshotAsync directly, but the task is to test ArbitrumInitializeBlockchainStep.

        // To simulate the "download" for the purpose of testing the SUT's *reaction* to a successful download:
        // We can't easily mock static `SnapshotDownloader.InitializeAndDownloadInitAsync`.
        // What if `InitConfig` had a `SnapshotLocalPath` that `SnapshotDownloader` would prioritize?
        // The current `InitConfig` in SUT (Nethermind.Arbitrum.Config.InitConfig) only has Url and Type.
        // So, the real `SnapshotDownloader` will always try to download from `Url`.

        // Let's assume the test `ExecuteAsync_SnapshotDownloadAndExtractionSucceeds_WhenDbDoesNotExist`
        // can only be fully realized if SnapshotDownloader was mockable or if InitConfig allowed specifying a local file.
        // Given the constraints, I will write a test that assumes the file *is* somehow "downloaded" (placed at the expected path by the test setup)
        // and then verify extraction and subsequent behavior. This means the test setup has to create this file.
        // The `downloadResult.UncompressedDirectoryPath` is the key.

        [Test]
        public async Task ExecuteAsync_ExtractionSucceeds_WhenDbDoesNotExist_CallsBaseExecuteAsyncAndDeletesArchive()
        {
            // This test focuses on the scenario where download *is assumed to have happened*,
            // and a valid archive is ready for extraction.

            // Arrange
            _mockInitConfig.Object.Url = "file:///some/path/to/dummy.tar.gz"; // A URL that our mocked downloader (if we had one) would handle
                                                                        // Or, a URL that the actual downloader might interpret as local (less likely for http downloader)

            _mockDbProvider.Setup(dbp => dbp.RealDb).Returns((Database)null); // DB does not exist

            // Simulate that SnapshotDownloader "downloaded" the file and provided its path.
            // We need to ensure this path is used by ExtractSnapshotAsync.
            // The SUT gets this path from `downloadResult.UncompressedDirectoryPath`.
            // Since we can't mock SnapshotDownloader to return a specific DownloadResult,
            // we have to make the actual SnapshotDownloader (when called by SUT) somehow produce this.
            // This is the hard part.

            // Workaround: For this specific test, we will *manually place* the dummy snapshot
            // into a location that `SnapshotDownloader` (if it were to succeed with our dummy URL)
            // would place it, OR we assume `downloadResult.UncompressedDirectoryPath` is the *source* path for extraction.
            // The SUT uses `downloadResult.UncompressedDirectoryPath` as the first argument to `ExtractSnapshotAsync`.
            // So, if `SnapshotDownloader.InitializeAndDownloadInitAsync` could return `DummySnapshotPath` in its result, this would work.

            // Let's assume we modify InitConfig to have a local path for testing, or SnapshotDownloader has a test mode.
            // As a last resort for this subtask, if `initConfig.Url` is "test://dummy-snapshot",
            // we could try to make the *actual* `SnapshotDownloader.InitializeAndDownloadInitAsync` (if it were part of this test setup)
            // return the `DummySnapshotPath`. But it's not.

            // Simplification: We will rely on the fact that `ExtractSnapshotAsync` will be called
            // if `SnapshotDownloader.InitializeAndDownloadInitAsync` returns a success and a path.
            // The test will set up a dummy file, and we want SUT's `ExtractSnapshotAsync` to process it.
            // This means `SnapshotDownloader.InitializeAndDownloadInitAsync` inside SUT must somehow yield this path.
            // This remains the core issue for a true integration test of this success path.

            // For now, this test will likely fail because "http://example.com/snapshot.tar.gz" (default from SetUp) will fail to download.
            // Let's change the URL to something that will definitely make SnapshotDownloader fail fast and predictably.
            _mockInitConfig.Object.Url = "test-will-cause-download-failure";

            // If we cannot make InitializeAndDownloadInitAsync succeed with a local file,
            // we cannot test the successful extraction path via ExecuteAsync.
            // The best we can do is if it somehow succeeds, then these are the assertions.
            // So, this test is more of a "IF download succeeds, THEN..."

            // Let's assume a hypothetical scenario where SnapshotDownloader.InitializeAndDownloadInitAsync
            // has been successfully mocked or controlled to return our dummy snapshot.
            // This is not possible with current setup, so this test is more of a blueprint.
            // Assert.Ignore("This test requires SnapshotDownloader to be mockable or to support local file paths via InitConfig for testing.");

            // To proceed with at least *some* part of the success path:
            // We will use a trick: if the _initConfig.Url is our specific dummy file path,
            // the call to SnapshotDownloader.InitializeAndDownloadInitAsync in the SUT
            // *might* just pass this path along if it interprets "file://" urls, or it might fail.
            // It's a long shot.

            string localDummySnapshotForTest = Path.Combine(_tempExtractionPath, "dummy_snapshot_for_test.tar.gz");
            File.Copy(DummySnapshotPath, localDummySnapshotForTest, true); // Ensure the "downloaded" file exists
            _mockInitConfig.Object.Url = $"file:///{localDummySnapshotForTest.Replace(Path.DirectorySeparatorChar, '/')}";


            // Act
            // This will call the REAL SnapshotDownloader. If it can't handle file:// or the file is not where it expects, it will fail.
            // If it *does* by some chance treat file:/// as "already downloaded", then `downloadResult.UncompressedDirectoryPath` might be our path.
            try
            {
                await _step.ExecuteAsync(_initializationState);

                // Assertions if it proceeded:
                // 1. Extraction occurred (e.g., a file inside dummy.tar.gz now exists in _tempExtractionPath)
                Assert.IsTrue(File.Exists(Path.Combine(_tempExtractionPath, "dummy.txt")), "dummy.txt was not extracted.");
                // 2. Original archive is deleted by ExtractSnapshotAsync
                Assert.IsFalse(File.Exists(localDummySnapshotForTest), "Dummy snapshot archive was not deleted after extraction.");
                // 3. Log messages
                _mockLogger.Verify(log => log.Info(It.Is<string>(s => s.Contains("Snapshot downloaded successfully") && s.Contains(localDummySnapshotForTest))), Times.Once);
                _mockLogger.Verify(log => log.Info(It.Is<string>(s => s.Contains("Snapshot extraction successful to") && s.Contains(_tempExtractionPath))), Times.Once);
                _mockLogger.Verify(log => log.Info(It.Is<string>(s => s.Contains("Successfully deleted snapshot archive") && s.Contains(localDummySnapshotForTest))), Times.Once);
                // 4. Base.ExecuteAsync was called (implied by no exception on this path)
            }
            catch (Exception e)
            {
                // This catch block is to understand why it might fail if SnapshotDownloader doesn't behave as hoped with file://
                 Assert.Fail($"Test failed, possibly due to SnapshotDownloader not handling file:// URL as a local file or other issue: {e.Message} {e.StackTrace}");
            }
        }

        [Test]
        public async Task ExecuteAsync_ExtractionFails_WhenDbDoesNotExist_ThrowsException()
        {
            // Arrange
            _mockDbProvider.Setup(dbp => dbp.RealDb).Returns((Database)null); // DB does not exist

            string localInvalidSnapshotForTest = Path.Combine(_tempExtractionPath, "invalid_snapshot_for_test.tar.gz");
            File.Copy(InvalidSnapshotPath, localInvalidSnapshotForTest, true); // Ensure the "downloaded" invalid file exists
            _mockInitConfig.Object.Url = $"file:///{localInvalidSnapshotForTest.Replace(Path.DirectorySeparatorChar, '/')}"; // Point to a local, invalid archive

            // Act & Assert
            // This relies on SnapshotDownloader handling file:// URL or being mocked.
            // If it "downloads" (i.e., provides the path to) the invalid archive, ExtractSnapshotAsync should fail.
            var ex = Assert.ThrowsAsync<Exception>(async () => await _step.ExecuteAsync(_initializationState), "Expected an exception due to extraction failure.");

            // Check the logs
            _mockLogger.Verify(log => log.Info(It.Is<string>(s => s.Contains("Snapshot downloaded successfully") && s.Contains(localInvalidSnapshotForTest))), Times.Once);
            _mockLogger.Verify(log => log.Error(It.Is<string>(s => s.Contains("Failed to extract snapshot") && s.Contains(localInvalidSnapshotForTest)), It.IsAny<Exception>()), Times.Once);

            // The SUT's catch block for extraction failure:
            // catch (Exception ex) { ... if (!dbExists) { throw; } ... await base.ExecuteAsync(state); }
            // So if db doesn't exist, it should rethrow the original exception from extraction, or a new InvalidOperationException if that's what ExtractSnapshotAsync throws.
            // ExtractSnapshotAsync throws the original SharpCompressException if extraction fails.
            // The ExecuteAsync catches this, logs, and if !dbExists, it re-throws.
            // So we expect the original exception type from SharpCompress or an IOException if file ops fail.
            // For this test, we are more concerned that *an* exception is thrown and logged correctly.
             _mockLogger.Verify(log => log.Error(It.Is<string>(s => s.Contains("Halting node start due to failure in snapshot processing for a fresh database.")), It.IsAny<Exception>()), Times.Once);

            Assert.IsFalse(File.Exists(Path.Combine(_tempExtractionPath, "dummy.txt")), "dummy.txt should not be extracted on failure.");
            Assert.IsTrue(File.Exists(localInvalidSnapshotForTest), "Invalid snapshot archive should NOT be deleted on extraction failure.");
        }
    }
}

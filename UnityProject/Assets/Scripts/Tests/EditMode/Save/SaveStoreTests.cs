using System.IO;
using AshesAndCrowns.Save;
using NUnit.Framework;

namespace AshesAndCrowns.Tests.Save
{
    public class SaveStoreTests
    {
        private string _dir;
        private SaveStore _store;

        [SetUp] public void SetUp() { _dir = Path.Combine(Path.GetTempPath(), "ashes_test_" + System.Guid.NewGuid().ToString("N")); _store = new SaveStore(_dir); }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        [Test]
        public void SaveThenLoad_FromMain()
        {
            Assert.IsFalse(_store.Exists);
            _store.Save(RunStateFactory.Sample());
            Assert.IsTrue(_store.Exists);
            Assert.IsFalse(File.Exists(_store.TempPath), "temp file must not linger");
            var r = _store.Load();
            Assert.AreEqual(LoadSource.Main, r.Source);
            RunStateFactory.AssertEqual(RunStateFactory.Sample(), r.State);
        }

        [Test]
        public void SecondSave_RotatesPreviousIntoBackup()
        {
            var first = RunStateFactory.Sample(); first.Kills = 1;
            var second = RunStateFactory.Sample(); second.Kills = 2;
            _store.Save(first); _store.Save(second);
            Assert.IsTrue(File.Exists(_store.BackupPath));
            Assert.AreEqual(2, _store.Load().State.Kills);
            Assert.AreEqual(1, SaveCodec.Decode(File.ReadAllBytes(_store.BackupPath)).Kills);
        }

        [Test]
        public void CorruptMain_FallsBackToBackup()
        {
            var first = RunStateFactory.Sample(); first.Kills = 1;
            var second = RunStateFactory.Sample(); second.Kills = 2;
            _store.Save(first); _store.Save(second);
            byte[] bytes = File.ReadAllBytes(_store.MainPath); bytes[bytes.Length - 1] ^= 0xFF; File.WriteAllBytes(_store.MainPath, bytes);
            var r = _store.Load();
            Assert.AreEqual(LoadSource.Backup, r.Source);
            Assert.AreEqual(1, r.State.Kills);
            StringAssert.Contains("checksum", r.MainError);
        }

        [Test]
        public void TruncatedMain_CrashMidWriteSimulation_FallsBackToBackup()
        {
            _store.Save(RunStateFactory.Sample()); _store.Save(RunStateFactory.Sample());
            byte[] bytes = File.ReadAllBytes(_store.MainPath);
            File.WriteAllBytes(_store.MainPath, new System.ArraySegment<byte>(bytes, 0, bytes.Length / 3).ToArray());
            File.WriteAllBytes(_store.TempPath, new byte[] { 1, 2, 3 }); // a half-written temp file too
            var r = _store.Load();
            Assert.AreEqual(LoadSource.Backup, r.Source);
            Assert.IsFalse(File.Exists(_store.TempPath), "stale temp file is discarded");
        }

        [Test]
        public void BothCorrupt_ReportsBothErrors_AndNoState()
        {
            _store.Save(RunStateFactory.Sample()); _store.Save(RunStateFactory.Sample());
            File.WriteAllBytes(_store.MainPath, new byte[10]); File.WriteAllBytes(_store.BackupPath, new byte[10]);
            var r = _store.Load();
            Assert.IsFalse(r.Found); Assert.AreEqual(LoadSource.None, r.Source);
            Assert.IsNotNull(r.MainError); Assert.IsNotNull(r.BackupError);
        }

        [Test]
        public void NoFiles_LoadIsEmptyNotAnError()
        {
            var r = _store.Load();
            Assert.IsFalse(r.Found);
            Assert.DoesNotThrow(() => _store.DeleteAll());
        }

        [Test]
        public void DeleteAll_LeavesNothing_EvenWithStaleTemp()
        {
            _store.Save(RunStateFactory.Sample()); _store.Save(RunStateFactory.Sample());
            File.WriteAllBytes(_store.TempPath, new byte[] { 1 });
            Assert.IsTrue(_store.DeleteAll());
            Assert.IsFalse(File.Exists(_store.MainPath)); Assert.IsFalse(File.Exists(_store.BackupPath)); Assert.IsFalse(File.Exists(_store.TempPath));
            Assert.IsFalse(_store.Exists);
            Assert.IsFalse(_store.Load().Found);
        }

        [Test]
        public void ManyAutosaves_NeverLoseALoadableState()
        {
            for (int i = 0; i < 50; i++)
            {
                var s = RunStateFactory.Sample(withBattle: i % 2 == 0); s.Kills = i;
                _store.Save(s);
                var r = _store.Load();
                Assert.IsTrue(r.Found); Assert.AreEqual(i, r.State.Kills);
            }
        }
    }
}

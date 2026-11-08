using System.Collections;
using System.IO;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Game;
using AshesAndCrowns.Save;
using AshesAndCrowns.Sim;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AshesAndCrowns.Tests.PlayMode
{
    /// <summary>
    /// Builds the minimum scene the SaveManager needs (bootstrap, player, horse, vitals, clock, HUD) in code,
    /// then checks capture/apply round trips, autosave on launch, and that death deletes the save
    /// before the death screen is shown.
    /// </summary>
    public class SaveManagerTests
    {
        private string _dir;
        private GameObject _root;
        private SaveManager _save;
        private PlayerVitals _vitals;
        private HorseController _horse;
        private PlayerController _player;
        private WorldClock _clock;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ashes_pm_" + System.Guid.NewGuid().ToString("N"));
            SaveManager.SaveDirectoryOverride = _dir;
            RunSession.LaunchHandled = false; RunSession.PendingResume = null; RunSession.NextSeed = 0;
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            var boot = GameBootstrap.Instance; if (boot != null) Object.DestroyImmediate(boot.gameObject);
            SaveManager.SaveDirectoryOverride = null;
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private IEnumerator BuildScene()
        {
            var bootGo = new GameObject("GameBootstrap"); bootGo.AddComponent<GameBootstrap>();
            yield return null; // Awake
            var balance = GameBootstrap.Instance.Balance;
            _root = new GameObject("root");
            var playerGo = new GameObject("Player"); playerGo.transform.SetParent(_root.transform);
            playerGo.AddComponent<CharacterController>();
            _vitals = playerGo.AddComponent<PlayerVitals>(); _vitals.Init(balance, GameBootstrap.Instance.Rng);
            _player = playerGo.AddComponent<PlayerController>(); _player.vitals = _vitals;
            var horseGo = new GameObject("Horse"); horseGo.transform.SetParent(_root.transform);
            horseGo.AddComponent<CharacterController>();
            _horse = horseGo.AddComponent<HorseController>(); _horse.seat = new GameObject("Seat").transform; _horse.seat.SetParent(horseGo.transform, false);
            _horse.Init(balance, GameBootstrap.Instance.Input);
            var clockGo = new GameObject("Clock"); clockGo.transform.SetParent(_root.transform);
            _clock = clockGo.AddComponent<WorldClock>(); _clock.Init(balance, GameBootstrap.Instance.Rng); _clock.timeScale = 0f;
            var hudGo = new GameObject("HUD"); hudGo.transform.SetParent(_root.transform);
            var doc = hudGo.AddComponent<UnityEngine.UIElements.UIDocument>();
            doc.panelSettings = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
            var hud = hudGo.AddComponent<UI.HudController>(); hud.Init(balance, _vitals, null, null, null);
            var saveGo = new GameObject("SaveManager"); saveGo.transform.SetParent(_root.transform);
            _save = saveGo.AddComponent<SaveManager>();
            _save.clock = _clock; _save.player = _player; _save.horse = _horse; _save.vitals = _vitals; _save.hud = hud;
            yield return null; // Start: autosaves the fresh run
        }

        [UnityTest]
        public IEnumerator FreshRun_IsSavedOnFirstFrame_AndCaptureRoundTrips()
        {
            yield return BuildScene();
            Assert.IsTrue(_save.Store.Exists, "a run must exist on disk from its first frame");

            _player.transform.position = new Vector3(100f, 5f, 200f);
            _vitals.TakeHit(0.2f, "test");
            _clock.SkipHours(5f);
            RunState captured = _save.Capture(AffairsChoice.Hide);
            Assert.AreEqual(AffairsChoice.Hide, captured.Affairs);
            Assert.AreEqual(100f, captured.PlayerX, 1e-3f);
            Assert.AreEqual(1, captured.Body.Wounds.Length);
            Assert.AreEqual(5.0, captured.CalendarTotalHours, 1e-3);

            // Disturb the scene, then apply the captured state without absence resolution.
            _player.transform.position = Vector3.zero;
            _clock.SkipHours(30f);
            _save.Apply(captured, resolveAbsence: false);
            Assert.AreEqual(100f, _player.transform.position.x, 1e-2f);
            Assert.AreEqual(5.0, _clock.Calendar.TotalHours, 1e-3);
            Assert.AreEqual(captured.Body.Blood, _vitals.Body.Blood, 1e-5f);
            var reloaded = _save.Store.Load();
            Assert.IsTrue(reloaded.Found);
            Assert.AreEqual(5.0, reloaded.State.CalendarTotalHours, 1e-3);
        }

        [UnityTest]
        public IEnumerator Death_DeletesTheSave_Synchronously()
        {
            yield return BuildScene();
            Assert.IsTrue(_save.Store.Exists);
            _vitals.TakeHit(1f, "test blade"); // kills whichever part it hits? ensure death:
            while (!_vitals.IsDead) _vitals.TakeHit(1f, "test blade");
            Assert.IsFalse(_save.Store.Exists, "the save must be gone the moment the player dies, before any frame renders");
            Assert.IsFalse(File.Exists(_save.Store.MainPath)); Assert.IsFalse(File.Exists(_save.Store.BackupPath));
            yield return null;
            Assert.IsFalse(_save.Store.Exists, "and it must stay gone (no autosave after death)");
        }

        [UnityTest]
        public IEnumerator ResumeWithAbsence_AdvancesTheClock_AndKeepsTheSave()
        {
            yield return BuildScene();
            RunState s = _save.Capture(AffairsChoice.SleepAtHome);
            s.SavedAtUtcTicks = System.DateTime.UtcNow.AddHours(-3).Ticks; // closed the game three real hours ago
            double before = _clock.Calendar.TotalHours;
            _save.Apply(s, resolveAbsence: true);
            float expected = 3f * GameBootstrap.Instance.Balance.simulation.realHoursToGameHours;
            Assert.AreEqual(before + expected, _clock.Calendar.TotalHours, 0.5);
            Assert.IsTrue(_save.Store.Exists);
            Assert.IsTrue(_save.IsQuitScreenOpen, "the chronicle is showing");
        }
    }
}

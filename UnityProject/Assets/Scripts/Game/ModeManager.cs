using AshesAndCrowns.Battle;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AshesAndCrowns.Game
{
    public enum GameMode : byte { OnFoot, Riding, Command, Dead }

    /// <summary>
    /// The one rule of command: you command only while you stand at the vantage. Ride into the fight and the
    /// RTS layer switches off; the army runs on its last orders. Ride back to command again.
    /// </summary>
    public sealed class ModeManager : MonoBehaviour
    {
        public PlayerController player;
        public HorseController horse;
        public PlayerVitals vitals;
        public MeleeController melee;
        public ThirdPersonCamera thirdPersonCamera;
        public Camera gameplayCamera;
        public CommandController command;
        public BattleDirector director;
        public Transform vantage;
        public HudController hud;

        private Balance _balance;
        private GameInput _input;
        private GameMode _mode = GameMode.OnFoot;
        private bool _wasInVantage;
        private float _startTime;

        public GameMode Mode => _mode;

        public void Init(Balance balance, GameInput input)
        {
            _balance = balance; _input = input; _startTime = Time.time;
            vitals.Died += OnPlayerDied;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            SetMode(GameMode.OnFoot);
        }

        private void Update()
        {
            if (_balance == null) return;
            if (_mode == GameMode.Dead)
            {
                if (_input.Restart.WasPressedThisFrame()) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            if (_input.ToggleCursor.WasPressedThisFrame() && _mode != GameMode.Command)
            {
                bool locked = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = locked;
            }

            // Mount / dismount
            bool mountChanged = false;
            if (_input.Mount.WasPressedThisFrame())
            {
                if (horse.Mounted) { horse.Dismount(player.transform); mountChanged = true; }
                else if (Vector3.Distance(player.transform.position, horse.transform.position) <= _balance.player.mountRadius) { horse.Mount(player.transform); mountChanged = true; }
            }

            Vector3 body = horse.Mounted ? horse.transform.position : player.transform.position;
            bool inVantage = vantage != null && Vector3.Distance(body, vantage.position) <= _balance.command.vantageRadius;
            GameMode wanted = inVantage ? GameMode.Command : (horse.Mounted ? GameMode.Riding : GameMode.OnFoot);
            if (wanted != _mode || mountChanged) SetMode(wanted);
            _wasInVantage = inVantage;

            hud.SetVantageDistance(vantage != null ? Vector3.Distance(body, vantage.position) - _balance.command.vantageRadius : 0f);
        }

        private void SetMode(GameMode mode)
        {
            _mode = mode;
            bool commanding = mode == GameMode.Command;
            command.enabled = commanding;
            gameplayCamera.enabled = !commanding;
            thirdPersonCamera.enabled = !commanding;
            // Your body keeps answering WASD in command mode so you can walk or ride off the hill (and lose command).
            player.enabled = !horse.Mounted && mode != GameMode.Dead;
            melee.enabled = mode == GameMode.OnFoot || mode == GameMode.Riding;
            horse.enabled = mode != GameMode.Dead;
            thirdPersonCamera.target = horse.Mounted ? horse.transform : player.transform;
            thirdPersonCamera.SetRiding(horse.Mounted);
            if (!commanding && mode != GameMode.Dead) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            hud.SetMode(mode);
        }

        private void OnPlayerDied()
        {
            director.SetPlayerCharacterAlive(false);
            if (horse.Mounted) horse.Dismount(player.transform);
            SetMode(GameMode.Dead);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            float days = (Time.time - _startTime) / 60f; // placeholder until the calendar arrives in Phase 1
            hud.ShowDeath(vitals.CauseOfDeath ?? "unknown", days, melee.Kills, director.PlayerLosses);
            // Phase 2: delete the save synchronously here, before the death screen renders.
        }
    }
}

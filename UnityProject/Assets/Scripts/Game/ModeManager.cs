using AshesAndCrowns.Battle;
using AshesAndCrowns.Combat;
using AshesAndCrowns.Sim;
using AshesAndCrowns.UI;
using UnityEngine;

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
        public SaveManager saveManager;
        public bool commandAllowed = true;   // false while serving in the ranks

        private Balance _balance;
        private GameInput _input;
        private GameMode _mode = GameMode.OnFoot;
        private bool _wasInVantage;

        public GameMode Mode => _mode;

        public void Init(Balance balance, GameInput input)
        {
            _balance = balance; _input = input;
            vitals.Died += OnPlayerDied;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            SetMode(GameMode.OnFoot);
        }

        private void Update()
        {
            if (_balance == null) return;
            if (_mode == GameMode.Dead) return;
            if (saveManager != null && saveManager.IsQuitScreenOpen) return;

            // Mount / dismount
            bool mountChanged = false;
            if (_input.Mount.WasPressedThisFrame() && !(melee != null && melee.GuardMode))
            {
                if (horse.Mounted) { horse.Dismount(player.transform); mountChanged = true; }
                else if (Vector3.Distance(player.transform.position, horse.transform.position) <= _balance.player.mountRadius) { horse.Mount(player.transform); mountChanged = true; }
            }

            Vector3 body = horse.Mounted ? horse.transform.position : player.transform.position;
            bool inVantage = commandAllowed && vantage != null && Vector3.Distance(body, vantage.position) <= _balance.command.vantageRadius;
            GameMode wanted = inVantage ? GameMode.Command : (horse.Mounted ? GameMode.Riding : GameMode.OnFoot);
            if (wanted != _mode || mountChanged) SetMode(wanted);
            _wasInVantage = inVantage;

            hud.SetVantageDistance(vantage != null ? Vector3.Distance(body, vantage.position) - _balance.command.vantageRadius : 0f);
            if (director != null) director.SetCommander(body, _mode == GameMode.Command, _mode != GameMode.Command && director.CountWithin(body, _balance.melee.enemyEngagePlayerRange, Side.Enemy) > 0);
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
            if (melee != null) melee.mounted = horse.Mounted;
            if (!commanding && mode != GameMode.Dead) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (commandAllowed || mode == GameMode.Dead) hud.SetMode(mode); else hud.SetMode("Ranks");
        }

        private void OnPlayerDied()
        {
            if (director != null) director.SetPlayerCharacterAlive(false);
            if (horse.Mounted) horse.Dismount(player.transform);
            SetMode(GameMode.Dead);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            // The SaveManager deletes the save and shows the death screen.
        }

        /// <summary>Restore mount state after a resume (the horse already has the rider parented).</summary>
        public void RefreshAfterResume() => SetMode(horse.Mounted ? GameMode.Riding : GameMode.OnFoot);

        /// <summary>The SaveManager found the player dead on resume (no Died event fires in that case).</summary>
        public void NotifyDied() { if (_mode != GameMode.Dead) OnPlayerDied(); }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace AshesAndCrowns.Game
{
    /// <summary>
    /// All player input, defined in code with the Input System so a controller can be bound later
    /// without touching gameplay code. One instance, owned by GameBootstrap.
    /// </summary>
    public sealed class GameInput : System.IDisposable
    {
        private readonly InputActionMap _map = new InputActionMap("Player");
        public readonly InputAction Move, Look, Sprint, Mount, Attack, Block, Restart, ToggleCursor;

        public GameInput()
        {
            Move = _map.AddAction("Move", InputActionType.Value);
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddBinding("<Gamepad>/leftStick");
            Look = _map.AddAction("Look", InputActionType.Value, "<Mouse>/delta");
            Look.AddBinding("<Gamepad>/rightStick").WithProcessor("scaleVector2(x=12,y=12)");
            Sprint = _map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            Sprint.AddBinding("<Gamepad>/leftStickPress");
            Mount = _map.AddAction("Mount", InputActionType.Button, "<Keyboard>/e");
            Mount.AddBinding("<Gamepad>/buttonNorth");
            Attack = _map.AddAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
            Attack.AddBinding("<Gamepad>/rightShoulder");
            Block = _map.AddAction("Block", InputActionType.Button, "<Mouse>/rightButton");
            Block.AddBinding("<Gamepad>/leftShoulder");
            Restart = _map.AddAction("Restart", InputActionType.Button, "<Keyboard>/r");
            ToggleCursor = _map.AddAction("ToggleCursor", InputActionType.Button, "<Keyboard>/escape");
            _map.Enable();
        }

        public Vector2 MoveValue => Move.ReadValue<Vector2>();
        public Vector2 LookDelta => Look.ReadValue<Vector2>();

        public void Dispose() { _map.Disable(); _map.Dispose(); }
    }
}

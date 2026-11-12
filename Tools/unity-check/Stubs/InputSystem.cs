using System;
using UnityEngine;
namespace UnityEngine.InputSystem
{
    namespace Controls { public class ButtonControl { public bool isPressed => false; public bool wasPressedThisFrame => false; public bool wasReleasedThisFrame => false; public float ReadValue() => 0; }
        public class KeyControl : ButtonControl { } public class Vector2Control { public Vector2 ReadValue() => default; public AxisControl x, y; } public class AxisControl { public float ReadValue() => 0; } public class AnyKeyControl : ButtonControl { } }
    public enum Key { None, Space, Enter, A, B }
    public class Keyboard { public static Keyboard current; public Controls.AnyKeyControl anyKey; public Controls.KeyControl this[Key k] => null;
        public Controls.KeyControl aKey,bKey,cKey,dKey,eKey,fKey,gKey,hKey,iKey,jKey,kKey,lKey,mKey,nKey,oKey,pKey,qKey,rKey,sKey,tKey,uKey,vKey,wKey,xKey,yKey,zKey,
            digit0Key,digit1Key,digit2Key,digit3Key,digit4Key,digit5Key,digit6Key,digit7Key,digit8Key,digit9Key,spaceKey,enterKey,escapeKey,backspaceKey,tabKey,backquoteKey,homeKey,endKey,deleteKey,
            leftShiftKey,rightShiftKey,leftCtrlKey,rightCtrlKey,leftAltKey,rightAltKey,leftCommandKey,rightCommandKey,upArrowKey,downArrowKey,leftArrowKey,rightArrowKey,minusKey,equalsKey,commaKey,periodKey,slashKey,
            f1Key,f2Key,f3Key,f4Key,f5Key,f6Key,f7Key,f8Key,f9Key,f10Key,f11Key,f12Key,numpadEnterKey,pageUpKey,pageDownKey,leftBracketKey,rightBracketKey,semicolonKey,quoteKey,backslashKey;
        public Controls.ButtonControl shiftKey, ctrlKey, altKey; }
    public class Mouse { public static Mouse current; public Controls.ButtonControl leftButton, rightButton, middleButton; public Controls.Vector2Control position, delta, scroll; public void WarpCursorPosition(Vector2 p) { } }
    public enum InputActionType { Value, Button, PassThrough }
    public class InputBinding { }
    public class InputAction { public InputAction(string name = null, InputActionType type = InputActionType.Value, string binding = null) { } public bool IsPressed() => false; public bool WasPressedThisFrame() => false; public bool WasReleasedThisFrame() => false; public T ReadValue<T>() where T : struct => default; public void Enable() { } public void Disable() { } public bool triggered => false; public event Action<CallbackContext> performed; public struct CallbackContext { } }
    public class InputActionMap { public InputActionMap(string name = null) { } public InputAction AddAction(string name, InputActionType type = InputActionType.Value, string binding = null, string interactions = null, string processors = null, string groups = null, string expectedControlLayout = null) => null; public void Enable() { } public void Disable() { } public void Dispose() { } }
    public static class InputActionSetupExtensions { public static BindingSyntax AddBinding(this InputAction a, string path, string interactions = null, string processors = null, string groups = null) => default; public static CompositeSyntax AddCompositeBinding(this InputAction a, string composite, string interactions = null, string processors = null) => default;
        public struct BindingSyntax { public BindingSyntax WithProcessor(string p) => default; public BindingSyntax WithInteraction(string p) => default; } public struct CompositeSyntax { public CompositeSyntax With(string name, string binding, string groups = null, string processors = null) => default; } }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace IdleArcade
{
    /// <summary>Movement from keyboard, gamepad or the on-screen floating joystick (mouse or touch).</summary>
    public static class InputReader
    {
        public static Vector2 Joystick;          // written by VirtualJoystick
        public static Vector2? Override;         // written by the autopilot
        public static bool Blocked;              // panels / cutscenes

        public static Vector2 Move
        {
            get
            {
                if (Blocked) return Vector2.zero;
                if (Override.HasValue) return Vector2.ClampMagnitude(Override.Value, 1f);
                Vector2 v = Joystick;
                var kb = Keyboard.current;
                if (kb != null)
                {
                    Vector2 k = Vector2.zero;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) k.y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) k.y -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) k.x += 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) k.x -= 1f;
                    if (k.sqrMagnitude > 0f) v = k.normalized;
                }
                var gp = Gamepad.current;
                if (gp != null)
                {
                    Vector2 s = gp.leftStick.ReadValue();
                    if (s.magnitude > 0.18f) v = s;
                }
                return Vector2.ClampMagnitude(v, 1f);
            }
        }
    }
}

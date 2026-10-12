using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace IdleArcade
{
    /// <summary>
    /// Floating joystick: press anywhere (outside buttons) and drag. Works with mouse and touch.
    /// The base follows the finger if dragged past its edge, like most mobile arcade games.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour
    {
        RectTransform area, baseRt, knob;
        CanvasGroup group;
        bool active, wasPressed, startedOnUI;
        Vector2 origin;
        const float Radius = 120f;

        public void Build(RectTransform parent)
        {
            area = parent;
            baseRt = UIFactory.Rect("Base", area).Anchor(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Radius * 2.2f, Radius * 2.2f));
            group = baseRt.gameObject.AddComponent<CanvasGroup>();
            var ring = UIFactory.Image("Ring", baseRt, GameConfig.I.spriteRing, new Color(1f, 1f, 1f, 0.55f));
            ring.rectTransform.Stretch();
            var fillBg = UIFactory.Image("Disc", baseRt, GameConfig.I.spriteCircle, new Color(0f, 0f, 0f, 0.18f));
            fillBg.rectTransform.Stretch(8, 8, 8, 8);
            knob = UIFactory.Image("Knob", baseRt, GameConfig.I.spriteCircle, new Color(1f, 1f, 1f, 0.92f)).rectTransform;
            knob.Anchor(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 120));
            group.alpha = 0f;
        }

        bool OverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            var data = new PointerEventData(es) { position = screenPos };
            var results = new System.Collections.Generic.List<RaycastResult>();
            es.RaycastAll(data, results);
            foreach (var r in results) if (r.gameObject.GetComponentInParent<Selectable>() != null) return true;
            return false;
        }

        Vector2 ToLocal(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var lp);
            return lp + area.rect.size * 0.5f;
        }

        void Update()
        {
            if (UIPanels.AnyModalOpen || InputReader.Override.HasValue)
            {
                if (active) Release();
                startedOnUI = true;  // the finger that closes a panel must not grab the stick
                return;
            }

            // Press state only, no per-frame phases: on a slow phone a finger can land and move inside one
            // frame, so the touch never reads as Began, and a missed Ended used to lock the stick forever.
            bool pressed = false;
            Vector2 pos = default;
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.isPressed)
            {
                pressed = true;
                pos = ts.primaryTouch.position.ReadValue();
            }
            else
            {
                var m = Mouse.current;
                if (m != null && m.leftButton.isPressed)
                {
                    pressed = true;
                    pos = m.position.ReadValue();
                }
            }
            bool down = pressed && !wasPressed;
            wasPressed = pressed;
            if (!pressed) startedOnUI = false;

            if (pressed && !active && !startedOnUI)
            {
                // a press that began on a button stays the button's until the finger lifts
                if (down && OverUI(pos)) { startedOnUI = true; return; }
                active = true;
                origin = ToLocal(pos);
                baseRt.anchoredPosition = origin;
                Tween.KillOwner(group);
                Tween.Fade(group, 1f, 0.1f);
            }
            if (active && pressed)
            {
                Vector2 p = ToLocal(pos);
                Vector2 d = p - origin;
                if (d.magnitude > Radius)
                {
                    origin += d - d.normalized * Radius;  // drag the base along
                    baseRt.anchoredPosition = origin;
                    d = d.normalized * Radius;
                }
                knob.anchoredPosition = d;
                Vector2 v = d / Radius;
                InputReader.Joystick = v.magnitude < 0.12f ? Vector2.zero : v;
            }
            else if (active && !pressed) Release();
        }

        void Release()
        {
            active = false;
            InputReader.Joystick = Vector2.zero;
            knob.anchoredPosition = Vector2.zero;
            Tween.Fade(group, 0f, 0.15f);
        }
    }
}

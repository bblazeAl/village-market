using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleArcade
{
    /// <summary>Small helpers to assemble uGUI hierarchies from code in one consistent style.</summary>
    public static class UIFactory
    {
        public static readonly Color Cream = new Color32(255, 248, 232, 255);
        public static readonly Color Brown = new Color32(92, 58, 38, 255);
        public static readonly Color Orange = new Color32(255, 150, 48, 255);
        public static readonly Color OrangeDark = new Color32(204, 98, 22, 255);
        public static readonly Color Green = new Color32(98, 200, 72, 255);
        public static readonly Color GreenDark = new Color32(52, 140, 44, 255);
        public static readonly Color Grey = new Color32(170, 170, 170, 255);
        public static readonly Color GreyDark = new Color32(120, 120, 120, 255);
        public static readonly Color Red = new Color32(240, 84, 72, 255);
        public static readonly Color RedDark = new Color32(176, 46, 40, 255);
        public static readonly Color Ink = new Color32(64, 44, 34, 255);
        public static readonly Color Shade = new Color(0f, 0f, 0f, 0.35f);
        public static readonly Color Gold = new Color32(255, 206, 64, 255);
        public static readonly Color Sky = new Color32(86, 170, 255, 255);

        static GameConfig C => GameConfig.I;

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Anchor(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        public static Image Image(string name, Transform parent, Sprite sprite, Color color, bool sliced = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sliced) { img.type = UnityEngine.UI.Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f; }
            else img.preserveAspect = true;
            return img;
        }

        public static Image Panel(string name, Transform parent, Color color, Sprite sprite = null)
            => Image(name, parent, sprite != null ? sprite : C.spriteRound, color, true);

        public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool outline = false)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = C.font;
            if (outline && C.fontOutline != null) t.fontSharedMaterial = C.fontOutline;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            // long Russian labels shrink to fit their box instead of spilling out
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = Mathf.Round(size * 0.6f);
            return t;
        }

        /// <summary>Chunky 3D-ish button: darker "lip" under a coloured face, punch + click sound.</summary>
        public static Button Button(string name, Transform parent, Color face, Color lip, Action onClick, out RectTransform faceRt)
        {
            var rt = Rect(name, parent);
            var lipImg = rt.gameObject.AddComponent<Image>();
            lipImg.sprite = C.spriteRound;
            lipImg.type = UnityEngine.UI.Image.Type.Sliced;
            lipImg.color = lip;
            var faceImg = Image("Face", rt, C.spriteRound, face, true);
            faceRt = faceImg.rectTransform;
            faceRt.Stretch(0, 0, 0, 10);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = faceImg;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.05f, 1.05f, 1.05f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            b.colors = colors;
            var press = rt.gameObject.AddComponent<PressFeel>();
            press.face = faceRt;
            b.onClick.AddListener(() =>
            {
                AudioManager.Play(Sfx.Click, 0.7f);
                Tween.UIPunch(rt, 0.08f, 0.15f);
                onClick?.Invoke();
            });
            return b;
        }

        public static void SetButtonColors(Button b, Color face, Color lip)
        {
            var lipImg = b.GetComponent<Image>();
            if (lipImg) lipImg.color = lip;
            if (b.targetGraphic) b.targetGraphic.color = face;
        }
    }

    /// <summary>Pushes the button face down while pressed.</summary>
    public class PressFeel : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform face;
        public void OnPointerDown(PointerEventData e) { if (face) face.offsetMax = new Vector2(face.offsetMax.x, -6f); if (face) face.offsetMin = new Vector2(face.offsetMin.x, 4f); }
        public void OnPointerUp(PointerEventData e) { if (face) face.offsetMax = new Vector2(face.offsetMax.x, 0f); if (face) face.offsetMin = new Vector2(face.offsetMin.x, 10f); }
    }
}

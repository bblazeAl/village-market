using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleArcade
{
    /// <summary>Speech bubble over a customer's head showing what they want and how many are left.</summary>
    public class CustomerBubble : MonoBehaviour
    {
        public RectTransform root;
        public Image icon;
        public TMP_Text count;
        public Image check;
        public Image bg;
        float impatience;
        Sprite lastIcon;
        int lastCount = -1;
        bool visible;

        void LateUpdate()
        {
            var cam = CameraRig.Cam;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }

        void Show()
        {
            if (visible) return;
            visible = true;
            root.gameObject.SetActive(true);
            Tween.KillOwner(root);
            Tween.PopIn(root, Vector3.one, 0.3f);
        }

        public void ShowWant(Sprite s, int n)
        {
            Show();
            check.gameObject.SetActive(false);
            icon.gameObject.SetActive(true);
            count.gameObject.SetActive(true);
            if (s != lastIcon) { icon.sprite = s; lastIcon = s; Tween.Punch(root, Vector3.one, 0.2f, 0.2f); }
            if (n != lastCount)
            {
                count.text = n > 0 ? n.ToString() : "";
                if (lastCount >= 0) Tween.Punch(count.rectTransform, Vector3.one, 0.3f, 0.2f);
                lastCount = n;
            }
        }

        public void SetImpatience(float k)
        {
            if (bg == null || Mathf.Abs(k - impatience) < 0.01f) return;
            impatience = k;
            bg.color = Color.Lerp(Color.white, new Color(1f, 0.62f, 0.58f), k);
            if (k > 0.6f) root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 30f) * 6f * k);
            else root.localRotation = Quaternion.identity;
        }

        public void ShowSad()
        {
            Show();
            icon.sprite = GameConfig.I.iconSad;
            lastIcon = icon.sprite;
            count.gameObject.SetActive(false);
            SetImpatience(0f);
            Tween.Punch(root, Vector3.one, 0.3f, 0.25f);
        }

        public void ShowCheckout()
        {
            Show();
            SetImpatience(0f);
            count.gameObject.SetActive(false);
            icon.sprite = GameConfig.I.iconCoin;
            lastIcon = icon.sprite;
            count.gameObject.SetActive(false);
            check.gameObject.SetActive(false);
            Tween.Punch(root, Vector3.one, 0.2f, 0.2f);
        }

        public void ShowHappy()
        {
            Show();
            icon.sprite = GameConfig.I.iconHeart;
            lastIcon = icon.sprite;
            count.gameObject.SetActive(false);
            Tween.Punch(root, Vector3.one, 0.35f, 0.3f);
            Hide(1.4f);
        }

        public void ShowEating()
        {
            Hide(0f);
        }

        public void Hide(float delay)
        {
            Tween.Delay(delay, () =>
            {
                if (!root || !visible) return;
                visible = false;
                lastCount = -1;
                Tween.PopOut(root, 0.18f, () => { if (root && !visible) root.gameObject.SetActive(false); });
            }, this);
        }

        public void ResetBubble()
        {
            SetImpatience(0f);
            visible = false;
            lastIcon = null;
            lastCount = -1;
            root.gameObject.SetActive(false);
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleArcade
{
    public enum PadAction { Build, Hire }

    /// <summary>
    /// Ground circle with a price. Standing on it drains coins into it (coins visibly fly in);
    /// when fully paid, the progression manager builds what it unlocks. Partial payments persist.
    /// </summary>
    public class PayPad : MonoBehaviour
    {
        [Header("Definition")]
        public string id;
        public int cost = 10;
        public string titleKey;
        public Sprite icon;
        public GameObject[] reveals;
        public GameObject[] hides;
        public string[] requires;
        public int requiresServed;
        public PadAction action = PadAction.Build;
        public HelperRole hireRole;
        public Transform hirePost;
        public bool focusOnReveal = true;
        public bool countsAsTask = true;
        public float radius = 1.05f;
        public int order;

        [Header("Visuals")]
        public Transform visualRoot;
        public Image fill;
        public Image iconImage;
        public TMP_Text priceText;
        public TMP_Text titleText;
        public Transform bobTarget;

        int paid;
        float standTime, coinTimer, denyCooldown;
        bool done;
        public bool Revealed { get; private set; }
        public bool IsDone => done;
        public int Remaining => Mathf.Max(0, cost - paid);
        public int Paid => paid;
        public bool PlayerOn { get; private set; }

        public void Init(int savedPaid)
        {
            paid = Mathf.Clamp(savedPaid, 0, cost);
            RefreshUI(false);
            Loc.Changed += OnLang;
        }

        void OnDestroy() { Loc.Changed -= OnLang; }
        void OnLang() { if (titleText) titleText.text = Loc.T(titleKey); }

        public void Reveal(bool instant)
        {
            if (Revealed) return;
            Revealed = true;
            gameObject.SetActive(true);
            OnLang();
            if (iconImage && icon) iconImage.sprite = icon;
            RefreshUI(false);
            if (!instant)
            {
                Tween.PopIn(visualRoot, Vector3.one, 0.45f, 0.1f);
                GameEvents.PadRevealed(this);
            }
            else visualRoot.localScale = Vector3.one;
        }

        public void Hide()
        {
            Revealed = false;
            gameObject.SetActive(false);
        }

        void RefreshUI(bool punch)
        {
            if (fill) fill.fillAmount = cost > 0 ? (float)paid / cost : 1f;
            if (priceText) priceText.text = Economy.Format(Remaining);
            if (punch && priceText) Tween.UIPunch(priceText.rectTransform, 0.12f, 0.12f);
        }

        void Update()
        {
            if (!Revealed || done) return;
            if (bobTarget) bobTarget.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 3f) * 0.06f, 0f);

            var p = PlayerController.I;
            PlayerOn = p != null && Station.InZone(p.transform.position, transform, radius);
            if (!PlayerOn) { standTime = 0f; return; }

            standTime += Time.deltaTime;
            var B = GameConfig.I.balance;
            if (standTime < B.padStartDelay) return;

            if (Economy.Money <= 0)
            {
                denyCooldown -= Time.deltaTime;
                if (denyCooldown <= 0f && Remaining > 0 && !p.IsMoving)
                {
                    denyCooldown = 2.5f;
                    Feedback.FloatText(Loc.T("hud.no_money"), transform.position + Vector3.up * 1.2f, Feedback.Red, 0.8f);
                    AudioManager.Play(Sfx.Error, 0.4f);
                    Tween.Punch(visualRoot, Vector3.one, 0.08f, 0.2f);
                }
                return;
            }

            float fillTime = Mathf.Clamp(B.padFillMin + cost * B.padFillPerCoin, B.padFillMin, B.padFillMax);
            float rate = cost / fillTime;
            int chunk = Mathf.Max(1, Mathf.CeilToInt(rate * Time.deltaTime));
            chunk = (int)Mathf.Min(chunk, Remaining, Economy.Money);
            if (chunk <= 0) return;
            if (!Economy.TrySpend(chunk)) return;
            paid += chunk;
            SaveSystem.SetPaid(id, paid);
            RefreshUI(false);

            coinTimer -= Time.deltaTime;
            if (coinTimer <= 0f)
            {
                coinTimer = 0.06f;
                FlyCoin(p.transform.position + Vector3.up * 1.2f);
                AudioManager.PlayCombo(Sfx.Pay, 0.35f);
            }

            if (paid >= cost)
            {
                done = true;
                ProgressionManager.I.OnPadPaid(this);
            }
        }

        void FlyCoin(Vector3 from)
        {
            var prefab = GameConfig.I.coinFlyPrefab;
            if (prefab == null) return;
            var go = Pool.Get(prefab, from, Quaternion.Euler(Random.Range(0, 360), Random.Range(0, 360), 0));
            var tr = go.transform;
            Vector3 target = transform.position + Vector3.up * 0.1f;
            Tween.Arc(tr, () => target, 1.0f, 0.28f, () => Pool.Release(go), Ease.InQuad, null, 1f, 0.5f);
        }

        /// <summary>Plays the "paid" burst and removes the pad.</summary>
        public void Complete(bool instant)
        {
            done = true;
            if (instant) { gameObject.SetActive(false); return; }
            if (fill) fill.fillAmount = 1f;
            Fx.CoinBurst(transform.position + Vector3.up * 0.3f, 0.8f);
            Tween.Scale(visualRoot, Vector3.one * 1.25f, 0.12f, Ease.OutQuad, () =>
                Tween.Scale(visualRoot, Vector3.zero, 0.22f, Ease.InBack, () => gameObject.SetActive(false)));
        }
    }
}

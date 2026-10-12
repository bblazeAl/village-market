using System.Collections.Generic;
using UnityEngine;

namespace IdleArcade
{
    /// <summary>Coins piling up at a till or table. Only the player collects them, by walking over the pile.</summary>
    public class MoneyPile : Station
    {
        public int cols = 3, rows = 3;
        public float spacing = 0.4f;
        public float layerHeight = 0.16f;
        public int maxVisual = 36;

        long amount;
        readonly List<Transform> visuals = new List<Transform>();
        float collectCooldown;

        public long Amount => amount;
        public bool HasMoney => amount > 0;

        protected override void OnEnable()
        {
            base.OnEnable();
            maxVisual = GameConfig.I.balance.pileMaxVisual;
        }

        Vector3 SlotLocal(int i)
        {
            int per = cols * rows;
            int layer = i / per, w = i % per;
            int c = w % cols, r = w / cols;
            return new Vector3((c - (cols - 1) * 0.5f) * spacing, layer * layerHeight, (r - (rows - 1) * 0.5f) * spacing);
        }

        public void Add(long value, Vector3 from, int visualCount = -1)
        {
            if (value <= 0) return;
            amount += value;
            int coinValue = Mathf.Max(1, GameConfig.I.balance.coinValue);
            int n = visualCount > 0 ? visualCount : Mathf.Clamp((int)(value / coinValue), 1, 6);
            var prefab = GameConfig.I.coinStackPrefab;
            for (int k = 0; k < n; k++)
            {
                if (visuals.Count >= maxVisual) break;
                int idx = visuals.Count;
                var go = Pool.Get(prefab, from, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                var tr = go.transform;
                visuals.Add(tr);
                Vector3 target = transform.TransformPoint(SlotLocal(idx));
                Vector3 scale = prefab.transform.localScale;
                tr.localScale = scale * 0.6f;
                Tween.Arc(tr, () => target, 0.9f, 0.32f + k * 0.04f, () =>
                {
                    if (!tr) return;
                    tr.SetParent(transform, true);
                    tr.localScale = scale;
                    Tween.Punch(tr, scale, 0.25f, 0.18f);
                }, Ease.InOutSine, null, 0.6f, 1f);
                if (k == 0) AudioManager.Play(Sfx.Coin, 0.55f);
            }
        }

        protected override void Update()
        {
            collectCooldown -= Time.deltaTime;
            base.Update();
        }

        protected override bool Interact(Carrier c)
        {
            if (!c.IsPlayer || amount <= 0 || collectCooldown > 0f) return false;
            CollectAll(c);
            return true;
        }

        public void CollectAll(Carrier c)
        {
            long value = amount;
            amount = 0;
            collectCooldown = 0.15f;
            Transform target = c.transform;
            int i = 0;
            for (int k = visuals.Count - 1; k >= 0; k--)
            {
                var tr = visuals[k];
                visuals.RemoveAt(k);
                if (!tr) continue;
                tr.SetParent(null, true);
                float delay = Mathf.Min(i * 0.025f, 0.5f);
                i++;
                Tween.Delay(delay, () =>
                {
                    if (!tr) return;
                    Tween.Arc(tr, () => target.position + Vector3.up * 1.1f, 1.2f, 0.3f, () =>
                    {
                        Pool.Release(tr.gameObject);
                        AudioManager.PlayCombo(Sfx.Coin, 0.5f);
                    }, Ease.InQuad, null, 1f, 0.4f);
                }, tr);
            }
            Economy.Add(value);
            Feedback.FloatText("+" + Economy.Format(value), transform.position + Vector3.up * 1.4f, Feedback.Gold, 1.15f);
            AudioManager.Play(Sfx.CoinsCollect, 0.9f);
            SaveSystem.Data.coinsCollected += value;
            GameEvents.MoneyCollected(value, this);
        }

        /// <summary>Restores an amount silently (no visuals flying), e.g. after loading.</summary>
        public void AddSilent(long value)
        {
            if (value <= 0) return;
            amount += value;
            int coinValue = Mathf.Max(1, GameConfig.I.balance.coinValue);
            int n = Mathf.Clamp((int)(value / coinValue), 1, maxVisual);
            var prefab = GameConfig.I.coinStackPrefab;
            for (int k = 0; k < n && visuals.Count < maxVisual; k++)
            {
                var go = Pool.Get(prefab, Vector3.zero, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
                go.transform.localPosition = SlotLocal(visuals.Count);
                visuals.Add(go.transform);
            }
        }
    }
}

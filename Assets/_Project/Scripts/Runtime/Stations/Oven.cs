using UnityEngine;
using UnityEngine.UI;

namespace IdleArcade
{
    public class Oven : Station
    {
        public ItemType output;
        public ItemContainer tray;
        public Transform body;
        public Transform mouth;
        public Image progress;
        public float cycle = 2.3f;
        float t;

        void Awake()
        {
            cycle = GameConfig.I.balance.ovenCycle;
            tray.max = GameConfig.I.balance.ovenTrayMax;
        }

        protected override void Update()
        {
            if (!tray.IsFull)
            {
                t += Time.deltaTime * Stations.SpeedMul(StationKind.Bread) * Perks.Mul(PerkId.Bake);
                if (t >= cycle) { t = 0f; Produce(); }
            }
            if (progress != null) progress.fillAmount = tray.IsFull ? 1f : t / cycle;
            base.Update();
        }

        void Produce()
        {
            var it = ItemVisual.Spawn(output, mouth.position);
            it.transform.localScale = Vector3.zero;
            Tween.Scale(it.transform, output.prefab.transform.localScale, 0.2f, Ease.OutBack);
            tray.Put(it, 0.35f, 0.7f);
            if (body != null) Shake(body, 0.06f);
            Fx.Smoke(mouth.position + Vector3.up * 0.3f, 0.35f);
        }

        protected override bool Interact(Carrier c)
        {
            if (tray.IsEmpty) return false;
            if (c.stack.IsFull) { if (c.IsPlayer) PlayerHud.NotifyFull(c); return false; }
            var it = tray.TakeTop();
            c.stack.Add(it);
            if (c.IsPlayer) AudioManager.PlayCombo(Sfx.Pickup, 0.8f);
            GameEvents.ItemPicked(output, c);
            return true;
        }
    }
}

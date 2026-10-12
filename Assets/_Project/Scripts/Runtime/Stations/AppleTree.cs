using UnityEngine;
using UnityEngine.UI;

namespace IdleArcade
{
    public class AppleTree : Station
    {
        public ItemType item;
        public Transform[] slots;
        public Transform shakeTarget;
        public float growInterval = 1.5f;

        ItemVisual[] grown;
        float timer;
        public int Available { get; private set; }
        public int Capacity => slots != null ? slots.Length : 0;

        void Awake()
        {
            grown = new ItemVisual[slots.Length];
            growInterval = GameConfig.I.balance.treeGrowInterval;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            timer = 0f;
        }

        /// <summary>Called when the tree is built: a few apples pop in right away.</summary>
        public void GrowInitial(int count, float delay)
        {
            for (int i = 0; i < count; i++)
            {
                int n = i;
                Tween.Delay(delay + n * 0.15f, () => GrowOne(), this);
            }
        }

        bool GrowOne()
        {
            int free = -1;
            for (int i = 0; i < grown.Length; i++) if (grown[i] == null) { free = i; break; }
            if (free < 0) return false;
            var it = ItemVisual.Spawn(item, slots[free].position, slots[free]);
            it.transform.localPosition = Vector3.zero;
            it.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-12f, 12f));
            Tween.PopIn(it.transform, item.prefab.transform.localScale, 0.4f);
            grown[free] = it;
            Available++;
            return true;
        }

        protected override void Update()
        {
            if (Available < Capacity)
            {
                timer += Time.deltaTime;
                if (timer >= growInterval / (Stations.SpeedMul(StationKind.Apples) * Perks.Mul(PerkId.TreeGrow))) { timer = 0f; GrowOne(); }
            }
            base.Update();
        }

        protected override bool Interact(Carrier c)
        {
            if (Available <= 0) return false;
            if (c.stack.IsFull) { if (c.IsPlayer) PlayerHud.NotifyFull(c); return false; }
            // pick the lowest apple first so the canopy empties top-last
            int best = -1;
            float bestY = float.MaxValue;
            for (int i = 0; i < grown.Length; i++)
            {
                if (grown[i] == null) continue;
                float y = grown[i].transform.position.y;
                if (y < bestY) { bestY = y; best = i; }
            }
            if (best < 0) return false;
            var it = grown[best];
            grown[best] = null;
            Available--;
            c.stack.Add(it, 0.3f, 0.6f);
            Shake(shakeTarget, 0.04f);
            if (c.IsPlayer) AudioManager.PlayCombo(Sfx.Pickup, 0.8f);
            GameEvents.ItemPicked(item, c);
            return true;
        }
    }
}

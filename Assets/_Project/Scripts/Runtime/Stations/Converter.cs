using UnityEngine;
using UnityEngine.UI;

namespace IdleArcade
{
    public class Converter : Station
    {
        public ItemType input, output;
        public ItemContainer inTray, outTray;
        public Transform outZone;
        public float outRadius = 1.0f;
        public Transform body, mouth;
        public Image progress;
        public GameObject progressRoot;
        public float cycle = 2.8f;
        float t;
        bool cooking;

        void Awake()
        {
            var b = GameConfig.I.balance;
            cycle = b.pieOvenCycle;
            inTray.max = b.pieOvenInputMax;
            outTray.max = b.pieOvenTrayMax;
        }

        protected override void Update()
        {
            if (!cooking && inTray.Count > 0 && !outTray.IsFull)
            {
                cooking = true;
                t = 0f;
                var it = inTray.TakeTop();
                var tr = it.transform;
                Tween.Arc(tr, () => mouth.position, 0.6f, 0.3f, () => it.Release());
            }
            if (cooking)
            {
                t += Time.deltaTime * Stations.SpeedMul(StationKind.Pies) * Perks.Mul(PerkId.Bake);
                if (t >= cycle)
                {
                    cooking = false;
                    var pie = ItemVisual.Spawn(output, mouth.position);
                    pie.transform.localScale = Vector3.zero;
                    Tween.Scale(pie.transform, output.prefab.transform.localScale, 0.2f, Ease.OutBack);
                    outTray.Put(pie, 0.35f, 0.7f);
                    if (body != null) Shake(body, 0.06f);
                    Fx.Smoke(mouth.position + Vector3.up * 0.3f, 0.35f);
                }
            }
            if (progress != null) progress.fillAmount = cooking ? t / cycle : 0f;
            if (progressRoot != null && progressRoot.activeSelf != cooking) progressRoot.SetActive(cooking);

            var list = Carrier.All;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                bool inIn = InZone(c.Position);
                bool inOut = InZone(c.Position, outZone, outRadius);
                if (!inIn && !inOut) continue;
                c.lastStation = this;
                c.lastInteractTime = Time.time;
                if (!c.Ready) continue;
                if (inIn && c.stack.Has(input) && inTray.CanAccept(input))
                {
                    inTray.Put(c.stack.Remove(input));
                    if (c.IsPlayer) AudioManager.PlayCombo(Sfx.Place, 0.8f);
                    GameEvents.ItemDropped(input, c, this);
                    c.Consume();
                }
                else if (inOut && !outTray.IsEmpty)
                {
                    if (c.stack.IsFull) { if (c.IsPlayer) PlayerHud.NotifyFull(c); continue; }
                    c.stack.Add(outTray.TakeTop());
                    if (c.IsPlayer) AudioManager.PlayCombo(Sfx.Pickup, 0.8f);
                    GameEvents.ItemPicked(output, c);
                    c.Consume();
                }
            }
        }

        protected override bool Interact(Carrier c) => false;
        public Vector3 OutZonePos => outZone.position;
    }
}

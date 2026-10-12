using UnityEngine;

namespace IdleArcade
{
    /// <summary>Customers with pies sit here, eat, then leave a dirty plate and a tip that must be cleared.</summary>
    public class PicnicTable : Station
    {
        public Transform[] seats;       // where the customer sits (faces table)
        public Transform[] plateSpots;  // on the table top, one per seat
        public MoneyPile tipPile;

        Customer[] occupants;
        ItemVisual[] food;
        ItemVisual[] plates;

        void Awake()
        {
            occupants = new Customer[seats.Length];
            food = new ItemVisual[seats.Length];
            plates = new ItemVisual[seats.Length];
        }

        public int DirtyCount
        {
            get { int n = 0; foreach (var p in plates) if (p != null) n++; return n; }
        }

        public bool HasFreeSeat
        {
            get
            {
                for (int i = 0; i < seats.Length; i++) if (occupants[i] == null && plates[i] == null) return true;
                return false;
            }
        }

        public bool TryReserve(Customer c, out int seat)
        {
            for (int i = 0; i < seats.Length; i++)
            {
                if (occupants[i] == null && plates[i] == null) { occupants[i] = c; seat = i; return true; }
            }
            seat = -1;
            return false;
        }

        public void Cancel(int seat)
        {
            if (seat < 0 || seat >= seats.Length) return;
            occupants[seat] = null;
        }

        public void ServeFood(int seat, ItemVisual it)
        {
            food[seat] = it;
            it.transform.SetParent(null, true);
            var target = plateSpots[seat];
            Tween.Arc(it.transform, () => target.position, 0.4f, 0.3f, () =>
            {
                if (!it) return;
                it.transform.SetParent(target, true);
                it.transform.localPosition = Vector3.zero;
            });
        }

        /// <summary>Food shrinks while eating (0..1).</summary>
        public void SetEatProgress(int seat, float k)
        {
            var f = food[seat];
            if (f == null) return;
            float s = Mathf.Lerp(1f, 0.35f, k);
            f.transform.localScale = f.type.prefab.transform.localScale * s;
        }

        public void FinishEating(int seat, int tip)
        {
            if (food[seat] != null) { food[seat].Release(); food[seat] = null; }
            occupants[seat] = null;
            var plateType = GameConfig.I.plate;
            var p = ItemVisual.Spawn(plateType, plateSpots[seat].position, plateSpots[seat]);
            p.transform.localPosition = Vector3.zero;
            Tween.PopIn(p.transform, plateType.prefab.transform.localScale, 0.3f);
            plates[seat] = p;
            if (tip > 0 && tipPile != null) tipPile.Add(tip, plateSpots[seat].position + Vector3.up * 0.3f, 2);
            GameEvents.TableDirty(this);
        }

        protected override bool Interact(Carrier c)
        {
            for (int i = 0; i < plates.Length; i++)
            {
                if (plates[i] == null) continue;
                if (c.stack.IsFull) { if (c.IsPlayer) PlayerHud.NotifyFull(c); return false; }
                var p = plates[i];
                plates[i] = null;
                Tween.KillOwner(p.transform);
                c.stack.Add(p, 0.25f, 0.5f);
                if (c.IsPlayer) AudioManager.PlayCombo(Sfx.Plate, 0.7f);
                GameEvents.ItemPicked(p.type, c);
                return true;
            }
            return false;
        }
    }
}

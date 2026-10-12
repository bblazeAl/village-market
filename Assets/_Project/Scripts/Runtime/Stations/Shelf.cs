using UnityEngine;

namespace IdleArcade
{
    /// <summary>Display stand: carriers drop matching items from the player side, customers take from the other side.</summary>
    public class Shelf : Station
    {
        public ItemType type;
        public ItemContainer container;
        public Transform[] customerSpots;
        public Transform body;

        Customer[] owners;

        void Awake() { owners = new Customer[customerSpots.Length]; }

        public int Stock => container.Count;
        public bool NeedsStock => container.Count < container.max;
        public float Fill => container.max > 0 ? (float)container.Count / container.max : 1f;

        protected override bool Interact(Carrier c)
        {
            if (!c.stack.Has(type)) return false;
            if (!container.CanAccept(type))
            {
                if (c.IsPlayer) PlayerHud.NotifyShelfFull(this);
                return false;
            }
            var it = c.stack.Remove(type);
            container.Put(it, 0.22f, 0.7f, () => { if (body) Shake(body, 0.02f); });
            if (c.IsPlayer) AudioManager.PlayCombo(Sfx.Place, 0.7f);
            GameEvents.ItemDropped(type, c, this);
            return true;
        }

        public bool TryReserve(Customer cu, out Transform spot)
        {
            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i] == cu) { spot = customerSpots[i]; return true; }
            }
            for (int i = 0; i < owners.Length; i++)
            {
                if (owners[i] == null) { owners[i] = cu; spot = customerSpots[i]; return true; }
            }
            spot = null;
            return false;
        }

        public void Release(Customer cu)
        {
            for (int i = 0; i < owners.Length; i++) if (owners[i] == cu) owners[i] = null;
        }

        public int FreeSpots
        {
            get
            {
                int n = 0;
                foreach (var o in owners) if (o == null) n++;
                return n;
            }
        }

        public ItemVisual TakeForCustomer() => container.TakeTop();

        /// <summary>Where a shopper waits when both spots are taken: a little queue behind the spots, on the customer side.</summary>
        public Vector3 WaitPoint(Customer c)
        {
            Vector3 mid = Vector3.zero;
            foreach (var s in customerSpots) mid += s.position;
            mid /= Mathf.Max(1, customerSpots.Length);
            Vector3 back = -transform.forward;
            int slot = Mathf.Abs(c.GetInstanceID()) % 3;
            return mid + back * (1.0f + slot * 0.55f) + transform.right * ((slot - 1) * 0.5f);
        }
    }
}

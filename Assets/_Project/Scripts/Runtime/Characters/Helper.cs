using UnityEngine;
using UnityEngine.AI;

namespace IdleArcade
{
    public enum HelperRole { None, Cashier, ApplePicker, Baker, PieHelper }

    /// <summary>
    /// Hired worker. Cashiers hold the till; carriers loop source → shelf. Item transfer itself is
    /// done by the stations (helpers are Carriers), so the AI only decides where to stand.
    /// </summary>
    public class Helper : MonoBehaviour
    {
        public HelperRole role;
        public NavMeshAgent agent;
        public Carrier carrier;
        public ItemStack stack;
        public CharacterAnim anim;

        Station goal;
        float repath, emptySourceTime;
        Balance B => GameConfig.I.balance;

        int idleFrames;

        void LateUpdate()
        {
            // arms folded while there is nothing to do; normal stance otherwise
            anim.mood = idleFrames > 0 && agent.velocity.sqrMagnitude < 0.05f && stack.IsEmpty ? 2f : 0f;
            if (idleFrames > 0) idleFrames--;
        }

        void Update()
        {
            if (!agent.enabled || !agent.isOnNavMesh) return;
            agent.speed = Economy.HelperSpeed;
            stack.capacity = Economy.HelperCapacity;
            carrier.transferInterval = B.helperTransferInterval;

            switch (role)
            {
                case HelperRole.Cashier: UpdateCashier(); break;
                case HelperRole.ApplePicker: UpdateCarrierLoop(GameConfig.I.apple, FindTree(), FindShelf(GameConfig.I.apple)); break;
                case HelperRole.Baker: UpdateCarrierLoop(GameConfig.I.bread, FindOven(), FindShelf(GameConfig.I.bread)); break;
                case HelperRole.PieHelper: UpdatePieHelper(); break;
            }
        }

        // ---------------- movement ----------------
        void GoTo(Station s, Vector3? overridePos = null)
        {
            if (s == null) return;
            Vector3 p = overridePos ?? s.ZonePos;
            if (goal == s && agent.hasPath && repath > 0f) { repath -= Time.deltaTime; return; }
            goal = s;
            repath = 1.5f;
            if (NavMesh.SamplePosition(p, out var hit, 1.2f, NavMesh.AllAreas)) p = hit.position;
            agent.SetDestination(p);
        }

        bool At(Station s, Vector3? pos = null, float r = -1f)
        {
            if (s == null) return false;
            float rad = r > 0 ? r : s.radius * 0.8f;
            Vector3 d = transform.position - (pos ?? s.ZonePos);
            d.y = 0f;
            return d.sqrMagnitude <= rad * rad;
        }

        // ---------------- cashier ----------------
        Checkout till;
        void UpdateCashier()
        {
            if (till == null) foreach (var s in Station.All) if (s is Checkout c && c.isActiveAndEnabled) till = c;
            if (till == null) return;
            if (At(till, null, 0.45f))
            {
                till.HasCashier = true;
                agent.ResetPath();
                var rot = till.zone.rotation;
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
            else
            {
                till.HasCashier = false;
                GoTo(till);
            }
        }

        // ---------------- carrier loop ----------------
        bool delivering;
        void UpdateCarrierLoop(ItemType item, Station source, Shelf sink)
        {
            if (sink == null) { Idle(); return; }
            bool holding = stack.Has(item);
            if (delivering)
            {
                if (!holding) { delivering = false; goal = null; return; }
                if (!At(sink)) GoTo(sink); else agent.ResetPath();
                return;
            }
            // collecting
            int stock = SourceStock(source);
            if (stack.IsFull) { delivering = true; return; }
            if (source == null || stock == 0)
            {
                if (holding)
                {
                    emptySourceTime += Time.deltaTime;
                    if (emptySourceTime > 0.9f) { delivering = true; emptySourceTime = 0f; }
                    return;
                }
                Idle();
                return;
            }
            emptySourceTime = 0f;
            if (!At(source)) GoTo(source); else agent.ResetPath();
        }

        int SourceStock(Station s)
        {
            if (s is AppleTree t) return t.Available;
            if (s is Oven o) return o.tray.Count;
            if (s is Converter cv) return cv.outTray.Count;
            return 0;
        }

        AppleTree FindTree()
        {
            AppleTree best = null;
            float bestScore = float.MinValue;
            foreach (var s in Station.All)
            {
                if (!(s is AppleTree t) || !t.isActiveAndEnabled) continue;
                float score = t.Available * 3f - (t.ZonePos - transform.position).magnitude;
                if (goal == t) score += 4f; // stickiness
                if (score > bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        Oven FindOven()
        {
            Oven best = null;
            int most = -1;
            foreach (var s in Station.All)
                if (s is Oven o && o.isActiveAndEnabled && o.tray.Count > most) { most = o.tray.Count; best = o; }
            return best;
        }

        Shelf FindShelf(ItemType t)
        {
            Shelf best = null;
            float bestFill = float.MaxValue;
            foreach (var s in Station.All)
                if (s is Shelf sh && sh.isActiveAndEnabled && sh.type == t && sh.Fill < bestFill) { bestFill = sh.Fill; best = sh; }
            return best;
        }

        // ---------------- pie helper ----------------
        void UpdatePieHelper()
        {
            var pie = GameConfig.I.pie;
            var apple = GameConfig.I.apple;
            var plate = GameConfig.I.plate;
            Converter oven = null;
            WashTub tub = null;
            foreach (var s in Station.All)
            {
                if (s is Converter c && c.isActiveAndEnabled) oven = c;
                if (s is WashTub w && w.isActiveAndEnabled) tub = w;
            }
            var pieShelf = FindShelf(pie);

            if (stack.Has(plate) && tub != null)
            {
                bool moreDirty = TableManager.TotalDirty() > 0 && !stack.IsFull;
                if (!moreDirty || goal == tub) { if (!At(tub)) GoTo(tub); else agent.ResetPath(); return; }
            }
            if (stack.Has(pie) && pieShelf != null)
            {
                if (!At(pieShelf)) GoTo(pieShelf); else agent.ResetPath();
                return;
            }
            if (stack.Has(apple) && oven != null)
            {
                if (!At(oven)) GoTo(oven); else agent.ResetPath();
                return;
            }
            var dirty = TableManager.DirtiestTable(transform.position);
            if (dirty != null && tub != null && !stack.IsFull)
            {
                if (!At(dirty)) GoTo(dirty); else agent.ResetPath();
                return;
            }
            if (oven != null && oven.outTray.Count > 0 && pieShelf != null && pieShelf.NeedsStock)
            {
                Vector3 outPos = oven.OutZonePos;
                if (!At(oven, outPos, oven.outRadius * 0.8f)) GoTo(oven, outPos); else agent.ResetPath();
                return;
            }
            if (oven != null && oven.inTray.Count < 3)
            {
                var tree = FindTree();
                if (tree != null && tree.Available > 0)
                {
                    if (!At(tree)) GoTo(tree); else agent.ResetPath();
                    return;
                }
            }
            Idle();
        }

        Vector3 idleSpot;
        bool hasIdle;
        void Idle()
        {
            if (!hasIdle) { idleSpot = transform.position; hasIdle = true; }
            goal = null;
            idleFrames = 2;
            if (agent.hasPath && agent.remainingDistance < 0.3f) agent.ResetPath();
        }
    }
}

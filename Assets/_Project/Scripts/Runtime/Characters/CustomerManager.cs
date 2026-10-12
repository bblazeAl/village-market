using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace IdleArcade
{
    /// <summary>Spawns shoppers from the village road at a pace that scales with how much the market sells.</summary>
    public class CustomerManager : MonoBehaviour
    {
        public static CustomerManager I { get; private set; }

        public GameObject customerBase;       // root with agent, anim, hands, bubble
        public Transform spawnPoint;
        public Transform[] spawnPoints;
        public Transform exitPoint;
        public Checkout checkout;

        readonly List<Customer> active = new List<Customer>();
        readonly Dictionary<int, Stack<Customer>> pool = new Dictionary<int, Stack<Customer>>();
        float timer = 1.5f;
        float vipTimer;
        int spawned;
        public int ActiveCount => active.Count;
        public IReadOnlyList<Customer> Active => active;

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        Balance B => GameConfig.I.balance;

        readonly List<Shelf> shelves = new List<Shelf>();

        void CollectShelves()
        {
            shelves.Clear();
            foreach (var s in Station.All) if (s is Shelf sh && sh.isActiveAndEnabled) shelves.Add(sh);
        }

        void Update()
        {
            if (checkout == null || !checkout.isActiveAndEnabled) return;
            CollectShelves();
            if (shelves.Count == 0) return;

            int max = B.customerBaseMax + B.customerMaxPerShelf * (shelves.Count - 1);
            // the first customer arrives as soon as there is something to buy
            bool anyStock = false;
            foreach (var s in shelves) if (s.Stock > 0) { anyStock = true; break; }
            if (spawned == 0 && !anyStock && SaveSystem.Data.customersServed == 0) return;

            timer -= Time.deltaTime;
            vipTimer += Time.deltaTime;
            if (timer > 0f || active.Count >= max) return;

            float interval = Mathf.Lerp(B.customerInterval, B.customerIntervalMin, Mathf.InverseLerp(1, 5, shelves.Count));
            timer = interval * Random.Range(0.8f, 1.2f);
            Spawn();
        }

        void Spawn()
        {
            var wants = new List<Customer.Want>();
            bool first = SaveSystem.Data.customersServed == 0 && spawned == 0;
            if (first)
            {
                wants.Add(new Customer.Want { shelf = shelves[0], count = 2 });
            }
            else
            {
                // 1-2 different shelves, weighted toward newer, pricier goods
                int kinds = shelves.Count > 1 && Random.value < 0.45f ? 2 : 1;
                var pick = new List<Shelf>(shelves);
                for (int k = 0; k < kinds && pick.Count > 0; k++)
                {
                    // shelves that have stock are three times as likely, so shoppers don't all crowd an empty one
                    float total = 0f;
                    foreach (var ps in pick) total += ps.Stock > 0 ? 3f : 1f;
                    float r = Random.value * total;
                    int idx = 0;
                    for (; idx < pick.Count - 1; idx++) { r -= pick[idx].Stock > 0 ? 3f : 1f; if (r <= 0f) break; }
                    var s = pick[idx];
                    pick.RemoveAt(idx);
                    int maxCount = s.type == GameConfig.I.pie ? 2 : 3;
                    wants.Add(new Customer.Want { shelf = s, count = Random.Range(1, maxCount + 1) });
                }
            }

            bool vip = false;
            if (SaveSystem.Data.marketLevel >= 2 && vipTimer > 45f && Random.value < B.vipChancePerMinute * Perks.Mul(PerkId.Vip) * (vipTimer / 60f) * 0.25f)
            {
                vip = true;
                vipTimer = 0f;
                foreach (var w in wants) w.count += 1;
            }

            var models = GameConfig.I.customerModels;
            int mi = Random.Range(0, models.Length);
            Transform sp = spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints[Random.Range(0, spawnPoints.Length)] : spawnPoint;
            Vector3 pos = sp.position;
            if (NavMesh.SamplePosition(pos, out var hit, 2f, NavMesh.AllAreas)) pos = hit.position;
            var c = GetCustomer(mi, pos);
            c.transform.SetPositionAndRotation(pos, sp.rotation);
            c.gameObject.SetActive(true);
            c.agent.enabled = true;
            c.agent.Warp(pos);
            c.bubble.ResetBubble();
            Vector3 exit = exitPoint.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            c.Init(wants, checkout, exit, vip);
            active.Add(c);
            spawned++;
            if (vip)
            {
                Feedback.Toast(Loc.T("toast.vip"), GameConfig.I.iconStar);
                AudioManager.Play(Sfx.Vip, 0.7f);
            }
        }

        Customer GetCustomer(int modelIndex, Vector3 pos)
        {
            if (pool.TryGetValue(modelIndex, out var st) && st.Count > 0) return st.Pop();
            var go = Instantiate(customerBase, pos, Quaternion.identity, transform);
            go.name = "Customer_" + modelIndex;
            var c = go.GetComponent<Customer>();
            c.modelIndex = modelIndex;
            var model = Instantiate(GameConfig.I.customerModels[modelIndex], go.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var animator = model.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = GameConfig.I.animator;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            c.anim.Bind(animator);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null && GameConfig.I.vipCrown != null)
            {
                c.crown = Instantiate(GameConfig.I.vipCrown, head);
                c.crown.transform.localPosition = new Vector3(0f, 0.22f, 0f);
                c.crown.transform.localRotation = Quaternion.identity;
                c.crown.SetActive(false);
            }
            return c;
        }

        public void OnCustomerGone(Customer c)
        {
            active.Remove(c);
            c.gameObject.SetActive(false);
            if (!pool.TryGetValue(c.modelIndex, out var st)) pool[c.modelIndex] = st = new Stack<Customer>();
            st.Push(c);
        }

        public int CountWaitingFor(Shelf s)
        {
            int n = 0;
            foreach (var c in active)
                if (c.IsShopping && c.CurrentWant != null && c.CurrentWant.shelf == s) n++;
            return n;
        }

        public void ClearAll()
        {
            for (int i = active.Count - 1; i >= 0; i--) active[i].ForceRemove();
        }
    }
}

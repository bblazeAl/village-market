using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace IdleArcade
{
    /// <summary>
    /// Shopper AI: visits each wanted shelf and waits for stock, queues at the till, pays,
    /// optionally eats a pie at a table (leaving a plate and a tip), then walks home.
    /// </summary>
    public class Customer : MonoBehaviour
    {
        public class Want
        {
            public Shelf shelf;
            public int count;
            public int got;
        }

        enum State { ToShelf, AtShelf, ToQueue, InQueue, ToTable, Eating, Leaving, Gone, Reacting }

        public NavMeshAgent agent;
        public CharacterAnim anim;
        public ItemStack hands;
        public CustomerBubble bubble;
        public Transform crownSocket;

        [System.NonSerialized] public int modelIndex;
        [System.NonSerialized] public GameObject crown;

        readonly List<Want> wants = new List<Want>();
        int wantIndex;
        State state;
        Transform shelfSpot;
        float takeTimer, stuckTimer, waitTimer, patience;
        Checkout checkout;
        int queueIndex = -1;
        Vector3 exitPos;
        PicnicTable table;
        int seat = -1;
        float eatT;
        GameObject basketGo;
        public bool IsVip { get; private set; }
        float reactUntil;
        System.Action afterReaction;

        /// <summary>Stands still for a moment to play an emote, then carries on.</summary>
        void React(int emote, float seconds, System.Action then)
        {
            state = State.Reacting;
            reactUntil = Time.time + seconds;
            afterReaction = then;
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            anim.mood = 0f;
            anim.Emote(emote);
        }
        public IReadOnlyList<Want> Wants => wants;
        public Want CurrentWant => wantIndex < wants.Count ? wants[wantIndex] : null;
        public bool IsShopping => state == State.ToShelf || state == State.AtShelf;
        public bool ReadyAtTill => state == State.InQueue && queueIndex == 0 && Arrived(0.25f);
        public bool IsGone => state == State.Gone;

        public void Init(List<Want> w, Checkout co, Vector3 exit, bool vip)
        {
            wants.Clear();
            wants.AddRange(w);
            wantIndex = 0;
            checkout = co;
            exitPos = exit;
            IsVip = vip;
            queueIndex = -1;
            table = null;
            seat = -1;
            basketGo = null;
            anim.holdOverride = false;
            hands.ClearAll();
            hands.capacity = 12;
            agent.speed = GameConfig.I.balance.customerSpeed * (vip ? 1.1f : Random.Range(0.92f, 1.08f));
            agent.enabled = true;
            if (crown != null) crown.SetActive(vip);
            anim.Sitting = false;
            anim.Eating = false;
            anim.mood = 0f;
            GoToShelf();
        }

        bool Arrived(float tolerance = 0.15f)
        {
            if (!agent.enabled || agent.pathPending) return false;
            return agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, tolerance);
        }

        void SetDest(Vector3 p)
        {
            if (!agent.enabled) return;
            if (NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas)) p = hit.position;
            agent.SetDestination(p);
            stuckTimer = 0f;
        }

        void FaceTowards(Quaternion rot)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        void GoToShelf()
        {
            var w = CurrentWant;
            if (w == null) { GoToQueue(); return; }
            state = State.ToShelf;
            shelfSpot = null;
            waitTimer = 0f;
            patience = 0f;
            if (w.shelf.TryReserve(this, out var spot)) shelfSpot = spot;
            Vector3 target = shelfSpot != null ? shelfSpot.position : w.shelf.WaitPoint(this);
            SetDest(target);
            bubble.ShowWant(w.shelf.type.icon, w.count - w.got);
        }

        void GoToQueue()
        {
            state = State.ToQueue;
            queueIndex = checkout.Join(this);
            var p = checkout.SpotPosition(queueIndex, out _);
            SetDest(p);
            bubble.ShowCheckout();
        }

        public void OnQueueIndexChanged(int idx)
        {
            queueIndex = idx;
            if (state == State.InQueue || state == State.ToQueue)
            {
                var p = checkout.SpotPosition(idx, out _);
                SetDest(p);
            }
        }

        void Update()
        {
            switch (state)
            {
                case State.ToShelf: UpdateToShelf(); break;
                case State.AtShelf: UpdateAtShelf(); break;
                case State.ToQueue:
                    if (Arrived(0.3f)) state = State.InQueue;
                    break;
                case State.InQueue:
                {
                    var p = checkout.SpotPosition(Mathf.Max(0, queueIndex), out var rot);
                    if (Arrived(0.3f)) FaceTowards(rot);
                    else if ((transform.position - p).sqrMagnitude > 0.2f && !agent.hasPath) SetDest(p);
                    break;
                }
                case State.ToTable:
                    if (Arrived(0.2f)) SitDown();
                    break;
                case State.Eating: UpdateEating(); break;
                case State.Leaving:
                    if (Arrived(0.6f)) Despawn();
                    break;
                case State.Reacting:
                    if (Time.time >= reactUntil) { var next = afterReaction; afterReaction = null; next?.Invoke(); }
                    break;
            }
            // safety: if a path fails, retry
            if (agent.enabled && state != State.Eating && state != State.Gone)
            {
                if (agent.velocity.sqrMagnitude < 0.01f && !Arrived(0.6f)) stuckTimer += Time.deltaTime; else stuckTimer = 0f;
                if (stuckTimer > 3f && agent.hasPath) { agent.ResetPath(); SetDest(agent.destination); }
            }
        }

        void UpdateToShelf()
        {
            var w = CurrentWant;
            if (shelfSpot == null)
            {
                if (TickPatience()) return;
                // queueing near a busy shelf until a spot frees
                waitTimer += Time.deltaTime;
                if (waitTimer > 0.5f)
                {
                    waitTimer = 0f;
                    if (w.shelf.TryReserve(this, out var spot)) { shelfSpot = spot; SetDest(spot.position); }
                }
                return;
            }
            if (Arrived(0.2f)) { state = State.AtShelf; takeTimer = 0.2f; }
        }

        void UpdateAtShelf()
        {
            var w = CurrentWant;
            if (shelfSpot != null) FaceTowards(shelfSpot.rotation);
            takeTimer -= Time.deltaTime;
            if (takeTimer > 0f) return;
            if (w.shelf.Stock == 0 && TickPatience()) return;
            if (w.shelf.Stock > 0)
            {
                patience = Mathf.Max(0f, patience - 4f);
                anim.mood = 0f;
                var it = w.shelf.TakeForCustomer();
                if (it != null)
                {
                    hands.Add(it, 0.25f, 0.5f);
                    w.got++;
                    takeTimer = 0.28f;
                    bubble.ShowWant(w.shelf.type.icon, w.count - w.got);
                    if (w.got >= w.count)
                    {
                        w.shelf.Release(this);
                        wantIndex++;
                        if (CurrentWant != null) GoToShelf(); else GoToQueue();
                    }
                }
            }
        }

        /// <summary>Counts up while the wanted shelf is empty; after a while the shopper gives up on that item.</summary>
        bool TickPatience()
        {
            patience += Time.deltaTime;
            float limit = GameConfig.I.balance.customerPatience * (IsVip ? 1.5f : 1f) * Perks.Mul(PerkId.Patience);
            float k = Mathf.InverseLerp(limit * 0.5f, limit, patience);
            bubble.SetImpatience(k);
            anim.mood = k > 0.05f ? 1f : 0f;   // foot tapping while waiting too long
            if (patience < limit) return false;
            GiveUp();
            return true;
        }

        void GiveUp()
        {
            var w = CurrentWant;
            w.count = w.got;
            w.shelf.Release(this);
            bubble.ShowSad();
            AudioManager.Play(Sfx.Error, 0.25f);
            GameEvents.Log("customer gave up on " + w.shelf.type.name);
            wantIndex++;
            patience = 0f;
            React(Random.value < 0.5f ? CharacterAnim.EmoteDisappointed : CharacterAnim.EmoteShrug, 1.3f, () =>
            {
                if (CurrentWant != null) { GoToShelf(); return; }
                if (hands.Count > 0) { GoToQueue(); return; }
                Leave();
            });
        }

        static int SoldIndex(ItemType t)
        {
            var C = GameConfig.I;
            if (t == C.apple) return (int)ItemKind.Apple;
            if (t == C.bread) return (int)ItemKind.Bread;
            if (t == C.pie) return (int)ItemKind.Pie;
            return -1;
        }

        /// <summary>Checkout finished: returns the amount paid and decides where to go next.</summary>
        public long Pay(Checkout co)
        {
            float total = 0f;
            bool hasPie = false;
            foreach (var w in wants)
            {
                total += w.shelf.type.price * Stations.PriceMul(w.shelf.type) * w.got;
                if (w.shelf.type == GameConfig.I.pie && w.got > 0) hasPie = true;
                int kind = SoldIndex(w.shelf.type);
                if (kind >= 0) SaveSystem.Data.sold[kind] += w.got;
            }
            total *= Economy.ProfitMultiplier;
            if (IsVip) total *= GameConfig.I.balance.vipMultiplier;
            long paid = Mathf.Max(1, Mathf.RoundToInt(total));

            // keep one pie in hand if a table is free; the rest goes into a basket
            ItemVisual keepPie = null;
            if (hasPie && TableManager.TryFindSeat(this, out table, out seat))
                keepPie = hands.Remove(GameConfig.I.pie);

            var bagPoint = co.bagPoint;
            int i = 0;
            while (hands.Count > 0)
            {
                var it = hands.Remove();
                var tr = it.transform;
                Tween.Arc(tr, () => bagPoint.position, 0.6f, 0.2f + i * 0.04f, () => it.Release());
                i++;
            }
            var bp = GameConfig.I.basketPrefab;
            if (bp != null)
            {
                var go = Pool.Get(bp, bagPoint.position, Quaternion.identity);
                var btr = go.transform;
                btr.localScale = Vector3.zero;
                Vector3 s = bp.transform.localScale;
                Tween.Delay(0.25f, () =>
                {
                    if (!btr) return;
                    btr.localScale = s;
                    Tween.Arc(btr, () => hands.anchor.position, 0.5f, 0.25f, () =>
                    {
                        if (!btr) return;
                        btr.SetParent(hands.anchor, true);
                        btr.localPosition = Vector3.zero;
                        btr.localRotation = Quaternion.identity;
                    });
                }, btr);
                basketGo = go;
                anim.holdOverride = true;
            }
            if (keepPie != null)
            {
                hands.Add(keepPie, 0.2f, 0.3f);
            }

            bubble.ShowHappy();
            Fx.Heart(transform.position + Vector3.up * 2.2f, 0.6f);
            AudioManager.Play(Sfx.Happy, 0.45f);
            if (IsVip) Feedback.FloatText(Loc.T("hud.vip"), transform.position + Vector3.up * 2.8f, Feedback.Gold, 1.2f);

            queueIndex = -1;
            int emote = Random.value < 0.25f ? CharacterAnim.EmoteThanks : Random.Range(CharacterAnim.EmoteHappyFirst, CharacterAnim.EmoteHappyLast + 1);
            React(emote, 1.15f, () =>
            {
                if (table != null)
                {
                    state = State.ToTable;
                    SetDest(table.seats[seat].position);
                }
                else Leave();
            });
            return paid;
        }

        void SitDown()
        {
            state = State.Eating;
            eatT = 0f;
            agent.enabled = false;
            var s = table.seats[seat];
            transform.SetPositionAndRotation(s.position, s.rotation);
            anim.Sitting = true;
            anim.Eating = true;
            var pie = hands.Remove(GameConfig.I.pie);
            if (pie != null) table.ServeFood(seat, pie);
            bubble.ShowEating();
        }

        void UpdateEating()
        {
            eatT += Time.deltaTime;
            float dur = GameConfig.I.balance.eatTime;
            table.SetEatProgress(seat, eatT / dur);
            if (eatT >= dur)
            {
                table.FinishEating(seat, GameConfig.I.balance.tableTip + Perks.Add(PerkId.Tips));
                anim.Sitting = false;
                anim.Eating = false;
                agent.enabled = true;
                table = null;
                seat = -1;
                Leave();
            }
        }

        void Leave()
        {
            state = State.Leaving;
            bubble.Hide(1.2f);
            foreach (var w in wants) w.shelf.Release(this);
            SetDest(exitPos);
        }

        void Despawn()
        {
            state = State.Gone;
            if (basketGo != null) { Pool.Release(basketGo); basketGo = null; }
            anim.holdOverride = false;
            hands.ClearAll();
            checkout?.Leave(this);
            CustomerManager.I?.OnCustomerGone(this);
        }

        public void ForceRemove()
        {
            foreach (var w in wants) w.shelf.Release(this);
            if (table != null) table.Cancel(seat);
            Despawn();
        }
    }
}

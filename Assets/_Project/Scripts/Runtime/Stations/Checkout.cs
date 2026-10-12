using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IdleArcade
{
    /// <summary>
    /// The till. Customers queue in front; whoever stands in the cashier zone (player or hired cashier)
    /// checks them out one by one. Payment lands on the money pile next to the till.
    /// </summary>
    public class Checkout : Station
    {
        public Transform[] queueSpots;
        public MoneyPile pile;
        public Transform body;
        public Transform bagPoint;
        public Image progress;
        public GameObject progressRoot;

        readonly List<Customer> queue = new List<Customer>();
        float t;
        public bool HasCashier { get; set; }
        public bool PlayerAttending { get; private set; }
        public bool Attended => PlayerAttending || HasCashier;
        public int QueueLength => queue.Count;
        public bool HasWaitingCustomer => queue.Count > 0 && queue[0].ReadyAtTill;

        public int Join(Customer c)
        {
            if (!queue.Contains(c)) queue.Add(c);
            return queue.IndexOf(c);
        }

        public void Leave(Customer c)
        {
            if (queue.Remove(c)) Reflow();
        }

        public int IndexOf(Customer c) => queue.IndexOf(c);

        public Vector3 SpotPosition(int index, out Quaternion rot)
        {
            if (index < queueSpots.Length)
            {
                rot = queueSpots[index].rotation;
                return queueSpots[index].position;
            }
            // extend the line past the last authored spot
            var last = queueSpots[queueSpots.Length - 1];
            var prev = queueSpots.Length > 1 ? queueSpots[queueSpots.Length - 2] : last;
            Vector3 step = last.position - prev.position;
            if (step.sqrMagnitude < 0.01f) step = -last.forward * 0.9f;
            rot = last.rotation;
            return last.position + step * (index - queueSpots.Length + 1);
        }

        void Reflow()
        {
            for (int i = 0; i < queue.Count; i++) queue[i].OnQueueIndexChanged(i);
        }

        protected override void Update()
        {
            PlayerAttending = false;
            foreach (var c in Carrier.All)
            {
                if (!c.IsPlayer || !InZone(c.Position)) continue;
                PlayerAttending = true;
                c.lastStation = this;
                c.lastInteractTime = Time.time;
            }

            bool working = Attended && HasWaitingCustomer;
            if (working)
            {
                float dur = (PlayerAttending ? GameConfig.I.balance.playerCheckoutTime : Economy.CashierTime)
                            / (Stations.SpeedMul(StationKind.Checkout) * Perks.Mul(PerkId.Checkout));
                t += Time.deltaTime / Mathf.Max(0.1f, dur);
                if (t >= 1f)
                {
                    t = 0f;
                    var cu = queue[0];
                    queue.RemoveAt(0);
                    long paid = cu.Pay(this);
                    if (paid > 0)
                    {
                        pile.Add(paid, bagPoint.position);
                        Feedback.FloatText("+" + Economy.Format(paid), cu.transform.position + Vector3.up * 2.3f, Feedback.Gold);
                    }
                    if (body) Shake(body, 0.03f);
                    AudioManager.Play(Sfx.Checkout, 0.7f);
                    SaveSystem.Data.customersServed++;
                    if (cu.IsVip) SaveSystem.Data.vipServed++;
                    var B = GameConfig.I.balance;
                    Progress.AddXp(cu.IsVip ? B.xpPerVip : B.xpPerCustomer, "customer");
                    GameEvents.CustomerServed(cu, paid);
                    Reflow();
                }
            }
            else t = Mathf.MoveTowards(t, 0f, Time.deltaTime * 0.5f);

            if (progressRoot != null) progressRoot.SetActive(working);
            if (progress != null) progress.fillAmount = t;
        }

        protected override bool Interact(Carrier c) => false;
    }
}

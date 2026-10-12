using System.Collections.Generic;
using UnityEngine;

namespace IdleArcade
{
    public enum CarrierKind { Player, Helper }

    /// <summary>Anything that moves items between stations: the player and helpers.</summary>
    public class Carrier : MonoBehaviour
    {
        public static readonly List<Carrier> All = new List<Carrier>();

        public CarrierKind kind;
        public ItemStack stack;
        public float transferInterval = 0.09f;
        public bool interactionsEnabled = true;

        float nextTransfer;
        public bool IsPlayer => kind == CarrierKind.Player;
        public Vector3 Position => transform.position;
        public bool Ready => interactionsEnabled && Time.time >= nextTransfer;
        public void Consume(float scale = 1f) => nextTransfer = Time.time + transferInterval * scale;

        /// <summary>Which station this carrier last interacted with (for guidance and helper AI).</summary>
        public Station lastStation;
        public float lastInteractTime;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }
    }
}

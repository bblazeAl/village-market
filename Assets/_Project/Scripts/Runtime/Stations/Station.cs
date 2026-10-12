using System.Collections.Generic;
using UnityEngine;

namespace IdleArcade
{
    /// <summary>Base for anything a carrier interacts with by standing in its zone.</summary>
    public abstract class Station : MonoBehaviour
    {
        public static readonly List<Station> All = new List<Station>();

        public Transform zone;
        public float radius = 1.1f;
        public string displayKey;

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        public Vector3 ZonePos => zone != null ? zone.position : transform.position;

        public static bool InZone(Vector3 p, Transform z, float r)
        {
            if (z == null) return false;
            Vector3 d = p - z.position;
            d.y = 0f;
            return d.sqrMagnitude <= r * r;
        }

        public bool InZone(Vector3 p) => InZone(p, zone, radius);

        protected virtual void Update()
        {
            var list = Carrier.All;
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (!InZone(c.Position)) continue;
                c.lastStation = this;
                c.lastInteractTime = Time.time;
                if (c.Ready && Interact(c)) c.Consume();
            }
        }

        protected abstract bool Interact(Carrier c);

        protected static void Shake(Transform t, float strength = 0.08f)
        {
            if (t == null) return;
            Vector3 baseScale = t.localScale;
            Tween.Punch(t, baseScale, strength, 0.25f);
        }
    }
}

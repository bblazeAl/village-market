using UnityEngine;

namespace IdleArcade
{
    public class ZoneMarker : MonoBehaviour
    {
        public Station station;
        public Transform zoneOverride;
        public float radiusOverride;
        public Renderer rend;
        public Color idle = new Color(1f, 1f, 1f, 0.38f);
        public Color active = new Color(1f, 0.92f, 0.45f, 0.95f);
        MaterialPropertyBlock mpb;
        float k;
        Vector3 baseScale;

        void Awake()
        {
            mpb = new MaterialPropertyBlock();
            baseScale = transform.localScale;
        }

        void Update()
        {
            Transform z = zoneOverride != null ? zoneOverride : station != null ? station.zone : null;
            float r = radiusOverride > 0f ? radiusOverride : station != null ? station.radius : 1f;
            bool inside = false;
            foreach (var c in Carrier.All)
                if (c.IsPlayer && Station.InZone(c.Position, z, r)) { inside = true; break; }
            k = Mathf.MoveTowards(k, inside ? 1f : 0f, Time.deltaTime * 6f);
            float pulse = inside ? 1f + Mathf.Sin(Time.time * 8f) * 0.025f : 1f;
            transform.localScale = baseScale * (1f + k * 0.06f) * pulse;
            if (rend != null)
            {
                rend.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", Color.Lerp(idle, active, k));
                rend.SetPropertyBlock(mpb);
            }
        }
    }
}

using UnityEngine;

namespace IdleArcade
{
    /// <summary>Bouncing 3D arrow over the current goal plus a dotted ground path from the player.</summary>
    public class Guide : MonoBehaviour
    {
        public static Guide I { get; private set; }

        public Transform arrow;
        public LineRenderer line;
        public float arrowHeight = 2.4f;

        public bool HasTarget { get; private set; }
        public Vector3 TargetPosition { get; private set; }
        public Sprite Icon { get; private set; }
        public bool ShowArrow { get; private set; }
        bool showLine;
        Vector3 arrowBaseScale;
        float lineAlpha, arrowK;
        Material lineMat;

        void Awake()
        {
            I = this;
            arrowBaseScale = arrow.localScale;
            arrow.gameObject.SetActive(false);
            lineMat = line.material;
            line.positionCount = 2;
        }

        void OnDestroy() { if (I == this) I = null; }

        public void Point(Vector3 pos, Sprite icon, bool withArrow, bool withLine)
        {
            bool moved = !HasTarget || (pos - TargetPosition).sqrMagnitude > 0.25f;
            HasTarget = true;
            TargetPosition = pos;
            Icon = icon;
            ShowArrow = withArrow;
            showLine = withLine;
            if (moved && withArrow) arrowK = 0f;
        }

        public void Clear()
        {
            HasTarget = false;
            ShowArrow = false;
            showLine = false;
        }

        void Update()
        {
            bool arrowOn = HasTarget && ShowArrow;
            if (arrowOn != arrow.gameObject.activeSelf) arrow.gameObject.SetActive(arrowOn);
            if (arrowOn)
            {
                arrowK = Mathf.Min(1f, arrowK + Time.deltaTime * 3f);
                float s = Easing.Evaluate(Ease.OutBack, arrowK);
                float bob = Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.45f;
                arrow.position = TargetPosition + Vector3.up * (arrowHeight + bob);
                // face the camera so the arrow reads as a bold shape from the tilted top-down view
                var camT = CameraRig.I != null ? CameraRig.I.transform : null;
                Quaternion face = camT != null ? Quaternion.Euler(camT.eulerAngles.x * 0.75f, camT.eulerAngles.y, 0f) : Quaternion.identity;
                arrow.rotation = face * Quaternion.Euler(0f, Mathf.Sin(Time.time * 2.2f) * 14f, 0f);
                float squash = 1f + (0.45f - bob) * 0.15f;
                arrow.localScale = new Vector3(arrowBaseScale.x * squash, arrowBaseScale.y / squash, arrowBaseScale.z * squash) * s;
            }

            var p = PlayerController.I;
            bool lineOn = HasTarget && showLine && p != null;
            Vector3 a = Vector3.zero, b = Vector3.zero;
            float dist = 0f;
            if (lineOn)
            {
                a = p.transform.position;
                b = TargetPosition;
                a.y = b.y = 0.06f;
                dist = Vector3.Distance(a, b);
                if (dist < 2.2f) lineOn = false;
            }
            lineAlpha = Mathf.MoveTowards(lineAlpha, lineOn ? 1f : 0f, Time.deltaTime * 4f);
            line.enabled = lineAlpha > 0.01f;
            if (line.enabled)
            {
                if (lineOn)
                {
                    Vector3 dir = (b - a).normalized;
                    line.SetPosition(0, a + dir * 0.7f);
                    line.SetPosition(1, b - dir * 1.0f);
                    lineMat.mainTextureScale = new Vector2(Mathf.Max(1f, dist / 0.6f), 1f);
                }
                lineMat.mainTextureOffset = new Vector2(-Time.time * 1.6f, 0f);
                var c = new Color(1f, 1f, 1f, 0.85f * lineAlpha);
                line.startColor = c;
                line.endColor = c;
            }
        }
    }
}

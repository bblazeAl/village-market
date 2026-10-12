using UnityEngine;

namespace IdleArcade
{
    /// <summary>
    /// Tilted follow camera. Keeps a fixed visible width around the player so portrait phones and
    /// landscape monitors both frame the action; can glide to a point of interest and come back.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I { get; private set; }
        public static Camera Cam { get; private set; }

        public Transform target;
        public float pitch = 52f;
        public float yaw = 0f;
        public float distance = 24f;
        public float visibleWidthPortrait = 12.5f;
        public float minVerticalFov = 31f;
        public float maxVerticalFov = 52f;
        public float followSharpness = 7f;
        public float lookAhead = 0.35f;
        public Vector3 targetOffset = new Vector3(0f, 0.8f, 0f);
        public float zoomOutPerLevel = 0.06f;
        [Tooltip("Portrait screens look further ahead (north) so the player sits below centre, like in mobile arcade games")]
        public float portraitLookAhead = 3.2f;

        Camera cam;
        Vector3 focusPoint;
        Vector3 shakeOffset;
        float shakeTime, shakeDur, shakeStrength;
        Vector3 poi;
        float poiHold, poiBlend;
        float zoom = 1f;

        void Awake()
        {
            I = this;
            cam = GetComponent<Camera>();
            Cam = cam;
            // phones already get 4x MSAA from the mobile pipeline asset; SMAA on top costs a full-screen pass
            if (Application.isMobilePlatform && TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var camData))
                camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
            Feedback.ShakeHandler = Shake;
            Feedback.FocusHandler = FocusOn;
        }

        void OnDestroy()
        {
            if (I == this) { I = null; Cam = null; }
        }

        void Start()
        {
            if (target != null) focusPoint = target.position;
            Apply(1f);
        }

        public void Shake(float strength, float dur)
        {
            shakeStrength = Mathf.Max(shakeStrength * (shakeTime > 0 ? 1f : 0f), strength);
            shakeDur = dur;
            shakeTime = dur;
        }

        public void FocusOn(Vector3 p, float hold)
        {
            poi = p;
            poiHold = hold;
        }

        public bool IsOnScreen(Vector3 world, float margin = 0.05f)
        {
            if (cam == null) return true;
            var v = cam.WorldToViewportPoint(world);
            return v.z > 0 && v.x > margin && v.x < 1 - margin && v.y > margin && v.y < 1 - margin;
        }

        void UpdateFov()
        {
            float aspect = Mathf.Max(0.1f, cam.aspect);
            float width = visibleWidthPortrait * zoom;
            // horizontal fov needed to see 'width' metres at the focus distance
            float hfov = 2f * Mathf.Atan(width * 0.5f / distance);
            float vfov = 2f * Mathf.Atan(Mathf.Tan(hfov * 0.5f) / aspect) * Mathf.Rad2Deg;
            cam.fieldOfView = Mathf.Clamp(vfov, minVerticalFov * zoom, maxVerticalFov);
        }

        void LateUpdate()
        {
            if (target == null) return;
            int lv = SaveSystem.Data != null ? SaveSystem.Data.marketLevel : 1;
            zoom = Mathf.Lerp(zoom, 1f + zoomOutPerLevel * (lv - 1), Time.unscaledDeltaTime * 2f);
            UpdateFov();

            Vector3 desired = target.position;
            float portrait = Mathf.Clamp01((1f - cam.aspect) / 0.45f);
            desired += Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * portraitLookAhead * portrait;
            var pc = target.GetComponent<PlayerController>();
            if (pc != null) desired += pc.Velocity * lookAhead;

            if (poiHold > 0f)
            {
                poiBlend = Mathf.MoveTowards(poiBlend, 1f, Time.unscaledDeltaTime * 2.2f);
                if (poiBlend >= 1f) poiHold -= Time.unscaledDeltaTime;
            }
            else poiBlend = Mathf.MoveTowards(poiBlend, 0f, Time.unscaledDeltaTime * 2.2f);
            float eb = Easing.Evaluate(Ease.InOutSine, poiBlend);
            desired = Vector3.Lerp(desired, poi, eb);

            focusPoint = Vector3.Lerp(focusPoint, desired, 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime));
            Apply(0f);
        }

        void Apply(float snap)
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pos = focusPoint + targetOffset - rot * Vector3.forward * distance;
            if (shakeTime > 0f)
            {
                shakeTime -= Time.unscaledDeltaTime;
                float k = shakeTime / Mathf.Max(0.01f, shakeDur);
                shakeOffset = Random.insideUnitSphere * shakeStrength * k;
            }
            else shakeOffset = Vector3.zero;
            transform.SetPositionAndRotation(pos + shakeOffset, rot);
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            focusPoint = target.position;
            Apply(1f);
        }
    }
}

using UnityEngine;

namespace IdleArcade
{
    /// <summary>Top-down character movement relative to the camera, with footsteps and start-dust.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController I { get; private set; }

        public Carrier carrier;
        public ItemStack stack;
        public CharacterAnim anim;
        public float turnSpeed = 16f;
        public float acceleration = 30f;

        CharacterController cc;
        Vector3 velocity;
        float stepDistance;
        bool wasMoving;
        float verticalVel;

        public bool IsMoving { get; private set; }
        public Vector3 Velocity => velocity;
        public float IdleTime { get; private set; }

        void Awake()
        {
            I = this;
            cc = GetComponent<CharacterController>();
        }

        void OnDestroy() { if (I == this) I = null; }

        public void Teleport(Vector3 pos, float yaw)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            cc.enabled = true;
            velocity = Vector3.zero;
        }

        void Update()
        {
            stack.capacity = Economy.PlayerCapacity;
            Vector2 input = InputReader.Move;
            var cam = CameraRig.I != null ? CameraRig.I.transform : Camera.main != null ? Camera.main.transform : null;
            Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized : Vector3.forward;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 wish = (right * input.x + fwd * input.y);
            float mag = Mathf.Clamp01(wish.magnitude);
            if (mag > 0.01f) wish /= Mathf.Max(wish.magnitude, 0.0001f);
            // analog stick: small deflection = walk, full = run
            float targetSpeed = Economy.PlayerSpeed * Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(0.15f, 0.8f, mag));
            Vector3 targetVel = mag > 0.05f ? wish * targetSpeed : Vector3.zero;
            velocity = Vector3.MoveTowards(velocity, targetVel, acceleration * Time.deltaTime);

            if (cc.isGrounded) verticalVel = -2f; else verticalVel -= 20f * Time.deltaTime;
            cc.Move((velocity + Vector3.up * verticalVel) * Time.deltaTime);

            IsMoving = velocity.sqrMagnitude > 0.05f;
            if (mag > 0.05f)
            {
                var look = Quaternion.LookRotation(wish, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }

            if (IsMoving)
            {
                IdleTime = 0f;
                stepDistance += velocity.magnitude * Time.deltaTime;
                if (stepDistance > 1.05f)
                {
                    stepDistance = 0f;
                    AudioManager.Play(Sfx.Step, 0.22f);
                }
                if (!wasMoving) Fx.Play(GameConfig.I.fxStep, transform.position, 0.5f, 1.5f);
            }
            else IdleTime += Time.deltaTime;
            wasMoving = IsMoving;
        }
    }
}

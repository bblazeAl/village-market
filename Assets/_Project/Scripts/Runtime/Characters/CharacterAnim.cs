using UnityEngine;

namespace IdleArcade
{
    /// <summary>Feeds the shared humanoid animator: locomotion speed, carry pose layer, sitting and cheering.</summary>
    public class CharacterAnim : MonoBehaviour
    {
        public Animator animator;
        public ItemStack stack;
        public float runSpeed = 4.6f;
        public Transform bobRoot;
        [System.NonSerialized] public bool holdOverride;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int SitId = Animator.StringToHash("Sit");
        static readonly int CheerId = Animator.StringToHash("Cheer");
        static readonly int MoveRateId = Animator.StringToHash("MoveRate");
        static readonly int MoodId = Animator.StringToHash("Mood");
        static readonly int EmoteId = Animator.StringToHash("Emote");
        static readonly int EmoteGoId = Animator.StringToHash("EmoteGo");
        static readonly int EatingId = Animator.StringToHash("Eating");

        public const int EmoteHappyFirst = 1, EmoteHappyLast = 5, EmoteDisappointed = 6, EmoteShrug = 7, EmoteThanks = 8;
        [System.NonSerialized] public float mood;   // 0 normal, 1 impatient, 2 relaxed
        float moodShown;

        Vector3 lastPos;
        float speed, carry;
        int carryLayer = -1;

        public float CurrentSpeed => speed;

        void Start()
        {
            lastPos = transform.position;
            if (animator != null && animator.runtimeAnimatorController != null)
                carryLayer = animator.GetLayerIndex("Carry");
        }

        public void Bind(Animator a)
        {
            animator = a;
            carryLayer = animator != null ? animator.GetLayerIndex("Carry") : -1;
        }

        public bool Sitting
        {
            set { if (animator) animator.SetBool(SitId, value); }
        }

        /// <summary>Happy reaction: thumbs up, arms raised, fist pump, clapping or two thumbs.</summary>
        public void Cheer() => Emote(Random.Range(EmoteHappyFirst, EmoteHappyLast + 1));

        public void Emote(int id)
        {
            if (!animator) return;
            animator.SetInteger(EmoteId, id);
            animator.SetTrigger(EmoteGoId);
        }

        public bool Eating
        {
            set { if (animator) animator.SetBool(EatingId, value); }
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 d = transform.position - lastPos;
            d.y = 0f;
            lastPos = transform.position;
            float v = d.magnitude / dt;
            speed = Mathf.Lerp(speed, v, 1f - Mathf.Exp(-14f * dt));
            if (animator == null) return;
            float n = Mathf.Clamp01(speed / runSpeed);
            animator.SetFloat(SpeedId, n < 0.03f ? 0f : n);
            moodShown = Mathf.MoveTowards(moodShown, mood, dt * 3f);
            animator.SetFloat(MoodId, moodShown);
            // play walk/run clips a bit faster or slower to match ground speed and reduce foot sliding
            animator.SetFloat(MoveRateId, Mathf.Lerp(0.9f, 1.25f, Mathf.InverseLerp(0.4f, 1.2f, speed / runSpeed)));
            if (carryLayer >= 0)
            {
                float target = holdOverride || (stack != null && stack.Count > 0) ? 1f : 0f;
                carry = Mathf.MoveTowards(carry, target, dt * 6f);
                animator.SetLayerWeight(carryLayer, carry);
            }
        }
    }
}

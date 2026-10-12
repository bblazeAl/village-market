using UnityEngine;

namespace IdleArcade
{
    /// <summary>Background villager at a neighbouring stall: idles in a stance and now and then reacts.</summary>
    public class AmbientNpc : MonoBehaviour
    {
        public Animator animator;
        public float mood;          // 0 idle, 2 arms folded
        float next;

        static readonly int MoodId = Animator.StringToHash("Mood");
        static readonly int EmoteId = Animator.StringToHash("Emote");
        static readonly int EmoteGoId = Animator.StringToHash("EmoteGo");

        void Start()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            next = Time.time + Random.Range(3f, 9f);
        }

        void Update()
        {
            if (animator == null) return;
            animator.SetFloat(MoodId, mood);
            if (Time.time < next) return;
            next = Time.time + Random.Range(7f, 15f);
            animator.SetInteger(EmoteId, Random.value < 0.6f ? CharacterAnim.EmoteThanks : CharacterAnim.EmoteHappyFirst + 3);
            animator.SetTrigger(EmoteGoId);
        }
    }
}

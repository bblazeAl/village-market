using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace IdleArcade
{
    /// <summary>Spawns hired helpers, either walking in from the road (new hire) or already at work (loaded save).</summary>
    public class HelperManager : MonoBehaviour
    {
        public static HelperManager I { get; private set; }
        public GameObject helperBase;
        public Transform entryPoint;
        readonly List<Helper> helpers = new List<Helper>();
        public int Count => helpers.Count;
        public IReadOnlyList<Helper> Helpers => helpers;

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        public Helper Hire(HelperRole role, Vector3 nearPost, bool instant)
        {
            Vector3 pos = instant ? nearPost : entryPoint.position;
            if (NavMesh.SamplePosition(pos, out var hit, 3f, NavMesh.AllAreas)) pos = hit.position;
            var go = Instantiate(helperBase, pos, Quaternion.identity, transform);
            go.name = "Helper_" + role;
            var h = go.GetComponent<Helper>();
            h.role = role;
            var models = GameConfig.I.helperModels;
            int mi = (int)role % Mathf.Max(1, models.Length);
            var model = Instantiate(models[mi], go.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var animator = model.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = GameConfig.I.animator;
            animator.applyRootMotion = false;
            h.anim.Bind(animator);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null && GameConfig.I.helperHat != null)
            {
                var hat = Instantiate(GameConfig.I.helperHat, head);
                hat.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                hat.transform.localRotation = Quaternion.identity;
            }
            h.agent.Warp(pos);
            helpers.Add(h);
            if (!instant)
            {
                Tween.PopIn(go.transform, Vector3.one, 0.4f);
                Fx.Sparkles(pos + Vector3.up, 0.8f);
            }
            return h;
        }

        public void ClearAll()
        {
            foreach (var h in helpers) if (h) Destroy(h.gameObject);
            helpers.Clear();
        }
    }
}

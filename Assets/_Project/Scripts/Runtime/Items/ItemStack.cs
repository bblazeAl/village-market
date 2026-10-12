using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleArcade
{
    /// <summary>
    /// A wobbly vertical stack of items held by a character. Items lerp into place every frame,
    /// so adding/removing from the middle re-settles smoothly; the column sways against movement.
    /// </summary>
    public class ItemStack : MonoBehaviour
    {
        public Transform anchor;
        public int capacity = 4;
        public float swayAmount = 0.06f;
        public float swayStiffness = 90f;
        public float swayDamping = 9f;
        public float settleSpeed = 22f;

        readonly List<ItemVisual> items = new List<ItemVisual>(24);
        readonly HashSet<ItemVisual> inFlight = new HashSet<ItemVisual>();
        Vector3 sway, swayVel, lastAnchorPos, smoothedVel;

        public event Action Changed;
        public IReadOnlyList<ItemVisual> Items => items;
        public int Count => items.Count;
        public bool IsFull => items.Count >= capacity;
        public bool IsEmpty => items.Count == 0;
        public ItemType TopType => items.Count > 0 ? items[items.Count - 1].type : null;
        public float Height { get; private set; }

        void Awake()
        {
            if (anchor == null) anchor = transform;
            lastAnchorPos = anchor.position;
        }

        public int CountOf(ItemType t)
        {
            int n = 0;
            for (int i = 0; i < items.Count; i++) if (items[i].type == t) n++;
            return n;
        }

        public bool Has(ItemType t) => CountOf(t) > 0;

        public bool HasAnyExcept(ItemType t)
        {
            for (int i = 0; i < items.Count; i++) if (items[i].type != t) return true;
            return false;
        }

        float HeightBelow(int index)
        {
            float h = 0f;
            for (int i = 0; i < index && i < items.Count; i++) h += items[i].type.stackHeight;
            return h;
        }

        Vector3 SlotLocal(int index)
        {
            float h = HeightBelow(index);
            float f = Mathf.Pow(Mathf.Clamp01(h / 2.2f), 1.35f);
            return new Vector3(sway.x * f * 2.2f, h, sway.z * f * 2.2f);
        }

        public Vector3 TopWorld => anchor.TransformPoint(new Vector3(0f, HeightBelow(items.Count), 0f));

        /// <summary>Adds an existing item (already in the world), flying it into the stack.</summary>
        public void Add(ItemVisual it, float flyTime = 0.26f, float arcHeight = 0.9f)
        {
            items.Add(it);
            it.transform.SetParent(null, true);
            inFlight.Add(it);
            var tr = it.transform;
            Tween.KillOwner(tr);
            Tween.Arc(tr, () => anchor.TransformPoint(SlotLocal(Mathf.Max(0, items.IndexOf(it)))), arcHeight, flyTime, () =>
            {
                if (!tr) return;
                inFlight.Remove(it);
                tr.SetParent(anchor, true);
                int idx = items.IndexOf(it);
                if (idx >= 0) tr.localPosition = SlotLocal(idx);
                tr.localScale = it.type.prefab.transform.localScale;
            }, Ease.InOutSine, () => anchor.rotation * Quaternion.Euler(0f, it.yaw, 0f));
            Changed?.Invoke();
        }

        /// <summary>Removes the top-most item of the given type (or the top item if type is null).</summary>
        public ItemVisual Remove(ItemType type = null)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (type != null && items[i].type != type) continue;
                var it = items[i];
                items.RemoveAt(i);
                inFlight.Remove(it);
                Tween.KillOwner(it.transform);
                it.transform.SetParent(null, true);
                Changed?.Invoke();
                return it;
            }
            return null;
        }

        public void ClearAll()
        {
            foreach (var it in items) if (it) it.Release();
            items.Clear();
            inFlight.Clear();
            Changed?.Invoke();
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 vel = (anchor.position - lastAnchorPos) / dt;
            lastAnchorPos = anchor.position;
            smoothedVel = Vector3.Lerp(smoothedVel, vel, 1f - Mathf.Exp(-12f * dt));
            Vector3 localVel = anchor.InverseTransformDirection(smoothedVel);
            Vector3 target = new Vector3(-localVel.x, 0f, -localVel.z) * swayAmount;
            Vector3 force = (target - sway) * swayStiffness - swayVel * swayDamping;
            swayVel += force * dt;
            sway += swayVel * dt;
            sway = Vector3.ClampMagnitude(sway, 0.35f);

            float h = 0f;
            float k = 1f - Mathf.Exp(-settleSpeed * dt);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (inFlight.Contains(it) || it == null) { if (it) h += it.type.stackHeight; continue; }
                var tr = it.transform;
                float f = Mathf.Pow(Mathf.Clamp01(h / 2.2f), 1.35f);
                Vector3 p = new Vector3(sway.x * f * 2.2f, h, sway.z * f * 2.2f);
                tr.localPosition = Vector3.Lerp(tr.localPosition, p, k);
                Quaternion tilt = Quaternion.Euler(sway.z * f * 40f, it.yaw, -sway.x * f * 40f);
                tr.localRotation = Quaternion.Slerp(tr.localRotation, tilt, k);
                h += it.type.stackHeight;
            }
            Height = h;
        }
    }
}

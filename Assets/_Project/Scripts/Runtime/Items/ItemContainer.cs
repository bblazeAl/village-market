using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleArcade
{
    /// <summary>Items laid out on a grid (shelf, oven tray, table). Layers stack upward when a layer is full.</summary>
    public class ItemContainer : MonoBehaviour
    {
        public ItemType accepts;          // null = anything
        public int max = 12;
        public int cols = 3, rows = 2;
        public float spacingX = 0.32f, spacingZ = 0.3f;
        public float layerHeight = 0.2f;
        public bool randomYaw = true;

        readonly List<ItemVisual> items = new List<ItemVisual>(24);
        public event Action Changed;
        public int Count => items.Count;
        public bool IsFull => items.Count >= max;
        public bool IsEmpty => items.Count == 0;
        public int Free => Mathf.Max(0, max - items.Count);
        public IReadOnlyList<ItemVisual> Items => items;

        public bool CanAccept(ItemType t) => !IsFull && (accepts == null || accepts == t);

        public Vector3 SlotLocal(int index)
        {
            int perLayer = Mathf.Max(1, cols * rows);
            int layer = index / perLayer;
            int within = index % perLayer;
            int c = within % cols, r = within / cols;
            float x = (c - (cols - 1) * 0.5f) * spacingX;
            float z = (r - (rows - 1) * 0.5f) * spacingZ;
            float lh = layerHeight > 0f ? layerHeight : (accepts != null ? accepts.stackHeight : 0.2f);
            return new Vector3(x, layer * lh, z);
        }

        public Vector3 SlotWorld(int index) => transform.TransformPoint(SlotLocal(index));
        public Vector3 NextSlotWorld => SlotWorld(items.Count);

        public void Put(ItemVisual it, float flyTime = 0.24f, float arc = 0.8f, Action landed = null)
        {
            int idx = items.Count;
            items.Add(it);
            var tr = it.transform;
            tr.SetParent(null, true);
            Tween.KillOwner(tr);
            float yaw = randomYaw ? it.yaw : 0f;
            Quaternion rot = transform.rotation * Quaternion.Euler(0f, yaw, 0f);
            Tween.Arc(tr, () => SlotWorld(Mathf.Max(0, items.IndexOf(it))), arc, flyTime, () =>
            {
                if (!tr) return;
                tr.SetParent(transform, true);
                int i = items.IndexOf(it);
                if (i >= 0) { tr.localPosition = SlotLocal(i); tr.localRotation = Quaternion.Euler(0f, yaw, 0f); }
                tr.localScale = it.type.prefab.transform.localScale;
                landed?.Invoke();
            }, Ease.InOutSine, () => rot);
            Changed?.Invoke();
        }

        /// <summary>Places an item instantly (used when restoring or producing in place).</summary>
        public void PutInstant(ItemVisual it, bool pop = true)
        {
            int idx = items.Count;
            items.Add(it);
            var tr = it.transform;
            tr.SetParent(transform, false);
            tr.localPosition = SlotLocal(idx);
            tr.localRotation = Quaternion.Euler(0f, randomYaw ? it.yaw : 0f, 0f);
            Vector3 s = it.type.prefab.transform.localScale;
            if (pop) Tween.PopIn(tr, s, 0.3f); else tr.localScale = s;
            Changed?.Invoke();
        }

        public ItemVisual TakeTop()
        {
            if (items.Count == 0) return null;
            var it = items[items.Count - 1];
            items.RemoveAt(items.Count - 1);
            Tween.KillOwner(it.transform);
            it.transform.SetParent(null, true);
            Changed?.Invoke();
            return it;
        }

        public void ClearAll()
        {
            foreach (var it in items) if (it) it.Release();
            items.Clear();
            Changed?.Invoke();
        }
    }
}

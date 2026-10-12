using System;
using UnityEngine;

namespace IdleArcade
{
    /// <summary>Marks a pooled item instance.</summary>
    public class ItemVisual : MonoBehaviour
    {
        public ItemType type;
        [NonSerialized] public float yaw;

        public static ItemVisual Spawn(ItemType type, Vector3 pos, Transform parent = null)
        {
            var it = Pool.Get<ItemVisual>(type.prefab, pos, Quaternion.identity, parent);
            it.type = type;
            it.yaw = UnityEngine.Random.Range(-25f, 25f);
            return it;
        }

        public void Release() => Pool.Release(gameObject);
    }
}

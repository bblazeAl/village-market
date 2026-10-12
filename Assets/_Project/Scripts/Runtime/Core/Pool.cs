using System.Collections.Generic;
using UnityEngine;

namespace IdleArcade
{
    /// <summary>Prefab-keyed GameObject pool. Pooled objects are parked inactive under one root.</summary>
    public static class Pool
    {
        static readonly Dictionary<GameObject, Stack<GameObject>> free = new Dictionary<GameObject, Stack<GameObject>>();
        static readonly Dictionary<GameObject, GameObject> origin = new Dictionary<GameObject, GameObject>();
        static Transform root;

        static Transform Root
        {
            get
            {
                if (root == null)
                {
                    var go = new GameObject("~Pool");
                    go.SetActive(false);
                    root = go.transform;
                }
                return root;
            }
        }

        public static GameObject Get(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
        {
            GameObject go = null;
            if (free.TryGetValue(prefab, out var stack))
            {
                while (stack.Count > 0 && go == null) go = stack.Pop();
            }
            if (go == null)
            {
                go = Object.Instantiate(prefab, pos, rot, parent);
                origin[go] = prefab;
            }
            else
            {
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(pos, rot);
                go.transform.localScale = prefab.transform.localScale;
            }
            go.SetActive(true);
            return go;
        }

        public static T Get<T>(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null) where T : Component
            => Get(prefab, pos, rot, parent).GetComponent<T>();

        public static void Release(GameObject go)
        {
            if (go == null) return;
            Tween.KillOwner(go.transform);
            if (!origin.TryGetValue(go, out var prefab)) { Object.Destroy(go); return; }
            go.SetActive(false);
            go.transform.SetParent(Root, false);
            if (!free.TryGetValue(prefab, out var stack)) free[prefab] = stack = new Stack<GameObject>();
            stack.Push(go);
        }

        public static void Clear()
        {
            free.Clear();
            origin.Clear();
            root = null;
        }
    }
}

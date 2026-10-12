using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IdleArcade.EditorTools
{
    /// <summary>Finds Synty prefabs by file name and places/normalises them.</summary>
    public static class SyntyLib
    {
        const string Root = "Assets/_Project/Art/Synty";
        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        public static GameObject Find(string name)
        {
            if (cache.TryGetValue(name, out var go) && go != null) return go;
            foreach (var g in AssetDatabase.FindAssets(name + " t:Prefab", new[] { Root }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == name)
                {
                    go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    cache[name] = go;
                    return go;
                }
            }
            Debug.LogWarning("[SyntyLib] prefab not found: " + name);
            return null;
        }

        public static Texture2D Texture(string fileName)
        {
            foreach (var g in AssetDatabase.FindAssets(System.IO.Path.GetFileNameWithoutExtension(fileName) + " t:Texture2D", new[] { Root }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (System.IO.Path.GetFileName(p) == fileName) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            }
            Debug.LogWarning("[SyntyLib] texture not found: " + fileName);
            return null;
        }

        /// <summary>Instantiates a Synty prefab as a prefab instance (keeps the link) under parent.</summary>
        public static GameObject Place(string name, Transform parent, Vector3 pos, float yaw = 0f, float scale = 1f, bool colliders = true)
        {
            var pf = Find(name);
            if (pf == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            if (!colliders) foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return go;
        }

        public static Bounds RenderBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        /// <summary>Wraps a model so that its bounds' bottom-centre sits at the wrapper origin, scaled to a target size.</summary>
        public static GameObject Normalised(string name, float targetMaxSize, string wrapperName, bool keepColliders = false)
        {
            var pf = Find(name);
            var wrapper = new GameObject(wrapperName);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(pf, wrapper.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            if (!keepColliders) foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var b = RenderBounds(model);
            float max = Mathf.Max(b.size.x, b.size.y, b.size.z);
            float s = max > 0.0001f ? targetMaxSize / max : 1f;
            model.transform.localScale = Vector3.one * s;
            b = RenderBounds(model);
            model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            return wrapper;
        }
    }
}

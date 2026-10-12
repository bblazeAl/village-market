using UnityEngine;

namespace IdleArcade
{
    [CreateAssetMenu(menuName = "IdleArcade/Item Type")]
    public class ItemType : ScriptableObject
    {
        public ItemKind kind;
        public string nameKey;
        public GameObject prefab;          // pooled visual (normalized size, pivot at bottom centre)
        public Sprite icon;
        public int price = 4;
        public float stackHeight = 0.24f;  // vertical step when stacked
        public float footprint = 0.3f;     // grid spacing on shelves
        public bool sellable = true;
    }
}

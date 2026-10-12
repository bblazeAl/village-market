using UnityEngine;

namespace IdleArcade
{
    public enum PanelKind { Upgrades, Helpers }

    /// <summary>Standing on this mat opens a UI panel; stepping off closes it.</summary>
    public class PanelZone : MonoBehaviour
    {
        public PanelKind panel;
        public Transform zone;
        public float radius = 1.0f;
        public Transform body;
        float inside;
        bool open;

        void Update()
        {
            bool playerIn = false;
            foreach (var c in Carrier.All)
                if (c.IsPlayer && Station.InZone(c.Position, zone, radius)) { playerIn = true; break; }

            if (playerIn) inside += Time.deltaTime; else inside = 0f;

            if (!open && inside > 0.3f && PlayerController.I != null && !PlayerController.I.IsMoving)
            {
                open = true;
                UIPanels.Open(panel);
                if (body) Tween.Punch(body, body.localScale, 0.06f, 0.25f);
            }
            else if (open && !playerIn)
            {
                open = false;
                UIPanels.Close(panel);
            }
        }
    }
}

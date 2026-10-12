using UnityEngine;

namespace IdleArcade
{
    /// <summary>
    /// Compost bin: drop unwanted items here when your hands are full of the wrong thing.
    /// The player must stop on it for a moment so walking past never throws goods away.
    /// </summary>
    public class TrashBin : Station
    {
        public Transform mouth;
        public Transform body;
        public float holdTime = 0.35f;
        float stillTime;

        protected override void Update()
        {
            var p = PlayerController.I;
            if (p != null && InZone(p.transform.position) && !p.IsMoving) stillTime += Time.deltaTime;
            else stillTime = 0f;
            base.Update();
        }

        protected override bool Interact(Carrier c)
        {
            if (c.stack.IsEmpty) return false;
            if (c.IsPlayer && stillTime < holdTime) return false;
            var it = c.stack.Remove();
            var tr = it.transform;
            Tween.Arc(tr, () => mouth.position, 0.8f, 0.25f, () =>
            {
                it.Release();
                if (body) Shake(body, 0.05f);
            }, Ease.InOutSine, null, 1f, 0.4f);
            if (c.IsPlayer) AudioManager.PlayCombo(Sfx.Place, 0.5f);
            GameEvents.ItemDropped(it.type, c, this);
            return true;
        }
    }
}

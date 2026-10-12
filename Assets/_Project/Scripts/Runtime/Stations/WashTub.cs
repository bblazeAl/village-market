using UnityEngine;

namespace IdleArcade
{
    /// <summary>Plates dropped here are washed away.</summary>
    public class WashTub : Station
    {
        public Transform mouth;
        public Transform body;

        protected override bool Interact(Carrier c)
        {
            var plate = GameConfig.I.plate;
            if (!c.stack.Has(plate)) return false;
            var it = c.stack.Remove(plate);
            var tr = it.transform;
            Tween.Arc(tr, () => mouth.position, 0.7f, 0.25f, () =>
            {
                Fx.Splash(mouth.position, 0.5f);
                it.Release();
                if (body) Shake(body, 0.04f);
            }, Ease.InOutSine, null, 1f, 0.5f);
            if (c.IsPlayer) AudioManager.PlayCombo(Sfx.Plate, 0.6f);
            SaveSystem.Data.platesWashed++;
            GameEvents.ItemDropped(plate, c, this);
            return true;
        }
    }
}

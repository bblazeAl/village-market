using UnityEngine;

namespace IdleArcade
{
    /// <summary>Finds a free, clean seat among the built picnic tables.</summary>
    public static class TableManager
    {
        public static bool TryFindSeat(Customer c, out PicnicTable table, out int seat)
        {
            foreach (var s in Station.All)
            {
                if (s is PicnicTable t && t.isActiveAndEnabled && t.TryReserve(c, out seat))
                {
                    table = t;
                    return true;
                }
            }
            table = null;
            seat = -1;
            return false;
        }

        public static int TotalDirty()
        {
            int n = 0;
            foreach (var s in Station.All) if (s is PicnicTable t && t.isActiveAndEnabled) n += t.DirtyCount;
            return n;
        }

        public static PicnicTable DirtiestTable(Vector3 near)
        {
            PicnicTable best = null;
            float bestScore = float.MaxValue;
            foreach (var s in Station.All)
            {
                if (!(s is PicnicTable t) || !t.isActiveAndEnabled || t.DirtyCount == 0) continue;
                float score = (t.ZonePos - near).sqrMagnitude - t.DirtyCount * 10f;
                if (score < bestScore) { bestScore = score; best = t; }
            }
            return best;
        }
    }
}

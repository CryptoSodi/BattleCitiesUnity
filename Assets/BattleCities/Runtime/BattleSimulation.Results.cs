using System;
using System.Linq;

namespace BattleCities.Core
{
    // Presentation totals are excluded from replay state hashes and never alter combat.
    public sealed class BattleResultStats
    {
        public bool Participated;
        public int Tier0, Tier1, Tier2, Tier3, Points, Bonus;
        public int Kills => Tier0 + Tier1 + Tier2 + Tier3;
        public int ForTier(int tier) => tier == 0 ? Tier0 : tier == 1 ? Tier1 : tier == 2 ? Tier2 : Tier3;
        public BattleResultStats Copy() => (BattleResultStats)MemberwiseClone();
    }

    public sealed partial class BattleSimulation
    {
        public readonly BattleResultStats[] ResultStats = Enumerable.Range(0, MaxPlayers).Select(_ => new BattleResultStats()).ToArray();

        void RecordResultKill(TankState victim, int ownerSlot)
        {
            if (!IsMultiplayer) ownerSlot = 0;
            if (ownerSlot < 0 || ownerSlot >= MaxPlayers || (victim.Player && (!IsPvp || victim.Slot == ownerSlot))) return;
            var result = ResultStats[ownerSlot];
            result.Participated = true;
            switch (victim.Tier) { case 0: result.Tier0++; break; case 1: result.Tier1++; break; case 2: result.Tier2++; break; default: result.Tier3++; break; }
            // Existing combat awards points only for AI enemies; PvP ranks by winner/kills.
            if (!victim.Player) result.Points += (victim.Tier + 1) * 100;
        }

        void RecordResultBonus(int points)
        {
            Score += points;
            int slot = IsMultiplayer ? actingSlot : 0;
            if (slot < 0 || slot >= MaxPlayers) return;
            var result = ResultStats[slot];
            result.Participated = true; result.Bonus += points; result.Points += points;
        }
    }
}

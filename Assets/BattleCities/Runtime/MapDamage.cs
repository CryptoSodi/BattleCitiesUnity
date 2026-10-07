using System;

namespace BattleCities.Core
{
    // Optional authored override. Null retains the campaign's original rules.
    [Serializable]
    public sealed class MapDamage
    {
        public int hitPoints = 1;
        public bool invulnerable;
        public bool normalShots = true;
        public bool powerShots = true;
        public MapDamage Copy() => (MapDamage)MemberwiseClone();
    }

    public sealed partial class BattleSimulation
    {
        private void ApplyAuthoredDamage(Wall wall, ShotState shot)
        {
            var rule = wall.Damage;
            if (!wall.Alive || rule == null || rule.invulnerable ||
                (shot.PowerShot ? !rule.powerShots : !rule.normalShots)) return;
            wall.Health = Math.Max(0, wall.Health - Math.Max(1, shot.Damage));
            if (wall.Health > 0) { WallDamaged?.Invoke(wall); return; }
            wall.Alive = false;
            WallDestroyed?.Invoke(wall);
        }
    }
}

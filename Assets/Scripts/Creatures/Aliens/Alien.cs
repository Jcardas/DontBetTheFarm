using UnityEngine;

// The team's agreed attack order, first to last.
public enum TargetTier { Animal, Farmer, Pen, Field, OtherBuilding, Farmhouse }

public class Alien : Creature
{
    public static TargetTier TierOf(IDamageable target) => target switch
    {
        FarmAnimal => TargetTier.Animal,
        Farmer => TargetTier.Farmer,
        AnimalPen => TargetTier.Pen,
        WheatField => TargetTier.Field,
        // The farmhouse maps to TargetTier.Farmhouse here once its class exists.
        _ => TargetTier.OtherBuilding,
    };

    // Lowest tier near the guard post first, nearest within a tier. With nothing in range it marches on the farm.
    public override IDamageable FindTarget(Vector3 guardPost)
    {
        IDamageable best = null, nearestBuilding = null;
        TargetTier bestTier = default;
        float bestGap = float.MaxValue, buildingGap = float.MaxValue;

        void Consider(IDamageable target)
        {
            if (target.Body == null || target.Down) return;
            float gap = Reach.Gap(transform.position, target.Body);
            if (target is Buildable && gap < buildingGap)
            {
                nearestBuilding = target;
                buildingGap = gap;
            }

            if (Reach.Gap(guardPost, target.Body) > GuardRadius) return;
            TargetTier tier = TierOf(target);
            if (best != null && (tier > bestTier || tier == bestTier && gap >= bestGap)) return;
            best = target;
            bestTier = tier;
            bestGap = gap;
        }

        if (CreatureManager.Instance != null)
            foreach (Creature enemy in CreatureManager.Instance.GetEnemies(Team)) Consider(enemy);
        if (BuildingManager.Instance != null)
            foreach (Buildable building in BuildingManager.Instance.Buildings)
                if (building != null) Consider(building);

        if (best != null || nearestBuilding == null) return best;

        // ponytail: the nearest building stands in for the farmhouse; target the farmhouse instead once it exists.
        // The post moves with the march, so whatever the alien passes on the way counts as in range.
        CreatureBrain.SetGuardPost(transform.position);
        return nearestBuilding;
    }

    protected override void Reset()
    {
        base.Reset();
        // Use global constants for farm animal stats
        maxHealth = GameConstants.Instance.BaseAlien.MaxHealth;
        moveSpeed = GameConstants.Instance.BaseAlien.Speed;
        attackSpeed = GameConstants.Instance.BaseAlien.AttackSpeed;
        team = GameConstants.Instance.BaseAlien.Team;
    }
    
    protected override void HandleZeroHealth()
    {
        // Aliens die when their health reaches zero.
        Die();
    }

    protected virtual void Die()
    {
        
    }
}

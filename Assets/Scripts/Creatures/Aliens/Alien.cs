using UnityEngine;
using System.Collections;

// The team's agreed attack order, first to last.
public enum TargetTier { Animal, Farmer, Pen, Field, OtherBuilding, Farmhouse }

public class Alien : Creature
{
    public static TargetTier TierOf(IDamageable target) => target switch
    {
        Animal => TargetTier.Animal,
        Farmer => TargetTier.Farmer,
        AnimalPen => TargetTier.Pen,
        WheatField => TargetTier.Field,
        Barn => TargetTier.Farmhouse, // the farmhouse is the research barn
        _ => TargetTier.OtherBuilding,
    };

    // Lowest tier near the guard post first, nearest within a tier. With nothing in range it marches on the farmhouse,
    // then on the farmer once the farmhouse is knocked down and he has nowhere left to hide, then on whatever is left.
    public override IDamageable FindTarget(Vector3 guardPost)
    {
        IDamageable best = null, nearestBuilding = null, farmer = null, farmhouse = null;
        TargetTier bestTier = default;
        float bestGap = float.MaxValue, buildingGap = float.MaxValue;
        bool barnDown = false;

        void Consider(IDamageable target)
        {
            if (target is Barn && target.Down) barnDown = true;
            if (target.Body == null || target.Down) return;
            if (target is Farmer) farmer = target;
            if (target is Barn) farmhouse = target;
            float gap = Reach.Gap(transform.position, target.Body);
            if (target is Buildable && gap < buildingGap)
            {
                nearestBuilding = target;
                buildingGap = gap;
            }

            if (Reach.Gap(guardPost, target.Body) > CalculatedViewRadius) return;
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

        IDamageable march = farmhouse ?? (barnDown && farmer != null ? farmer : nearestBuilding);
        if (best != null || march == null) return best;

        // The post moves with the march, so whatever the alien passes on the way counts as in range.
        CreatureBrain.SetGuardPost(transform.position);
        return march;
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
        // Aliens die when their health reaches zero. The body stays, but the base stops it blocking anyone.
        base.HandleZeroHealth();
        Die();
    }

    protected virtual void Die()
    {
        
    }

    


}

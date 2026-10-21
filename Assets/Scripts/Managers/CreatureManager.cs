using System.Collections.Generic;
using UnityEngine;

public class CreatureManager : MonoBehaviour
{
    public static CreatureManager Instance { get; private set; }

    // Bucket by team to eliminate checking friendly units entirely
    private readonly Dictionary<GameConstants.Team, List<Creature>> creaturesByTeam = new();
    
    // Maintain a list of FarmAnimals and which Pens they are in, so that when a Pen is destroyed, the animals become
    // unassigned and must be reassigned to a new Pem before they can be healed, etc.
    // private readonly Dictionary<FarmAnimal, Pen> animalsInPens = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Ensure that the instance is set in Start() as well, in case Awake() is called before this script is initialized.
    private void Start()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void Register(Creature creature)
    {
        if (!creaturesByTeam.TryGetValue(creature.Team, out var list))
        {
            list = new List<Creature>();
            creaturesByTeam[creature.Team] = list;
        }

        if (!list.Contains(creature))
            list.Add(creature);
    }

    public void Unregister(Creature creature)
    {
        if (creaturesByTeam.TryGetValue(creature.Team, out var list))
        {
            list.Remove(creature);
        }
    }

    /// <summary>
    /// Finds the closest standing enemy to `fromPos` within `radius` of `guardPost`.
    /// </summary>
    public Creature GetNearestEnemy(GameConstants.Team myTeam, Vector3 fromPos, Vector3 guardPost, float radius)
    {
        Creature best = null;
        float bestDistance = float.MaxValue;

        // Iterate over opposing teams only
        foreach (var pair in creaturesByTeam)
        {
            // Skip friendly or neutral factions
            if (!GameConstants.IsHostile(myTeam, pair.Key)) continue;

            var enemies = pair.Value;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.Down || enemy.Body == null) continue;

                if (Reach.Gap(guardPost, enemy.Body) > radius) continue;

                float dist = Reach.Gap(fromPos, enemy.Body);
                if (dist < bestDistance)
                {
                    best = enemy;
                    bestDistance = dist;
                }
            }
        }

        return best;
    }
}
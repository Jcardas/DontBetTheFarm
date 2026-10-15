using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum Team { Farm, Alien }

// Anything that can be attacked: animals, the farmer, aliens, the barn.
// At 0 health it is knocked out, not destroyed (design: knocked-out animals can be healed or abducted).
// Needs a collider, on it or a child: attackers measure their reach to it.
public class Health : MonoBehaviour
{
    static readonly List<Health> all = new();
    public static IReadOnlyList<Health> All => all;

    public Team team;
    public int maxHealth = 50;

    public int Current { get; private set; }
    public bool Down => Current <= 0;
    public Collider Body { get; private set; }

    void Awake()
    {
        Current = maxHealth;
        Body = GetComponentInChildren<Collider>();
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    public void TakeDamage(int amount)
    {
        if (Down) return;
        Current = Mathf.Max(0, Current - amount);
        if (Down && TryGetComponent(out NavMeshAgent agent)) agent.enabled = false;
    }

    // The standing enemy of `team` closest to `from`, counting only enemies within `radius` of `post`. Null if none.
    // ponytail: straight-line distance and a scan of every unit; switch to path length or a spatial grid if waves get big.
    public static Health NearestEnemy(Team team, Vector3 from, Vector3 post, float radius)
    {
        Health best = null;
        float bestDistance = float.MaxValue;
        foreach (var h in all)
        {
            if (h.team == team || h.Down || Reach.Gap(post, h.Body) > radius) continue;
            float d = Reach.Gap(from, h.Body);
            if (d < bestDistance) { best = h; bestDistance = d; }
        }
        return best;
    }
}

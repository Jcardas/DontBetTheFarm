using UnityEngine;

// The player's saucer. It waits at its spawn point inside the barn until dawn, when it flies out, beams up every
// knocked-out alien, immediately deposits each one into research (Technology), and returns to its spawn point.
// In a scene with no clock it goes out a few seconds after the last alien goes down instead.
// Its lights and beam are blue so it never reads as an enemy.
public class PlayerSaucer : UFO
{
    [Tooltip("With no clock in the scene: seconds with no enemy left standing before it goes out to collect.")]
    [SerializeField] float calmDelay = 3f;
    [Tooltip("Research each knocked-out alien is worth.")]
    [SerializeField] int researchPerAlien = 1; // TODO: Calculate based on difficulty of alien

    Vector3 home;
    bool sweeping, returningHome;
    int lastDay;
    float calmSince = -1f;

    protected override void Awake()
    {
        base.Awake();
        home = transform.position - Vector3.up * hoverHeight;
        exitPoint = home;
    }

    protected override void OnIdle()
    {
        if (sweeping)
        {
            Creature next = NearestDownedAlien();
            if (next != null)
            {
                Claim(next);
            }
            else
            {
                sweeping = false;
                returningHome = true;
            }
        }
        else if (returningHome)
        {
            if (FlyTo(home)) returningHome = false;
        }
        else if (TimeToSweep())
        {
            sweeping = true;
        }
    }

    // Immediately deposits the beamed-up alien into research storage and destroys its model.
    protected override void OnAboard()
    {
        if (victim != null)
        {
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.Deposit(ResourceType.Technology, researchPerAlien);

            Release(victim);
            Destroy(victim.gameObject);
            victim = null;
        }
        state = State.Idle;
    }

    // Dawn (the clock's day count goes up at 6am) with knocked-out aliens to collect. With no clock in the scene,
    // a few seconds after the fight ends.
    bool TimeToSweep()
    {
        if (TimeManager.Instance == null) return CalmLongEnough();
        int day = TimeManager.Instance.getDay();
        if (day == lastDay) return false;
        lastDay = day;
        return NearestDownedAlien() != null;
    }

    // True once no enemy has been standing for calmDelay seconds and there is a knocked-out alien to collect.
    bool CalmLongEnough()
    {
        if (CreatureManager.Instance == null) return false;
        bool fighting = false, bodies = false;
        foreach (Creature c in CreatureManager.Instance.GetEnemies(GameConstants.Team.Farm))
        {
            if (!c.Down) fighting = true;
            else if (c is Alien && !IsClaimed(c)) bodies = true;
        }
        if (fighting || !bodies)
        {
            calmSince = -1f;
            return false;
        }
        if (calmSince < 0f) calmSince = Time.time;
        if (Time.time - calmSince < calmDelay) return false;
        calmSince = -1f;
        return true;
    }

    Creature NearestDownedAlien()
    {
        if (CreatureManager.Instance == null) return null;
        Creature best = null;
        foreach (Creature c in CreatureManager.Instance.GetEnemies(GameConstants.Team.Farm))
            if (c is Alien && c.Down && !IsClaimed(c) &&
                (best == null || Vector3.Distance(transform.position, c.Position) < Vector3.Distance(transform.position, best.Position)))
                best = c;
        return best;
    }
}

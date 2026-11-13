using System.Collections.Generic;
using UnityEngine;

// The player's saucer. It waits inside the barn until dawn, opens the doors and flies out the front, rises to
// cruising altitude, beams up every knocked-out alien, immediately deposits each one into research (Technology),
// climbs back to cruising altitude, flies to the waypoint above the barn entrance, descends, and enters the barn.
// In a scene with no clock it goes out a few seconds after the last alien goes down instead.
// Its lights and beam are blue so it never reads as an enemy.
public class PlayerSaucer : UFO
{
    enum Phase
    {
        Parked,
        OpeningToExit,
        Exiting,
        AscendingToCruise,
        Sweeping,
        ClimbingAfterSweep,
        CruisingToHangar,
        DescendingToThreshold,
        EnteringBay
    }

    [SerializeField] BarnHangar hangar;
    [Tooltip("With no clock in the scene: seconds with no enemy left standing before it goes out to collect.")]
    [SerializeField] float calmDelay = 3f;
    [Tooltip("Research each knocked-out alien is worth.")]
    [SerializeField] int researchPerAlien = 1; // TODO: Calculate based on difficulty of alien
    
    [Tooltip("If true, the player's saucer is broken and cannot fly out to collect aliens.")]
    [SerializeField] bool broken = false; 
    [Tooltip("Material to use when the saucer is broken.")]
    [SerializeField] Material brokenMat;

    readonly Dictionary<Renderer, Material[]> originalMaterials = new();
    Phase phase = Phase.Parked;
    Vector3 climbTarget;
    int lastDay;
    float calmSince = -1f;

    public bool IsBroken => broken;

    protected override void Awake()
    {
        base.Awake();
        if (hangar == null) hangar = GetComponentInParent<BarnHangar>();
        if (hangar != null)
        {
            exitPoint = hangar.BayPosition;
            transform.position = hangar.BayPosition;
        }

        // Cache original materials so repairing the barn can restore the saucer's look.
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
            originalMaterials[r] = r.sharedMaterials;

        if (broken) ApplyBrokenVisuals(true);
    }

    protected override void OnIdle()
    {
        if (hangar == null) return;

        switch (phase)
        {
            case Phase.Parked:
                if (!broken && TimeToSweep())
                {
                    hangar.OpenDoors();
                    phase = Phase.OpeningToExit;
                }
                break;

            case Phase.OpeningToExit:
                if (broken)
                {
                    hangar.CloseDoors();
                    phase = Phase.Parked;
                }
                else if (hangar.AreDoorsOpen())
                {
                    phase = Phase.Exiting;
                }
                break;

            case Phase.Exiting:
                if (FlyDirectTo(hangar.ThresholdPosition)) phase = Phase.AscendingToCruise;
                break;

            case Phase.AscendingToCruise:
                if (FlyDirectTo(hangar.CruisePosition)) phase = Phase.Sweeping;
                break;

            case Phase.Sweeping:
                Creature next = broken ? null : NearestDownedAlien();
                if (next != null)
                {
                    Claim(next);
                }
                else
                {
                    // Climb vertically to the cruise waypoint's Y before flying horizontally toward the barn.
                    climbTarget = new Vector3(transform.position.x, hangar.CruisePosition.y, transform.position.z);
                    phase = Phase.ClimbingAfterSweep;
                }
                break;

            case Phase.ClimbingAfterSweep:
                if (FlyDirectTo(climbTarget))
                {
                    hangar.OpenDoors();
                    phase = Phase.CruisingToHangar;
                }
                break;

            case Phase.CruisingToHangar:
                if (FlyDirectTo(hangar.CruisePosition)) phase = Phase.DescendingToThreshold;
                break;

            case Phase.DescendingToThreshold:
                if (FlyDirectTo(hangar.ThresholdPosition) && hangar.AreDoorsOpen()) phase = Phase.EnteringBay;
                break;

            case Phase.EnteringBay:
                if (FlyDirectTo(hangar.BayPosition))
                {
                    hangar.CloseDoors();
                    phase = Phase.Parked;
                }
                break;
        }
    }

    // Moves directly to a 3D waypoint without adding hoverHeight, keeping the saucer on an exact flight path.
    bool FlyDirectTo(Vector3 target)
    {
        transform.position = Vector3.MoveTowards(transform.position, target, flySpeed * Time.deltaTime);
        return (transform.position - target).sqrMagnitude < 0.01f;
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

    // Called when the barn is destroyed: swaps the saucer's visuals to brokenMat and grounds it in the bay.
    public void BreakSaucer()
    {
        broken = true;
        ApplyBrokenVisuals(true);

        // If mid-approach or mid-beam when broken, abort the pickup and return home.
        if (state is State.Approach or State.Beam)
        {
            if (beam) beam.SetActive(false);
            if (victim != null)
            {
                Release(victim);
                victim = null;
            }
            state = State.Idle;
        }
    }

    // Called when the barn is repaired: restores original materials and allows sweeping again.
    public void RepairSaucer()
    {
        broken = false;
        ApplyBrokenVisuals(false);
    }

    void ApplyBrokenVisuals(bool isBroken)
    {
        foreach (var kvp in originalMaterials)
        {
            Renderer r = kvp.Key;
            if (r == null) continue;

            if (isBroken && brokenMat != null)
            {
                Material[] mats = new Material[kvp.Value.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = brokenMat;
                r.sharedMaterials = mats;
            }
            else
            {
                r.sharedMaterials = kvp.Value;
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// The player's saucer. It waits inside the barn until dawn, opens the doors and flies out the front, rises to
// cruising altitude, flies quickly between knocked-out aliens at hover height, slows down while its tractor beam
// pulls multiple aliens aboard simultaneously, stores aliens with unresearched mutations in the barn (and converts
// duplicates directly into Technology), climbs back to cruising altitude, and enters the barn.
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
    [Tooltip("Research each knocked-out alien is worth when it has no new mutations to store in the barn.")]
    [SerializeField] int researchPerAlien = 1; // TODO: Calculate based on difficulty of alien

    [Header("Continuous Tractor Sweep")]
    [Tooltip("Horizontal radius around the saucer's ground projection within which downed aliens start getting pulled up.")]
    [SerializeField] float sweepPullRadius = 5f;
    [Tooltip("Speed at which the saucer drifts toward the next alien while actively abducting one or more aliens.")]
    [SerializeField] float abductingMoveSpeed = 2f;
    [Tooltip("Speed at which downed aliens are pulled upward toward the saucer.")]
    [SerializeField] float multiPullSpeed = 6f;
    [Tooltip("Distance to the saucer at which a pulled alien is collected aboard.")]
    [SerializeField] float collectDistance = 1.5f;

    [Header("Broken State")]
    [Tooltip("If true, the player's saucer is broken and cannot fly out to collect aliens.")]
    [SerializeField] bool broken = false;
    [Tooltip("Material to use when the saucer is broken.")]
    [SerializeField] Material brokenMat;

    readonly Dictionary<Renderer, Material[]> originalMaterials = new();
    readonly List<Creature> activePickups = new();
    Phase phase = Phase.Parked;
    Vector3 climbTarget;
    int lastDay;
    float calmSince = -1f;

    static readonly SlotType[] AllSlots = { SlotType.Head, SlotType.Back, SlotType.Limbs, SlotType.Tail };

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
                if (broken)
                {
                    AbortActivePickups();
                    climbTarget = new Vector3(transform.position.x, hangar.CruisePosition.y, transform.position.z);
                    phase = Phase.ClimbingAfterSweep;
                    break;
                }

                // 1. Grab any downed aliens within sweepPullRadius underneath the saucer simultaneously
                AcquireNearbyDownedAliens();

                // 2. Continuously pull all currently beamed aliens toward the saucer
                UpdateActivePickups();

                // 3. Move quickly between targets, drift slowly if another target remains, or stop completely on the final pickup
                Creature next = NearestDownedAlien();
                if (next != null)
                {
                    float currentSpeed = activePickups.Count > 0 ? abductingMoveSpeed : flySpeed;
                    Vector3 sweepPassTarget = new Vector3(
                        next.Position.x,
                        next.Position.y + hoverHeight,
                        next.Position.z
                    );
                    transform.position = Vector3.MoveTowards(transform.position, sweepPassTarget, currentSpeed * Time.deltaTime);
                }
                else if (activePickups.Count > 0)
                {
                    // Last alien(s) in the sweep: stay completely still until they are fully inside the saucer
                }
                else
                {
                    // All downed aliens are aboard; now ascend to cruise height and return home
                    if (beam) beam.SetActive(false);
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

    void AcquireNearbyDownedAliens()
    {
        if (CreatureManager.Instance == null) return;

        foreach (Creature c in CreatureManager.Instance.GetEnemies(GameConstants.Team.Farm))
        {
            if (c == null || !(c is Alien) || !c.Down) continue;
            if (activePickups.Contains(c) || IsClaimed(c)) continue;

            // Check horizontal XZ distance so hover/cruise height doesn't block pickup
            Vector2 saucerXZ = new Vector2(transform.position.x, transform.position.z);
            Vector2 alienXZ = new Vector2(c.Position.x, c.Position.z);

            if (Vector2.Distance(saucerXZ, alienXZ) <= sweepPullRadius)
            {
                Claim(c);
                // Keep UFO in State.Idle so OnIdle() keeps flying continuously instead of stopping in State.Approach/Beam
                state = State.Idle;
                victim = null;

                if (c.TryGetComponent<NavMeshAgent>(out var agent)) agent.enabled = false;
                if (c.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;

                activePickups.Add(c);
            }
        }
    }

    void UpdateActivePickups()
    {
        if (beam) beam.SetActive(activePickups.Count > 0);

        for (int i = activePickups.Count - 1; i >= 0; i--)
        {
            Creature alien = activePickups[i];
            if (alien == null)
            {
                activePickups.RemoveAt(i);
                continue;
            }

            // Pull the alien smoothly toward the moving saucer
            alien.transform.position = Vector3.MoveTowards(
                alien.transform.position,
                transform.position,
                multiPullSpeed * Time.deltaTime
            );

            if ((alien.transform.position - transform.position).sqrMagnitude <= collectDistance * collectDistance)
            {
                activePickups.RemoveAt(i);
                Release(alien);
                ProcessCollectedAlien(alien);
            }
        }
    }

    void ProcessCollectedAlien(Creature alien)
    {
        if (alien == null) return;

        if (HasNewMutationForPlayer(alien))
        {
            StoreAlienInBarn(alien);
        }
        else
        {
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.Deposit(ResourceType.Technology, researchPerAlien);

            if (CreatureManager.Instance != null)
                CreatureManager.Instance.Unregister(alien);

            Destroy(alien.gameObject);
        }
    }

    bool HasNewMutationForPlayer(Creature alien)
    {
        if (!alien.TryGetComponent<CreatureMutator>(out var mutator))
            return false;

        // Collect all mutations already waiting on aliens currently stored in the barn
        HashSet<MutationData> alreadyInBarn = new();
        if (CreatureManager.Instance != null)
        {
            foreach (Creature stored in CreatureManager.Instance.GetCapturedAliens())
            {
                if (stored == null || !stored.TryGetComponent<CreatureMutator>(out var storedMutator)) continue;

                bool foundStored = false;
                foreach (SlotType slot in AllSlots)
                {
                    MutationData m = storedMutator.GetEquippedMutation(slot);
                    if (m != null)
                    {
                        alreadyInBarn.Add(m);
                        foundStored = true;
                    }
                }

                if (!foundStored)
                {
                    foreach (MutationData m in storedMutator.GetConfiguredStartingMutations())
                    {
                        if (m != null) alreadyInBarn.Add(m);
                    }
                }
            }
        }

        // 1. Check live equipped mutations on the collected alien
        bool foundAnyEquipped = false;
        foreach (SlotType slot in AllSlots)
        {
            MutationData mutation = mutator.GetEquippedMutation(slot);
            if (mutation == null) continue;

            foundAnyEquipped = true;
            bool unlocked = MutationUnlockManager.Instance != null && MutationUnlockManager.Instance.IsUnlocked(mutation);
            if (!unlocked && !alreadyInBarn.Contains(mutation))
                return true;
        }

        // 2. Fallback if CreatureMutator.Start() hasn't populated equippedMutations yet
        if (!foundAnyEquipped)
        {
            foreach (MutationData mutation in mutator.GetConfiguredStartingMutations())
            {
                if (mutation == null) continue;
                bool unlocked = MutationUnlockManager.Instance != null && MutationUnlockManager.Instance.IsUnlocked(mutation);
                if (!unlocked && !alreadyInBarn.Contains(mutation))
                    return true;
            }
        }

        return false;
    }

    void StoreAlienInBarn(Creature alien)
    {
        if (hangar != null)
        {
            alien.transform.SetParent(hangar.transform, worldPositionStays: true);
            alien.transform.position = hangar.BayPosition;
        }

        if (alien.TryGetComponent<CreatureBrain>(out var brain)) brain.enabled = false;
        if (alien.TryGetComponent<NavMeshAgent>(out var agent)) agent.enabled = false;
        if (alien.TryGetComponent<Selectable>(out var selectable)) selectable.enabled = false;
        if (alien.TryGetComponent<ToyBob>(out var bob)) bob.enabled = false;

        foreach (var col in alien.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        foreach (var r in alien.GetComponentsInChildren<Renderer>(true))
            r.enabled = false;

        if (CreatureManager.Instance != null)
        {
            CreatureManager.Instance.Unregister(alien);
            CreatureManager.Instance.RegisterCapturedAlien(alien);
        }

        var barnUI = FindAnyObjectByType<BarnResearchUIController>();
        if (barnUI != null && barnUI.IsMenuOpen)
        {
            barnUI.OpenMenu();
        }
    }

    void AbortActivePickups()
    {
        if (beam) beam.SetActive(false);
        for (int i = activePickups.Count - 1; i >= 0; i--)
        {
            Creature c = activePickups[i];
            if (c != null) Release(c);
        }
        activePickups.Clear();
    }

    // Moves directly to a 3D waypoint without adding hoverHeight, keeping the saucer on an exact flight path.
    bool FlyDirectTo(Vector3 target)
    {
        transform.position = Vector3.MoveTowards(transform.position, target, flySpeed * Time.deltaTime);
        return (transform.position - target).sqrMagnitude < 0.01f;
    }

    // Fallback if base UFO OnAboard is ever triggered directly
    protected override void OnAboard()
    {
        if (victim != null)
        {
            Creature collected = victim;
            Release(collected);
            victim = null;
            ProcessCollectedAlien(collected);
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
            if (c is Alien && c.Down && !IsClaimed(c) && !activePickups.Contains(c) &&
                (best == null || Vector3.Distance(transform.position, c.Position) < Vector3.Distance(transform.position, best.Position)))
                best = c;
        return best;
    }

    // Called when the barn is destroyed: swaps the saucer's visuals to brokenMat and grounds it in the bay.
    public void BreakSaucer()
    {
        broken = true;
        ApplyBrokenVisuals(true);
        AbortActivePickups();

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
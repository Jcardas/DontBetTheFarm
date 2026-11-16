using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Creature))]
public class CreatureFlight : MonoBehaviour
{
    public enum FlightState
    {
        Grounded,
        TakingOff,
        Cruising,
        Swooping,
        Landing
    }

    public enum GroundCombatStyle
    {
        [Tooltip("Swoops down to strike and remains low in melee until the target dies or moves far away.")]
        StayLowInCombat,
        [Tooltip("Swoops down to strike, then climbs back up to cruise altitude while waiting for attack cooldown.")]
        SwoopAndClimbBetweenAttacks
    }

    [Header("Altitude & Movement")]
    [Tooltip("Target height above ground while cruising.")]
    [SerializeField] private float cruiseAltitude = 3.2f;
    [Tooltip("Vertical climb speed (m/s).")]
    [SerializeField] private float climbSpeed = 4.5f;
    [Tooltip("Vertical descent speed when landing or swooping (m/s).")]
    [SerializeField] private float descendSpeed = 5.0f;
    [Tooltip("Airborne horizontal speed multiplier over CalculatedMoveSpeed.")]
    [SerializeField] private float airSpeedMultiplier = 1.15f;
    [Tooltip("Turn rate in degrees/sec while airborne.")]
    [SerializeField] private float airTurnSpeed = 360f;
    [Tooltip("Subtle idle hover bob amplitude while airborne.")]
    [SerializeField] private float hoverBobAmplitude = 0.1f;
    [Tooltip("Subtle idle hover bob frequency (cycles/sec).")]
    [SerializeField] private float hoverBobFrequency = 1.6f;

    [Header("Takeoff & Landing")]
    [Tooltip("Minimum horizontal distance to destination before taking off instead of walking.")]
    [SerializeField] private float minTakeoffDistance = 2.2f;
    [Tooltip("Horizontal distance from destination at which the creature begins descending to land.")]
    [SerializeField] private float landingApproachDistance = 2.0f;
    [Tooltip("Forces NavMeshAgent.baseOffset to 0 so Unity doesn't hover grounded fliers above the NavMesh.")]
    [SerializeField] private bool forceZeroBaseOffset = true;
    [Tooltip("Layers considered solid ground for altitude raycasts.")]
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Ground Attack Swoop")]
    [SerializeField] private GroundCombatStyle groundCombatStyle = GroundCombatStyle.StayLowInCombat;
    [Tooltip("Height above ground when swooping down to attack a ground target.")]
    [SerializeField] private float swoopAttackHeight = 0.15f;
    [Tooltip("Horizontal distance from a ground target at which the flier begins its dive.")]
    [SerializeField] private float swoopTriggerDistance = 3.5f;

    private Creature creature;
    private NavMeshAgent agent;
    private float savedBaseOffset;

    private FlightState state = FlightState.Grounded;
    private Vector3 flightDestination;
    private Vector3 resolvedLandingPoint;
    private bool hasFlightDestination;
    private bool wantLandAtDestination;
    private float currentHeightAboveGround;
    private float hoverPhase;

    public FlightState State => state;
    public bool CanFly => creature != null && !creature.Down && creature.HasCapability(CreatureCapabilities.CanFly);
    public bool IsAirborne => state != FlightState.Grounded;
    public float CurrentAltitude => currentHeightAboveGround;
    public float CruiseAltitude => cruiseAltitude;
    public bool HasDestination => hasFlightDestination;
    public float CurrentHorizontalSpeed { get; private set; }

    private void Awake()
    {
        creature = GetComponent<Creature>();
        agent = GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            if (forceZeroBaseOffset)
                agent.baseOffset = 0f;
            savedBaseOffset = agent.baseOffset;
        }
    }

    private void OnDisable()
    {
        ForceGroundImmediate();
    }

    public bool FlyToAndLand(Vector3 worldDestination)
    {
        if (!CanFly) return false;

        float flatDist = FlatDistance(transform.position, worldDestination);
        if (state == FlightState.Grounded && flatDist < minTakeoffDistance)
            return false;

        flightDestination = ResolveNavMeshPoint(worldDestination);
        resolvedLandingPoint = flightDestination;
        hasFlightDestination = true;
        wantLandAtDestination = true;

        if (state == FlightState.Grounded || state == FlightState.Landing)
            BeginTakeoff();

        return true;
    }

    public bool FlyToHover(Vector3 worldDestination)
    {
        if (!CanFly) return false;

        flightDestination = worldDestination;
        hasFlightDestination = true;
        wantLandAtDestination = false;

        if (state == FlightState.Grounded || state == FlightState.Landing)
            BeginTakeoff();

        return true;
    }
    
    private static Vector3 NearestPointOnCollider(Collider col, Vector3 from)
    {
        if (col == null) return from;
        Vector3 pt = col is MeshCollider { convex: false }
            ? col.ClosestPointOnBounds(from)
            : col.ClosestPoint(from);
        pt.y = col.bounds.min.y;
        return pt;
    }

    public bool EngageTarget(IDamageable target, float attackRange, float attackCooldownRemaining)
    {
        if (!CanFly || target == null || target.Down) return false;

        bool targetIsAirborne = IsTargetAirborne(target);
        Vector3 targetPos = (!targetIsAirborne && target.Body != null)
            ? NearestPointOnCollider(target.Body, transform.position)
            : target.Position;
        float flatGap = targetIsAirborne
            ? FlatDistance(transform.position, targetPos)
            : (target.Body != null ? Reach.Gap(transform.position, target.Body) : FlatDistance(transform.position, targetPos));

        // 1. Air-to-Air Combat
        if (targetIsAirborne)
        {
            float desiredAlt = Mathf.Max(cruiseAltitude, SampleHeightAboveGround(targetPos));
            wantLandAtDestination = false;

            if (state == FlightState.Grounded || state == FlightState.Landing || state == FlightState.Swooping)
                BeginTakeoff();

            if (flatGap > attackRange * 0.9f)
            {
                flightDestination = targetPos;
                hasFlightDestination = true;
            }
            else
            {
                hasFlightDestination = false;
            }

            float groundY = SampleGroundY(transform.position);
            currentHeightAboveGround = Mathf.MoveTowards(
                currentHeightAboveGround,
                desiredAlt,
                climbSpeed * Time.deltaTime
            );
            ApplyVerticalPosition(groundY);
            return true;
        }

        // 2. Ranged Air-to-Ground Combat (stays at cruise altitude)
        if (creature.HasCapability(CreatureCapabilities.IsRangedAttacker))
        {
            wantLandAtDestination = false;
            if (state == FlightState.Grounded || state == FlightState.Landing)
                BeginTakeoff();

            if (flatGap > attackRange * 0.9f)
            {
                flightDestination = targetPos;
                hasFlightDestination = true;
            }
            else
            {
                hasFlightDestination = false;
            }
            return true;
        }

        // 3. Melee Air-to-Ground Combat
        bool shouldClimbBetweenSwings =
            groundCombatStyle == GroundCombatStyle.SwoopAndClimbBetweenAttacks &&
            attackCooldownRemaining > 0.35f;

        if (shouldClimbBetweenSwings)
        {
            wantLandAtDestination = false;
            hasFlightDestination = false;
            if (state != FlightState.TakingOff && state != FlightState.Cruising)
                BeginTakeoff();
            return true;
        }

        if (flatGap > swoopTriggerDistance)
        {
            wantLandAtDestination = false;
            flightDestination = targetPos;
            hasFlightDestination = true;

            if (state == FlightState.Grounded || state == FlightState.Landing)
                BeginTakeoff();
            return true;
        }

        wantLandAtDestination = false;
        if (flatGap > attackRange * 0.85f)
        {
            flightDestination = targetPos;
            hasFlightDestination = true;
        }
        else
        {
            hasFlightDestination = false;
        }

        if (state != FlightState.Grounded)
            state = FlightState.Swooping;

        return true;
    }

    public void StopAndLand()
    {
        if (state == FlightState.Grounded) return;

        resolvedLandingPoint = ResolveNavMeshPoint(transform.position);
        flightDestination = resolvedLandingPoint;
        hasFlightDestination = FlatDistance(transform.position, resolvedLandingPoint) > 0.15f;
        wantLandAtDestination = true;
        state = FlightState.Landing;
    }

    private void BeginTakeoff()
    {
        state = FlightState.TakingOff;
        if (agent != null && agent.enabled)
        {
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
        }
    }

    private void Update()
    {
        if (creature == null) return;

        // Enforce baseOffset = 0 even when grounded if configured
        if (agent != null && agent.enabled && forceZeroBaseOffset && !Mathf.Approximately(agent.baseOffset, 0f))
        {
            agent.baseOffset = 0f;
        }

        if (!CanFly && state != FlightState.Grounded)
        {
            StopAndLand();
        }

        if (state == FlightState.Grounded)
        {
            CurrentHorizontalSpeed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
            currentHeightAboveGround = 0f;
            return;
        }

        float groundY = state == FlightState.Landing ? resolvedLandingPoint.y : SampleGroundY(transform.position);
        float flySpeed = creature.CalculatedMoveSpeed * airSpeedMultiplier;

        // Horizontal movement
        if (hasFlightDestination)
        {
            Vector3 toDest = flightDestination - transform.position;
            toDest.y = 0f;
            float dist = toDest.magnitude;

            if (dist > 0.08f)
            {
                Vector3 dir = toDest / dist;
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, airTurnSpeed * Time.deltaTime);

                float step = Mathf.Min(dist, flySpeed * Time.deltaTime);
                transform.position += dir * step;
                CurrentHorizontalSpeed = step / Mathf.Max(Time.deltaTime, 0.0001f);
            }
            else
            {
                transform.position = new Vector3(flightDestination.x, transform.position.y, flightDestination.z);
                CurrentHorizontalSpeed = 0f;
                hasFlightDestination = false;

                if (wantLandAtDestination && state != FlightState.Landing)
                {
                    resolvedLandingPoint = ResolveNavMeshPoint(transform.position);
                    state = FlightState.Landing;
                }
            }

            // Smoothly start descending as we approach the landing point
            if (wantLandAtDestination && dist <= landingApproachDistance && (state == FlightState.Cruising || state == FlightState.TakingOff))
            {
                resolvedLandingPoint = ResolveNavMeshPoint(flightDestination);
                flightDestination = resolvedLandingPoint;
                state = FlightState.Landing;
            }
        }
        else
        {
            CurrentHorizontalSpeed = 0f;
            if (wantLandAtDestination && state != FlightState.Landing)
            {
                resolvedLandingPoint = ResolveNavMeshPoint(transform.position);
                state = FlightState.Landing;
            }
        }

        // Vertical altitude handling
        switch (state)
        {
            case FlightState.TakingOff:
                currentHeightAboveGround = Mathf.MoveTowards(
                    currentHeightAboveGround,
                    cruiseAltitude,
                    climbSpeed * Time.deltaTime
                );
                if (Mathf.Approximately(currentHeightAboveGround, cruiseAltitude))
                {
                    state = FlightState.Cruising;
                }
                break;

            case FlightState.Cruising:
                hoverPhase += Time.deltaTime * hoverBobFrequency * Mathf.PI * 2f;
                float bobOffset = Mathf.Sin(hoverPhase) * hoverBobAmplitude;
                currentHeightAboveGround = Mathf.MoveTowards(
                    currentHeightAboveGround,
                    cruiseAltitude + bobOffset,
                    climbSpeed * 0.5f * Time.deltaTime
                );
                break;

            case FlightState.Swooping:
                currentHeightAboveGround = Mathf.MoveTowards(
                    currentHeightAboveGround,
                    swoopAttackHeight,
                    descendSpeed * 1.25f * Time.deltaTime
                );
                break;

            case FlightState.Landing:
                // Synchronize vertical descent with remaining horizontal travel so touchdown happens smoothly at the target
                float remainingHoriz = hasFlightDestination ? FlatDistance(transform.position, resolvedLandingPoint) : 0f;
                float effectiveDescendSpeed = descendSpeed;
                if (remainingHoriz > 0.1f && flySpeed > 0.1f)
                {
                    float timeToReach = remainingHoriz / flySpeed;
                    effectiveDescendSpeed = Mathf.Max(descendSpeed, currentHeightAboveGround / Mathf.Max(timeToReach, 0.1f));
                }

                currentHeightAboveGround = Mathf.MoveTowards(
                    currentHeightAboveGround,
                    0f,
                    effectiveDescendSpeed * Time.deltaTime
                );

                if (currentHeightAboveGround <= 0.01f && !hasFlightDestination)
                {
                    CompleteLanding();
                    return;
                }
                break;
        }

        ApplyVerticalPosition(groundY);
    }

    private void ApplyVerticalPosition(float groundY)
    {
        Vector3 pos = transform.position;
        pos.y = groundY + currentHeightAboveGround;
        transform.position = pos;
    }

    private void CompleteLanding()
    {
        currentHeightAboveGround = 0f;
        state = FlightState.Grounded;
        hasFlightDestination = false;
        wantLandAtDestination = false;
        CurrentHorizontalSpeed = 0f;

        if (NavMesh.SamplePosition(resolvedLandingPoint, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            resolvedLandingPoint = hit.position;

        transform.position = resolvedLandingPoint;

        if (agent != null && !creature.Down)
        {
            agent.baseOffset = forceZeroBaseOffset ? 0f : savedBaseOffset;
            agent.enabled = true;
            agent.Warp(resolvedLandingPoint);
        }
    }

    public void ForceGroundImmediate()
    {
        if (state == FlightState.Grounded) return;
        resolvedLandingPoint = ResolveNavMeshPoint(transform.position);
        CompleteLanding();
    }

    private Vector3 ResolveNavMeshPoint(Vector3 desiredPos)
    {
        if (NavMesh.SamplePosition(desiredPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            return hit.position;

        float y = SampleGroundY(desiredPos);
        return new Vector3(desiredPos.x, y, desiredPos.z);
    }

    private float SampleGroundY(Vector3 worldPos)
    {
        Vector3 rayOrigin = new Vector3(worldPos.x, worldPos.y + 15f, worldPos.z);
        var hits = Physics.RaycastAll(rayOrigin, Vector3.down, 40f, groundMask, QueryTriggerInteraction.Ignore);
        float highestGround = float.NegativeInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i].collider;
            if (col.GetComponentInParent<Creature>() != null) continue;
            if (col.GetComponentInParent<Buildable>() != null) continue;
            if (col.GetComponentInParent<UFO>() != null) continue;

            if (hits[i].point.y > highestGround)
                highestGround = hits[i].point.y;
        }

        if (highestGround > float.NegativeInfinity)
            return highestGround;

        if (NavMesh.SamplePosition(new Vector3(worldPos.x, 0f, worldPos.z), out NavMeshHit navHit, 5f, NavMesh.AllAreas))
            return navHit.position.y;

        return 0f;
    }

    private float SampleHeightAboveGround(Vector3 worldPos)
    {
        return Mathf.Max(0f, worldPos.y - SampleGroundY(worldPos));
    }

    public static bool IsTargetAirborne(IDamageable target)
    {
        if (target == null) return false;
        if (target is UFO) return true;
        if (target is Creature c) return c.IsAirborne;
        return false;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
    }
}
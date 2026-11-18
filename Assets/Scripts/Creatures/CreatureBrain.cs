using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Creature))]
public class CreatureBrain : MonoBehaviour
{
    private Creature creature;
    private NavMeshAgent agent;
    private Gatherer gatherer;

    private IDamageable currentTarget;
    private IDamageable playerOrderedTarget;

    private Vector3 guardPost;
    private float nextThinkTime;
    private float nextAttackTime;
    private float obeyGraceEnds;
    private bool isObeying;
    private bool retreating;

    public bool IsPassive { get; set; }
    public bool Engaged => currentTarget != null && !currentTarget.Down;

    private bool CanTargetAirborne =>
        creature is Farmer ||
        creature is RangedAnimal ||
        creature.HasCapability(CreatureCapabilities.IsRangedAttacker) ||
        creature.HasCapability(CreatureCapabilities.CanFly);

    private void Awake()
    {
        creature = GetComponent<Creature>();
        agent = GetComponent<NavMeshAgent>();
        gatherer = GetComponentInChildren<Gatherer>();
    }

    private void Start() => guardPost = transform.position;

    public void RefreshGatherer(Gatherer updated = null)
    {
        gatherer = updated != null ? updated : GetComponentInChildren<Gatherer>();
    }

    private Gatherer EnsureGatherer()
    {
        if (gatherer == null)
            gatherer = GetComponentInChildren<Gatherer>();
        return gatherer;
    }

    public void ExecuteCommand(Order order)
    {
        if (creature == null || creature.Down) return;

        var activeGatherer = EnsureGatherer();

        if (order.kind == OrderKind.Attack && creature.CalculatedAttackDamage > 0 && CanAttackTarget(order.enemy))
        {
            if (activeGatherer != null) activeGatherer.AbortWork();
            Attack(order.enemy);
            return;
        }

        if (order.kind == OrderKind.Barn && creature is Farmer farmer && order.building is Barn barn)
        {
            Guard(barn.Door);
            barn.TakeIn(farmer);
            return;
        }

        if (order.kind == OrderKind.Pen && creature is Animal animal && order.building is AnimalPen pen)
        {
            if (pen.AddAnimal(animal))
            {
                Guard(order.point);
            }
            return;
        }

        if (order.kind == OrderKind.Revive && creature is Farmer && order.animalToRevive != null && order.animalToRevive.Down && activeGatherer != null)
        {
            ClearCombatAndObey();
            activeGatherer.Revive(order.animalToRevive);
            return;
        }

        if (activeGatherer != null)
        {
            if (order.kind == OrderKind.Work && activeGatherer.CanWork(order.node))
            {
                ClearCombatAndObey();
                activeGatherer.Work(order.node);
                return;
            }

            if (order.kind == OrderKind.Harvest && creature.HasCapability(CreatureCapabilities.CanHarvestCrops))
            {
                ClearCombatAndObey();
                activeGatherer.Harvest(order.field);
                return;
            }

            if (order.kind == OrderKind.Repair && activeGatherer.CanRepair)
            {
                ClearCombatAndObey();
                activeGatherer.Repair(order.building);
                return;
            }

            activeGatherer.AbortWork();
        }

        Guard(order.point);
    }

    // Ground melee units cannot hit airborne UFOs or airborne flying creatures;
    // Farmer (shotgun), ranged animals, and other flying creatures can.
    private bool CanAttackTarget(IDamageable target)
    {
        if (target == null || target.Down) return false;
        if (target is UFO ufo)
            return ufo.CanBeShotDown && CanTargetAirborne && GameConstants.IsHostile(creature.Team, GameConstants.Team.Alien);
        if (target is Creature targetCreature && targetCreature.IsAirborne)
            return CanTargetAirborne;
        return true;
    }

    // Whether ExecuteCommand would carry this order out rather than just walk to its point (the same checks).
    public bool CanObey(Order order)
    {
        var activeGatherer = EnsureGatherer();
        return order.kind switch
        {
            OrderKind.Attack => creature.CalculatedAttackDamage > 0 && CanAttackTarget(order.enemy),
            OrderKind.Work => activeGatherer != null && activeGatherer.CanWork(order.node),
            OrderKind.Harvest => activeGatherer != null && creature.HasCapability(CreatureCapabilities.CanHarvestCrops),
            OrderKind.Repair => activeGatherer != null && activeGatherer.CanRepair,
            OrderKind.Pen => creature is Animal,
            OrderKind.Barn => creature is Farmer,
            OrderKind.Revive => creature is Farmer && order.animalToRevive != null && order.animalToRevive.Down,
            _ => true,
        };
    }

    private void ClearCombatAndObey()
    {
        currentTarget = playerOrderedTarget = null;
        isObeying = false;
    }

    public void Guard(Vector3 point)
    {
        if (gatherer != null) gatherer.AbortWork();
        currentTarget = playerOrderedTarget = null;
        isObeying = true;
        obeyGraceEnds = Time.time + 0.5f;

        guardPost = NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, NavMesh.AllAreas) ? hit.position : point;

        if (creature.Flight != null && creature.Flight.FlyToAndLand(guardPost))
            return;

        if (agent.isOnNavMesh)
        {
            agent.updateRotation = true;
            agent.SetDestination(guardPost);
        }
    }

    public void Obey()
    {
        if (gatherer != null) gatherer.AbortWork();
        currentTarget = playerOrderedTarget = null;
        isObeying = true;
        obeyGraceEnds = Time.time + 0.5f;
    }

    public void Attack(IDamageable enemy)
    {
        if (gatherer != null) gatherer.AbortWork();
        isObeying = false;
        currentTarget = playerOrderedTarget = enemy;
        nextThinkTime = 0f;
    }

    public void SetGuardPost(Vector3 point) => guardPost = point;

    private float DistanceToTarget(IDamageable target)
    {
        if (target == null) return float.MaxValue;
        if (target is UFO || (target is Creature c && c.IsAirborne))
        {
            Vector3 a = transform.position;
            Vector3 b = target.Position;
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        }
        return target.Body != null ? Reach.Gap(transform.position, target.Body) : Vector3.Distance(transform.position, target.Position);
    }

    private void MoveTowardTarget(IDamageable target)
    {
        if (target == null) return;

        if (creature.Flight != null && creature.Flight.CanFly)
        {
            float cooldownRemaining = Mathf.Max(0f, nextAttackTime - Time.time);
            creature.Flight.EngageTarget(target, creature.CalculatedAttackRange, cooldownRemaining);
            return;
        }

        if (!agent.isOnNavMesh) return;

        agent.updateRotation = true;
        if (target is UFO)
        {
            Vector3 groundUnderUfo = new Vector3(target.Position.x, transform.position.y, target.Position.z);
            if (NavMesh.SamplePosition(groundUnderUfo, out NavMeshHit navHit, 8f, NavMesh.AllAreas))
                agent.SetDestination(navHit.position);
            else
                agent.SetDestination(groundUnderUfo);
            return;
        }

        if (target.Body != null)
            Reach.WalkTo(agent, target.Body);
    }

    private IDamageable FindTargetWithUFOs()
    {
        IDamageable best = creature.FindTarget(guardPost);

        if (!CanTargetAirborne || UFO.Active.Count == 0 || !GameConstants.IsHostile(creature.Team, GameConstants.Team.Alien))
            return best;

        float maxRange = Mathf.Max(creature.CalculatedViewRadius, creature.CalculatedAttackRange);
        float bestDist = best != null ? DistanceToTarget(best) : maxRange;

        foreach (UFO ufo in UFO.Active)
        {
            if (ufo == null || !ufo.CanBeShotDown || ufo.Down) continue;
            float dist = DistanceToTarget(ufo);
            if (dist > maxRange || dist >= bestDist) continue;
            bestDist = dist;
            best = ufo;
        }

        return best;
    }

    private void Update()
    {
        if (creature == null || creature.Down || IsPassive) return;

        if (isObeying)
        {
            if (creature.Flight != null && creature.Flight.IsAirborne)
            {
                if (Time.time >= obeyGraceEnds && !creature.Flight.HasDestination && creature.Flight.State == CreatureFlight.FlightState.Grounded)
                    isObeying = false;
                return;
            }

            bool near = !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 1f);
            bool stalled = !agent.pathPending && agent.hasPath && agent.remainingDistance <= 3f && agent.velocity.sqrMagnitude < 0.04f;
            if (Time.time < obeyGraceEnds || !(near || stalled)) return;
            if (stalled) agent.ResetPath();
            isObeying = false;
        }

        if (Time.time >= nextThinkTime)
        {
            nextThinkTime = Time.time + 0.25f;

            if (playerOrderedTarget != null && (playerOrderedTarget.Down || playerOrderedTarget.Body == null || !CanAttackTarget(playerOrderedTarget)))
                playerOrderedTarget = null;

            if (!Engaged)
                guardPost = transform.position;

            bool wasFighting = currentTarget != null;
            currentTarget = playerOrderedTarget != null ? playerOrderedTarget : FindTargetWithUFOs();

            if (currentTarget == null)
            {
                if (wasFighting)
                {
                    if (creature.Flight != null && creature.Flight.IsAirborne)
                        creature.Flight.StopAndLand();
                    else if (agent.isOnNavMesh && agent.hasPath)
                        agent.ResetPath();
                }
                return;
            }

            retreating = false;
            float gap = DistanceToTarget(currentTarget);
            if (gap > creature.CalculatedAttackRange)
            {
                MoveTowardTarget(currentTarget);
            }
            else if (gap < creature.KeepAwayRange && currentTarget is not UFO)
            {
                BackAwayFrom(currentTarget, gap);
            }
        }

        if (currentTarget == null || currentTarget.Down || currentTarget.Body == null || !CanAttackTarget(currentTarget)) return;
        if (DistanceToTarget(currentTarget) > creature.CalculatedAttackRange) return;

        // Ensure melee fliers finish swooping down to ground level before striking a ground target
        if (creature.Flight != null && creature.Flight.CanFly)
        {
            float cooldownRemaining = Mathf.Max(0f, nextAttackTime - Time.time);
            creature.Flight.EngageTarget(currentTarget, creature.CalculatedAttackRange, cooldownRemaining);

            bool targetIsAirborne = CreatureFlight.IsTargetAirborne(currentTarget);
            if (!targetIsAirborne && !creature.HasCapability(CreatureCapabilities.IsRangedAttacker) && creature.Flight.CurrentAltitude > 0.6f)
                return;
        }

        if (agent.enabled && agent.hasPath && !retreating) agent.ResetPath();
        // Backing away: face the way it walks instead of moonwalking. Otherwise turn to face the target.
        if (agent.enabled) agent.updateRotation = retreating;

        Vector3 toTarget = currentTarget.Position - transform.position;
        toTarget.y = 0;
        if (!retreating && toTarget.sqrMagnitude > 0.001f)
        {
            float turnSpeed = agent.enabled ? agent.angularSpeed : 360f;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(toTarget),
                turnSpeed * Time.deltaTime
            );
        }

        if (Time.time < nextAttackTime) return;
        nextAttackTime = Time.time + 1f / creature.CalculatedAttackSpeed;
        creature.PerformAttack(currentTarget);
    }

    private void BackAwayFrom(IDamageable enemy, float gap)
    {
        Vector3 away = transform.position - enemy.Position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.001f) away = -transform.forward;

        Vector3 to = transform.position + away.normalized * (creature.KeepAwayRange - gap + 1f);

        if (creature.Flight != null && creature.Flight.IsAirborne)
        {
            creature.Flight.FlyToHover(to);
            retreating = true;
            return;
        }

        if (!agent.isOnNavMesh || !NavMesh.SamplePosition(to, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return;

        agent.SetDestination(hit.position);
        retreating = true;
    }
}
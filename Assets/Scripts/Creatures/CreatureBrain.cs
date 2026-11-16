using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Creature))]
public class CreatureBrain : MonoBehaviour
{
    private Creature creature;
    private NavMeshAgent agent;
    private Gatherer gatherer;

    private IDamageable currentTarget;
    private Creature playerOrderedTarget;

    private Vector3 guardPost;
    private float nextThinkTime;
    private float nextAttackTime;
    private float obeyGraceEnds;
    private bool isObeying;
    private bool retreating;

    public bool IsPassive { get; set; }
    public bool Engaged => currentTarget != null && !currentTarget.Down;

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

        if (order.kind == OrderKind.Attack && creature.CalculatedAttackDamage > 0)
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

    // Whether ExecuteCommand would carry this order out rather than just walk to its point (the same checks).
    public bool CanObey(Order order)
    {
        var activeGatherer = EnsureGatherer();
        return order.kind switch
        {
            OrderKind.Attack => creature.CalculatedAttackDamage > 0,
            OrderKind.Work => activeGatherer != null && activeGatherer.CanWork(order.node),
            OrderKind.Harvest => activeGatherer != null && creature.HasCapability(CreatureCapabilities.CanHarvestCrops),
            OrderKind.Repair => activeGatherer != null && activeGatherer.CanRepair,
            OrderKind.Pen => creature is Animal,
            OrderKind.Barn => creature is Farmer,
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

    public void Attack(Creature enemy)
    {
        if (gatherer != null) gatherer.AbortWork();
        isObeying = false;
        currentTarget = playerOrderedTarget = enemy;
        nextThinkTime = 0f;
    }

    public void SetGuardPost(Vector3 point) => guardPost = point;

    private void Update()
    {
        if (creature == null || creature.Down || IsPassive) return;

        if (isObeying)
        {
            bool near = !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 1f);
            bool stalled = !agent.pathPending && agent.hasPath && agent.remainingDistance <= 3f && agent.velocity.sqrMagnitude < 0.04f;
            if (Time.time < obeyGraceEnds || !(near || stalled)) return;
            if (stalled) agent.ResetPath();
            isObeying = false;
        }

        if (Time.time >= nextThinkTime)
        {
            nextThinkTime = Time.time + 0.25f;

            if (playerOrderedTarget != null && playerOrderedTarget.Down)
                playerOrderedTarget = null;

            if (!Engaged)
                guardPost = transform.position;

            bool wasFighting = currentTarget != null;
            currentTarget = playerOrderedTarget != null ? playerOrderedTarget : creature.FindTarget(guardPost);

            if (currentTarget == null)
            {
                if (wasFighting && agent.isOnNavMesh && agent.hasPath) agent.ResetPath();
                return;
            }

            retreating = false;
            if (currentTarget.Body != null)
            {
                float gap = Reach.Gap(transform.position, currentTarget.Body);
                if (gap > creature.CalculatedAttackRange)
                {
                    agent.updateRotation = true;
                    Reach.WalkTo(agent, currentTarget.Body);
                }
                else if (gap < creature.KeepAwayRange)
                {
                    BackAwayFrom(currentTarget, gap);
                }
            }
        }

        if (currentTarget == null || currentTarget.Down || currentTarget.Body == null) return;
        if (Reach.Gap(transform.position, currentTarget.Body) > creature.CalculatedAttackRange) return;

        if (agent.hasPath && !retreating) agent.ResetPath();
        agent.updateRotation = false;

        Vector3 toTarget = currentTarget.Position - transform.position;
        toTarget.y = 0;
        if (toTarget.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(toTarget),
                agent.angularSpeed * Time.deltaTime
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
        if (!agent.isOnNavMesh || !NavMesh.SamplePosition(to, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return;

        agent.SetDestination(hit.position);
        retreating = true;
    }
}
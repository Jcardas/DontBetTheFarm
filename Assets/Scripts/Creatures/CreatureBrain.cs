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
        gatherer = GetComponent<Gatherer>();
    }

    private void Start() => guardPost = transform.position;

    public void ExecuteCommand(Order order)
    {
        if (creature == null || creature.Down) return;

        if (order.kind == OrderKind.Attack && creature.CalculatedAttackDamage > 0)
        {
            if (gatherer != null) gatherer.AbortWork();
            Attack(order.enemy);
            return;
        }

        if (gatherer != null)
        {
            if (order.kind == OrderKind.Work && gatherer.CanWork(order.node))
            {
                ClearCombatAndObey();
                gatherer.Work(order.node);
                return;
            }

            if (order.kind == OrderKind.Harvest && creature.HasCapability(CreatureCapabilities.CanHarvestCrops))
            {
                ClearCombatAndObey();
                gatherer.Harvest(order.field);
                return;
            }

            if (order.kind == OrderKind.Repair && creature.HasCapability(CreatureCapabilities.CanRepair))
            {
                ClearCombatAndObey();
                gatherer.Repair(order.building);
                return;
            }

            gatherer.AbortWork();
        }

        Guard(order.point);
    }

    // Whether ExecuteCommand would carry this order out rather than just walk to its point (the same checks).
    public bool CanObey(Order order) => order.kind switch
    {
        OrderKind.Attack => creature.CalculatedAttackDamage > 0,
        OrderKind.Work => gatherer != null && gatherer.CanWork(order.node),
        OrderKind.Harvest => gatherer != null && creature.HasCapability(CreatureCapabilities.CanHarvestCrops),
        OrderKind.Repair => gatherer != null && creature.HasCapability(CreatureCapabilities.CanRepair),
        _ => true,
    };

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
            bool arrived = !agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f);
            if (Time.time < obeyGraceEnds || !arrived) return;
            isObeying = false;
        }

        if (Time.time >= nextThinkTime)
        {
            nextThinkTime = Time.time + 0.25f;

            if (playerOrderedTarget != null && playerOrderedTarget.Down)
                playerOrderedTarget = null;

            if (!creature.Anchored && !Engaged)
                guardPost = transform.position;

            currentTarget = playerOrderedTarget != null ? playerOrderedTarget : creature.FindTarget(guardPost);

            if (currentTarget == null)
            {
                if (Vector3.Distance(transform.position, guardPost) > agent.stoppingDistance + 0.5f && agent.isOnNavMesh)
                {
                    agent.updateRotation = true;
                    agent.SetDestination(guardPost);
                }
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
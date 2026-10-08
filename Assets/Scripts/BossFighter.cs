using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Collider))]
public class BossFighter : MonoBehaviour
{
    [Header("Combat Stats")]
    public float attackRange = 1.5f; // How close the boss's edge needs to be to the target's edge
    public float attackCooldown = 1.5f;
    public int damage = 15;

    private NavMeshAgent agent;
    private Collider selfCollider;
    private float nextAttackTime;
    private ToyBob toyVisuals;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        selfCollider = GetComponent<Collider>();
        toyVisuals = GetComponentInChildren<ToyBob>();
    }

    // We will call this from AlienBossController instead of the standard Fighter
    public void EngageTarget(Health target)
    {
        if (target == null || target.Down) return;

        // 1. Walk towards the target
        agent.SetDestination(target.transform.position);

        // 2. Calculate true edge-to-edge distance, completely ignoring Reach.cs
        Vector3 myEdge = selfCollider.ClosestPoint(target.transform.position);
        Vector3 targetEdge = target.Body.ClosestPoint(transform.position);
        float trueDistance = Vector3.Distance(myEdge, targetEdge);

        // 3. Attack if close enough
        if (trueDistance <= attackRange)
        {
            // We are in range. Stop sliding into them and swing.
            agent.velocity = Vector3.zero;
            agent.isStopped = true;

            if (Time.time >= nextAttackTime)
            {
                target.TakeDamage(damage);
                nextAttackTime = Time.time + attackCooldown;

                if (toyVisuals != null) toyVisuals.Bump(); // Play the attack animation
            }
        }
        else
        {
            // Target is too far, keep walking
            agent.isStopped = false;
        }
    }
}
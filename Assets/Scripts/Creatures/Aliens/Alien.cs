using UnityEngine;
using System.Collections;

public class Alien : Creature
{
    private Vector3 despawnPoint;
    public bool isRetreating = false;

    protected override void Reset()
    {
        base.Reset();
        // Use global constants for farm animal stats
        maxHealth = GameConstants.Instance.BaseAlien.MaxHealth;
        moveSpeed = GameConstants.Instance.BaseAlien.Speed;
        attackSpeed = GameConstants.Instance.BaseAlien.AttackSpeed;
        team = GameConstants.Instance.BaseAlien.Team;
    }
    
    protected override void HandleZeroHealth()
    {
        // Aliens die when their health reaches zero.
        Die();
    }

    protected virtual void Die()
    {
        
    }

    public void SetDespawnPoint(Vector3 point)
    {
        despawnPoint = point;
    }

    public void Retreat()
    {
        if (Down || isRetreating) return;

        isRetreating = true;

        if(CreatureBrain != null)
        {
            CreatureBrain.enabled = false; // Disable the AI behavior
        }

        if (NavMeshAgent != null && NavMeshAgent.enabled)
        {
            NavMeshAgent.SetDestination(despawnPoint);
            StartCoroutine(RetreatRoutine());
        }
    }

    private IEnumerator RetreatRoutine()
    {
        while (NavMeshAgent != null && NavMeshAgent.enabled)
        {
            if (!NavMeshAgent.pathPending && NavMeshAgent.remainingDistance <= NavMeshAgent.stoppingDistance)
            {
                if (!NavMeshAgent.hasPath || NavMeshAgent.velocity.sqrMagnitude == 0f)
                {
                    BeamUp();
                    yield break;
                }
            }
            yield return null;
        }
    }

    private void BeamUp()
    {
        Destroy(gameObject);
    }
}

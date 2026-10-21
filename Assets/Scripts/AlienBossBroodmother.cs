using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(currentHealth), typeof(BossFighter))]
public class AlienBossBroodmother : MonoBehaviour
{
    [Header("Boss Settings")]
    public GameObject standardAlienPrefab;
    public int spawnCountOnDeath = 5;

    private currentHealth _selfCurrentHealth;
    private BossFighter bossFighter;
    private bool hasSpawnedMinions = false;

    private currentHealth currentPriorityTarget;

    void Awake()
    {
        _selfCurrentHealth = GetComponent<currentHealth>();
        bossFighter = GetComponent<BossFighter>();
    }

    void Update()
    {
        // 1. Check for death to trigger the split
        if (_selfCurrentHealth != null && _selfCurrentHealth.Down)
        {
            HandleDeathSplit();
            return;
        }

        // 2. If we don't have a specific priority target, or our target died, find the next one
        if (currentPriorityTarget == null || currentPriorityTarget.Down)
        {
            currentPriorityTarget = FindNextPriorityTarget();
        }

        // 3. Engage the target if one was found
        if (currentPriorityTarget != null)
        {
            bossFighter.EngageTarget(currentPriorityTarget);
        }
    }

    /// <summary>
    /// Scans Logan's global Health.All list to find the closest target matching the current priority tier.
    /// Priority: 1. WheatField -> 2. Barn -> 3. Player -> 4. Animal
    /// </summary>
    private currentHealth FindNextPriorityTarget()
    {
        currentHealth target;

        // Phase 1: Wheat Fields
        target = FindNearestInHealthPool(h => h.GetComponent<WheatField>() != null);
        if (target != null) return target;

        // Phase 2: Barn 
        target = FindNearestInHealthPool(h => h.CompareTag("Barn") || h.name.Contains("Barn"));
        if (target != null) return target;

        // Phase 3: Player 
        target = FindNearestInHealthPool(h => h.CompareTag("Player") || h.name.Contains("Farmer") || h.name.Contains("Player"));
        if (target != null) return target;

        // Phase 4: Animals 
        target = FindNearestInHealthPool(h => h.CompareTag("Animal") || h.name.Contains("Animal"));
        if (target != null) return target;

        // Phase 5: Fallback if everything is dead
        return null;
    }

    /// <summary>
    /// Helper method to filter the Health list for the closest matching valid target.
    /// </summary>
    private currentHealth FindNearestInHealthPool(System.Func<currentHealth, bool> condition)
    {
        currentHealth best = null;
        float bestDistance = float.MaxValue;
        Vector3 currentPos = transform.position;

        foreach (currentHealth h in currentHealth.All)
        {
            // Ignore dead targets or allies
            if (h.Down || h.team == _selfCurrentHealth.team) continue;

            if (condition(h))
            {
                // We use standard Vector3 distance here for target sorting 
                // to completely bypass Reach.cs issues on massive models.
                float d = Vector3.Distance(currentPos, h.transform.position);
                if (d < bestDistance)
                {
                    best = h;
                    bestDistance = d;
                }
            }
        }
        return best;
    }

    private void HandleDeathSplit()
    {
        if (hasSpawnedMinions) return;
        hasSpawnedMinions = true;

        if (standardAlienPrefab != null)
        {
            for (int i = 0; i < spawnCountOnDeath; i++)
            {
                // Scatter the new aliens slightly around the boss's death position
                Vector3 randomOffset = Random.insideUnitSphere * 2f;
                randomOffset.y = 0;
                Vector3 spawnPos = transform.position + randomOffset;

                // Ensure they spawn safely on the NavMesh
                if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    Instantiate(standardAlienPrefab, hit.position, Quaternion.identity);
                }
                else
                {
                    Instantiate(standardAlienPrefab, transform.position, Quaternion.identity);
                }
            }
        }

        Destroy(gameObject);
    }
}
using UnityEngine;
using UnityEngine.AI;

public class SpawnPlatform : MonoBehaviour
{
    public bool isActive = true;
    private Collider platformCollider;

    void Awake()
    {
        platformCollider = GetComponent<Collider>();
        if (platformCollider == null)
        {
            Debug.LogError("SpawnPlatform requires a Collider component.");
        }
    }

    public Vector3 GetRandomSpawnPosition()
    {
        Vector3 randomPoint = transform.position;

        if (platformCollider != null)
        {
            Bounds bounds = platformCollider.bounds;

            float randomX = Random.Range(bounds.min.x, bounds.max.x);
            float randomZ = Random.Range(bounds.min.z, bounds.max.z);

            randomPoint = new Vector3(randomX, bounds.max.y, randomZ);
        }

        NavMeshHit hit;

        if (NavMesh.SamplePosition(randomPoint, out hit, 1.0f, NavMesh.AllAreas))
        {
            return hit.position;
        }
        else
        {
            Debug.LogWarning("Failed to find a valid NavMesh position for spawning.");
        }
        return randomPoint; // Return the original point if no valid position is found
    }
}

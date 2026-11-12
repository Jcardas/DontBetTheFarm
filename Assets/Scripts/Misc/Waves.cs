using UnityEngine;

[System.Serializable]
public class EnemySpawnWeight
{
    public GameObject enemyPrefab;
    public int weight;
}

[System.Serializable]
public class Wave
{
    // Wave Name
    public string waveName;

    public EnemySpawnWeight[] enemyPool;

    // Enemies Spawning Levers
    public float spawnDelay = 0.5f;         // Delay between spawning each enemy in a batch
    public int batchSize = 3;               // Number of enemies that spawn at a platform before the Mothership moves on to the next platform
    public float flightTime = 5f;           // Time it takes for the Mothership to move from one platform to the next
    public int maxOnScreenEnemies = 10;     // Maximum number of enemies allowed on screen at once

    public GameObject GetRandomEnemyPrefab()
    {
        if (enemyPool == null || enemyPool.Length == 0) return null;

        int totalWeight = 0;
        foreach (var entry in enemyPool)
        {
            totalWeight += entry.weight;
        }

        // Pick a random number between 0 and the total weight
        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (var entry in enemyPool)
        {
            currentWeight += entry.weight;
            if (randomValue < currentWeight)
            {
                return entry.enemyPrefab;
            }
        }

        return enemyPool[0].enemyPrefab;
    }
}
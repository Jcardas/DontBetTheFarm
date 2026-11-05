using UnityEngine;

public class Wave : MonoBehaviour
{
    // Wave Name
    public string waveName;

    // Enemy to Spawn
    public GameObject enemyPrefab;

    // Enemies Spawning Levers
    public float spawnDelay = 0.5f;         // Delay between spawning each enemy in a batch
    public int batchSize = 3;               // Number of enemies that spawn at a platform before the Mothership moves on to the next platform
    public float flightTime = 5f;           // Time it takes for the Mothership to move from one platform to the next
    public int maxOnScreenEnemies = 10;     // Maximum number of enemies allowed on screen at once

    // enemies defeated in this wave?
}
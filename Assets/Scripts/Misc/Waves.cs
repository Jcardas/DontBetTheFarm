using UnityEngine;

public class Wave : MonoBehaviour
{
    public string waveName;
    public GameObject enemyPrefab;
    public int enemyCount;
    public float spawnDelay = 0.5f; // Delay between spawning each enemy
}
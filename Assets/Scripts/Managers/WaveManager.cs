using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public Wave[] waves;
    public Transform[] spawnPoints;

    private List<Alien> activeAliens = new List<Alien>();

    public void SpawnWave(int currentDay)
    {
        if (waves.Length == 0)
        {
            Debug.LogWarning("No waves defined in WaveManager.");
            return;
        }

        activeAliens.Clear();

        int waveIndex = (currentDay - 1) % waves.Length;

        //if(waveIndex > waves.Length - 1)
        //{
        //    Debug.LogWarning("Max Wave Reached");
        //    // Play Final Cutscene?
        //}

        Wave currentWave = waves[waveIndex];
        StartCoroutine(SpawnWaveRoutine(currentWave));
    }

    private IEnumerator SpawnWaveRoutine(Wave wave)
    {
        for (int i = 0; i < wave.enemyCount; i++)
        {
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            Vector3 spawnPosition = spawnPoint.position + Vector3.up * 1.5f;

            GameObject alienObj = Instantiate(wave.enemyPrefab, spawnPosition, spawnPoint.rotation);

            Alien alien = alienObj.GetComponent<Alien>();
            if (alien != null)
            {
                alien.SetDespawnPoint(spawnPosition);
                activeAliens.Add(alien);
            }
            else
            {
                Debug.LogError("Spawned enemy does not have an Alien component.");
            }

            yield return new WaitForSeconds(wave.spawnDelay); // Adjust the spawn delay as needed
        }
    }

    public void RetreatWave()
    {
        foreach (Alien alien in activeAliens)
        {
            if(alien != null)
            {
                alien.Retreat();
            }
        }
        
    }

}

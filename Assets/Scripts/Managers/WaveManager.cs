using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{ 
    public Wave[] waves;
    public Transform[] spawnPoints;

    public void SpawnWave(int currentDay)
    {
        if(waves.Length == 0)
        {
            Debug.LogWarning("No waves defined in WaveManager.");
            return;
        }

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

            Instantiate(wave.enemyPrefab, spawnPosition, spawnPoint.rotation);
            yield return new WaitForSeconds(wave.spawnDelay); // Adjust the spawn delay as needed
        }
    }
}

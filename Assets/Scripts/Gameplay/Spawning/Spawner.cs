using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    public SpawnPlatform[] platforms;
    public GameObject beamEffectPrefab;
    public float flightHeight = 5f; 
    public bool isCloaked =     true;

    private WaveManager waveManager;
    private Wave currentWave;
    private bool isSpawning = false;
    private float waitTimeIfOverEnemies = 0.1f;

    public void StartNightRoutine(Wave wave, WaveManager manager)
    {
        if(platforms.Length == 0)
        {
            Debug.LogWarning(gameObject.name + ": No platforms assigned to Spawner.");
            return;
        }

        currentWave = wave;
        waveManager = manager;
        isSpawning = true;

        StartCoroutine(NightRoutine());
    }

    public void StopSpawning()
    {
        isSpawning = false;
        StopAllCoroutines();
    }

    public void SetCloak(bool cloak)
    {
        isCloaked = cloak;
        if(isCloaked)
        {   
            // Hide the mothership visually
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = false;
            }
        }
        else
        {
            // Show the mothership visually
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                renderer.enabled = true;
            }
        }
    }

    private IEnumerator NightRoutine()
    {
        int platformIndex;
        platformIndex = 0;

        while (isSpawning)
        {
            SpawnPlatform target;
            target = GetNextActivePlatform(ref platformIndex);

            if (target != null)
            {
                yield return StartCoroutine(FlyToPosistion(target.transform.position + Vector3.up * flightHeight));

                for (int i = 0 ; i < currentWave.batchSize; i++)
                {
                    if(!isSpawning) yield break;

                    while (waveManager.GetActiveAlienCount() >= currentWave.maxOnScreenEnemies && isSpawning)
                    {
                        yield return new WaitForSeconds(waitTimeIfOverEnemies);
                    }

                    if (!isSpawning) yield break;

                    SpawnAlien(target);
                    yield return new WaitForSeconds(currentWave.spawnDelay);
                }
            }
            else
            {
                yield return new WaitForSeconds(1f);
            }
        }
    }

    private void SpawnAlien(SpawnPlatform platform)
    {
        if (platform != null && currentWave != null)
        {
            Vector3 spawnPosition = platform.GetRandomSpawnPosition();

            if (beamEffectPrefab != null)
            {
                Vector3 beamSpawnPosition = spawnPosition;
                beamSpawnPosition.y -= 2.5f;
                Instantiate(beamEffectPrefab, beamSpawnPosition, Quaternion.identity);
            }

            GameObject enemyPrefab = currentWave.GetRandomEnemyPrefab();
            if (enemyPrefab == null)
            {
                return;
            }
            
            GameObject enemyObj = Instantiate(enemyPrefab, spawnPosition, platform.transform.rotation);
            Creature enemy = enemyObj.GetComponent<Creature>();


            if (enemy != null)
            {
                enemy.SetDespawnPoint(spawnPosition);
                waveManager.RegisterEnemy(enemy);
            }
        }
    }

    private IEnumerator FlyToPosistion(Vector3 targetPosition)
    {
        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < currentWave.flightTime)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPosition, targetPosition, elapsed / currentWave.flightTime);
            yield return null;
        }

        transform.position = targetPosition;
    }

    private SpawnPlatform GetNextActivePlatform(ref int currentIndex)
    {
        int startIndex = currentIndex;

        do
        {
            currentIndex = (currentIndex + 1) % platforms.Length;
            if (platforms[currentIndex].isActive)
            {
                return platforms[currentIndex];
            }
        }
        while (currentIndex != startIndex);

        return null;
    }
}

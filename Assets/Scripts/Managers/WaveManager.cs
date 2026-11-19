using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }


    public Wave[] waves;
    public Spawner[] motherships; // Alien Motherships that will spawn the waves


    private List<Creature> activeEnemies = new List<Creature>();
    private Queue<GameObject> specialEnemyQueue = new Queue<GameObject>();

    
    
    private int _wavesStarted;
    public static event Action<int> OnWaveStarted;
    public static event Action<int> OnAllWavesCleared;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    public void SpawnWave(int currentDay)
    {
        _wavesStarted++;
        OnWaveStarted?.Invoke(_wavesStarted);

        if (waves.Length == 0)
        {
            Debug.LogWarning("No waves defined in WaveManager.");
            return;
        }

        activeEnemies.Clear();
        specialEnemyQueue.Clear();

        int waveIndex = (_wavesStarted - 1) % waves.Length;
        Wave currentWave = waves[waveIndex];

        //if(waveIndex > waves.Length - 1)
        //{
        //    Debug.LogWarning("Max Wave Reached");
        //    // Play Final Cutscene?
        //}

        if(currentWave.specialEnemyPool != null)
        {
            foreach (SpecialWaveEnemy special in currentWave.specialEnemyPool)
            {
                if (special.enemyPrefab == null) continue;

                for (int i = 0; i < special.number; i++)
                {
                    specialEnemyQueue.Enqueue(special.enemyPrefab);
                }
            }
        }


        foreach (Spawner ship in motherships)
        {
            if (ship != null)
            {
                ship.StartNightRoutine(currentWave, this);
            }
        }
    }

    public void RegisterEnemy(Creature enemy)
    {
        if (enemy == null) return;

        if (!activeEnemies.Contains(enemy))
            activeEnemies.Add(enemy);

        if (enemy is Animal animal && animal.Team != GameConstants.Team.Alien)
        {
            if (AbductionManager.Instance != null)
            {
                AbductionManager.Instance.EnrollMindControlledAnimal(animal);
            }
            else
            {
                animal.SetTeam(GameConstants.Team.Alien);
                if (animal.TryGetComponent(out Selectable selectable))
                    selectable.enabled = false;
            }
        }
    }

    public void UnregisterEnemy(Creature enemy) => activeEnemies.Remove(enemy);

    public GameObject GetNextEnemyPrefab(Wave wave)
    {
        if(specialEnemyQueue.Count > 0)
        {
            return specialEnemyQueue.Dequeue();
        }
        return wave.GetRandomEnemyPrefab();
    }

    public int GetActiveAlienCount()
    {
        activeEnemies.RemoveAll(enemy => enemy == null || enemy.Down);
        return activeEnemies.Count;
    }

    public void RetreatWave()
    {
        foreach (Spawner ship in motherships)
        {
            if (ship != null)
            {
                ship.StopSpawning();
            }
        }

        foreach (Creature enemy in activeEnemies)
        {
            if (enemy != null && !enemy.Down)
            {
                enemy.Retreat();
            }
        }
        
        // TimeManager calls RetreatWave() at 6am when night ends.
        // If we just survived the final wave, trigger victory immediately!
        if (waves != null && waves.Length > 0 && _wavesStarted >= waves.Length)
        {
            OnAllWavesCleared?.Invoke(_wavesStarted);
        }
    }

    public void CloakMotherships()
    {
        foreach (Spawner ship in motherships)
        {
            if (ship != null)
            {
                ship.SetCloak(true);
            }
        }
    }

    public void UncloakMotherships()
    {
        foreach (Spawner ship in motherships)
        {
            if (ship != null)
            {
                ship.SetCloak(false);
            }
        }
    }
}

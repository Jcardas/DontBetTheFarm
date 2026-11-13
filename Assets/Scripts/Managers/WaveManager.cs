using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }


    public Wave[] waves;
    public Spawner[] motherships; // Alien Motherships that will spawn the waves


    private List<Creature> activeEnemies = new List<Creature>();

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
        if (waves.Length == 0)
        {
            Debug.LogWarning("No waves defined in WaveManager.");
            return;
        }

        activeEnemies.Clear();

        int waveIndex = (currentDay - 1) % waves.Length;

        //if(waveIndex > waves.Length - 1)
        //{
        //    Debug.LogWarning("Max Wave Reached");
        //    // Play Final Cutscene?
        //}

        Wave currentWave = waves[waveIndex];
        
        foreach (Spawner ship in motherships)
        {
            if (ship != null)
            {
                ship.StartNightRoutine(currentWave, this);
            }
        }
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

    }

    public void RegisterEnemy(Creature enemy)
    { 
        activeEnemies.Add(enemy);
    }

    public void UnregisterEnemy(Creature enemy) => activeEnemies.Remove(enemy);

    public int GetActiveAlienCount()
    {
        activeEnemies.RemoveAll(enemy => enemy == null || enemy.Down);
        return activeEnemies.Count;
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

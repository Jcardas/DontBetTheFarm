using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }


    public Wave[] waves;
    public Spawner[] motherships; // Alien Motherships that will spawn the waves


    private List<Alien> activeAliens = new List<Alien>();

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
        // Ensure that the WaveManager persists across scenes
        DontDestroyOnLoad(gameObject);
    }

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

        foreach (Alien alien in activeAliens)
        {
            if (alien != null && !alien.Down)
            {
                alien.Retreat();
            }
        }

    }

    public void RegisterAlien(Alien alien)
    { 
        activeAliens.Add(alien);
    }

    public int GetActiveAlienCount()
    {
        activeAliens.RemoveAll(alien => alien == null || alien.Down);
        return activeAliens.Count;
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

using System;
using System.Collections.Generic;
using UnityEngine;

public class AnimalPen : Buildable
{
    [Header("Animal Pen Settings")]
    [SerializeField]
    private int MaxAnimals; // Arbitrary limit for the maximum number of animals in the pen
    
    [SerializeField]
    private List<FarmAnimal> animalsInPen; // Array to hold the animals in the pen
    
    public override (ResourceType, int) CostAndResource => 
  
        // GameConstants base material and base cost for wheat field
        (GameConstants.Instance.BaseAnimalPen.ResourceType, GameConstants.Instance.BaseAnimalPen.BuildingCost);

    protected override void Reset()
    {
        base.Reset();
        MaxAnimals = GameConstants.Instance.MaxAnimalsInPen;
        animalsInPen = new List<FarmAnimal>(MaxAnimals);
    }
    
    private void AddAnimal(FarmAnimal animal)
    {
        // Check if there's space in the pen
        if (animalsInPen.Count <= MaxAnimals)
        {
            animalsInPen.Add(animal);
            return;
        }
        Debug.LogWarning("No space in the pen to add a new animal.");
    }

    private void RemoveAnimal(FarmAnimal animal)
    {
        if (animalsInPen.Contains(animal))
        {
            animalsInPen.Remove(animal);
            return;
        }
        Debug.LogWarning("Animal not found in the pen.");
    }
    
    private void SpawnAnimal(FarmAnimal animalPrefab)
    {
        // Check if there's space in the pen
        if (animalsInPen.Count <= MaxAnimals)
        {
            // Place the new animal at a random position within the pen's bounds,
            // ensuring it doesn't overlap with existing animals.
            // We achieve this by generating a random position within a certain range around the pen's
            // position and checking if that position is already occupied by another animal.
            Vector3 spawnPosition;
            Quaternion spawnRotation;
            bool positionFound;
            int attempts = 0;
            do
            {
                spawnPosition = transform.position +
                                new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f));
                
                // Random rotation for the animal
                spawnRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0);
                
                positionFound = true;
                foreach (var existingAnimal in animalsInPen)
                {
                    if (Vector3.Distance(spawnPosition, existingAnimal.transform.position) < 1f) // Assuming 1 unit is the minimum distance between animals
                    {
                        positionFound = false;
                        break;
                    }
                }
                attempts++;
            } while (!positionFound && attempts < 10); // Try up to 10 times (arbitrary limit)
            
            if (!positionFound)
            {
                // simply place the animal at the last generated position if no free space was found after 10 attempts
                spawnPosition = transform.position +
                                new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f));
                spawnRotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0);
            }

            FarmAnimal newAnimal = Instantiate(animalPrefab, spawnPosition, spawnRotation);
            AddAnimal(newAnimal);
            return;
        }
        Debug.LogWarning("No space in the pen to spawn a new animal.");
    }

    /// <summary>
    /// Breeds animals in the pen if there are at least two of the same species and there's space for a new animal.
    ///
    /// Subtracts hay from the farm's resources when breeding occurs. If there are not enough resources, breeding will not occur.
    /// </summary>
    private void BreedAnimals()
    {
        // Check if there are at least two animals of the same species in the pen
        // and if so, create a new animal and add it to the pen if there's space.
        
        // Check if there are at least two animals of the same species in the pen
        // ...
    }
}

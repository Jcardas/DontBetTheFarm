using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AnimalPen : Buildable, IContextInteractable
{
    [SerializeField]
    private bool isStartingPen; // Determines if this pen is a starting pen for the player

    [SerializeField]
    private List<Animal> startingAnimals = new(); // Animals to spawn if starting pen

    [SerializeField]
    private int MaxAnimals; // Limit for the maximum number of animals in the pen

    [SerializeField]
    private List<Animal> animalsInPen = new(); // Active animals in the pen

    [Header("Context Menu Styling")]
    [SerializeField] private Sprite breedIcon;
    [SerializeField] private Sprite healIcon;
    [SerializeField] private Color enabledButtonTint = new(0.18f, 0.28f, 0.20f, 1f);
    [SerializeField] private Color disabledButtonTint = new(0.25f, 0.25f, 0.25f, 0.7f);

    public override (ResourceType, int) CostAndResource =>
        (GameConstants.Instance.BaseAnimalPen.ResourceType, GameConstants.Instance.BaseAnimalPen.BuildingCost);

    protected override void Reset()
    {
        base.Reset();

        if (GameConstants.Instance != null)
        {
            MaxAnimals = GameConstants.Instance.MaxAnimalsInPen;
        }

        animalsInPen ??= new List<Animal>(MaxAnimals);
    }

    protected override void Awake()
    {
        base.Awake();
        animalsInPen ??= new List<Animal>(MaxAnimals);

        if (!isStartingPen || startingAnimals == null) return;

        foreach (var animalPrefab in startingAnimals.Where(animalPrefab => animalPrefab != null))
        {
            SpawnAnimal(animalPrefab);
        }
    }

    private void AddAnimal(Animal animal)
    {
        if (animal == null) return;

        PruneNullAnimals();

        if (animalsInPen.Count < MaxAnimals)
        {
            animalsInPen.Add(animal);
            return;
        }

        Debug.LogWarning($"[{name}] No space in the pen to add animal: {animal.name}.");
    }

    private void RemoveAnimal(Animal animal)
    {
        if (animalsInPen.Remove(animal))
        {
            return;
        }

        Debug.LogWarning($"[{name}] Animal {animal.name} not found in pen.");
    }

    private void SpawnAnimal(Animal animalPrefab)
    {
        if (animalPrefab == null)
        {
            Debug.LogWarning($"[{name}] Cannot spawn a null FarmAnimal prefab.");
            return;
        }

        PruneNullAnimals();

        if (animalsInPen.Count >= MaxAnimals)
        {
            Debug.LogWarning($"[{name}] No space in the pen to spawn a new animal.");
            return;
        }

        Vector3 spawnPosition = transform.position;
        Quaternion spawnRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        bool positionFound;
        int attempts = 0;

        do
        {
            Vector3 randomOffset = new(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f));
            spawnPosition = transform.position + randomOffset;
            spawnRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            positionFound = true;
            foreach (var existingAnimal in animalsInPen)
            {
                if (existingAnimal != null && Vector3.Distance(spawnPosition, existingAnimal.transform.position) < 1f)
                {
                    positionFound = false;
                    break;
                }
            }
            attempts++;
        } while (!positionFound && attempts < 10);

        // Instantiate the animals original prefab at the calculated position and rotation, and parent it to the pen
        Animal newAnimal = Instantiate(animalPrefab, spawnPosition, spawnRotation, CreatureManager.AnimalContainer);
        
        AddAnimal(newAnimal);
    }

    private void BreedAnimals()
    {
        PruneNullAnimals();

        if (animalsInPen.Count >= MaxAnimals)
        {
            Debug.LogWarning($"[{name}] No space in pen to breed ({animalsInPen.Count}/{MaxAnimals}).");
            return;
        }

        if (!TryGetBreedableAnimal(out Animal parentTemplate))
        {
            Debug.LogWarning($"[{name}] Need at least 2 animals of the same species to breed.");
            return;
        }
        
        // Since there are at least 2 animals of the same species, check to make sure 2 are also alive
            var aliveAnimals =
                animalsInPen.Where(a =>
                    a != null && !a.Down && GetSpeciesKey(a) == GetSpeciesKey(parentTemplate)).ToList();
        
        if (aliveAnimals.Count < 2) return;
        
        
        int hayCost = parentTemplate.BreedingCost;
        if (!TryConsumeHay(hayCost))
        {
            Debug.LogWarning($"[{name}] Insufficient Hay. Requires: {hayCost}.");
            return;
        }

        SpawnAnimal(parentTemplate);
    }

    private void HealAllAnimals()
    {
        PruneNullAnimals();

        List<Animal> injuredAnimals = GetInjuredAnimals();
        if (injuredAnimals.Count == 0)
        {
            Debug.Log($"[{name}] All animals are healthy.");
            return;
        }

        int totalHayCost = CalculateHealingHayCost(injuredAnimals);

        if (!TryConsumeHay(totalHayCost))
        {
            Debug.LogWarning($"[{name}] Insufficient Hay. Requires: {totalHayCost}.");
            return;
        }

        foreach (Animal animal in injuredAnimals)
        {
            HealAnimal(animal);
        }

        Debug.Log($"[{name}] Healed {injuredAnimals.Count} animals for {totalHayCost} Hay.");
    }

    private List<Animal> GetInjuredAnimals()
    {
        var injured = new List<Animal>();
        foreach (Animal animal in animalsInPen)
        {
            if (animal != null && IsAnimalInjured(animal))
            {
                injured.Add(animal);
            }
        }
        return injured;
    }

    private static bool IsAnimalInjured(Animal animal) => animal.CurrentHealth < animal.MaxHealth;

    private static void HealAnimal(Animal animal) => animal.HealToFull();

    private static int CalculateHealingHayCost(List<Animal> injuredAnimals)
    {
        return injuredAnimals.Sum(animal => animal.HealCost);
    }

    private bool TryGetBreedableAnimal(out Animal breedableTemplate)
    {
        var speciesCounts = new Dictionary<string, (int count, Animal template)>();

        foreach (Animal animal in animalsInPen)
        {
            if (animal == null) continue;

            string key = GetSpeciesKey(animal);

            if (speciesCounts.TryGetValue(key, out var entry))
            {
                entry.count++;
                speciesCounts[key] = entry;

                if (entry.count >= 2)
                {
                    breedableTemplate = entry.template;
                    return true;
                }
            }
            else
            {
                speciesCounts[key] = (1, animal);
            }
        }

        breedableTemplate = null;
        return false;
    }

    private static string GetSpeciesKey(Animal animal)
    {
        return animal.name.Replace("(Clone)", string.Empty).Trim();
    }

    private static bool TryConsumeHay(int hayCost)
    {
        return ResourceManager.Spend(ResourceType.Hay, hayCost);
    }

    private void PruneNullAnimals()
    {
        animalsInPen.RemoveAll(a => a == null);
    }

    #region IContextInteractable Implementation

    public IReadOnlyList<ContextActionData> GetContextActions()
    {
        PruneNullAnimals();

        var actions = new List<ContextActionData>();
        int currentHay = ResourceManager.Count(ResourceType.Hay);

        // Breed Action
        bool hasSpace = animalsInPen.Count < MaxAnimals;
        bool hasBreedingPair = TryGetBreedableAnimal(out Animal breedableAnimal);
        bool hasHayForBreeding = breedableAnimal != null && currentHay >= breedableAnimal.BreedingCost;
        bool enoughAnimalsToBreed = animalsInPen.Count(a => a != null && !a.Down && GetSpeciesKey(a) == GetSpeciesKey(breedableAnimal)) >= 2;
        bool canBreed = hasSpace && hasBreedingPair && hasHayForBreeding && enoughAnimalsToBreed;

        string breedLabel;
        if (!hasSpace)
        {
            breedLabel = $"Breed (Full {animalsInPen.Count}/{MaxAnimals})";
        }
        else if (!hasBreedingPair)
        {
            breedLabel = "Can't Breed (<2 Same Species)";
        }
        else
        {
            string speciesName = GetSpeciesKey(breedableAnimal);
            breedLabel = hasHayForBreeding
                ? $"Breed {speciesName}'s ({breedableAnimal.BreedingCost} Hay)"
                : $"Breed {speciesName}'s (Need {breedableAnimal.BreedingCost} Hay)";
        }

        actions.Add(new ContextActionData(
            label: breedLabel,
            onExecute: BreedAnimals,
            icon: breedIcon,
            textColor: canBreed ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f),
            buttonTint: canBreed ? enabledButtonTint : disabledButtonTint
        ));

        // Heal Action
        List<Animal> injuredAnimals = GetInjuredAnimals();
        bool hasInjured = injuredAnimals.Count > 0;
        int healHayCost = hasInjured ? CalculateHealingHayCost(injuredAnimals) : 0;
        bool hasHayForHeal = currentHay >= healHayCost;
        bool canHeal = hasInjured && hasHayForHeal;

        string healLabel;
        if (animalsInPen.Count == 0)
        {
            healLabel = "Heal All (Pen Empty)";
        }
        else if (!hasInjured)
        {
            healLabel = "Heal All (All Healthy)";
        }
        else
        {
            healLabel = hasHayForHeal
                ? $"Heal All ({injuredAnimals.Count}) [{healHayCost} Hay]"
                : $"Heal All ({injuredAnimals.Count}) [Need {healHayCost} Hay]";
        }

        actions.Add(new ContextActionData(
            label: healLabel,
            onExecute: HealAllAnimals,
            icon: healIcon,
            textColor: canHeal ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f),
            buttonTint: canHeal ? enabledButtonTint : disabledButtonTint
        ));

        return actions;
    }

    #endregion
}
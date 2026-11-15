using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AnimalPen : Buildable, IContextInteractable
{
    private const int BreedingCostPerMutation = 5;

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

    public int Capacity => MaxAnimals;

    public IReadOnlyList<Animal> AnimalsInPen
    {
        get
        {
            PruneGoneAnimals();
            return animalsInPen;
        }
    }

    [SerializeField] private PenUIController penMenuController;

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

    public bool AddAnimal(Animal animal)
    {
        if (animal == null) return false;

        PruneGoneAnimals();

        if (animalsInPen.Contains(animal)) return true;

        if (animalsInPen.Count >= MaxAnimals)
        {
            Debug.LogWarning($"[{name}] No space in the pen to add animal: {animal.name}.");
            return false;
        }

        if (animal.AssignedPen != null && animal.AssignedPen != this)
        {
            animal.AssignedPen.RemoveAnimal(animal);
        }

        animalsInPen.Add(animal);
        animal.AssignedPen = this;
        return true;
    }

    public void RemoveAnimal(Animal animal)
    {
        if (animalsInPen.Remove(animal) && animal != null && animal.AssignedPen == this)
        {
            animal.AssignedPen = null;
        }
    }

    private void SpawnAnimal(Animal animalPrefab)
    {
        if (animalPrefab == null)
        {
            Debug.LogWarning($"[{name}] Cannot spawn a null FarmAnimal prefab.");
            return;
        }

        PruneGoneAnimals();

        if (animalsInPen.Count >= MaxAnimals)
        {
            Debug.LogWarning($"[{name}] No space in the pen to spawn a new animal.");
            return;
        }

        BoxCollider penArea = GetComponent<BoxCollider>();
        if (!penArea) return;

        float halfX = penArea.size.x / 2f - 1f;
        float halfZ = penArea.size.z / 2f - 1f;
        if (halfX <= 0f || halfZ <= 0f) return;

        Animal[] nearbyAnimals = FindObjectsByType<Animal>(FindObjectsSortMode.None);
        Vector3 spawnPosition = transform.position;
        Quaternion spawnRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
        bool positionFound;
        int attempts = 0;

        do
        {
            Vector3 randomOffset = new(UnityEngine.Random.Range(-halfX, halfX), 0f,
                                        UnityEngine.Random.Range(-halfZ, halfZ));
            spawnPosition = penArea.transform.TransformPoint(penArea.center + randomOffset);
            spawnRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            positionFound = true;
            foreach (var existingAnimal in nearbyAnimals)
            {
                if (existingAnimal != null && existingAnimal.gameObject.activeInHierarchy &&
                    Vector2.Distance(new Vector2(spawnPosition.x, spawnPosition.z),
                                     new Vector2(existingAnimal.transform.position.x, existingAnimal.transform.position.z)) < 1.5f)
                {
                    positionFound = false;
                    break;
                }
            }
            attempts++;
        } while (!positionFound && attempts < 25);

        if (!positionFound) return;

        Animal prefabAsset = ResolveAnimalPrefab(animalPrefab);
        if (prefabAsset == null)
        {
            Debug.LogWarning($"[{name}] Could not resolve a prefab asset for '{animalPrefab.name}'.");
            return;
        }

        bool isAsset = !prefabAsset.gameObject.scene.IsValid();
        Animal newAnimal = Instantiate(prefabAsset, spawnPosition, spawnRotation, CreatureManager.AnimalContainer);
        if (isAsset)
        {
            newAnimal.SourcePrefab = prefabAsset;
        }
        else
        {
            // Cloned from a live scene parent: restore full HP and strip parent's equipped mutations before inheriting
            newAnimal.HealToFull();
            if (newAnimal.TryGetComponent<CreatureMutator>(out var clonedMut))
            {
                foreach (SlotType slot in new[] { SlotType.Head, SlotType.Back, SlotType.Limbs, SlotType.Tail })
                    clonedMut.UnequipMutation(slot);
            }
        }

        AddAnimal(newAnimal);
    }

    private Animal ResolveAnimalPrefab(Animal source)
    {
        if (source == null) return null;
        if (!source.gameObject.scene.IsValid()) return source;
        if (source.SourcePrefab != null && !source.SourcePrefab.gameObject.scene.IsValid())
            return source.SourcePrefab;

        string species = GetSpeciesKey(source);

        // 1. Check startingAnimals on this pen and all other pens
        foreach (var pen in FindObjectsByType<AnimalPen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (pen.startingAnimals == null) continue;
            foreach (var candidate in pen.startingAnimals)
            {
                if (candidate != null && !candidate.gameObject.scene.IsValid() && GetSpeciesKey(candidate) == species)
                    return candidate;
            }
        }

        // 2. Check all loaded Animal prefab assets in memory (e.g., referenced by GameConstants, spawners, etc.)
        foreach (var candidate in Resources.FindObjectsOfTypeAll<Animal>())
        {
            if (candidate != null && !candidate.gameObject.scene.IsValid() && GetSpeciesKey(candidate) == species)
                return candidate;
        }

        // 3. Fallback to the live parent instance (safe now that Highlight.Show() null-guards outline)
        return source;
    }

    public int CalculateBreedingCost(Animal parentA, Animal parentB)
    {
        if (parentA == null && parentB == null) return 0;

        int baseCost = parentA != null
            ? parentA.BreedingCost
            : parentB.BreedingCost;

        int mutationCount = CountEquippedMutations(parentA) + CountEquippedMutations(parentB);
        return Mathf.Max(0, baseCost + mutationCount * BreedingCostPerMutation);
    }

    private static int CountEquippedMutations(Animal animal)
    {
        if (animal == null || !animal.TryGetComponent<CreatureMutator>(out var mut))
            return 0;

        int count = 0;
        foreach (SlotType slot in new[] { SlotType.Head, SlotType.Back, SlotType.Limbs, SlotType.Tail })
        {
            if (mut.GetEquippedMutation(slot) != null)
                count++;
        }
        return count;
    }

    public bool CanBreedPair(Animal parentA, Animal parentB, out int hayCost)
    {
        hayCost = CalculateBreedingCost(parentA, parentB);
        PruneGoneAnimals();

        if (parentA == null || parentB == null || parentA == parentB) return false;
        if (parentA.Down || parentB.Down) return false;
        if (animalsInPen.Count >= MaxAnimals) return false;
        if (GetSpeciesKey(parentA) != GetSpeciesKey(parentB)) return false;
        if (ResolveAnimalPrefab(parentA) == null) return false;

        return ResourceManager.Count(ResourceType.Hay) >= hayCost;
    }

    public Animal BreedPair(Animal parentA, Animal parentB)
    {
        if (!CanBreedPair(parentA, parentB, out int hayCost)) return null;

        Animal breedTemplate = ResolveAnimalPrefab(parentA);
        if (breedTemplate == null) return null;

        // Spawn first BEFORE spending Hay so a failed spawn position check never wastes Hay
        int beforeCount = animalsInPen.Count;
        SpawnAnimal(breedTemplate);

        if (animalsInPen.Count > beforeCount)
        {
            TryConsumeHay(hayCost);
            Animal offspring = animalsInPen[^1];

            if (offspring.TryGetComponent<CreatureMutator>(out var childMutator))
            {
                parentA.TryGetComponent<CreatureMutator>(out var mutA);
                parentB.TryGetComponent<CreatureMutator>(out var mutB);

                foreach (SlotType slot in new[] { SlotType.Head, SlotType.Back, SlotType.Limbs, SlotType.Tail })
                {
                    MutationData fromA = mutA != null ? mutA.GetEquippedMutation(slot) : null;
                    MutationData fromB = mutB != null ? mutB.GetEquippedMutation(slot) : null;
                    MutationData inherited = UnityEngine.Random.value < 0.5f ? (fromA ?? fromB) : (fromB ?? fromA);

                    if (inherited != null)
                        childMutator.EquipMutation(inherited, slot);
                }
            }

            return offspring;
        }

        return null;
    }

    private static bool IsAnimalInjured(Animal animal) => animal.CurrentHealth < animal.CalculatedMaxHealth;

    public bool TryHealSingleAnimal(Animal animal)
    {
        if (animal == null || !IsAnimalInjured(animal)) return false;
        if (!TryConsumeHay(animal.HealCost)) return false;

        HealAnimal(animal);
        return true;
    }

    private void BreedAnimals()
    {
        PruneGoneAnimals();

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
        PruneGoneAnimals();

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
        if (animal == null) return string.Empty;
        string creatureName = animal.GetCreatureName();
        return !string.IsNullOrWhiteSpace(creatureName)
            ? creatureName.Trim()
            : animal.name.Replace("(Clone)", string.Empty).Trim();
    }

    private static bool TryConsumeHay(int hayCost)
    {
        return ResourceManager.Spend(ResourceType.Hay, hayCost);
    }

    private void PruneGoneAnimals()
    {
        for (int i = animalsInPen.Count - 1; i >= 0; i--)
        {
            Animal a = animalsInPen[i];
            if (a == null || !a.isActiveAndEnabled || a.Team != GameConstants.Team.Farm || UFO.Abducted.Contains(a))
            {
                if (a != null && a.AssignedPen == this)
                    a.AssignedPen = null;

                animalsInPen.RemoveAt(i);
            }
        }
    }

    public void OpenPenMenu()
    {
        ContextMenuEventBus.RequestClose();

        if (penMenuController == null)
            penMenuController = FindAnyObjectByType<PenUIController>(FindObjectsInactive.Include);

        if (penMenuController != null)
        {
            penMenuController.OpenMenu(this);
        }
        else
        {
            Debug.LogWarning("[AnimalPen] PenUIController not found in scene!");
        }
    }

    private void AssignSelectedAnimal()
    {
        PruneGoneAnimals();

        var selectedUnits = Selectable.All
            .Where(s => s.Selected && s.TryGetComponent<Animal>(out _))
            .ToList();

        if (selectedUnits.Count == 0) return;

        int remainingSpace = Mathf.Max(0, MaxAnimals - animalsInPen.Count);
        int newAnimalsCount = selectedUnits.Count(u => !animalsInPen.Contains(u.GetComponent<Animal>()));
        if (newAnimalsCount > remainingSpace) return;

        foreach (Selectable unit in selectedUnits)
        {
            if (unit.TryGetComponent<Animal>(out Animal animal) && AddAnimal(animal))
            {
                unit.Command(Order.ForPen(this));
            }
        }
    }

    #region IContextInteractable Implementation

    public IReadOnlyList<ContextActionData> GetContextActions()
    {
        PruneGoneAnimals();

        var selectedAnimals = Selectable.All
            .Where(s => s.Selected && s.TryGetComponent<Animal>(out _))
            .Select(s => s.GetComponent<Animal>())
            .ToList();

        // No animals selected: open the Pen UI directly without showing a context menu
        if (selectedAnimals.Count == 0)
        {
            OpenPenMenu();
            return Array.Empty<ContextActionData>();
        }

        var actions = new List<ContextActionData>
        {
            new ContextActionData("View Pen", OpenPenMenu, buttonTint: enabledButtonTint)
        };

        int remainingSpace = Mathf.Max(0, MaxAnimals - animalsInPen.Count);
        int newAnimalsCount = selectedAnimals.Count(a => !animalsInPen.Contains(a));

        if (newAnimalsCount > remainingSpace)
        {
            string overflowLabel = remainingSpace == 0
                ? $"Pen is full ({animalsInPen.Count}/{MaxAnimals})"
                : $"Too many animals selected ({newAnimalsCount} > {remainingSpace} free)";

            actions.Add(new ContextActionData(
                label: overflowLabel,
                onExecute: null,
                textColor: new Color(0.75f, 0.75f, 0.75f, 1f),
                buttonTint: disabledButtonTint
            ));
        }
        else
        {
            string animalName = selectedAnimals.Count == 1
                ? GetSpeciesKey(selectedAnimals[0])
                : $"{selectedAnimals.Count} Animals";

            string assignLabel = $"Assign {animalName} to pen ({animalsInPen.Count}/{MaxAnimals})";
            actions.Add(new ContextActionData(
                label: assignLabel,
                onExecute: AssignSelectedAnimal,
                buttonTint: enabledButtonTint
            ));
        }

        return actions;
    }

    #endregion
}
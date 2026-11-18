using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// A farm animal is a friendly creature that can be controlled by the farmer.
/// It inherits from the Creature class and can be used to create different types of farm animals.
/// </summary>
[RequireComponent(typeof(Selectable), typeof(Gatherer))]
public class Animal : Creature, IContextInteractable
{
    [Header("Prefab Reference")]
    [Tooltip("Prefab reference for the farm animal. This is used for spawning and instantiation.")]
    [SerializeField] protected Animal sourcePrefab;

    [Header("Abducted Animal Modifiers")]
    [Tooltip("Size of the animal's neck. This can be used to adjust the appearance of the mind control collar " +
             "when it is abducted by a UFO and returns as an enemy.")]
    [SerializeField] protected float neckSize = 1f;

    [SerializeField] protected int breedingCost;
    [SerializeField] protected int healCost;

    public Animal SourcePrefab
    {
        get => sourcePrefab != null ? sourcePrefab : this;
        set => sourcePrefab = value;
    }

    public float NeckSize => neckSize;

    public int BreedingCost => breedingCost;
    public int HealCost => healCost;

    public AnimalPen AssignedPen { get; set; }

    protected override void Reset()
    {
        base.Reset();
        // Use global constants for farm animal stats
        maxHealth = GameConstants.Instance.BaseAnimal.MaxHealth;
        moveSpeed = GameConstants.Instance.BaseAnimal.Speed;
        attackSpeed = GameConstants.Instance.BaseAnimal.AttackSpeed;
        team = GameConstants.Instance.BaseAnimal.Team;
        breedingCost = GameConstants.Instance.BaseBreedingCost;
        healCost = GameConstants.Instance.BaseHealCost;
    }

    protected override void HandleZeroHealth()
    {
        // Animals do not die, they are knocked out. The base turns off its NavMeshAgent so the others walk past it.
        AnimalKnockedOut();
        base.HandleZeroHealth();
    }

    protected override void HandleRevive()
    {
        // When a farm animal is revived, it should be reset to its default state.
        base.HandleRevive();
    }

    protected virtual void AnimalKnockedOut()
    {
    }

    public override void HealToFull()
    {
        base.HealToFull();
        // Additional logic for farm animals when healed, if needed
    }

    public IReadOnlyList<ContextActionData> GetContextActions()
    {
        if (!Down) return Array.Empty<ContextActionData>();

        var farmerSelectable = Selectable.All.FirstOrDefault(s =>
            s.Selected && s.Creature is Farmer f && !f.Down);

        if (farmerSelectable == null)
        {
            return new List<ContextActionData>
            {
                new ContextActionData(
                    label: "Select Farmer to Revive",
                    onExecute: null,
                    textColor: new Color(0.75f, 0.75f, 0.75f, 1f),
                    buttonTint: new Color(0.25f, 0.25f, 0.25f, 0.7f)
                )
            };
        }

        bool hasHay = ResourceManager.Count(ResourceType.Hay) >= healCost;
        return new List<ContextActionData>
        {
            new ContextActionData(
                label: $"Revive {CreatureName} ({healCost} <sprite=\"ResourceIcons\" name=\"Hay\">)",
                onExecute: () =>
                {
                    if (hasHay)
                        farmerSelectable.Command(Order.ForRevive(this));
                },
                textColor: hasHay ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f),
                buttonTint: hasHay ? new Color(0.18f, 0.28f, 0.20f, 1f) : new Color(0.25f, 0.25f, 0.25f, 0.7f)
            )
        };
    }
}
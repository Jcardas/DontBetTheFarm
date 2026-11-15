using System;
using System.Collections.Generic;
using UnityEngine;

public class MutationUnlockManager : MonoBehaviour
{
    public static MutationUnlockManager Instance { get; private set; }

    [Header("Starting Unlocked Mutations (Optional)")]
    [SerializeField] private List<MutationData> defaultUnlockedMutations = new();

    private readonly HashSet<MutationData> _unlockedMutations = new();

    public event Action<MutationData> OnMutationUnlocked;

    public IReadOnlyCollection<MutationData> UnlockedMutations => _unlockedMutations;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        foreach (var mutation in defaultUnlockedMutations)
        {
            if (mutation != null)
                _unlockedMutations.Add(mutation);
        }
    }

    public bool IsUnlocked(MutationData mutation)
    {
        return mutation != null && _unlockedMutations.Contains(mutation);
    }

    public void UnlockMutation(MutationData mutation)
    {
        if (mutation == null || !_unlockedMutations.Add(mutation))
            return;

        OnMutationUnlocked?.Invoke(mutation);
    }
}
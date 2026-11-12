using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public class CreatureMutator : MonoBehaviour
{
    [Serializable]
    public struct SocketSlot
    {
        [Tooltip("The socket Transform (e.g. Socket_Limb_FL)")]
        public Transform transform;
        [Tooltip("Optional original visual part to spawn here by default (e.g. Cow_Leg_FL). Purely visual, grants no stats.")]
        public GameObject defaultVisualPrefab;
    }

    [Serializable]
    public struct SocketBinding
    {
        public SlotType slotType;
        [Tooltip("List of individual sockets belonging to this slot, each with its own default visual.")]
        public List<SocketSlot> sockets;
    }

    [Serializable]
    public struct StartingMutation
    {
        public MutationData mutation;
        public SlotType slot;
    }

    [Header("Sockets Configuration")]
    [SerializeField] private List<SocketBinding> socketBindings = new List<SocketBinding>();
    [SerializeField] private float socketScaleFactor = 1f;

    [Header("Default Loadout")]
    [SerializeField] private List<StartingMutation> defaultMutations = new List<StartingMutation>();

    // Track active mutations and instances: Slot -> Data, Slot -> List of active GameObjects
    private readonly Dictionary<SlotType, MutationData> equippedMutations = new Dictionary<SlotType, MutationData>();
    private readonly Dictionary<SlotType, List<GameObject>> activePartObjects = new Dictionary<SlotType, List<GameObject>>();
    private readonly List<IMutationBehavior> activeBehaviors = new List<IMutationBehavior>();

    private Creature ownerCreature;
    
    // Whenever a mutation is equipped or unequipped, this event is triggered to notify listeners (e.g., Creature) that the mutations have changed.
    public event Action OnMutationsUpdated;

    public IReadOnlyDictionary<SlotType, MutationData> EquippedMutations => equippedMutations;
    public IReadOnlyList<IMutationBehavior> ActiveBehaviors => activeBehaviors;

    private void Awake()
    {
        ownerCreature = GetComponentInParent<Creature>();
    }

    private void Start()
    {
        // 1. Spawn all base visual defaults for every slot (e.g., normal cow legs, head, tail)
        RestoreAllDefaults();

        // 2. Apply any starting mutations configured in the Inspector (overwrites defaults for that slot)
        foreach (var entry in defaultMutations)
        {
            if (entry.mutation == null) continue;

            foreach (SlotType individualSlot in Enum.GetValues(typeof(SlotType)))
            {
                if (individualSlot == SlotType.None) continue;
                if ((entry.slot & individualSlot) == individualSlot)
                {
                    EquipMutation(entry.mutation, individualSlot);
                }
            }
        }
    }

    /// <summary>
    /// Checks if this creature has physical sockets for this slot and if the mutation fits the slot mask.
    /// </summary>
    public bool CanEquip(MutationData mutation, SlotType targetSlot)
    {
        if (mutation == null || targetSlot == SlotType.None) return false;
        if ((mutation.allowedSlots & targetSlot) == 0) return false;

        int index = socketBindings.FindIndex(b => b.slotType == targetSlot);
        return index != -1 && socketBindings[index].sockets != null && socketBindings[index].sockets.Count > 0;
    }

    /// <summary>
    /// Attaches mutation parts to the sockets, clearing any defaults or previous mutations in that slot.
    /// </summary>
    public void EquipMutation(MutationData mutation, SlotType targetSlot)
    {
        if (!CanEquip(mutation, targetSlot))
        {
            Debug.LogWarning($"[CreatureMutator] Cannot equip {mutation?.mutationName} on {targetSlot}");
            return;
        }

        // Clear existing visuals/mutations in this slot without restoring defaults yet
        ClearSlotVisuals(targetSlot);

        int bindingIndex = socketBindings.FindIndex(b => b.slotType == targetSlot);
        if (bindingIndex == -1) return;

        var binding = socketBindings[bindingIndex];
        List<GameObject> spawnedParts = new List<GameObject>();

        for (int i = 0; i < binding.sockets.Count; i++)
        {
            var socketSlot = binding.sockets[i];
            if (socketSlot.transform == null || mutation.prefab == null) continue;

            GameObject part = SpawnMutationObject(
                mutation.prefab,
                socketSlot.transform,
                defaultPart: false,
                mutationScale: mutation.localScale
            );

            // If the mutation prefab is paired (e.g., wings, legs), determine which side to keep based on the socket's local X position relative to the creature's origin.
            if (mutation.pairedPrefab)
            {
                // Local X < 0 is Left (_L), Local X >= 0 is Right (_R)
                float localX = transform.InverseTransformPoint(socketSlot.transform.position).x;
                bool useLeftSide = localX < -0.01f;
                FilterPairedSide(part, useLeftSide);
            }

            // Hook behaviors (e.g. venom slow, spores aura)
            var behaviors = part.GetComponentsInChildren<IMutationBehavior>();
            foreach (var behavior in behaviors)
            {
                behavior.Initialize(ownerCreature);
                behavior.OnEquipped();
                activeBehaviors.Add(behavior);
            }

            spawnedParts.Add(part);
        }

        equippedMutations[targetSlot] = mutation;
        activePartObjects[targetSlot] = spawnedParts;

        // Notify all listeners (Creature stat calculations, CombatFeedback visuals, etc.)
        OnMutationsUpdated?.Invoke();
    }
    
    /// <summary>
    /// Destroys the unused side (_L or _R) of a paired mutation prefab instance using Regex
    /// to safely handle trailing suffixes like (Clone) or .001.
    /// </summary>
    private void FilterPairedSide(GameObject partInstance, bool keepLeft)
    {
        // Matches "_R" or "_L" at the end of the base name, allowing optional Blender (.001) or Unity ((Clone)) suffixes
        string sideToRemove = keepLeft ? "R" : "L";
        string sideToKeep   = keepLeft ? "L" : "R";

        Regex removeRegex = new Regex($@"_{sideToRemove}(?:[.\s_-]\d+|\s*\(Clone\))*$", RegexOptions.IgnoreCase);
        Regex keepRegex   = new Regex($@"_{sideToKeep}(?:[.\s_-]\d+|\s*\(Clone\))*$", RegexOptions.IgnoreCase);

        List<GameObject> toDestroy = new List<GameObject>();

        foreach (Transform child in partInstance.GetComponentsInChildren<Transform>(true))
        {
            if (child == partInstance.transform) continue;

            if (removeRegex.IsMatch(child.name))
            {
                toDestroy.Add(child.gameObject);
            }
            else if (keepRegex.IsMatch(child.name))
            {
                child.gameObject.SetActive(true);
            }
        }

        foreach (var obj in toDestroy)
        {
            DestroyImmediate(obj);
        }
    }

    /// <summary>
    /// Removes active mutations from a slot and reverts back to the default visuals.
    /// </summary>
    public void UnequipMutation(SlotType targetSlot)
    {
        ClearSlotVisuals(targetSlot);
        equippedMutations.Remove(targetSlot);

        // Revert to original default parts for this slot
        RestoreSlotDefaults(targetSlot);
    }

    /// <summary>
    /// Cleans up spawned mutation objects and behaviors without spawning defaults.
    /// </summary>
    private void ClearSlotVisuals(SlotType targetSlot)
    {
        if (activePartObjects.TryGetValue(targetSlot, out var parts))
        {
            foreach (var part in parts)
            {
                if (part == null) continue;

                var behaviors = part.GetComponentsInChildren<IMutationBehavior>();
                foreach (var b in behaviors)
                {
                    b.OnUnequipped();
                    activeBehaviors.Remove(b);
                }
                Destroy(part);
            }
            parts.Clear();
            activePartObjects.Remove(targetSlot);
        }
    }

    /// <summary>
    /// Restores the default visuals configured on the sockets for a given slot.
    /// </summary>
    private void RestoreSlotDefaults(SlotType targetSlot)
    {
        int bindingIndex = socketBindings.FindIndex(b => b.slotType == targetSlot);
        if (bindingIndex == -1) return;

        var binding = socketBindings[bindingIndex];
        List<GameObject> spawnedDefaults = new List<GameObject>();

        foreach (var socketSlot in binding.sockets)
        {
            if (socketSlot.transform == null || socketSlot.defaultVisualPrefab == null) continue;

            GameObject defaultPart = SpawnMutationObject(
                socketSlot.defaultVisualPrefab,
                socketSlot.transform,
                true
                );
            spawnedDefaults.Add(defaultPart);
        }

        if (spawnedDefaults.Count > 0)
        {
            activePartObjects[targetSlot] = spawnedDefaults;
        }
        
        // Notify listeners that the visuals have changed, so they can update any relevant state (e.g., Creature recalculating stats)
        OnMutationsUpdated?.Invoke();
    }

    private void RestoreAllDefaults()
    {
        foreach (var binding in socketBindings)
        {
            RestoreSlotDefaults(binding.slotType);
        }
    }

    public void UnequipAll()
    {
        var slots = new List<SlotType>(equippedMutations.Keys);
        foreach (var slot in slots)
        {
            UnequipMutation(slot);
        }
    }

    private void Reset()
    {
        AutoBindChildSockets();
    }

    /// <summary>
    /// Auto-discovers sockets while preserving any already-assigned default visual prefabs.
    /// </summary>
    private void AutoBindChildSockets()
    {
        // Cache existing default prefabs by transform so re-scanning doesn't wipe inspector setups
        Dictionary<Transform, GameObject> existingDefaults = new Dictionary<Transform, GameObject>();
        foreach (var binding in socketBindings)
        {
            if (binding.sockets == null) continue;
            foreach (var slot in binding.sockets)
            {
                if (socketSlotIsValid(slot))
                    existingDefaults[slot.transform] = slot.defaultVisualPrefab;
            }
        }

        socketBindings.Clear();

        foreach (SlotType slot in Enum.GetValues(typeof(SlotType)))
        {
            if (slot == SlotType.None) continue;

            string slotStr = slot.ToString();
            string rootName = slotStr.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                ? slotStr.Substring(0, slotStr.Length - 1)
                : slotStr;

            string pattern = $@"^Socket_{rootName}s?([._\-\s\d].*|$)";
            Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);

            List<Transform> foundTransforms = new List<Transform>();

            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform) continue;

                if (regex.IsMatch(child.name))
                {
                    foundTransforms.Add(child);
                }
            }

            if (foundTransforms.Count > 0)
            {
                foundTransforms.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

                List<SocketSlot> socketSlots = new List<SocketSlot>();
                foreach (var t in foundTransforms)
                {
                    existingDefaults.TryGetValue(t, out GameObject defaultPrefab);
                    socketSlots.Add(new SocketSlot
                    {
                        transform = t,
                        defaultVisualPrefab = defaultPrefab
                    });
                }

                socketBindings.Add(new SocketBinding
                {
                    slotType = slot,
                    sockets = socketSlots
                });
            }
        }

        bool socketSlotIsValid(SocketSlot s) => s.transform != null;
    }

    private GameObject SpawnMutationObject(
        GameObject visualObject,
        Transform parentPosition,
        bool defaultPart = false,
        float mutationScale = 1f)
    {
        if (visualObject == null || parentPosition == null) return null;

        GameObject part = Instantiate(visualObject, parentPosition);
    
        int parentLayer = parentPosition.gameObject.layer;
        void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
        SetLayerRecursively(part, parentLayer);
    
        part.transform.localPosition = Vector3.zero;
        part.transform.localRotation = Quaternion.identity;
    
        // Combine the mutation's base scale with the creature's socket scale factor
        float finalScale = defaultPart ? 1.0f : (mutationScale * socketScaleFactor);
        part.transform.localScale = Vector3.one * finalScale;
    
        return part;
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

public class CreatureMutator : MonoBehaviour
{
    [Serializable]
    public struct SocketBinding
    {
        public SlotType slotType;
        [Tooltip("Child transforms where mutation visuals attach (e.g., 2 for Goose, 4 for Cow, 6 for Alien)")]
        public Transform[] socketTransforms;
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

    // Track state: Slot -> Data, Slot -> List of spawned visual objects
    private readonly Dictionary<SlotType, MutationData> equippedMutations = new Dictionary<SlotType, MutationData>();
    private readonly Dictionary<SlotType, List<GameObject>> activePartObjects = new Dictionary<SlotType, List<GameObject>>();
    private readonly List<IMutationBehavior> activeBehaviors = new List<IMutationBehavior>();

    private Creature ownerCreature;
    // private SelectionHighlight selectionHighlight;

    public IReadOnlyDictionary<SlotType, MutationData> EquippedMutations => equippedMutations;
    public IReadOnlyList<IMutationBehavior> ActiveBehaviors => activeBehaviors;

    private void Awake()
    {
        ownerCreature = GetComponentInParent<Creature>();
        // selectionHighlight = GetComponentInParent<SelectionHighlight>();
    }

    private void Start()
    {
        // Apply starting loadout if configured in the inspector
        foreach (var entry in defaultMutations)
        {
            if (entry.mutation != null)
            {
                EquipMutation(entry.mutation, entry.slot);
            }
        }
    }

    private void Reset()
    {
        AutoBindChildSockets();
    }

    /// <summary>
    /// Checks if this creature has physical sockets for this slot and if the mutation fits the slot mask.
    /// Does not restrict creature types.
    /// </summary>
    public bool CanEquip(MutationData mutation, SlotType targetSlot)
    {
        if (mutation == null) return false;
        
        // 1. Mutation must allow this slot
        if ((mutation.allowedSlots & targetSlot) == 0) return false;

        // 2. Creature must actually have matching sockets for this slot
        SocketBinding binding = socketBindings.Find(b => b.slotType == targetSlot);
        return binding.socketTransforms != null && binding.socketTransforms.Length > 0;
    }

    /// <summary>
    /// Returns true if a slot currently has a mutation installed.
    /// </summary>
    public bool IsSlotOccupied(SlotType slot) => equippedMutations.ContainsKey(slot);

    /// <summary>
    /// Returns the active mutation data in a given slot, or null if empty.
    /// </summary>
    public MutationData GetEquippedMutation(SlotType slot)
    {
        equippedMutations.TryGetValue(slot, out var data);
        return data;
    }

    /// <summary>
    /// Attaches visual parts, hooks behaviors, and notifies the creature.
    /// Replaces whatever was previously in the slot.
    /// </summary>
    public void EquipMutation(MutationData mutation, SlotType targetSlot)
    {
        if (!CanEquip(mutation, targetSlot))
        {
            Debug.LogWarning($"[CreatureMutator] Cannot equip {mutation?.mutationName} on {targetSlot} (invalid slot or missing sockets)");
            return;
        }

        // 1. Clear existing part in this slot if present
        UnequipMutation(targetSlot);

        // 2. Find target socket(s)
        SocketBinding binding = socketBindings.Find(b => b.slotType == targetSlot);
        if (binding.socketTransforms == null || binding.socketTransforms.Length == 0) return;

        // 3. Instantiate visuals onto every socket in the binding
        List<GameObject> spawnedParts = new List<GameObject>();
        foreach (Transform socket in binding.socketTransforms)
        {
            if (socket == null || mutation.prefab == null) continue;

            GameObject part = Instantiate(mutation.prefab, socket);
            part.transform.localPosition = Vector3.zero;
            part.transform.localRotation = Quaternion.identity;
            
            // Apply local scale from mutation data, multiplied by the creature's socket scale factor
            part.transform.localScale = Vector3.Scale(Vector3.one * mutation.localScale, Vector3.one * socketScaleFactor);

            // Register any active behaviors (Auras, stingers, lasers)
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

        // 4. Update parent systems
        if (ownerCreature != null) ownerCreature.OnMutationsChanged();
        // if (selectionHighlight != null) selectionHighlight.BuildOutlines();
    }

    /// <summary>
    /// Removes and destroys mutation visuals from a slot.
    /// </summary>
    public void UnequipMutation(SlotType targetSlot)
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

        equippedMutations.Remove(targetSlot);

        if (ownerCreature != null) ownerCreature.OnMutationsChanged();
        // if (selectionHighlight != null) selectionHighlight.BuildOutlines();
    }

    /// <summary>
    /// Removes all equipped mutations across all slots.
    /// </summary>
    public void UnequipAll()
    {
        var slots = new List<SlotType>(equippedMutations.Keys);
        foreach (var slot in slots)
        {
            UnequipMutation(slot);
        }
    }

    /// <summary>
    /// Automatically searches child transforms for "Socket_[SlotName]" conventions,
    /// handling both singular/plural names and Blender's dot formatting (.001, .002).
    /// </summary>
    private void AutoBindChildSockets()
    {
        socketBindings.Clear();

        foreach (SlotType slot in Enum.GetValues(typeof(SlotType)))
        {
            if (slot == SlotType.None) continue;

            List<Transform> foundTransforms = new List<Transform>();
            string slotStr = slot.ToString(); // e.g. "Limbs", "Head", "Back", "Tail"

            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child == transform) continue; // Skip self

                string name = child.name;

                // Direct check for "Socket_Head", "Socket_Back", etc.
                bool isMatch = name.StartsWith($"Socket_{slotStr}", StringComparison.OrdinalIgnoreCase);

                // Special case for Limbs: match Blender's "Socket_Limb.001", "Socket_Limb_L", etc.
                if (!isMatch && slot == SlotType.Limbs)
                {
                    isMatch = name.StartsWith("Socket_Limb.", StringComparison.OrdinalIgnoreCase) ||
                              name.StartsWith("Socket_Limb_", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(name, "Socket_Limb", StringComparison.OrdinalIgnoreCase);
                }

                if (isMatch)
                {
                    foundTransforms.Add(child);
                }
            }

            if (foundTransforms.Count > 0)
            {
                // Sort alphabetically so .001, .002, etc. match predictable socket ordering
                foundTransforms.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

                socketBindings.Add(new SocketBinding
                {
                    slotType = slot,
                    socketTransforms = foundTransforms.ToArray()
                });
            }
        }
    }
}
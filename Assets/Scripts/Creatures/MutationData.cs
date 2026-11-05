using System;
using UnityEngine;

[Serializable]
[Flags]
public enum SlotType
{
    None  = 0,
    Head  = 1 << 0, // 1
    Back  = 1 << 1, // 2
    Limbs = 1 << 2, // 4
    Tail  = 1 << 3  // 8
}

[CreateAssetMenu(fileName = "NewMutation", menuName = "Creatures/Mutation")]
public class MutationData : ScriptableObject
{
    [Header("Identity & Compatibility")]
    public string mutationName;
    [Tooltip("Bitmask of slots where this part can legally attach (e.g. Limbs + Tail for Scythe)")]
    public SlotType allowedSlots; // Uses Flags enum
    public GameObject prefab;
    public Vector3 localScale = Vector3.one;

    [Header("Capabilities Granted")]
    public CreatureCapabilities grantedCapabilities;

    [Header("Stat Modifiers")]
    public int bonusMaxHealth = 0;
    [Range(0f, 1f)] public float damageResistance = 0f; // e.g. 0.2 = 20% damage reduction (Metal Armour)
    public float bonusMoveSpeed = 0f;
    public int bonusAttackDamage = 0;
    public float bonusAttackRange = 0f;       // Tentacles
    public float attackSpeedMultiplier = 1f;  // (e.g., 0.8 = 20% faster)
    public float bonusGuardRadius = 0f;       // Antenna (spot enemies from further away)

    [Header("Attack Replacement (Optional)")]
    public bool overridesAttack;              // True for Laser Eye or Fire Breath
    // public AttackType replacementAttackType;  // Melee, RangedLaser, RangedAoEFire
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;

/// <summary>
/// Controls the 3D preview of a Creature in the UI, allowing players to stage mutations and see their effects before confirming changes.
/// This component manages a separate preview instance of the Creature, applies staged mutations, and updates the UI to reflect pending stat changes. It does not affect the live Creature until the player confirms the changes
/// </summary>
public class CreaturePreviewController : MonoBehaviour
{
    [Header("Stage References")]
    [SerializeField] private Transform previewSpawnPoint;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private RenderTexture previewRenderTexture;
    [SerializeField] private UIDocument uiDocument;

    private GameObject _previewInstance;
    private Creature _targetCreature;
    private int _previewLayer;

    // Tracks staged changes per individual slot (Head, Back, Limbs, Tail)
    private readonly Dictionary<SlotType, MutationData> _pendingMutations = new();

    private void Awake()
    {
        _previewLayer = LayerMask.NameToLayer("UI_Preview");
        if (_previewLayer == -1)
        {
            Debug.LogError("[CreaturePreviewController] Layer 'UI_Preview' not found! Add it in Project Settings > Tags and Layers.");
        }
    }

    private void OnEnable()
    {
        // Set the RenderTexture as the background of the viewport in the UI
        var root = uiDocument.rootVisualElement;
        var viewport = root.Q<VisualElement>("creature-viewport");
        if (viewport != null)
        {
            viewport.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(previewRenderTexture));
        }

        // Register button callbacks for confirming or canceling staged mutations
        root.Q<Button>("btn-confirm")?.RegisterCallback<ClickEvent>(_ => ConfirmChanges());
        root.Q<Button>("btn-cancel")?.RegisterCallback<ClickEvent>(_ => CloseMenu());
        
        // Allow the player to click and drag to rotate the previewed creature 
        bool isDragging = false;
        viewport.RegisterCallback<PointerDownEvent>(_ => isDragging = true);
        viewport.RegisterCallback<PointerUpEvent>(_ => isDragging = false);
        viewport.RegisterCallback<PointerLeaveEvent>(_ => isDragging = false);
        viewport.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (isDragging && previewSpawnPoint != null)
                previewSpawnPoint.Rotate(Vector3.up, -evt.deltaPosition.x * 0.6f, Space.World);
        });
    }

    public void OpenPreviewFor(Creature liveCreature)
    {
        _targetCreature = liveCreature;
        _pendingMutations.Clear();

        if (_previewInstance != null)
            Destroy(_previewInstance);

        // Clone the live creature (including its currently equipped mutations)
        _previewInstance = Instantiate(
            liveCreature.gameObject,
            previewSpawnPoint.position,
            previewSpawnPoint.rotation,
            previewSpawnPoint
        );

        SanitizePreviewClone(_previewInstance);
        SetLayerRecursively(_previewInstance, _previewLayer);

        previewCamera.enabled = true;
        UpdateStatPreviewUI();
    }

    /// <summary>
    /// Stages a mutation on a specific single slot (e.g., SlotType.Limbs) and updates the 3D preview.
    /// </summary>
    public void PreviewMutation(SlotType targetSlot, MutationData mutation)
    {
        if (mutation != null && (mutation.allowedSlots & targetSlot) == 0)
        {
            Debug.LogWarning($"[CreaturePreview] {mutation.mutationName} cannot be equipped in slot {targetSlot}.");
            return;
        }

        _pendingMutations[targetSlot] = mutation;

        if (_previewInstance != null && _previewInstance.TryGetComponent<CreatureMutator>(out var previewMutator))
        {
            previewMutator.EquipMutation(mutation, targetSlot);

            // Re-apply layer so newly instantiated single or pairedPrefab (_L/_R) children render in PreviewCamera
            SetLayerRecursively(_previewInstance, _previewLayer);
        }

        UpdateStatPreviewUI();
    }

    private void UpdateStatPreviewUI()
    {
        var root = uiDocument.rootVisualElement;
        int totalBonusHp = 0;
        int totalBonusDmg = 0;
        float totalBonusSpeed = 0f;

        foreach (var mutation in _pendingMutations.Values)
        {
            if (mutation == null) continue;
            totalBonusHp += mutation.bonusMaxHealth;
            totalBonusDmg += mutation.bonusAttackDamage;
            totalBonusSpeed += mutation.bonusMoveSpeed;
        }

        // Optional UI labels showing pending stat changes before confirmation
        var hpLabel = root.Q<Label>("lbl-bonus-hp");
        if (hpLabel != null) hpLabel.text = totalBonusHp >= 0 ? $"+{totalBonusHp} HP" : $"{totalBonusHp} HP";

        var dmgLabel = root.Q<Label>("lbl-bonus-dmg");
        if (dmgLabel != null) dmgLabel.text = totalBonusDmg >= 0 ? $"+{totalBonusDmg} DMG" : $"{totalBonusDmg} DMG";

        var spdLabel = root.Q<Label>("lbl-bonus-spd");
        if (spdLabel != null) spdLabel.text = totalBonusSpeed >= 0 ? $"+{totalBonusSpeed:0.0} SPD" : $"{totalBonusSpeed:0.0} SPD";
    }

    private void ConfirmChanges()
    {
        if (_targetCreature != null && _targetCreature.TryGetComponent<CreatureMutator>(out var liveMutator))
        {
            foreach (var kvp in _pendingMutations)
            {
                liveMutator.EquipMutation(kvp.Value, kvp.Key);
            }
        }
        CloseMenu();
    }

    public void CloseMenu()
    {
        previewCamera.enabled = false;
        if (_previewInstance != null)
            Destroy(_previewInstance);

        _targetCreature = null;
        _pendingMutations.Clear();
    }

    private void SanitizePreviewClone(GameObject clone)
    {
        // Prevent AI, movement, bobbing, or outline scripts from interfering on the preview stage
        if (clone.TryGetComponent<NavMeshAgent>(out var agent)) agent.enabled = false;
        if (clone.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
        if (clone.TryGetComponent<Creature>(out var creature)) creature.enabled = false;
        if (clone.TryGetComponent<ToyBob>(out var bob)) bob.enabled = false;
        if (clone.TryGetComponent<Selectable>(out var selectable)) selectable.enabled = false;

        // Destroy any existing selection/hover outline components on the clone so it renders cleanly
        foreach (var outline in clone.GetComponentsInChildren<Outline>(true))
        {
            Destroy(outline);
        }
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        if (layer < 0) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }
}
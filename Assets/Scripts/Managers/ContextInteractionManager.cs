using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ContextInteractionManager : MonoBehaviour
{
    [Header("Raycasting Configuration")]
    [Tooltip("Leave unassigned to resolve automatically.")]
    [SerializeField] private Camera isometricCamera;
    [SerializeField] private LayerMask interactableLayers = ~0;
    [SerializeField] private float maxRayDistance = 250f;

    [Header("Click & Drag Filtering")]
    [Tooltip("Maximum allowed hold time (in seconds) to still count as a click.")]
    [SerializeField] private float maxHoldDuration = 0.28f;
    [Tooltip("Maximum pointer movement (in pixels) between press and release.")]
    [SerializeField] private float maxDragDistance = 12f;

#if ENABLE_INPUT_SYSTEM
    [Header("Input System Actions")]
    [SerializeField] private InputActionReference selectActionRef;
    [SerializeField] private InputActionReference pointActionRef;
#endif

    private static readonly List<RaycastResult> s_UiRaycastResults = new(16);

    private PenUIController _penUI;
    private float _pressTime;
    private Vector2 _pressScreenPosition;
    private bool _isTrackingClick;

    private void Reset() => ResolveCamera();

    private void Awake()
    {
        if (isometricCamera == null) ResolveCamera();
        _penUI = FindAnyObjectByType<PenUIController>(FindObjectsInactive.Include);
    }

    private void ResolveCamera()
    {
        if (TryGetComponent(out Camera attachedCamera))
        {
            isometricCamera = attachedCamera;
            return;
        }

        isometricCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
    }

#if ENABLE_INPUT_SYSTEM
    private void OnEnable()
    {
        if (selectActionRef != null && selectActionRef.action != null)
        {
            selectActionRef.action.Enable();
            selectActionRef.action.started += OnSelectStarted;
            selectActionRef.action.canceled += OnSelectCanceled;
        }

        if (pointActionRef != null && pointActionRef.action != null)
        {
            pointActionRef.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (selectActionRef != null && selectActionRef.action != null)
        {
            selectActionRef.action.started -= OnSelectStarted;
            selectActionRef.action.canceled -= OnSelectCanceled;
            selectActionRef.action.Disable();
        }

        if (pointActionRef != null && pointActionRef.action != null)
        {
            pointActionRef.action.Disable();
        }

        _isTrackingClick = false;
    }

    private void OnSelectStarted(InputAction.CallbackContext context)
    {
        RecordPointerDown();
    }

    private void OnSelectCanceled(InputAction.CallbackContext context)
    {
        EvaluatePointerUp();
    }

#else
    private void Update()
    {
        if (Input.GetButtonDown("Select") || Input.GetMouseButtonDown(1))
        {
            RecordPointerDown();
        }

        if (_isTrackingClick && (Input.GetButtonUp("Select") || Input.GetMouseButtonUp(1)))
        {
            EvaluatePointerUp();
        }
    }
#endif

    private void RecordPointerDown()
    {
        if (_penUI != null && _penUI.IsOpen)
        {
            _isTrackingClick = false;
            return;
        }

        Vector2 screenPos = GetPointerScreenPosition();

        if (IsPointerOverUIElement(screenPos))
        {
            _isTrackingClick = false;
            return;
        }

        // If a Farmer is currently selected and the right-click started on a Barn,
        // PlayerControls is sending the Farmer inside on pointer-down—do not open the Barn menu on pointer-up.
        if (isometricCamera != null)
        {
            Ray ray = isometricCamera.ScreenPointToRay(screenPos);
            if (MouseTarget.Find(ray, out RaycastHit hit, maxRayDistance, interactableLayers))
            {
                var order = Order.From(ray, hit);
                if (order.kind == OrderKind.Barn && Selectable.All.Any(s => s.Selected && s.CanObey(order)))
                {
                    _isTrackingClick = false;
                    return;
                }
            }
        }

        _pressTime = Time.unscaledTime;
        _pressScreenPosition = screenPos;
        _isTrackingClick = true;
    }

    private void EvaluatePointerUp()
    {
        if (!_isTrackingClick) return;
        _isTrackingClick = false;

        if (_penUI != null && _penUI.IsOpen) return;

        Vector2 releasePos = GetPointerScreenPosition();
        float holdDuration = Time.unscaledTime - _pressTime;
        float dragDistance = Vector2.Distance(releasePos, _pressScreenPosition);

        if (holdDuration > maxHoldDuration || dragDistance > maxDragDistance)
        {
            return;
        }

        ExecuteRaycastInteraction(releasePos);
    }

    private void ExecuteRaycastInteraction(Vector2 screenPos)
    {
        if (isometricCamera == null) return;
        if (IsPointerOverUIElement(screenPos)) return;
        if (_penUI != null && _penUI.IsOpen) return;

        Ray ray = isometricCamera.ScreenPointToRay(screenPos);

        Component menuTarget = null;
        Transform anchorTransform = null;

        // 1. Check renderer bounds first for broken buildings whose colliders are disabled
        Buildable brokenTarget = Order.BrokenAt(ray);
        if (brokenTarget != null)
        {
            menuTarget = brokenTarget;
            anchorTransform = brokenTarget.transform;
        }
        else if (MouseTarget.Find(ray, out RaycastHit hit, maxRayDistance, interactableLayers))
        {
            // Fall back to MouseTarget.Building so clicking the open ground inside a pen still targets the pen
            menuTarget = MouseTarget.Menu(ray, hit) ?? MouseTarget.Building(ray, hit);
            anchorTransform = menuTarget != null ? menuTarget.transform : hit.transform;
        }

        if (menuTarget != null)
        {
            string menuTitle = menuTarget.name.ToUpper();

            // Broken buildings only show the Repair menu if a repair-capable unit is currently selected
            if (menuTarget is Buildable buildable && buildable.IsBroken)
            {
                var repairer = Selectable.All.FirstOrDefault(s => s.Selected && s.CanObey(Order.ForRepair(buildable)));
                if (repairer == null)
                {
                    ContextMenuEventBus.RequestClose();
                    return;
                }

                int woodCost = buildable.RepairWoodCost;
                bool hasWood = ResourceManager.Count(ResourceType.Wood) >= woodCost;

                var repairActions = new List<ContextActionData>
                {
                    new ContextActionData(
                        label: $"Repair ({woodCost} <sprite name=\"Wood\">)",
                        onExecute: () =>
                        {
                            if (hasWood)
                                repairer.Command(Order.ForRepair(buildable));
                            ContextMenuEventBus.RequestClose();
                        },
                        textColor: hasWood ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f),
                        buttonTint: hasWood ? new Color(0.18f, 0.28f, 0.20f, 1f) : new Color(0.25f, 0.25f, 0.25f, 0.7f)
                    )
                };

                ContextMenuEventBus.RequestOpen(repairActions, anchorTransform, menuTitle, "Requires Repairs");
                return;
            }

            if (menuTarget is IContextInteractable interactable)
            {
                IReadOnlyList<ContextActionData> actions = interactable.GetContextActions();

                if (actions != null && actions.Count == 1)
                {
                    actions[0].onExecute?.Invoke();
                    ContextMenuEventBus.RequestClose();
                    return;
                }

                if (actions != null && actions.Count > 0)
                {
                    ContextMenuEventBus.RequestOpen(actions, anchorTransform, menuTitle, "Operational");
                    return;
                }
            }
        }

        ContextMenuEventBus.RequestClose();
    }

    private static bool IsPointerOverUIElement(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        s_UiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, s_UiRaycastResults);

        return s_UiRaycastResults.Count > 0;
    }

    private Vector2 GetPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        if (pointActionRef != null && pointActionRef.action != null)
        {
            return pointActionRef.action.ReadValue<Vector2>();
        }

        if (Pointer.current != null)
        {
            return Pointer.current.position.ReadValue();
        }

        return Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }
}
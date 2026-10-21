using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

#if ENABLE_INPUT_SYSTEM
    [Header("Input System Actions")]
    [SerializeField] private InputActionReference selectActionRef;
    [SerializeField] private InputActionReference pointActionRef;
#endif

    private static readonly List<RaycastResult> s_UiRaycastResults = new(16);

    private void Reset() => ResolveCamera();

    private void Awake()
    {
        if (isometricCamera == null) ResolveCamera();
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
            selectActionRef.action.performed += OnSelectPerformed;
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
            selectActionRef.action.performed -= OnSelectPerformed;
            selectActionRef.action.Disable();
        }

        if (pointActionRef != null && pointActionRef.action != null)
        {
            pointActionRef.action.Disable();
        }
    }

    private void OnSelectPerformed(InputAction.CallbackContext context)
    {
        ExecuteRaycastInteraction();
    }
#else
    private void Update()
    {
        if (Input.GetButtonDown("Select") || Input.GetMouseButtonDown(1))
        {
            ExecuteRaycastInteraction();
        }
    }
#endif

    private void ExecuteRaycastInteraction()
    {
        if (isometricCamera == null) return;

        Vector2 screenPos = GetPointerScreenPosition();

        // Immediate graphic raycast avoids the EventSystem timing warning
        if (IsPointerOverUIElement(screenPos))
        {
            return;
        }

        Ray ray = isometricCamera.ScreenPointToRay(screenPos);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, interactableLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<IContextInteractable>() is { } interactable)
            {
                IReadOnlyList<ContextActionData> actions = interactable.GetContextActions();
                if (actions != null && actions.Count > 0)
                {
                    ContextMenuEventBus.RequestOpen(actions, screenPos);
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
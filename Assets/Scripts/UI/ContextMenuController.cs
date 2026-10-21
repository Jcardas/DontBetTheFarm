using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class ContextMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas parentCanvas;
    [SerializeField] private RectTransform menuRect;
    [SerializeField] private RectTransform buttonContainer;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private ContextButtonUI buttonPrefab;

    [Header("Spawning & Clamping")]
    [Tooltip("Pixel offset to the right/bottom of the mouse cursor in reference resolution space.")]
    [SerializeField] private Vector2 cursorOffset = new(0, 0f);
    [Tooltip("Minimum padding from the edges of the screen/canvas.")]
    [SerializeField] private Vector2 viewportPadding = new(12f, 12f);

    [Header("Animation Settings")]
    [SerializeField, Min(0.01f)] private float openDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float closeDuration = 0.12f;

    [Tooltip("Overshoot/elastic curve for scale pop-in (evaluates 0 to 1 over openDuration).")]
    [SerializeField] private AnimationCurve openScaleCurve = new(
        new Keyframe(0f, 0.5f, 0f, 4.5f),
        new Keyframe(0.65f, 1.08f, 0f, 0f),
        new Keyframe(1f, 1f, -0.5f, 0f)
    );

    [SerializeField] private AnimationCurve openFadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve closeScaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.85f);
    [SerializeField] private AnimationCurve closeFadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private readonly List<ContextButtonUI> _buttonPool = new();
    private Coroutine _animationCoroutine;
    private Camera _uiCamera;
    private RectTransform _canvasRect;
    private bool _isOpen;
    private int _openedFrame = -1;

    private void Reset()
    {
        menuRect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        parentCanvas = GetComponentInParent<Canvas>();
        buttonContainer = menuRect;
    }

    private void Awake()
    {
        if (menuRect == null) menuRect = GetComponent<RectTransform>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
        if (buttonContainer == null) buttonContainer = menuRect;

        _canvasRect = parentCanvas.GetComponent<RectTransform>();
        _uiCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;

        // Enforce top-right pivot(0, 1) so the menu expands downward and to the right of the cursor
        menuRect.pivot = new Vector2(0f, 1f);

        SetHiddenStateImmediate();
    }

    private void OnEnable()
    {
        ContextMenuEventBus.OnRequestOpen += HandleOpenRequest;
        ContextMenuEventBus.OnRequestClose += CloseMenu;
    }

    private void OnDisable()
    {
        ContextMenuEventBus.OnRequestOpen -= HandleOpenRequest;
        ContextMenuEventBus.OnRequestClose -= CloseMenu;
    }

    private void Update()
    {
        if (!_isOpen) return;

        if (WasEscapePressedThisFrame())
        {
            CloseMenu();
            return;
        }

        // Ignore the exact frame the menu was opened so the spawning click doesn't immediately dismiss it
        if (Time.frameCount > _openedFrame && WasAnyMouseButtonDownThisFrame())
        {
            Vector2 pointerPos = GetPointerScreenPosition();
            if (!RectTransformUtility.RectangleContainsScreenPoint(menuRect, pointerPos, _uiCamera))
            {
                CloseMenu();
            }
        }
    }

    public void HandleOpenRequest(IReadOnlyList<ContextActionData> actions, Vector2 screenPosition)
    {
        if (actions == null || actions.Count == 0)
        {
            CloseMenu();
            return;
        }

        _isOpen = true;
        _openedFrame = Time.frameCount;

        PopulateButtons(actions);

        // Force layout rebuild immediately so menuRect.rect reflects the new button count this frame
        LayoutRebuilder.ForceRebuildLayoutImmediate(buttonContainer);
        if (buttonContainer != menuRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(menuRect);
        }

        PositionAndClampMenu(screenPosition);

        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        _animationCoroutine = StartCoroutine(AnimateMenu(opening: true));
    }

    public void CloseMenu()
    {
        if (!_isOpen) return;
        _isOpen = false;

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        if (_animationCoroutine != null) StopCoroutine(_animationCoroutine);
        _animationCoroutine = StartCoroutine(AnimateMenu(opening: false));
    }

    private void PopulateButtons(IReadOnlyList<ContextActionData> actions)
    {
        EnsurePoolCapacity(actions.Count);

        for (int i = 0; i < _buttonPool.Count; i++)
        {
            ContextButtonUI buttonUI = _buttonPool[i];
            if (i < actions.Count)
            {
                ContextActionData actionData = actions[i];
                buttonUI.gameObject.SetActive(true);

                buttonUI.Initialize(actionData, () =>
                {
                    // Execute action and immediately close menu
                    actionData.onExecute?.Invoke();
                    CloseMenu();
                });
            }
            else
            {
                buttonUI.gameObject.SetActive(false);
            }
        }
    }

    private void EnsurePoolCapacity(int requiredCount)
    {
        while (_buttonPool.Count < requiredCount)
        {
            ContextButtonUI instance = Instantiate(buttonPrefab, buttonContainer);
            instance.gameObject.SetActive(false);
            _buttonPool.Add(instance);
        }
    }

    private void PositionAndClampMenu(Vector2 screenPosition)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, screenPosition, _uiCamera, out Vector2 localPoint))
        {
            return;
        }

        // Apply cursor offset (to the left of the cursor)
        Vector2 targetPosition = localPoint + cursorOffset;

        Rect canvasBounds = _canvasRect.rect;
        Vector2 menuSize = menuRect.rect.size;

        // With pivot at (0, 1):
        // Left edge is targetPosition.x, Right edge is targetPosition.x + menuSize.x
        // Top edge is targetPosition.y, Bottom edge is targetPosition.y - menuSize.y
        float minX = canvasBounds.xMin + viewportPadding.x;
        float maxX = canvasBounds.xMax - menuSize.x - viewportPadding.x;

        float minY = canvasBounds.yMin + menuSize.y + viewportPadding.y;
        float maxY = canvasBounds.yMax - viewportPadding.y;

        // Guard against extreme cases where menu is larger than canvas
        targetPosition.x = maxX >= minX ? Mathf.Clamp(targetPosition.x, minX, maxX) : minX;
        targetPosition.y = maxY >= minY ? Mathf.Clamp(targetPosition.y, minY, maxY) : maxY;

        menuRect.anchoredPosition = targetPosition;
    }

    private IEnumerator AnimateMenu(bool opening)
    {
        float duration = opening ? openDuration : closeDuration;
        AnimationCurve scaleCurve = opening ? openScaleCurve : closeScaleCurve;
        AnimationCurve fadeCurve = opening ? openFadeCurve : closeFadeCurve;

        if (opening)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);

            float scaleValue = scaleCurve.Evaluate(normalizedTime);
            float alphaValue = fadeCurve.Evaluate(normalizedTime);

            menuRect.localScale = new Vector3(scaleValue, scaleValue, 1f);
            canvasGroup.alpha = alphaValue;

            yield return null;
        }

        float finalScale = scaleCurve.Evaluate(1f);
        menuRect.localScale = new Vector3(finalScale, finalScale, 1f);
        canvasGroup.alpha = fadeCurve.Evaluate(1f);

        if (!opening)
        {
            SetHiddenStateImmediate();
        }

        _animationCoroutine = null;
    }

    private void SetHiddenStateImmediate()
    {
        _isOpen = false;
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        menuRect.localScale = Vector3.zero;
    }

    #region Input Abstraction (Legacy & New Input System Compatible)

    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    private static bool WasAnyMouseButtonDownThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return false;
        return Mouse.current.leftButton.wasPressedThisFrame ||
               Mouse.current.rightButton.wasPressedThisFrame ||
               Mouse.current.middleButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
#endif
    }

    private static Vector2 GetPointerScreenPosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    #endregion
}
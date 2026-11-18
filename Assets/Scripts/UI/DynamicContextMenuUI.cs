using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class DynamicContextMenuUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private ContextActionButton buttonPrefab;

    [Header("Dynamic Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;

    [Header("Disabled Button Styling")]
    [SerializeField] private Color disabledBackgroundColor = new(0.28f, 0.28f, 0.28f, 0.85f);
    [SerializeField] private Color disabledTextColor = new(0.65f, 0.65f, 0.65f, 1f);

    [Header("Sizing & 3D Tracking")]
    [SerializeField] private float menuScale = 1.35f;
    [SerializeField] private Vector3 worldOffset = new Vector3(0, 3f, 0);

    private Transform currentTarget;
    private RectTransform panelRect;
    private Camera cam;
    private int openedOnFrame = -1;

    private void Awake()
    {
        cam = Camera.main;
        if (panelRoot != null)
        {
            panelRect = panelRoot.GetComponent<RectTransform>();
            panelRoot.transform.localScale = Vector3.one * menuScale;
        }
    }

    private void OnEnable()
    {
        ContextMenuEventBus.OnRequestOpen += OpenMenu;
        ContextMenuEventBus.OnRequestClose += CloseMenu;
    }

    private void OnDisable()
    {
        ContextMenuEventBus.OnRequestOpen -= OpenMenu;
        ContextMenuEventBus.OnRequestClose -= CloseMenu;
    }

    private void Start() => CloseMenu();

    private void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;

        if (UIBlocker.IsModalUIOpen ||
            (BuildingManager.Instance != null && BuildingManager.Instance.IsBusy) ||
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame))
        {
            CloseMenu();
            return;
        }

        if (Time.frameCount > openedOnFrame && Mouse.current != null)
        {
            bool leftDown = Mouse.current.leftButton.wasPressedThisFrame;
            bool middleDown = Mouse.current.middleButton.wasPressedThisFrame;

            if (leftDown || middleDown)
            {
                Vector2 pointerPos = Mouse.current.position.ReadValue();
                bool insidePanel = panelRect != null &&
                                   RectTransformUtility.RectangleContainsScreenPoint(panelRect, pointerPos, null);
                if (!insidePanel)
                {
                    CloseMenu();
                    return;
                }
            }
        }

        if (currentTarget != null && cam != null)
        {
            panelRoot.transform.position = cam.WorldToScreenPoint(currentTarget.position + worldOffset);
        }
    }

    private void OpenMenu(IReadOnlyList<ContextActionData> actions, Transform targetTransform, string title, string subtitle)
    {
        currentTarget = targetTransform;
        openedOnFrame = Time.frameCount;

        if (panelRoot != null)
        {
            panelRoot.transform.localScale = Vector3.one * menuScale;
            panelRoot.SetActive(true);
        }

        if (titleText != null) titleText.text = title;
        if (subtitleText != null) subtitleText.text = subtitle;

        foreach (Transform child in buttonContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (ContextActionData actionData in actions)
        {
            ContextActionButton newButton = Instantiate(buttonPrefab, buttonContainer);
            var callback = actionData.onExecute;
            bool isEnabled = callback != null;

            newButton.Setup(actionData.label, isEnabled
                ? () =>
                {
                    callback.Invoke();
                    CloseMenu();
                }
                : null);

            if (!isEnabled)
            {
                ApplyDisabledStyle(newButton);
            }
        }
    }

    private void ApplyDisabledStyle(ContextActionButton actionButton)
    {
        if (actionButton == null) return;

        if (actionButton.TryGetComponent(out Button btn))
        {
            btn.interactable = false;
            var colors = btn.colors;
            colors.disabledColor = disabledBackgroundColor;
            btn.colors = colors;
        }

        if (actionButton.TryGetComponent(out Image bg))
        {
            bg.color = disabledBackgroundColor;
        }

        TMP_Text label = actionButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = disabledTextColor;
        }
    }

    private void CloseMenu()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        currentTarget = null;
    }
}
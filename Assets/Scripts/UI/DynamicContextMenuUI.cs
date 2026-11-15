using System.Collections.Generic;
using UnityEngine;
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

    [Header("3D Tracking")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0, 3f, 0); // Makes the menu float above the building

    private Transform currentTarget;
    private Camera cam;

    private void Awake()
    {
        cam = Camera.main; // Grab the main camera to calculate 3D-to-2D space
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
        // If the menu is open, constantly glue it to the 3D building's position on screen
        if (panelRoot.activeSelf && currentTarget != null && cam != null)
        {
            panelRoot.transform.position = cam.WorldToScreenPoint(currentTarget.position + worldOffset);
        }
    }

    private void OpenMenu(IReadOnlyList<ContextActionData> actions, Transform targetTransform, string title, string subtitle)
    {
        currentTarget = targetTransform;
        panelRoot.SetActive(true);

        if (titleText != null) titleText.text = title;
        if (subtitleText != null) subtitleText.text = subtitle;

        foreach (Transform child in buttonContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (ContextActionData actionData in actions)
        {
            ContextActionButton newButton = Instantiate(buttonPrefab, buttonContainer);
            newButton.Setup(actionData.label, actionData.onExecute);
        }
    }

    private void CloseMenu()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        currentTarget = null; // Clear the target when closed
    }
}
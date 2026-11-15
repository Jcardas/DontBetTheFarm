using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public static class UIBlocker
{
    private static readonly List<RaycastResult> s_UguiResults = new(16);
    private static readonly List<UIDocument> s_CachedDocs = new(8);
    private static float s_NextDocRefreshTime;

    private static PenUIController s_PenUI;
    private static BarnResearchUIController s_BarnUI;

    /// <summary>
    /// True when a full-screen / modal menu is open (Pen UI, Barn Research UI, etc.).
    /// </summary>
    public static bool IsModalUIOpen
    {
        get
        {
            if (s_PenUI == null)
                s_PenUI = Object.FindAnyObjectByType<PenUIController>(FindObjectsInactive.Include);
            if (s_BarnUI == null)
                s_BarnUI = Object.FindAnyObjectByType<BarnResearchUIController>(FindObjectsInactive.Include);

            return (s_PenUI != null && s_PenUI.IsOpen) ||
                   (s_BarnUI != null && s_BarnUI.IsMenuOpen);
        }
    }

    /// <summary>
    /// True if a modal menu is open OR the pointer is currently over any interactive UI Toolkit or uGUI element.
    /// </summary>
    public static bool IsPointerOverUI()
    {
        Vector2 screenPos = Pointer.current != null
            ? Pointer.current.position.ReadValue()
            : (Vector2)Input.mousePosition;

        return IsPointerOverUI(screenPos);
    }

    public static bool IsPointerOverUI(Vector2 screenPos)
    {
        if (IsModalUIOpen)
            return true;

        if (IsPointerOverUIToolkit(screenPos))
            return true;

        return IsPointerOverUGUI(screenPos);
    }

    private static bool IsPointerOverUIToolkit(Vector2 screenPos)
    {
        if (Time.unscaledTime >= s_NextDocRefreshTime)
        {
            s_CachedDocs.Clear();
            s_CachedDocs.AddRange(Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None));
            s_NextDocRefreshTime = Time.unscaledTime + 0.5f;
        }

        // Convert Input System screen coordinates (bottom-left origin) to UI Toolkit panel coordinates (top-left origin)
        Vector2 topLeftScreenPos = new Vector2(screenPos.x, Screen.height - screenPos.y);

        for (int i = 0; i < s_CachedDocs.Count; i++)
        {
            UIDocument doc = s_CachedDocs[i];
            if (doc == null || !doc.isActiveAndEnabled) continue;

            VisualElement root = doc.rootVisualElement;
            if (root == null || root.panel == null) continue;
            if (root.resolvedStyle.display == DisplayStyle.None) continue;

            Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, topLeftScreenPos);
            VisualElement picked = root.panel.Pick(panelPos);

            if (picked != null)
                return true;
        }

        return false;
    }

    private static bool IsPointerOverUGUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };

        s_UguiResults.Clear();
        EventSystem.current.RaycastAll(pointerData, s_UguiResults);
        return s_UguiResults.Count > 0;
    }
}
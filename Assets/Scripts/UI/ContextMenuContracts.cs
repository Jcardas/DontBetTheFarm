using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Data payload consumed directly by ContextButtonUI.
/// Must remain a class to satisfy the reference null-checks in ContextButtonUI.
/// </summary>
[Serializable]
public class ContextActionData
{
    [Header("Content")]
    public string label = "Action";
    public Sprite icon;

    [Header("Styling")]
    public Color textColor = Color.white;
    public Sprite customBackground;
    public Color buttonTint = Color.white;

    /// <summary>
    /// Runtime callback invoked when the player selects this context option.
    /// </summary>
    [NonSerialized]
    public UnityAction onExecute;

    public ContextActionData() { }

    public ContextActionData(
        string label,
        UnityAction onExecute,
        Sprite icon = null,
        Color? textColor = null,
        Color? buttonTint = null,
        Sprite customBackground = null)
    {
        this.label = label;
        this.onExecute = onExecute;
        this.icon = icon;
        this.textColor = textColor ?? Color.white;
        this.buttonTint = buttonTint ?? Color.white;
        this.customBackground = customBackground;
    }
}

/// <summary>
/// Implement on any world collider in the isometric scene that supports a context menu.
/// </summary>
public interface IContextInteractable
{
    /// <summary>
    /// Returns the list of actions available for this object in its current state.
    /// Return null or an empty collection to suppress the menu.
    /// </summary>
    IReadOnlyList<ContextActionData> GetContextActions();
}

/// <summary>
/// Zero-dependency static event bus decoupling 3D isometric raycasters from the UI Canvas.
/// </summary>
public static class ContextMenuEventBus
{
    // Changed Vector2 to Transform
    public static event Action<IReadOnlyList<ContextActionData>, Transform, string, string> OnRequestOpen;
    public static event Action OnRequestClose;

    public static void RequestOpen(IReadOnlyList<ContextActionData> actions, Transform targetTransform, string title, string subtitle)
    {
        OnRequestOpen?.Invoke(actions, targetTransform, title, subtitle);
    }

    public static void RequestClose() => OnRequestClose?.Invoke();
}
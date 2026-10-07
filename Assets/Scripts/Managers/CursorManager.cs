using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enumeration of different cursor types that can be used in the game. Each type corresponds to a specific cursor texture and behavior.
/// </summary>
public enum CursorType
{
    Default,
    Grab,
    EdgeScrollWest,
    EdgeScrollEast,
    EdgeScrollNorth,
    EdgeScrollSouth,
    EdgeScrollNorthWest,
    EdgeScrollNorthEast,
    EdgeScrollSouthWest,
    EdgeScrollSouthEast,
    Attack,
    Interact,
    Harvest,
    Chop
}

/// <summary>
/// Manages the cursor appearance and behavior in the game.
/// This class allows for changing the cursor texture based on different game contexts,
/// such as when hovering over interactive objects or during specific actions.
/// It uses a singleton pattern to ensure only one instance exists and provides a public method to set the cursor type.
/// 
/// </summary>
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    /// <summary>
    /// Struct to hold data for each cursor type, including the texture and hotspot position.
    /// </summary>
    [System.Serializable]
    public struct CursorData
    {
        public CursorType type;
        public Texture2D texture;
        
        // The hotspot is the point within the cursor texture that will be used as the click point
        public Vector2 hotspot;
    }

    // List of cursor data that can be set in the Unity Inspector
    [SerializeField] private List<CursorData> cursorList;
    private Dictionary<CursorType, CursorData> _cursorDict;
    
    // The current cursor type being used. Initialized to an invalid value to ensure it gets set on first use.
    private CursorType _currentCursorType = (CursorType)(-1);

    private void Awake()
    {
        // Singleton pattern to ensure only one instance of CursorManager exists
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Initialize the cursor dictionary for quick lookup
        _cursorDict = new Dictionary<CursorType, CursorData>();
        foreach (var data in cursorList)
        {
            _cursorDict[data.type] = data;
        }
        
        // Set the default cursor at the start
        SetCursor(CursorType.Default);
    }

    /// <summary>
    /// Sets the cursor to the specified type. If the type is Default or not found in the dictionary,
    /// it resets to the default system cursor.
    ///
    /// This is a public method that can be called from other scripts to change the cursor based on game context,
    /// such as when hovering over interactive objects or during specific actions.
    /// </summary>
    /// <param name="type"> The type of cursor to set. </param>
    public void SetCursor(CursorType type)
    {
        if (_currentCursorType == type)
        {
            // If the requested cursor type is already set, do nothing
            return;
        }
        
        // Check if the cursor type exists in the dictionary and has a valid texture
        if (_cursorDict.TryGetValue(type, out CursorData data) && data.texture != null)
        {
            // Set the cursor to the specified texture and hotspot
            Cursor.SetCursor(data.texture, data.hotspot, CursorMode.Auto);
            _currentCursorType = type;
            return;
        }

        // Fall back to system default cursor if type wasn't configured or texture is null
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        _currentCursorType = type;
    }
}
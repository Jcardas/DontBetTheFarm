using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

public enum ResourceType { Wood, Hay, Technology }

[ExecuteAlways]
public class ResourceManager : MonoBehaviour
{
    [SerializeField] private TMP_Text woodValue;
    [SerializeField] private TMP_Text hayValue;
    [SerializeField] private TMP_Text technologyValue;
    public static ResourceManager Instance { get; private set; }
    
    static readonly List<ResourceManager> all = new();
    static readonly Dictionary<ResourceType, int> totals = new();

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            BindResourceTexts();
            return;
        }

        Body = GetComponentInChildren<Collider>();
        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BindResourceTexts();
        
        totals[ResourceType.Wood] = 5;
        totals[ResourceType.Hay] = 5;
        totals[ResourceType.Technology] = 0;
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        UpdateResourceHUD();
    }

    // Clears totals between plays even when "Reload Domain" is turned off in Enter Play Mode settings.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetTotals() => totals.Clear();

    public Collider Body { get; private set; }

    void OnEnable()
    {
        BindResourceTexts();

        if (Application.isPlaying)
            all.Add(this);
    }

    void OnDisable()
    {
        if (Application.isPlaying)
            all.Remove(this);
    }

    private void BindResourceTexts()
    {
        if (woodValue != null && hayValue != null && technologyValue != null)
            return;

        TMP_Text[] allTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool changed = false;

        foreach (TMP_Text tmp in allTexts)
        {
            if (woodValue == null && tmp.gameObject.name == "WoodValueText")
            {
                woodValue = tmp;
                changed = true;
            }
            else if (hayValue == null && tmp.gameObject.name == "HayValueText")
            {
                hayValue = tmp;
                changed = true;
            }
            else if (technologyValue == null && tmp.gameObject.name == "TechnologyValueText")
            {
                technologyValue = tmp;
                changed = true;
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying && changed)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    public static int Count(ResourceType type) => totals.TryGetValue(type, out int n) ? n : 0;

    public void Deposit(ResourceType type, int amount) => totals[type] = Count(type) + amount;

    public static ResourceManager Nearest(Vector3 from) =>
        all.Where(s => s != null)
           .OrderBy(s => s.Body != null ? Reach.Gap(from, s.Body)
                                        : Vector3.Distance(from, s.transform.position))
           .FirstOrDefault();
       
    public static bool Spend(ResourceType type, int amount)
    {
        Debug.Log("Spending " + amount + " " + type);
        
        if (amount < 0 || Count(type) < amount)
            return false;

        totals[type] = Count(type) - amount;
        Debug.Log(type + " remaining: " + totals[type]);

        return true;
    }
    
    void UpdateResourceHUD()
    {
        if (woodValue != null) woodValue.text = Count(ResourceType.Wood).ToString();
        if (hayValue != null) hayValue.text = Count(ResourceType.Hay).ToString();
        if (technologyValue != null) technologyValue.text = Count(ResourceType.Technology).ToString();
    }

    public static int GetResourceAmount(ResourceType type)
    {
        return Count(type);
    }
}
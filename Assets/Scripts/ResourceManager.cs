using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Where gatherers drop off what they carry (the barn). Every stockpile adds to one shared total.
// Needs a collider, on it or a child: gatherers measure their reach to it.
// Put storage close to the resources: the paper prototype found long hauls are the inefficient part.
public class ResourceManager : MonoBehaviour
{
    static readonly List<ResourceManager> all = new();
    static readonly Dictionary<ResourceType, int> totals = new();

    // Clears totals between plays even when "Reload Domain" is turned off in Enter Play Mode settings.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetTotals() => totals.Clear();

    public Collider Body { get; private set; }

    void Awake() => Body = GetComponentInChildren<Collider>();
    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    public static int Count(ResourceType type) => totals.TryGetValue(type, out int n) ? n : 0;

    public void Deposit(ResourceType type, int amount) => totals[type] = Count(type) + amount;

    public static ResourceManager Nearest(Vector3 from) =>
        all.OrderBy(s => Reach.Gap(from, s.Body)).FirstOrDefault();

    public static bool Spend(ResourceType type, int amount)
    {
        if (amount < 0 || Count(type) < amount)
            return false;

        totals[type] -= amount;
        Debug.Log(type + " remaining: " + totals[type]);

        return true;
    }

    // Placeholder readout until the real HUD exists.
    void OnGUI()
    {
        string text = string.Join("   ", Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().Select(t => $"{t} {Count(t)}"));
        GUI.Label(new Rect(10, 10, 400, 24), text);
    }
}

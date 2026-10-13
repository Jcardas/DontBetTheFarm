using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ResourceType { Wood, Hay }

// Something gatherers harvest, like a tree. When used up it shrinks to a sapling and grows back
// (design: trees regrow, but slower than a sawmill). Needs a collider, on it or a child, so it can be clicked and reached.
public class ResourceNode : MonoBehaviour
{
    static readonly List<ResourceNode> all = new();

    public ResourceType type = ResourceType.Wood;
    public int amount = 20;
    public float regrowSeconds = 30f;

    int left;
    float emptiedAt;
    Vector3 fullScale;

    public bool Empty => left <= 0;
    public Collider Body { get; private set; }

    void Awake()
    {
        left = amount;
        fullScale = transform.localScale;
        Body = GetComponentInChildren<Collider>();
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    // Removes up to `wanted` and returns how much was actually taken.
    public int Take(int wanted)
    {
        int taken = Mathf.Min(wanted, left);
        left -= taken;
        if (Empty) emptiedAt = Time.time;
        return taken;
    }

    void Update()
    {
        if (!Empty) return;
        float grown = (Time.time - emptiedAt) / regrowSeconds;
        transform.localScale = fullScale * Mathf.Lerp(0.35f, 1f, grown);
        if (grown >= 1f) left = amount;
    }

    public static ResourceNode Nearest(Vector3 from, ResourceType type) =>
        all.Where(n => n.type == type && !n.Empty).OrderBy(n => Reach.Gap(from, n.Body)).FirstOrDefault();
}

using UnityEngine;

// A short-lived disc where the player just gave an order, so every click visibly lands somewhere.
// It shrinks away over `seconds`, coloured by what the order was.
public class OrderMarker : MonoBehaviour
{
    public float seconds = 0.6f;
    public Color move = new(1f, 0.85f, 0.1f);
    public Color attack = new(0.9f, 0.15f, 0.1f);
    [Tooltip("Work and harvest orders.")]
    public Color work = new(0.3f, 0.85f, 0.25f);

    Vector3 fullScale;
    float shownAt;

    // A property block instead of .material, so each marker doesn't leak a material copy when it is destroyed.
    public void Show(OrderKind kind)
    {
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", kind switch { OrderKind.Move => move, OrderKind.Attack => attack, _ => work });
        foreach (var r in GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(block);
    }

    void Start()
    {
        fullScale = transform.localScale;
        shownAt = Time.time;
    }

    void Update()
    {
        float t = (Time.time - shownAt) / seconds;
        if (t >= 1f) Destroy(gameObject);
        else transform.localScale = fullScale * (1f - t);
    }
}

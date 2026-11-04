using UnityEngine;

// Anything a creature can fight: other creatures and farm buildings.
public interface IDamageable
{
    Collider Body { get; }
    bool Down { get; }
    Vector3 Position { get; }
    void TakeDamage(int amount);
}

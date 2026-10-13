using UnityEngine;


///
/// A base class for all creatures, including the farmer, aliens and animals.
/// It is a MonoBehaviour so it can be attached to a GameObject in the scene,
/// but it does not have any functionality on its own.
/// It is meant to be inherited by other classes that implement specific creature behaviors.
///
public class Creature : MonoBehaviour
{
    /// <summary>
    /// The name of the creature.
    /// </summary>
    public string name;

    /// <summary>
    /// The health of the creature.
    /// </summary>
    public int health;

    /// <summary>
    /// The speed of the creature.
    /// </summary>
    public float speed;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

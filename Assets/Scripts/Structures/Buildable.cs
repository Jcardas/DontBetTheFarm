using UnityEngine;

public class Buildable : MonoBehaviour
{
    [Header("Buildable Settings")]
    [SerializeField] protected string buildableName;

    public virtual void Build()
    {
        Debug.Log(buildableName + " has been built.");
    }

    public virtual void Remove()
    {
        Debug.Log(buildableName + " has been removed.");

        Destroy(gameObject);
    }
}
using UnityEngine;
using UnityEngine.AI;

// Sandbox testing aid: a "Spawn alien" button in the top-right corner. Each click drops an alien a few metres
// from the farmer in a random direction, close enough that it picks a fight right away.
public class AlienSpawnButton : MonoBehaviour
{
    public GameObject alien;
    [Tooltip("Metres from the farmer. Keep it inside the alien's guard radius (8) so it engages.")]
    public float distance = 6f;

    void OnGUI()
    {
        if (!alien || !GUI.Button(new Rect(Screen.width - 130, 10, 120, 30), "Spawn alien")) return;
        var farmer = FindAnyObjectByType<Farmer>();
        Vector2 offset = Random.insideUnitCircle.normalized * distance;
        Vector3 spot = (farmer ? farmer.transform.position : Vector3.zero) + new Vector3(offset.x, 0f, offset.y);
        if (NavMesh.SamplePosition(spot, out NavMeshHit hit, distance, NavMesh.AllAreas)) spot = hit.position;
        Instantiate(alien, spot, Quaternion.identity);
    }
}

using UnityEngine;

// Holds the saucer's interior parking spot and exterior doorway threshold on the barn root, and forwards door
// commands to whichever barn visual state is currently active.
public class BarnHangar : MonoBehaviour
{
    [Tooltip("Inside the barn where the saucer parks.")]
    [SerializeField] Transform bayPoint;
    [Tooltip("Directly in front of the barn doors at bay height.")]
    [SerializeField] Transform thresholdPoint;
    [Tooltip("Height above the threshold where the saucer cruises above the barn roof.")]
    [SerializeField] float cruiseHeight = 5f;

    public Vector3 BayPosition => bayPoint ? bayPoint.position : transform.position;
    public Vector3 ThresholdPosition => thresholdPoint ? thresholdPoint.position : transform.position;
    public Vector3 CruisePosition => ThresholdPosition + Vector3.up * cruiseHeight;

    BarnDoors ActiveDoors => GetComponentInChildren<BarnDoors>(false);

    public void OpenDoors() => ActiveDoors?.SetOpen(true);
    public void CloseDoors() => ActiveDoors?.SetOpen(false);
    public bool AreDoorsOpen() => ActiveDoors == null || ActiveDoors.IsFullyOpen;

    void OnDrawGizmosSelected()
    {
        Vector3 bay = BayPosition;
        Vector3 threshold = ThresholdPosition;
        Vector3 cruise = CruisePosition;

        // Flight path lines (bay -> threshold -> cruise waypoint)
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Gizmos.DrawLine(bay, threshold);
        Gizmos.DrawLine(threshold, cruise);

        // Waypoint spheres
        Gizmos.DrawWireSphere(bay, 0.25f);
        Gizmos.DrawWireSphere(threshold, 0.25f);

        // Cruise waypoint sphere + horizontal plane reference
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(cruise, 0.3f);
        Gizmos.DrawWireCube(cruise, new Vector3(2f, 0.02f, 2f));
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(AnimalPen))]
public class PenDoor : MonoBehaviour
{
    [Header("Existing Gate Models")]
    [SerializeField] private Transform leftGate;
    [SerializeField] private Transform rightGate;
    [SerializeField] private Transform gateCenter;

    [Header("Sliding Animation")]
    [SerializeField] private float slideDistance = 1.6f;
    [SerializeField] private float secondsToOpen = 0.5f;
    [SerializeField] private float openWhenAnimalWithin = 3f;

    [Header("Animal-Only Navigation")]
    [SerializeField] private string animalGateAreaName = "AnimalGate";
    [SerializeField] private float scanEverySeconds = 0.6f;

    private Vector3 leftClosedPosition;
    private Vector3 rightClosedPosition;
    private Vector3 doorwayPosition;
    private bool hasGates;
    private float openness;
    private float nextScan;
    private int gateAreaBit;

    private readonly List<Animal> animals = new();

    private void Start()
    {
        if (!leftGate) leftGate = FindNamedGate("fencegatel");
        if (!rightGate) rightGate = FindNamedGate("fencegater");

        hasGates = leftGate && rightGate;

        if (!hasGates)
        {
            Debug.LogWarning("Assign FenceGate L and FenceGate R.", this);
        }
        else
        {
            leftClosedPosition =
                transform.InverseTransformPoint(leftGate.position);

            rightClosedPosition =
                transform.InverseTransformPoint(rightGate.position);

            doorwayPosition = gateCenter
                ? transform.InverseTransformPoint(gateCenter.position)
                : (leftClosedPosition + rightClosedPosition) * 0.5f;
        }

        int areaIndex = NavMesh.GetAreaFromName(animalGateAreaName);

        if (areaIndex < 0)
        {
            Debug.LogWarning(
                $"NavMesh area '{animalGateAreaName}' does not exist.", this);
            gateAreaBit = 0;
        }
        else
        {
            gateAreaBit = 1 << areaIndex;
        }

        RefreshAnimalsAndAreaMasks();
    }

    private void Update()
    {
        if (Time.time >= nextScan)
            RefreshAnimalsAndAreaMasks();

        if (!hasGates) return;

        float target = IsAnimalNearDoor() ? 1f : 0f;

        openness = Mathf.MoveTowards(
            openness,
            target,
            Time.deltaTime / Mathf.Max(0.05f, secondsToOpen));

        float distance = Mathf.SmoothStep(0f, slideDistance, openness);

        leftGate.position = transform.TransformPoint(
            leftClosedPosition + Vector3.left * distance);

        rightGate.position = transform.TransformPoint(
            rightClosedPosition + Vector3.right * distance);
    }

    private void RefreshAnimalsAndAreaMasks()
    {
        nextScan = Time.time + Mathf.Max(0.15f, scanEverySeconds);
        animals.Clear();

        foreach (NavMeshAgent agent in
            FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
        {
            if (!agent) continue;

            Animal animal = agent.GetComponentInParent<Animal>();
            bool isAnimal = animal != null && animal.Team == GameConstants.Team.Farm;

            if (gateAreaBit != 0)
            {
                int wanted = isAnimal
                    ? agent.areaMask | gateAreaBit
                    : agent.areaMask & ~gateAreaBit;

                if (agent.areaMask != wanted)
                    agent.areaMask = wanted;
            }

            if (isAnimal && !animals.Contains(animal))
                animals.Add(animal);
        }
    }

    private bool IsAnimalNearDoor()
    {
        Vector3 door = transform.TransformPoint(doorwayPosition);
        float maxDistanceSquared =
            openWhenAnimalWithin * openWhenAnimalWithin;

        foreach (Animal animal in animals)
        {
            if (!animal ||
                !animal.gameObject.activeInHierarchy ||
                animal.Down ||
                animal.Team != GameConstants.Team.Farm)
                continue;

            Vector3 difference = animal.transform.position - door;
            difference.y = 0f;

            if (difference.sqrMagnitude <= maxDistanceSquared)
                return true;
        }

        return false;
    }

    private Transform FindNamedGate(string normalizedName)
    {
        foreach (Transform part in
            GetComponentsInChildren<Transform>(true))
        {
            string key = part.name
                .Replace(" ", "")
                .Replace("_", "")
                .Replace("-", "")
                .ToLowerInvariant();

            if (key == normalizedName)
                return part;
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center;

        if (gateCenter)
            center = gateCenter.position;
        else if (leftGate && rightGate)
            center = (leftGate.position + rightGate.position) * 0.5f;
        else
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, openWhenAnimalWithin);
    }
}

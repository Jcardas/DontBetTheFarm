using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Attach to the Wings mutation prefab. Automatically detects whether this spawned wing is Left or Right
/// and animates flapping/folding based on the host creature's CreatureFlight state.
/// </summary>
public class WingFlapBehavior : MonoBehaviour, IMutationBehavior
{
    public enum FlapAxis
    {
        X,
        Y,
        Z
    }

    [Header("Wing Hinge & Orientation")]
    [Tooltip("Optional explicit child pivot to rotate. If empty, searches for _L/_R child or uses this transform.")]
    [SerializeField] private Transform wingPivot;
    [Tooltip("Local axis the wing rotates around when flapping.")]
    [SerializeField] private FlapAxis flapAxis = FlapAxis.Z;
    [Tooltip("Flip left/right mirror sign if wings flap inverted.")]
    [SerializeField] private bool invertMirrorSign = false;

    [Header("Flap Frequencies (Cycles / Sec)")]
    [SerializeField] private float takeoffFlapHz = 5.5f;
    [SerializeField] private float cruiseFlapHz = 3.2f;
    [SerializeField] private float hoverFlapHz = 4.0f;
    [SerializeField] private float landingFlapHz = 4.5f;
    [SerializeField] private float groundedIdleFlapHz = 0.5f;

    [Header("Flap Angles (Degrees)")]
    [Tooltip("Max up/down stroke angle while flying/taking off.")]
    [SerializeField] private float airborneFlapDegrees = 42f;
    [Tooltip("Subtle breathing/twitch angle while landed on the ground.")]
    [SerializeField] private float groundedIdleDegrees = 5f;
    [Tooltip("Resting fold angle when grounded (tucks wings neatly against body).")]
    [SerializeField] private float groundedFoldDegrees = -25f;
    [Tooltip("Tucked glide angle while diving/swooping onto a ground target.")]
    [SerializeField] private float swoopGlideDegrees = 18f;
    [Tooltip("How quickly wing angle blends between flight states.")]
    [SerializeField] private float poseBlendSpeed = 10f;

    private Creature owner;
    private CreatureFlight flight;
    private Quaternion baseLocalRotation;
    private float sideSign = 1f;
    private float flapPhase;
    private float currentAngle;
    private bool isInitialized;

    public void Initialize(Creature owner)
    {
        this.owner = owner;
        EnsureInitialized();
    }

    public void OnEquipped()
    {
        EnsureInitialized();
    }

    public void OnUnequipped()
    {
        if (wingPivot != null && isInitialized)
            wingPivot.localRotation = baseLocalRotation;
    }

    public void OnCreatureAttack(Creature target) { }

    private void Start()
    {
        // Fallback in case prefab was placed directly in scene without CreatureMutator
        if (owner == null)
            owner = GetComponentInParent<Creature>();
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (isInitialized) return;

        ResolvePivotAndSide();
        if (wingPivot != null)
        {
            baseLocalRotation = wingPivot.localRotation;
            flapPhase = Random.Range(0f, Mathf.PI * 0.25f);
            isInitialized = true;
        }
    }

    private void ResolvePivotAndSide()
    {
        if (wingPivot == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(false))
            {
                if (child == transform) continue;
                if (Regex.IsMatch(child.name, @"_(L|R)(?:[.\s_-]\d+|\s*\(Clone\))*$", RegexOptions.IgnoreCase))
                {
                    wingPivot = child;
                    break;
                }
            }
            if (wingPivot == null) wingPivot = transform;
        }

        string checkName = wingPivot != transform ? wingPivot.name : (transform.parent != null ? transform.parent.name : name);
        bool isLeft = Regex.IsMatch(checkName, @"(?:^|[._\-\s])(L|Left)(?:[._\-\s\d]|$)", RegexOptions.IgnoreCase);
        bool isRight = Regex.IsMatch(checkName, @"(?:^|[._\-\s])(R|Right)(?:[._\-\s\d]|$)", RegexOptions.IgnoreCase);

        if (isLeft != isRight)
        {
            sideSign = isLeft ? 1f : -1f;
        }
        else if (owner != null)
        {
            float localX = owner.transform.InverseTransformPoint(wingPivot.position).x;
            sideSign = localX < 0f ? 1f : -1f;
        }

        if (invertMirrorSign)
            sideSign = -sideSign;
    }

    private void LateUpdate()
    {
        if (!isInitialized)
            EnsureInitialized();

        if (wingPivot == null) return;

        if (owner == null)
            owner = GetComponentInParent<Creature>();

        // Lazy-resolve flight because Creature.OnMutationsChanged() adds CreatureFlight AFTER EquipMutation.Initialize()
        if (flight == null && owner != null)
            flight = owner.Flight != null ? owner.Flight : owner.GetComponent<CreatureFlight>();

        if (owner != null && owner.Down)
        {
            currentAngle = Mathf.Lerp(currentAngle, groundedFoldDegrees, poseBlendSpeed * Time.deltaTime);
            ApplyAngle(currentAngle);
            return;
        }

        float targetHz = groundedIdleFlapHz;
        float targetAmplitude = groundedIdleDegrees;
        float baseBias = groundedFoldDegrees;

        if (flight != null)
        {
            switch (flight.State)
            {
                case CreatureFlight.FlightState.TakingOff:
                    targetHz = takeoffFlapHz;
                    targetAmplitude = airborneFlapDegrees * 1.15f;
                    baseBias = 0f;
                    break;

                case CreatureFlight.FlightState.Cruising:
                    bool isMoving = flight.CurrentHorizontalSpeed > 0.25f;
                    targetHz = isMoving ? cruiseFlapHz : hoverFlapHz;
                    targetAmplitude = airborneFlapDegrees;
                    baseBias = 0f;
                    break;

                case CreatureFlight.FlightState.Swooping:
                    targetHz = 0.5f;
                    targetAmplitude = 4f;
                    baseBias = swoopGlideDegrees;
                    break;

                case CreatureFlight.FlightState.Landing:
                    targetHz = landingFlapHz;
                    targetAmplitude = airborneFlapDegrees * 0.9f;
                    baseBias = 5f;
                    break;

                case CreatureFlight.FlightState.Grounded:
                default:
                    targetHz = groundedIdleFlapHz;
                    targetAmplitude = groundedIdleDegrees;
                    baseBias = groundedFoldDegrees;
                    break;
            }
        }

        flapPhase = Mathf.Repeat(flapPhase + targetHz * Mathf.PI * 2f * Time.deltaTime, Mathf.PI * 2f);
        float desiredAngle = baseBias + Mathf.Sin(flapPhase) * targetAmplitude;
        currentAngle = Mathf.Lerp(currentAngle, desiredAngle, poseBlendSpeed * Time.deltaTime);

        ApplyAngle(currentAngle);
    }

    private void ApplyAngle(float angle)
    {
        float signedAngle = angle * sideSign;
        Vector3 euler = flapAxis switch
        {
            FlapAxis.X => new Vector3(signedAngle, 0f, 0f),
            FlapAxis.Y => new Vector3(0f, signedAngle, 0f),
            _          => new Vector3(0f, 0f, signedAngle),
        };

        wingPivot.localRotation = baseLocalRotation * Quaternion.Euler(euler);
    }
}
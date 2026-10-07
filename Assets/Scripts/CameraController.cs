using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Pan Settings")] [SerializeField]
    private float panSpeed = 25f;

    // Multiplier for the drag panning speed. Adjust this value to make dragging faster or slower.
    [SerializeField] private float dragPanMultiplier = 1f;

    [SerializeField] private float edgePanSpeed = 20f;
    [SerializeField] private float edgeBorderThickness = 15f;
    [SerializeField] private bool useEdgeScrolling = false;

    // Damping factor for smoothing the camera's panning movement.
    // Higher values result in faster response to input.
    [SerializeField] private float panDamping = 10f;

    [Header("Orthographic Zoom Settings")] [SerializeField]
    private float zoomStep = 2f;

    [SerializeField] private float minOrthoSize = 4f;
    [SerializeField] private float maxOrthoSize = 25f;
    [SerializeField] private float zoomDamping = 10f;

    [Header("Camera Reference")] [SerializeField]
    private Transform cameraTransform;

    /// <summary>
    /// The input actions for handling camera controls, including panning and zooming
    /// </summary>
    private InputActions _input;

    /// <summary>
    /// The camera component reference.
    /// </summary>
    private Camera _cam;

    /// <summary>
    /// The target position for the camera to move towards.
    /// </summary>
    private Vector3 _targetPosition;

    /// <summary>
    /// The target orthographic size for the camera to zoom towards.
    /// </summary>
    private float _targetOrthoSize;

    /// <summary>
    /// The current movement input vector (x, y) for panning.
    /// </summary>
    private Vector2 _moveInput;

    /// <summary>
    /// The current zoom input value (positive for zoom in, negative for zoom out).
    /// </summary>
    private float _zoomAccumulator;

    /// <summary>
    /// Whether the camera is currently being dragged with the middle mouse button.
    /// </summary>
    private bool _isDragging;

    /// <summary>
    /// Whether the camera is currently being panned using edge scrolling or keyboard input.
    /// </summary>
    private bool _isPanning;

    private CursorType _currentEdgeCursor = CursorType.Default;

    // A plane representing the ground level (XZ plane) for raycasting (Zoom at cursor functionality)
    private readonly Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);

    /// <summary>
    /// Initializes the camera controller, sets up input actions, and configures the camera component.
    /// </summary>
    private void Awake()
    {
        _input = new InputActions();
        _targetPosition = transform.position;

        // Checks if the cameraTransform reference is null, if so it tries to get the main camera
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
            _cam = Camera.main;
        }
        // If cameraTransform is not null, it tries to get the Camera component from it
        else if (cameraTransform != null)
        {
            _cam = cameraTransform.GetComponent<Camera>();
        }

        // If the camera component is found, it sets the orthographic size and clamps it within the specified range
        if (_cam != null)
        {
            _cam.orthographic = true;
            _targetOrthoSize = Mathf.Clamp(_cam.orthographicSize, minOrthoSize, maxOrthoSize);
            _cam.orthographicSize = _targetOrthoSize;
        }
    }

    /// <summary>
    /// Enables the input actions and sets up the event handlers for camera controls.
    /// </summary>
    private void OnEnable()
    {
        _input.Camera.Enable();

        _input.Camera.Pan.performed += OnPanPerformed;
        _input.Camera.Pan.canceled += OnPanCanceled;

        _input.Camera.Zoom.performed += OnZoomPerformed;
        _input.Camera.Zoom.canceled += OnZoomCanceled;

        _input.Camera.MiddleClick.performed += OnDragStarted;
        _input.Camera.MiddleClick.canceled += OnDragCanceled;
    }

    /// <summary>
    /// Disables the input actions for camera controls.
    /// </summary>
    private void OnDisable()
    {
        _input.Camera.Pan.performed -= OnPanPerformed;
        _input.Camera.Pan.canceled -= OnPanCanceled;

        _input.Camera.Zoom.performed -= OnZoomPerformed;
        _input.Camera.Zoom.canceled -= OnZoomCanceled;

        _input.Camera.MiddleClick.performed -= OnDragStarted;
        _input.Camera.MiddleClick.canceled -= OnDragCanceled;

        _input.Camera.Disable();
    }

    private void OnDestroy()
    {
        _input?.Dispose();
    }

    private void OnPanPerformed(InputAction.CallbackContext ctx) => _moveInput = ctx.ReadValue<Vector2>();
    private void OnPanCanceled(InputAction.CallbackContext ctx) => _moveInput = Vector2.zero;

    private void OnZoomPerformed(InputAction.CallbackContext ctx) => _zoomAccumulator += ctx.ReadValue<float>();
    private void OnZoomCanceled(InputAction.CallbackContext ctx) => _zoomAccumulator = 0f;

    private void OnDragStarted(InputAction.CallbackContext ctx)
    {
        StartDragging();
    }

    private void OnDragCanceled(InputAction.CallbackContext ctx)
    {
        StopDragging();
    }

    /// <summary>
    /// Handles the camera movement and zooming each frame based on user input.
    /// </summary>
    private void Update()
    {
        if (_isDragging)
        {
            HandleDragPan();
        }
        else
        {
            HandleKeyboardAndEdgePan();
        }

        HandleZoomToCursor();
        ApplyMovement();
    }

    /// <summary>
    /// Handles camera panning based on keyboard input and edge-of-screen mouse position.
    /// </summary>
    private void HandleKeyboardAndEdgePan()
    {
        Transform referenceTransform = cameraTransform != null ? cameraTransform : transform;
        Vector3 forward = referenceTransform.forward;
        Vector3 right = referenceTransform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 direction = forward * _moveInput.y + right * _moveInput.x;

        // If the camera is being dragged, we don't want to allow edge scrolling or keyboard panning to interfere.
        _isPanning = direction != Vector3.zero;
        if (useEdgeScrolling && !_isDragging && Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 edgeDir = Vector3.zero;

            int horizontalEdge = 0;
            int verticalEdge = 0;

            if (mousePos.x >= Screen.width - edgeBorderThickness)
            {
                horizontalEdge = 1;
                edgeDir += right;
            }
            else if (mousePos.x <= edgeBorderThickness)
            {
                horizontalEdge = -1;
                edgeDir -= right;
            }

            if (mousePos.y >= Screen.height - edgeBorderThickness)
            {
                verticalEdge = 1;
                edgeDir += forward;
            }
            else if (mousePos.y <= edgeBorderThickness)
            {
                verticalEdge = -1;
                edgeDir -= forward;
            }

            SetEdgePanCursor(horizontalEdge, verticalEdge);

            _isPanning = edgeDir != Vector3.zero;

            if (edgeDir != Vector3.zero)
            {
                direction += edgeDir.normalized * (edgePanSpeed / panSpeed);
            }
        }
        else if (!_isDragging && !_isPanning && _moveInput == Vector2.zero)
        {
            if (_currentEdgeCursor != CursorType.Default)
            {
                _currentEdgeCursor = CursorType.Default;
                CursorManager.Instance?.SetCursor(CursorType.Default);
            }
        }

        _targetPosition += direction * (panSpeed * Time.deltaTime);
    }

    private void SetEdgePanCursor(int horizontalEdge, int verticalEdge)
    {
        CursorType cursorType = CursorType.Default;

        if (horizontalEdge == -1 && verticalEdge == 0)
            cursorType = CursorType.EdgeScrollWest;
        else if (horizontalEdge == 1 && verticalEdge == 0)
            cursorType = CursorType.EdgeScrollEast;
        else if (horizontalEdge == 0 && verticalEdge == 1)
            cursorType = CursorType.EdgeScrollNorth;
        else if (horizontalEdge == 0 && verticalEdge == -1)
            cursorType = CursorType.EdgeScrollSouth;
        else if (horizontalEdge == 1 && verticalEdge == 1)
            cursorType = CursorType.EdgeScrollNorthEast;
        else if (horizontalEdge == -1 && verticalEdge == 1)
            cursorType = CursorType.EdgeScrollNorthWest;
        else if (horizontalEdge == 1 && verticalEdge == -1)
            cursorType = CursorType.EdgeScrollSouthEast;
        else if (horizontalEdge == -1 && verticalEdge == -1)
            cursorType = CursorType.EdgeScrollSouthWest;

        if (_currentEdgeCursor != cursorType)
        {
            _currentEdgeCursor = cursorType;
            CursorManager.Instance?.SetCursor(cursorType);
        }
    }

    // Handles the start and stop of dragging, changing the cursor accordingly.
    private void StartDragging()
    {
        _isDragging = true;
        CursorManager.Instance?.SetCursor(CursorType.Grab);
    }

    // Handles the stop of dragging, reverting the cursor to default.
    private void StopDragging()
    {
        _isDragging = false;
        CursorManager.Instance?.SetCursor(CursorType.Default);
    }

    /// <summary>
    /// Handles camera panning based on mouse drag input.
    /// Uses pitch correction to ensure that vertical mouse movement corresponds
    /// accurately to world space movement.
    /// </summary>
    private void HandleDragPan()
    {
        if (!_isDragging || _cam == null) return;

        Vector2 mouseDelta = _input.Camera.MouseDelta.ReadValue<Vector2>();
        if (mouseDelta.sqrMagnitude < 0.0001f) return;

        Transform referenceTransform = cameraTransform != null ? cameraTransform : transform;

        // World units per screen pixel along the camera's local screen plane
        float unitsPerPixel = (_cam.orthographicSize * 2f) / Screen.height;

        // Pitch angle in radians (tilt down from the horizontal plane)
        float pitchAngleRad = referenceTransform.eulerAngles.x * Mathf.Deg2Rad;
        float sinPitch = Mathf.Sin(pitchAngleRad);

        // Prevent division by zero if camera is looking completely horizontally
        float verticalCorrectionFactor = sinPitch > 0.01f ? (1f / sinPitch) : 1f;

        // Planar forward & right vectors
        Vector3 forward = referenceTransform.forward;
        Vector3 right = referenceTransform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        // Scale vertical mouse delta by verticalCorrectionFactor so 1 pixel of vertical drag 
        // tracks 1:1 with the ground under the cursor
        Vector3 dragMovement = (-right * mouseDelta.x + -forward * (mouseDelta.y * verticalCorrectionFactor))
                               * unitsPerPixel * dragPanMultiplier;

        _targetPosition += dragMovement;
    }

    /// <summary>
    /// Handles camera zooming based on mouse scroll input, keeping the zoom centered around the cursor's position.
    /// </summary>
    private void HandleZoomToCursor()
    {
        if (_cam == null || Mouse.current == null) return;

        if (Mathf.Abs(_zoomAccumulator) > 0.01f)
        {
            // Convert the accumulated scroll value to zoom ticks (each tick represents a standard scroll unit)
            float scrollTicks = _zoomAccumulator / 120f;
            _zoomAccumulator = 0f;

            // Store the previous target orthographic size before applying the zoom change
            float previousTargetSize = _targetOrthoSize;
            _targetOrthoSize = Mathf.Clamp(_targetOrthoSize - (scrollTicks * zoomStep), minOrthoSize, maxOrthoSize);

            if (!Mathf.Approximately(previousTargetSize, _targetOrthoSize))
            {
                Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

                // Calculate the world position of the point under the cursor before and after the zoom change
                Vector3 worldPointBefore =
                    GetGroundPointAtScreenPos(mouseScreenPos, previousTargetSize, _targetPosition);

                // Calculate the world position of the point under the cursor after the zoom change
                Vector3 worldPointAfter = GetGroundPointAtScreenPos(mouseScreenPos, _targetOrthoSize, _targetPosition);

                // Calculate the shift needed to keep the point under the cursor stationary in world space
                // If zooming out, always zoom from center of the screen to avoid moving the camera too far away from the target area
                Vector3 shift = (worldPointBefore - worldPointAfter);
                if (scrollTicks < 0f)
                {
                    Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
                    Vector3 worldCenterBefore =
                        GetGroundPointAtScreenPos(screenCenter, previousTargetSize, _targetPosition);
                    Vector3 worldCenterAfter =
                        GetGroundPointAtScreenPos(screenCenter, _targetOrthoSize, _targetPosition);
                    shift = (worldCenterBefore - worldCenterAfter);
                }

                _targetPosition += shift;
            }
        }

        // Smoothly interpolate the camera's orthographic size
        float zoomT = 1f - Mathf.Exp(-zoomDamping * Time.deltaTime);
        _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetOrthoSize, zoomT);
    }

    /// <summary>
    /// Calculates the ground plane hit point for a given screen position, projected size, and rig position.
    /// </summary>
    private Vector3 GetGroundPointAtScreenPos(Vector2 screenPos, float orthoSize, Vector3 rigPosition)
    {
        Transform refCam = cameraTransform != null ? cameraTransform : transform;

        // Viewport coordinate normalized from -1 to 1 relative to center
        float viewportX = (screenPos.x / Screen.width - 0.5f) * 2f;
        float viewportY = (screenPos.y / Screen.height - 0.5f) * 2f;

        float aspect = (float)Screen.width / Screen.height;
        float halfHeight = orthoSize;
        float halfWidth = halfHeight * aspect;

        // Camera local offsets
        Vector3 rightOffset = refCam.right * (viewportX * halfWidth);
        Vector3 upOffset = refCam.up * (viewportY * halfHeight);

        // Virtual ray origin based on target rig position + current local camera offset
        Vector3 camOffsetFromRig =
            (cameraTransform != null) ? (cameraTransform.position - transform.position) : Vector3.zero;
        Vector3 rayOrigin = rigPosition + camOffsetFromRig + rightOffset + upOffset;
        Ray ray = new Ray(rayOrigin, refCam.forward);

        if (_groundPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }

        return rigPosition;
    }

    /**
     * <summary>
     * Smoothly applies the calculated target position to the camera's transform.
     * This method uses linear interpolation (Lerp) to create a smooth transition effect.
     * </summary>
     */
    private void ApplyMovement()
    {
        float panT = 1f - Mathf.Exp(-panDamping * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, _targetPosition, panT);
    }
}
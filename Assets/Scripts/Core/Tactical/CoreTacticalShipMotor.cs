using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class CoreTacticalShipMotor : MonoBehaviour
{
    private const int MaxAvoidanceHits = 24;
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly RaycastHit[] AvoidanceHits = new RaycastHit[MaxAvoidanceHits];
    private static readonly Collider[] AvoidanceOverlaps = new Collider[MaxAvoidanceHits];
    private static MaterialPropertyBlock colorBlock;

    [Header("Identity")]
    public string shipId = "ship";
    public string displayName = "Ship";

    [Header("Hull")]
    public Vector3 hullSizeMeters = new Vector3(24f, 7f, 72f);
    public float massKg = 900000f;

    [Header("Linear Motion")]
    public float maxForwardSpeedMS = 34f;
    public float maxReverseSpeedMS = 6f;
    public float maxLateralSpeedMS = 3.5f;
    public float forwardSpeedMultiplier = 1f;
    public float forwardAccelerationMS2 = 5.5f;
    public float lateralAccelerationMS2 = 2.0f;
    public float brakingAccelerationMS2 = 7.5f;
    public float slowdownDistanceMeters = 46f;
    public float arrivalRadiusMeters = 7f;
    public float noseFirstYawReadyDeg = 10f;
    public float noseFirstYawHardGateDeg = 105f;
    public float holdFacingTranslationYawGateDeg = 35f;

    [Header("Obstacle Avoidance")]
    public bool obstacleAvoidanceEnabled = true;
    public bool avoidOtherShips = true;
    public float obstacleAvoidanceLookAheadMeters = 260f;
    public float obstacleAvoidanceMarginMeters = 12f;
    public float obstacleAvoidanceStrength = 1.45f;

    [Header("Altitude")]
    public float ascentSpeedMS = 7f;
    public float descentSpeedMS = 7f;
    public float verticalAccelerationMS2 = 8f;

    [Header("Yaw")]
    public float maxYawRateDegPerSecond = 32f;
    public float yawAccelerationDegPerSecond2 = 110f;
    public float finalFacingDistanceMeters = 22f;

    [Header("Visual")]
    public Color normalColor = new Color(0.16f, 0.38f, 0.86f, 1f);
    public Color selectedColor = new Color(0.98f, 0.70f, 0.16f, 1f);

    private Rigidbody body;
    private Renderer[] renderers;
    private Vector3 targetPosition;
    private Vector3 targetForward = Vector3.forward;
    private bool hasMoveTarget;
    private bool selected;
    private bool holdFacingDuringMove;
    private Collider currentAvoidanceObstacle;
    private float currentAvoidanceSide = 1f;
    private Vector3 currentTravelForward = Vector3.forward;

    public bool IsSelected => selected;
    public Vector3 TargetPosition => targetPosition;
    public Vector3 TargetForward => targetForward;
    public bool HoldFacingDuringMove => holdFacingDuringMove;
    public Rigidbody Body => body;

    private void Awake()
    {
        ResolveComponents();
        ConfigureBody();
        targetPosition = transform.position;
        targetForward = FlattenDirection(transform.forward, Vector3.forward);
    }

    private void Reset()
    {
        hullSizeMeters = new Vector3(24f, 7f, 72f);
        ResolveComponents();
        ConfigureBody();
    }

    private void OnValidate()
    {
        hullSizeMeters = new Vector3(
            Mathf.Max(1f, hullSizeMeters.x),
            Mathf.Max(1f, hullSizeMeters.y),
            Mathf.Max(1f, hullSizeMeters.z));
        massKg = Mathf.Max(1f, massKg);
        maxForwardSpeedMS = Mathf.Max(0.1f, maxForwardSpeedMS);
        maxReverseSpeedMS = Mathf.Max(0f, maxReverseSpeedMS);
        maxLateralSpeedMS = Mathf.Max(0f, maxLateralSpeedMS);
        forwardSpeedMultiplier = Mathf.Max(0.01f, forwardSpeedMultiplier);
        forwardAccelerationMS2 = Mathf.Max(0.01f, forwardAccelerationMS2);
        lateralAccelerationMS2 = Mathf.Max(0.01f, lateralAccelerationMS2);
        brakingAccelerationMS2 = Mathf.Max(0.01f, brakingAccelerationMS2);
        noseFirstYawReadyDeg = Mathf.Clamp(noseFirstYawReadyDeg, 1f, 60f);
        noseFirstYawHardGateDeg = Mathf.Clamp(noseFirstYawHardGateDeg, noseFirstYawReadyDeg + 1f, 160f);
        ascentSpeedMS = Mathf.Max(0.01f, ascentSpeedMS);
        descentSpeedMS = Mathf.Max(0.01f, descentSpeedMS);
        verticalAccelerationMS2 = Mathf.Max(0.01f, verticalAccelerationMS2);
        maxYawRateDegPerSecond = Mathf.Max(0.01f, maxYawRateDegPerSecond);
        yawAccelerationDegPerSecond2 = Mathf.Max(0.01f, yawAccelerationDegPerSecond2);
        slowdownDistanceMeters = Mathf.Max(1f, slowdownDistanceMeters);
        arrivalRadiusMeters = Mathf.Max(0.1f, arrivalRadiusMeters);
        obstacleAvoidanceLookAheadMeters = Mathf.Max(1f, obstacleAvoidanceLookAheadMeters);
        obstacleAvoidanceMarginMeters = Mathf.Max(0f, obstacleAvoidanceMarginMeters);
        obstacleAvoidanceStrength = Mathf.Clamp(obstacleAvoidanceStrength, 0.1f, 4f);
        holdFacingTranslationYawGateDeg = Mathf.Clamp(holdFacingTranslationYawGateDeg, 1f, 120f);
        finalFacingDistanceMeters = Mathf.Max(arrivalRadiusMeters, finalFacingDistanceMeters);
    }

    private void FixedUpdate()
    {
        if (body == null)
        {
            ResolveComponents();
        }

        if (body == null)
        {
            return;
        }

        if (!hasMoveTarget)
        {
            targetPosition = transform.position;
            targetForward = FlattenDirection(transform.forward, Vector3.forward);
            hasMoveTarget = true;
        }

        float deltaSeconds = Mathf.Max(0.001f, Time.fixedDeltaTime);
        UpdateTravelForward();
        ApplyYawControl(deltaSeconds);
        ApplyLinearControl(deltaSeconds);
        ApplyAltitudeControl(deltaSeconds);
    }

    public void InitializePrototypeShip(string newShipId, string newDisplayName, Vector3 sizeMeters, Color color)
    {
        shipId = string.IsNullOrWhiteSpace(newShipId) ? shipId : newShipId;
        displayName = string.IsNullOrWhiteSpace(newDisplayName) ? shipId : newDisplayName;
        hullSizeMeters = sizeMeters;
        normalColor = color;
        transform.localScale = hullSizeMeters;
        ResolveComponents();
        ConfigureBody();
        RefreshVisualState();
    }

    public void SetSelected(bool value)
    {
        selected = value;
        RefreshVisualState();
    }

    public void SetCommand(Vector3 position, Vector3 forward)
    {
        SetCommand(position, forward, false);
    }

    public void SetCommand(Vector3 position, Vector3 forward, bool holdFacing)
    {
        targetPosition = position;
        targetForward = FlattenDirection(forward, transform.forward);
        holdFacingDuringMove = holdFacing;
        hasMoveTarget = true;
    }

    public void SetCommandPlaneAltitude(float altitudeMeters)
    {
        targetPosition.y = altitudeMeters;
        hasMoveTarget = true;
    }

    public int BuildRoutePreview(Vector3[] routePoints)
    {
        if (routePoints == null || routePoints.Length == 0)
        {
            return 0;
        }

        Vector3 start = body != null ? body.position : transform.position;
        start.y = targetPosition.y;
        routePoints[0] = start;
        if (routePoints.Length == 1)
        {
            return 1;
        }

        Vector3 simulatedPosition = start;
        Vector3 simulatedForward = FlattenDirection(transform.forward, targetForward);
        int pointCount = 1;
        int maxIterations = routePoints.Length - 1;
        for (int i = 0; i < maxIterations; i++)
        {
            Vector3 flatDelta = targetPosition - simulatedPosition;
            flatDelta.y = 0f;
            float flatDistance = flatDelta.magnitude;
            if (flatDistance <= arrivalRadiusMeters * 1.5f)
            {
                break;
            }

            Vector3 directForward = flatDelta / Mathf.Max(0.001f, flatDistance);
            Vector3 routeForward = holdFacingDuringMove
                ? directForward
                : ResolveAvoidedDirectionAt(simulatedPosition, directForward, flatDistance, simulatedForward, false);
            float stepMeters = Mathf.Clamp(
                Mathf.Max(hullSizeMeters.z * 0.45f, obstacleAvoidanceLookAheadMeters * 0.32f),
                28f,
                220f);
            simulatedPosition += routeForward * Mathf.Min(stepMeters, flatDistance);
            simulatedPosition.y = targetPosition.y;
            simulatedForward = routeForward;
            routePoints[pointCount++] = simulatedPosition;

            if (flatDistance <= stepMeters)
            {
                break;
            }
        }

        if (pointCount < routePoints.Length)
        {
            routePoints[pointCount++] = targetPosition;
        }
        else
        {
            routePoints[pointCount - 1] = targetPosition;
        }

        return pointCount;
    }

    private void ResolveComponents()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
        }

        renderers = GetComponentsInChildren<Renderer>();
    }

    private void ConfigureBody()
    {
        if (body == null)
        {
            return;
        }

        body.mass = Mathf.Max(1f, massKg);
        body.useGravity = false;
        body.linearDamping = 0.22f;
        body.angularDamping = 0.9f;
        body.maxAngularVelocity = Mathf.Max(body.maxAngularVelocity, maxYawRateDegPerSecond * Mathf.Deg2Rad * 1.5f);
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void ApplyYawControl(float deltaSeconds)
    {
        Vector3 flatDelta = targetPosition - body.position;
        flatDelta.y = 0f;
        float flatDistance = flatDelta.magnitude;
        Vector3 desiredForward = holdFacingDuringMove
            ? targetForward
            : currentTravelForward;
        if (!holdFacingDuringMove && flatDistance <= finalFacingDistanceMeters)
        {
            desiredForward = targetForward;
        }

        desiredForward = FlattenDirection(desiredForward, transform.forward);

        Vector3 currentForward = FlattenDirection(transform.forward, Vector3.forward);
        Vector3 nextForward = Vector3.RotateTowards(
            currentForward,
            desiredForward,
            maxYawRateDegPerSecond * Mathf.Deg2Rad * deltaSeconds,
            0f);
        body.angularVelocity = new Vector3(0f, 0f, 0f);
        body.MoveRotation(Quaternion.LookRotation(nextForward, Vector3.up));
    }

    private void UpdateTravelForward()
    {
        Vector3 flatDelta = targetPosition - body.position;
        flatDelta.y = 0f;
        float flatDistance = flatDelta.magnitude;
        if (holdFacingDuringMove || flatDistance <= finalFacingDistanceMeters)
        {
            currentTravelForward = FlattenDirection(targetForward, transform.forward);
            currentAvoidanceObstacle = null;
            return;
        }

        Vector3 directForward = flatDelta / Mathf.Max(0.001f, flatDistance);
        currentTravelForward = ResolveObstacleAvoidedDirection(directForward, flatDistance);
    }

    private void ApplyLinearControl(float deltaSeconds)
    {
        Vector3 flatDelta = targetPosition - body.position;
        flatDelta.y = 0f;
        float flatDistance = flatDelta.magnitude;
        if (flatDistance <= arrivalRadiusMeters)
        {
            ApplyFlatAcceleration(Vector3.zero, brakingAccelerationMS2, deltaSeconds);
            return;
        }

        if (!holdFacingDuringMove)
        {
            Vector3 travelDirection = FlattenDirection(currentTravelForward, flatDelta);
            float yawReadiness = GetNoseFirstTranslationReadiness(travelDirection);
            float speedRatio = Mathf.Clamp01(flatDistance / Mathf.Max(1f, slowdownDistanceMeters));
            float forwardSpeedLimit = maxForwardSpeedMS * Mathf.Max(0.01f, forwardSpeedMultiplier);
            Vector3 desiredVelocity = transform.forward * (forwardSpeedLimit * speedRatio * yawReadiness);
            float noseFirstAccelerationLimit = yawReadiness <= 0.01f ? brakingAccelerationMS2 : forwardAccelerationMS2;
            ApplyFlatAcceleration(desiredVelocity, noseFirstAccelerationLimit, deltaSeconds);
            return;
        }

        Vector3 localDelta = transform.InverseTransformDirection(flatDelta);
        float forwardRatio = Mathf.Clamp(localDelta.z / slowdownDistanceMeters, -1f, 1f);
        float lateralRatio = Mathf.Clamp(localDelta.x / slowdownDistanceMeters, -1f, 1f);
        float holdFacingForwardSpeedLimit = maxForwardSpeedMS * Mathf.Max(0.01f, forwardSpeedMultiplier);
        float desiredForwardSpeed = forwardRatio >= 0f
            ? forwardRatio * holdFacingForwardSpeedLimit
            : forwardRatio * maxReverseSpeedMS;
        float desiredLateralSpeed = lateralRatio * maxLateralSpeedMS;
        Vector3 desiredFlatVelocity = transform.forward * desiredForwardSpeed + transform.right * desiredLateralSpeed;
        desiredFlatVelocity *= GetHoldFacingTranslationReadiness();

        float accelerationLimit = Mathf.Abs(desiredForwardSpeed) < 0.1f && Mathf.Abs(desiredLateralSpeed) < 0.1f
            ? brakingAccelerationMS2
            : Mathf.Max(forwardAccelerationMS2, lateralAccelerationMS2);
        ApplyFlatAcceleration(desiredFlatVelocity, accelerationLimit, deltaSeconds);
    }

    private void ApplyFlatAcceleration(Vector3 desiredFlatVelocity, float accelerationLimit, float deltaSeconds)
    {
        Vector3 currentFlatVelocity = body.linearVelocity;
        currentFlatVelocity.y = 0f;
        desiredFlatVelocity.y = 0f;
        Vector3 requiredAcceleration = (desiredFlatVelocity - currentFlatVelocity) / deltaSeconds;
        requiredAcceleration = Vector3.ClampMagnitude(requiredAcceleration, Mathf.Max(0.01f, accelerationLimit));
        body.AddForce(requiredAcceleration * body.mass, ForceMode.Force);
    }

    private void ApplyAltitudeControl(float deltaSeconds)
    {
        float altitudeError = targetPosition.y - body.position.y;
        float desiredVerticalSpeed = Mathf.Clamp(altitudeError * 0.65f, -descentSpeedMS, ascentSpeedMS);
        float requiredAcceleration = Mathf.Clamp(
            (desiredVerticalSpeed - body.linearVelocity.y) / deltaSeconds,
            -verticalAccelerationMS2,
            verticalAccelerationMS2);
        body.AddForce(Vector3.up * requiredAcceleration * body.mass, ForceMode.Force);
    }

    private Vector3 ResolveObstacleAvoidedDirection(Vector3 desiredForward, float flatDistance)
    {
        return ResolveAvoidedDirectionAt(body.position, desiredForward, flatDistance, transform.forward, true);
    }

    private Vector3 ResolveAvoidedDirectionAt(
        Vector3 origin,
        Vector3 desiredForward,
        float flatDistance,
        Vector3 fallbackForward,
        bool rememberSide)
    {
        desiredForward = FlattenDirection(desiredForward, fallbackForward);
        if (!obstacleAvoidanceEnabled || flatDistance <= arrivalRadiusMeters)
        {
            if (rememberSide)
            {
                currentAvoidanceObstacle = null;
            }

            return desiredForward;
        }

        float hullRadius = Mathf.Max(2f, hullSizeMeters.x * 0.5f + obstacleAvoidanceMarginMeters);
        float lookAhead = Mathf.Min(
            flatDistance,
            Mathf.Max(obstacleAvoidanceLookAheadMeters, hullSizeMeters.z * 0.75f));
        if (lookAhead <= hullRadius)
        {
            if (rememberSide)
            {
                currentAvoidanceObstacle = null;
            }

            return desiredForward;
        }

        if (TryResolveOverlappingObstacle(origin, hullRadius, desiredForward, fallbackForward, rememberSide, out Vector3 escapeForward))
        {
            return escapeForward;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            hullRadius,
            desiredForward,
            AvoidanceHits,
            lookAhead,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        RaycastHit nearestHit = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = AvoidanceHits[i];
            if (hit.collider == null || !IsAvoidanceCollider(hit.collider))
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
            }
        }

        if (nearestHit.collider == null)
        {
            if (rememberSide)
            {
                currentAvoidanceObstacle = null;
            }

            return desiredForward;
        }

        Vector3 right = Vector3.Cross(Vector3.up, desiredForward);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.Cross(Vector3.up, fallbackForward);
        }

        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }

        right.Normalize();
        Collider obstacleCollider = nearestHit.collider;
        float avoidanceSide = ResolveAvoidanceSide(obstacleCollider, origin, desiredForward, fallbackForward, rememberSide);
        if (rememberSide)
        {
            currentAvoidanceObstacle = obstacleCollider;
            currentAvoidanceSide = avoidanceSide;
        }

        float closeness = 1f - Mathf.Clamp01(nearestDistance / Mathf.Max(1f, lookAhead));
        float sideWeight = Mathf.Lerp(0.65f, obstacleAvoidanceStrength, closeness);
        Vector3 avoidedForward = desiredForward + right * avoidanceSide * sideWeight;
        return FlattenDirection(avoidedForward, desiredForward);
    }

    private float ResolveAvoidanceSide(
        Collider obstacleCollider,
        Vector3 origin,
        Vector3 desiredForward,
        Vector3 fallbackForward,
        bool rememberSide)
    {
        if (rememberSide && currentAvoidanceObstacle == obstacleCollider && Mathf.Abs(currentAvoidanceSide) > 0.01f)
        {
            return currentAvoidanceSide;
        }

        Vector3 right = Vector3.Cross(Vector3.up, desiredForward);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.Cross(Vector3.up, fallbackForward);
        }

        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }

        right.Normalize();

        Vector3 obstacleOffset = obstacleCollider.bounds.center - origin;
        obstacleOffset.y = 0f;
        float signedSide = Vector3.Dot(obstacleOffset, right);
        float sideDeadZone = Mathf.Max(1f, hullSizeMeters.x * 0.15f);
        if (Mathf.Abs(signedSide) > sideDeadZone)
        {
            return signedSide > 0f ? -1f : 1f;
        }

        Vector3 previousForward = FlattenDirection(fallbackForward, desiredForward);
        float previousSide = Vector3.Dot(previousForward, right);
        if (Mathf.Abs(previousSide) > 0.01f)
        {
            return previousSide >= 0f ? 1f : -1f;
        }

        return Mathf.Abs(currentAvoidanceSide) > 0.01f ? Mathf.Sign(currentAvoidanceSide) : 1f;
    }

    private bool TryResolveOverlappingObstacle(
        Vector3 origin,
        float hullRadius,
        Vector3 desiredForward,
        Vector3 fallbackForward,
        bool rememberSide,
        out Vector3 escapeForward)
    {
        escapeForward = desiredForward;
        int overlapCount = Physics.OverlapSphereNonAlloc(
            origin,
            hullRadius,
            AvoidanceOverlaps,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        Collider nearestCollider = null;
        Vector3 nearestPush = Vector3.zero;
        float nearestDistanceSqr = float.PositiveInfinity;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = AvoidanceOverlaps[i];
            if (overlap == null || !IsAvoidanceCollider(overlap))
            {
                continue;
            }

            Vector3 closestPoint = overlap.ClosestPoint(origin);
            Vector3 push = origin - closestPoint;
            push.y = 0f;
            if (push.sqrMagnitude <= 0.0001f)
            {
                push = origin - overlap.bounds.center;
                push.y = 0f;
            }

            float distanceSqr = push.sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr && distanceSqr > 0.0001f)
            {
                nearestCollider = overlap;
                nearestPush = push;
                nearestDistanceSqr = distanceSqr;
            }
        }

        if (nearestCollider == null)
        {
            return false;
        }

        Vector3 right = Vector3.Cross(Vector3.up, desiredForward);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.Cross(Vector3.up, fallbackForward);
        }

        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }

        right.Normalize();

        float avoidanceSide = ResolveAvoidanceSide(nearestCollider, origin, desiredForward, fallbackForward, rememberSide);
        Vector3 lateralEscape = right * avoidanceSide;
        Vector3 pushDirection = FlattenDirection(nearestPush, desiredForward);
        float pushAlongTravel = Mathf.Abs(Vector3.Dot(pushDirection, desiredForward));
        Vector3 blendedEscape = pushAlongTravel > 0.55f
            ? desiredForward * 0.25f + lateralEscape * obstacleAvoidanceStrength * 1.85f
            : desiredForward + pushDirection * obstacleAvoidanceStrength * 1.1f + lateralEscape * 0.35f;

        if (rememberSide)
        {
            currentAvoidanceObstacle = nearestCollider;
            currentAvoidanceSide = avoidanceSide;
        }

        escapeForward = FlattenDirection(blendedEscape, desiredForward);
        return true;
    }

    private bool IsAvoidanceCollider(Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        CoreTacticalShipMotor ship = candidate.GetComponentInParent<CoreTacticalShipMotor>();
        if (ship != null)
        {
            if (!avoidOtherShips || ship == this)
            {
                return false;
            }

            return ship.hullSizeMeters.z >= hullSizeMeters.z * 0.9f;
        }

        return candidate.GetComponentInParent<CoreTacticalObstacle>() != null;
    }

    private void RefreshVisualState()
    {
        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>();
        }

        Color color = selected ? selectedColor : normalColor;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer targetRenderer = renderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            MaterialPropertyBlock block = GetColorBlock();
            targetRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorPropertyId, color);
            block.SetColor(ColorPropertyId, color);
            targetRenderer.SetPropertyBlock(block);
        }
    }

    private static MaterialPropertyBlock GetColorBlock()
    {
        colorBlock ??= new MaterialPropertyBlock();
        return colorBlock;
    }

    private static Vector3 FlattenDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    private float GetNoseFirstTranslationReadiness(Vector3 travelDirection)
    {
        Vector3 currentForward = FlattenDirection(transform.forward, Vector3.forward);
        float yawError = Mathf.Abs(Vector3.SignedAngle(currentForward, travelDirection, Vector3.up));
        float readyAngle = Mathf.Max(1f, noseFirstYawReadyDeg);
        if (yawError <= readyAngle)
        {
            return 1f;
        }

        float hardGate = Mathf.Max(readyAngle + 1f, noseFirstYawHardGateDeg);
        if (yawError >= hardGate)
        {
            return 0f;
        }

        return Mathf.InverseLerp(hardGate, readyAngle, yawError);
    }

    private float GetHoldFacingTranslationReadiness()
    {
        Vector3 currentForward = FlattenDirection(transform.forward, Vector3.forward);
        float yawError = Mathf.Abs(Vector3.SignedAngle(currentForward, targetForward, Vector3.up));
        float gate = Mathf.Max(1f, holdFacingTranslationYawGateDeg);
        if (yawError <= gate)
        {
            return 1f;
        }

        if (yawError >= gate * 2.5f)
        {
            return 0.18f;
        }

        return Mathf.Lerp(1f, 0.18f, (yawError - gate) / (gate * 1.5f));
    }
}

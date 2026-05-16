using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public struct RouteEtaInfo
{
    public bool hasTarget;
    public bool routeEnabled;
    public bool canEstimate;
    public int waypointIndex;
    public int waypointCount;
    public Vector3 targetPosition;
    public float horizontalDistance;
    public float horizontalRemaining;
    public float verticalError;
    public float verticalRemaining;
    public float horizontalSpeed;
    public float horizontalClosingSpeed;
    public float verticalSpeed;
    public float verticalClosingSpeed;
    public float etaSeconds;
    public string status;
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class Leviathan : MonoBehaviour
{
    private static readonly List<Leviathan> ActiveLeviathans = new List<Leviathan>();

    public string leviathanId = "";
    public string typeId = "";
    public string zoneId = "";
    public string displayName = "";
    public string carcassItemId = "";
    public Vector3 homeCenter;
    public float zoneRadiusMeters = 300f;
    public float minY = 40f;
    public float maxY = 180f;
    public float bodyLengthMeters = 28f;
    public float bodyRadiusMeters = 5f;
    public float massKg = 5000f;
    public float maxHealth = 300f;
    public float health = 300f;
    public float maxFlightCapability = 240f;
    public float flightCapability = 240f;
    public float claudiumLiftKg = 5500f;
    public float swimForceN = 9000f;
    public float maxSpeedMS = 9f;
    public float wanderRadiusMeters = 220f;
    public float turnTorque = 1200f;
    public float headArmorMm = 28f;
    public float bodyArmorMm = 5f;
    public float ramDamageMultiplier = 1f;
    public int carcassMassKg = 2500;
    public bool isCarcass;
    public bool debugLogging;
    public float harpoonStruggleForceMultiplier = 1.15f;
    public float harpoonDiveBias = 0.35f;
    public float harpoonPanicTurnIntervalSeconds = 2.4f;

    private Rigidbody rb;
    private Vector3 wanderTarget;
    private Vector3 harpoonPanicDirection;
    private float nextHarpoonPanicTurnTime;
    private float nextWanderChangeTime;
    private float nextStatusLogTime;
    private Color visualColor = new Color(0.35f, 0.55f, 0.7f, 1f);
    private HarpoonTether activeTether;

    public Rigidbody Body
    {
        get
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            return rb;
        }
    }

    public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
    public float FlightFraction => maxFlightCapability > 0f ? Mathf.Clamp01(flightCapability / maxFlightCapability) : 0f;
    public bool CanBeClaimed => isCarcass || health <= 0f || flightCapability <= 0f;

    private void OnEnable()
    {
        if (!ActiveLeviathans.Contains(this))
        {
            ActiveLeviathans.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveLeviathans.Remove(this);
    }

    public void Initialize(string id, LeviathanTypeConfig type, LeviathanZoneConfig zone, Vector3 position)
    {
        leviathanId = id;
        typeId = type != null ? type.id : "";
        zoneId = zone != null ? zone.id : "";
        displayName = type != null ? type.DisplayNameRu : id;
        carcassItemId = type != null ? type.carcassItemId : "";
        bodyLengthMeters = type != null ? type.bodyLengthMeters : bodyLengthMeters;
        bodyRadiusMeters = type != null ? type.bodyRadiusMeters : bodyRadiusMeters;
        massKg = type != null ? type.massKg : massKg;
        maxHealth = type != null ? type.maxHealth : maxHealth;
        health = maxHealth;
        maxFlightCapability = type != null ? type.maxFlightCapability : maxFlightCapability;
        flightCapability = maxFlightCapability;
        claudiumLiftKg = type != null ? type.claudiumLiftKg : claudiumLiftKg;
        swimForceN = type != null ? type.swimForceN : swimForceN;
        maxSpeedMS = type != null ? type.maxSpeedMS : maxSpeedMS;
        wanderRadiusMeters = type != null ? type.wanderRadiusMeters : wanderRadiusMeters;
        turnTorque = type != null ? type.turnTorque : turnTorque;
        headArmorMm = type != null ? type.headArmorMm : headArmorMm;
        bodyArmorMm = type != null ? type.bodyArmorMm : bodyArmorMm;
        ramDamageMultiplier = type != null ? type.ramDamageMultiplier : ramDamageMultiplier;
        carcassMassKg = type != null ? type.CarcassMassKg : Mathf.Max(1, Mathf.RoundToInt(massKg * 0.5f));
        visualColor = type != null ? type.color : visualColor;
        homeCenter = zone != null ? zone.center : position;
        zoneRadiusMeters = zone != null ? zone.radiusMeters : zoneRadiusMeters;
        minY = zone != null ? zone.minY : minY;
        maxY = zone != null ? zone.maxY : maxY;

        transform.position = position;
        name = displayName;

        rb = GetComponent<Rigidbody>();
        rb.mass = Mathf.Max(1f, massKg);
        rb.useGravity = true;
        rb.linearDamping = 0.4f;
        rb.angularDamping = 1.6f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.direction = 2;
            capsule.radius = Mathf.Max(0.5f, bodyRadiusMeters);
            capsule.height = Mathf.Max(bodyRadiusMeters * 2f, bodyLengthMeters);
        }

        PickNewWanderTarget(true);
        EnsureVisual();
    }

    private void Start()
    {
        EnsureVisual();
    }

    private void FixedUpdate()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        EnsureVisual();

        if (health <= 0f || flightCapability <= 0f)
        {
            BecomeCarcass();
        }

        if (isCarcass)
        {
            ClampCarcassSpeed();
            return;
        }

        UpdateLift();
        if (activeTether != null && activeTether.IsAttached)
        {
            UpdateHarpoonedStruggle();
        }
        else
        {
            UpdateWander();
        }

        ClampAliveSpeed();

        if (debugLogging && Time.time >= nextStatusLogTime)
        {
            nextStatusLogTime = Time.time + 5f;
            Debug.Log("[Левиафан] " + GetStatusRu(), this);
        }
    }

    private void EnsureVisual()
    {
        if (transform.Find("Визуал левиафана") != null) return;

        Transform visualRoot = new GameObject("Визуал левиафана").transform;
        visualRoot.SetParent(transform, false);
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Тело";
        body.transform.SetParent(visualRoot, false);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(bodyRadiusMeters * 2f, bodyLengthMeters * 0.5f, bodyRadiusMeters * 2f);
        RemoveVisualCollider(body);
        SetVisualColor(body, visualColor);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Бронированная голова";
        head.transform.SetParent(visualRoot, false);
        head.transform.localPosition = Vector3.forward * bodyLengthMeters * 0.48f;
        head.transform.localScale = Vector3.one * bodyRadiusMeters * 2.2f;
        RemoveVisualCollider(head);
        SetVisualColor(head, Color.Lerp(visualColor, Color.white, 0.2f));

        GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tail.name = "Хвост";
        tail.transform.SetParent(visualRoot, false);
        tail.transform.localPosition = Vector3.back * bodyLengthMeters * 0.52f;
        tail.transform.localScale = new Vector3(bodyRadiusMeters * 1.2f, bodyRadiusMeters * 0.8f, bodyRadiusMeters * 1.8f);
        RemoveVisualCollider(tail);
        SetVisualColor(tail, Color.Lerp(visualColor, Color.black, 0.12f));

        GameObject dorsal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dorsal.name = "Спинной плавник";
        dorsal.transform.SetParent(visualRoot, false);
        dorsal.transform.localPosition = new Vector3(0f, bodyRadiusMeters * 0.75f, -bodyLengthMeters * 0.05f);
        dorsal.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        dorsal.transform.localScale = new Vector3(bodyRadiusMeters * 0.35f, bodyRadiusMeters * 1.2f, bodyLengthMeters * 0.32f);
        RemoveVisualCollider(dorsal);
        SetVisualColor(dorsal, Color.Lerp(visualColor, Color.black, 0.2f));
    }

    private static void RemoveVisualCollider(GameObject obj)
    {
        Collider collider = obj != null ? obj.GetComponent<Collider>() : null;
        if (collider == null) return;

        if (Application.isPlaying)
        {
            Destroy(collider);
        }
        else
        {
            DestroyImmediate(collider);
        }
    }

    private static void SetVisualColor(GameObject obj, Color color)
    {
        Renderer renderer = obj != null ? obj.GetComponent<Renderer>() : null;
        if (renderer == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material material = shader != null ? new Material(shader) : new Material(renderer.sharedMaterial);
        material.color = color;
        renderer.sharedMaterial = material;
    }

    public void ApplyHarpoonFatigue(float amount)
    {
        if (amount <= 0f || isCarcass) return;
        flightCapability = Mathf.Max(0f, flightCapability - amount);
        if (flightCapability <= 0f) BecomeCarcass();
    }

    public void SetHarpoonTether(HarpoonTether tether)
    {
        activeTether = tether;
        nextHarpoonPanicTurnTime = 0f;
    }

    public void ClearHarpoonTether(HarpoonTether tether)
    {
        if (activeTether == tether)
        {
            activeTether = null;
        }
    }

    public void ApplyDamage(float amount)
    {
        if (amount <= 0f || isCarcass) return;
        health = Mathf.Max(0f, health - amount);
        if (health <= 0f) BecomeCarcass();
    }

    public string GetStatusRu()
    {
        string state = isCarcass ? "туша" : "жив";
        return $"{displayName}: {state}, здоровье {health:F0}/{maxHealth:F0}, полет {flightCapability:F0}/{maxFlightCapability:F0}, масса туши {carcassMassKg} кг.";
    }

    private void UpdateLift()
    {
        float liftKg = Mathf.Min(Mathf.Max(0f, claudiumLiftKg), massKg * 1.08f) * FlightFraction;
        rb.AddForce(Vector3.up * liftKg * 9.81f, ForceMode.Force);
        if (rb.position.y < minY + 10f)
        {
            rb.AddForce(Vector3.up * massKg * 5f, ForceMode.Force);
        }
    }

    private void UpdateWander()
    {
        if (Time.time >= nextWanderChangeTime || Vector3.Distance(rb.position, wanderTarget) < bodyLengthMeters)
        {
            PickNewWanderTarget(false);
        }

        Vector3 toTarget = wanderTarget - rb.position;
        Vector3 desiredDirection = toTarget.sqrMagnitude > 1f ? toTarget.normalized : transform.forward;
        Vector3 desiredVelocity = desiredDirection * Mathf.Max(0.1f, maxSpeedMS);
        Vector3 velocityError = desiredVelocity - rb.linearVelocity;
        Vector3 force = Vector3.ClampMagnitude(velocityError * rb.mass * 0.35f, Mathf.Max(0f, swimForceN));
        rb.AddForce(force, ForceMode.Force);

        Vector3 flatDirection = Vector3.ProjectOnPlane(desiredDirection, Vector3.up);
        if (flatDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            Quaternion newRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, Mathf.Max(5f, turnTorque * 0.01f) * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
        }
    }

    private void PickNewWanderTarget(bool immediate)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(Random.value) * Mathf.Min(zoneRadiusMeters, wanderRadiusMeters);
        float y = Random.Range(Mathf.Min(minY, maxY), Mathf.Max(minY, maxY));
        wanderTarget = homeCenter + new Vector3(Mathf.Cos(angle) * radius, y - homeCenter.y, Mathf.Sin(angle) * radius);
        nextWanderChangeTime = Time.time + (immediate ? 1f : Random.Range(12f, 28f));
    }

    private void UpdateHarpoonedStruggle()
    {
        if (activeTether == null || !activeTether.IsAttached)
        {
            activeTether = null;
            UpdateWander();
            return;
        }

        Vector3 awayFromShip = rb.worldCenterOfMass - activeTether.ShipPoint;
        awayFromShip.y = 0f;
        if (awayFromShip.sqrMagnitude < 0.1f)
        {
            awayFromShip = transform.forward;
            awayFromShip.y = 0f;
        }

        if (Time.time >= nextHarpoonPanicTurnTime || harpoonPanicDirection.sqrMagnitude < 0.1f)
        {
            Vector2 random = Random.insideUnitCircle.normalized;
            harpoonPanicDirection = new Vector3(random.x, 0f, random.y);
            nextHarpoonPanicTurnTime = Time.time + Mathf.Max(0.2f, harpoonPanicTurnIntervalSeconds) * Random.Range(0.7f, 1.35f);
        }

        Vector3 dive = Vector3.down * Mathf.Clamp01(harpoonDiveBias);
        if (rb.position.y < minY)
        {
            dive = Vector3.up * 0.4f;
        }

        Vector3 direction = awayFromShip.normalized + harpoonPanicDirection * 0.35f + dive;
        if (direction.sqrMagnitude < 0.1f)
        {
            direction = awayFromShip.normalized;
        }

        direction.Normalize();

        float panicFromTension = activeTether.maxTensionKg > 0f
            ? Mathf.Clamp01(activeTether.CurrentTensionKg / activeTether.maxTensionKg)
            : 0f;
        float panic = Mathf.Lerp(0.65f, 1.35f, panicFromTension);
        float force = Mathf.Max(0f, swimForceN) * Mathf.Max(0f, harpoonStruggleForceMultiplier) * panic;
        rb.AddForce(direction * force, ForceMode.Force);

        Vector3 flatDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (flatDirection.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            Quaternion newRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, Mathf.Max(8f, turnTorque * 0.014f) * Time.fixedDeltaTime);
            rb.MoveRotation(newRotation);
        }
    }

    private void ClampAliveSpeed()
    {
        float speedLimit = Mathf.Max(0.1f, maxSpeedMS);
        if (rb.linearVelocity.magnitude > speedLimit)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * speedLimit;
        }
    }

    private void ClampCarcassSpeed()
    {
        float speedLimit = Mathf.Max(12f, maxSpeedMS * 1.5f);
        if (rb.linearVelocity.magnitude > speedLimit)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * speedLimit;
        }
    }

    private void BecomeCarcass()
    {
        if (isCarcass) return;
        isCarcass = true;
        rb.linearDamping = 0.15f;
        rb.angularDamping = 0.6f;
        Debug.Log("[Левиафан] " + displayName + " потерял полетоспособность и стал добычей.", this);
    }

    public static Leviathan FindNearest(Vector3 position, float rangeMeters, bool requireAlive, string ignoredLeviathanId = "")
    {
        Leviathan best = null;
        float bestSqr = Mathf.Max(0f, rangeMeters) * Mathf.Max(0f, rangeMeters);
        for (int i = 0; i < ActiveLeviathans.Count; i++)
        {
            Leviathan leviathan = ActiveLeviathans[i];
            if (leviathan == null) continue;
            if (requireAlive && leviathan.CanBeClaimed) continue;
            if (!string.IsNullOrWhiteSpace(ignoredLeviathanId) && leviathan.leviathanId == ignoredLeviathanId) continue;

            float sqr = (leviathan.transform.position - position).sqrMagnitude;
            if (sqr > bestSqr) continue;

            best = leviathan;
            bestSqr = sqr;
        }

        return best;
    }

    public static Leviathan FindById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        for (int i = 0; i < ActiveLeviathans.Count; i++)
        {
            Leviathan leviathan = ActiveLeviathans[i];
            if (leviathan != null && leviathan.leviathanId == id) return leviathan;
        }

        return null;
    }

    public static int CountInZone(string zoneId)
    {
        int count = 0;
        for (int i = 0; i < ActiveLeviathans.Count; i++)
        {
            Leviathan leviathan = ActiveLeviathans[i];
            if (leviathan != null && leviathan.zoneId == zoneId) count++;
        }

        return count;
    }
}

[DisallowMultipleComponent]
public class HarpoonTether : MonoBehaviour
{
    public ShipPhysics ownerShip;
    public Leviathan target;
    public float ropeLengthMeters = 80f;
    public float maxTensionKg = 3000f;
    public float stiffnessNPerMeter = 320f;
    public float dampingNsPerMeter = 90f;
    public float reelForceN;
    public float fatiguePerSecond = 14f;
    public float collectionRadiusMeters = 14f;
    public float carcassWinchSpeedMetersPerSecond = 6f;
    public float currentTensionN;
    public string lastMessage = "";

    private Rigidbody shipBody;
    private Rigidbody targetBody;
    private LineRenderer lineRenderer;
    private bool attached;

    public bool IsAttached => attached && ownerShip != null && target != null;
    public float CurrentTensionKg => currentTensionN / 9.81f;
    public Vector3 ShipPoint => shipBody != null ? shipBody.worldCenterOfMass : transform.position;
    public Vector3 TargetPoint => targetBody != null ? targetBody.worldCenterOfMass : transform.position;
    public float DistanceToTargetMeters
    {
        get
        {
            if (shipBody == null || targetBody == null) return 0f;
            return Vector3.Distance(shipBody.worldCenterOfMass, targetBody.worldCenterOfMass);
        }
    }

    public bool TargetInCollectionRadius => IsAttached && target != null && target.CanBeClaimed && DistanceToTargetMeters <= Mathf.Max(0.1f, collectionRadiusMeters);

    public void Attach(ShipPhysics ship, Leviathan leviathan)
    {
        ownerShip = ship;
        target = leviathan;
        shipBody = ship != null ? ship.GetComponent<Rigidbody>() : null;
        targetBody = leviathan != null ? leviathan.Body : null;
        attached = shipBody != null && targetBody != null;

        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();

        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.18f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = new Color(1f, 0.75f, 0.2f, 1f);
        lineRenderer.endColor = new Color(1f, 0.35f, 0.05f, 1f);

        if (attached)
        {
            float initialDistance = Vector3.Distance(shipBody.worldCenterOfMass, targetBody.worldCenterOfMass);
            ropeLengthMeters = Mathf.Min(Mathf.Max(1f, ropeLengthMeters), Mathf.Max(1f, initialDistance * 0.92f));
            lastMessage = "Гарпун держит " + target.displayName + ".";
            target.SetHarpoonTether(this);
        }
        else
        {
            lastMessage = "Гарпун не смог закрепиться.";
        }
    }

    private void FixedUpdate()
    {
        if (!IsAttached)
        {
            Detach("Трос потерял цель.");
            return;
        }

        Vector3 shipPoint = shipBody.worldCenterOfMass;
        Vector3 targetPoint = targetBody.worldCenterOfMass;
        Vector3 delta = targetPoint - shipPoint;
        float distance = delta.magnitude;
        if (distance < 0.01f)
        {
            currentTensionN = 0f;
            UpdateLine(shipPoint, targetPoint);
            return;
        }

        Vector3 direction = delta / distance;
        bool winchingCarcass = target.CanBeClaimed;
        if (winchingCarcass)
        {
            float targetRopeLength = Mathf.Max(0.5f, collectionRadiusMeters * 0.65f);
            ropeLengthMeters = Mathf.MoveTowards(
                ropeLengthMeters,
                targetRopeLength,
                Mathf.Max(0.1f, carcassWinchSpeedMetersPerSecond) * Time.fixedDeltaTime);
        }

        float stretch = Mathf.Max(0f, distance - Mathf.Max(0.5f, ropeLengthMeters));
        float separatingSpeed = Vector3.Dot(targetBody.linearVelocity - shipBody.linearVelocity, direction);
        float dampingForce = separatingSpeed > 0f ? separatingSpeed * Mathf.Max(0f, dampingNsPerMeter) : 0f;
        currentTensionN = stretch * Mathf.Max(0f, stiffnessNPerMeter) + dampingForce;
        if (stretch > 0.01f) currentTensionN += Mathf.Max(0f, reelForceN) * (winchingCarcass ? 1.8f : 1f);

        float maxTensionN = Mathf.Max(1f, maxTensionKg) * 9.81f;
        if (currentTensionN > maxTensionN)
        {
            Detach($"Гарпун оборвался: натяжение {CurrentTensionKg:F0}/{maxTensionKg:F0} кг.");
            return;
        }

        if (winchingCarcass)
        {
            if (currentTensionN > 0f)
            {
                shipBody.AddForce(direction * currentTensionN, ForceMode.Force);
                targetBody.AddForce(-direction * currentTensionN, ForceMode.Force);
            }

            lastMessage = TargetInCollectionRadius
                ? $"Туша в радиусе сбора: {distance:F1}/{collectionRadiusMeters:F1} м. Можно грузить."
                : $"Лебедка тянет тушу: {distance:F1}/{collectionRadiusMeters:F1} м, трос {ropeLengthMeters:F1} м, натяжение {CurrentTensionKg:F0}/{maxTensionKg:F0} кг.";
            UpdateLine(shipPoint, targetPoint);
            return;
        }

        if (currentTensionN > 0f)
        {
            shipBody.AddForce(direction * currentTensionN, ForceMode.Force);
            targetBody.AddForce(-direction * currentTensionN, ForceMode.Force);

            float fatigueScale = Mathf.Clamp01(currentTensionN / maxTensionN);
            target.ApplyHarpoonFatigue(Mathf.Max(0f, fatiguePerSecond) * fatigueScale * Time.fixedDeltaTime);
            lastMessage = $"Натяжение {CurrentTensionKg:F0}/{maxTensionKg:F0} кг, полет цели {target.flightCapability:F0}/{target.maxFlightCapability:F0}.";
        }
        else
        {
            lastMessage = "Трос провис, натяжения нет.";
        }

        UpdateLine(shipPoint, targetPoint);
    }

    public void Detach(string reason)
    {
        if (!string.IsNullOrWhiteSpace(reason)) lastMessage = reason;
        attached = false;
        currentTensionN = 0f;
        if (target != null) target.ClearHarpoonTether(this);
        if (lineRenderer != null) lineRenderer.enabled = false;
        if (ownerShip != null) ownerShip.ClearHarpoonReference(this, lastMessage);
        Destroy(gameObject);
    }

    private void UpdateLine(Vector3 shipPoint, Vector3 targetPoint)
    {
        if (lineRenderer == null) return;
        lineRenderer.enabled = true;
        lineRenderer.SetPosition(0, shipPoint);
        lineRenderer.SetPosition(1, targetPoint);
    }
}

[DisallowMultipleComponent]
public class LeviathanManager : MonoBehaviour
{
    public MetaGameState metaGameState;
    public bool spawnLeviathansOnPlay = true;
    public float syncIntervalSeconds = 2f;

    private Transform leviathanRoot;
    private float nextSyncTime;

    private MetaGameState Meta
    {
        get
        {
            if (metaGameState == null) metaGameState = FindFirstObjectByType<MetaGameState>();
            return metaGameState;
        }
    }

    private void Start()
    {
        SpawnConfiguredLeviathans();
    }

    private void Update()
    {
        if (!spawnLeviathansOnPlay || Time.unscaledTime < nextSyncTime) return;
        nextSyncTime = Time.unscaledTime + Mathf.Max(0.2f, syncIntervalSeconds);
        SpawnConfiguredLeviathans();
    }

    public void SpawnConfiguredLeviathans(bool forceRebuild = false)
    {
        if (!Application.isPlaying || !spawnLeviathansOnPlay) return;

        MetaGameState meta = Meta;
        if (meta == null || meta.WorldConfig == null || !meta.WorldConfig.isLoaded) return;

        if (leviathanRoot != null && forceRebuild)
        {
            Destroy(leviathanRoot.gameObject);
            leviathanRoot = null;
        }

        if (leviathanRoot == null)
        {
            GameObject root = new GameObject("Левиафаны");
            leviathanRoot = root.transform;
        }

        for (int i = 0; i < meta.WorldConfig.leviathanZones.Count; i++)
        {
            LeviathanZoneConfig zone = meta.WorldConfig.leviathanZones[i];
            if (zone == null || string.IsNullOrWhiteSpace(zone.id)) continue;

            int activeInZone = Leviathan.CountInZone(zone.id);
            int desired = Mathf.Min(Mathf.Max(0, zone.initialCount), Mathf.Max(0, zone.maxActive));
            for (int spawnIndex = activeInZone; spawnIndex < desired; spawnIndex++)
            {
                LeviathanTypeConfig type = PickType(meta.WorldConfig, zone, spawnIndex);
                if (type == null) continue;

                string id = zone.id + "_" + type.id + "_" + spawnIndex.ToString("00");
                if (Leviathan.FindById(id) != null) continue;

                CreateLeviathan(id, type, zone, PickSpawnPosition(zone, spawnIndex));
            }
        }
    }

    private static LeviathanTypeConfig PickType(WorldConfigDatabase config, LeviathanZoneConfig zone, int index)
    {
        if (config == null || zone == null || zone.leviathanTypeIds == null || zone.leviathanTypeIds.Count == 0) return null;
        for (int attempt = 0; attempt < zone.leviathanTypeIds.Count; attempt++)
        {
            string typeId = zone.leviathanTypeIds[(index + attempt) % zone.leviathanTypeIds.Count];
            LeviathanTypeConfig type = config.GetLeviathanType(typeId);
            if (type != null) return type;
        }

        return null;
    }

    private static Vector3 PickSpawnPosition(LeviathanZoneConfig zone, int index)
    {
        float angle = (index * 137.5f) * Mathf.Deg2Rad;
        float radius = Mathf.Lerp(zone.radiusMeters * 0.18f, zone.radiusMeters * 0.62f, Mathf.Repeat(index * 0.37f, 1f));
        float y = Mathf.Lerp(zone.minY, zone.maxY, 0.35f + 0.3f * Mathf.Repeat(index * 0.23f, 1f));
        return zone.center + new Vector3(Mathf.Cos(angle) * radius, y - zone.center.y, Mathf.Sin(angle) * radius);
    }

    private void CreateLeviathan(string id, LeviathanTypeConfig type, LeviathanZoneConfig zone, Vector3 position)
    {
        GameObject leviathanObject = new GameObject(type.DisplayNameRu);
        leviathanObject.transform.SetParent(leviathanRoot, false);
        leviathanObject.transform.position = position;

        Rigidbody body = leviathanObject.AddComponent<Rigidbody>();
        body.mass = Mathf.Max(1f, type.massKg);
        body.useGravity = true;

        CapsuleCollider collider = leviathanObject.AddComponent<CapsuleCollider>();
        collider.direction = 2;
        collider.radius = Mathf.Max(0.5f, type.bodyRadiusMeters);
        collider.height = Mathf.Max(type.bodyLengthMeters, type.bodyRadiusMeters * 2f);

        Leviathan leviathan = leviathanObject.AddComponent<Leviathan>();
        leviathan.Initialize(id, type, zone, position);
    }

    private static void BuildVisual(Transform parent, LeviathanTypeConfig type)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Тело";
        body.transform.SetParent(parent, false);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(type.bodyRadiusMeters * 2f, type.bodyLengthMeters * 0.5f, type.bodyRadiusMeters * 2f);
        DestroyImmediateSafe(body.GetComponent<Collider>());
        SetColor(body, type.color);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Бронированная голова";
        head.transform.SetParent(parent, false);
        head.transform.localPosition = Vector3.forward * type.bodyLengthMeters * 0.48f;
        head.transform.localScale = Vector3.one * type.bodyRadiusMeters * 2.2f;
        DestroyImmediateSafe(head.GetComponent<Collider>());
        SetColor(head, Color.Lerp(type.color, Color.white, 0.18f));

        GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tail.name = "Хвост";
        tail.transform.SetParent(parent, false);
        tail.transform.localPosition = Vector3.back * type.bodyLengthMeters * 0.52f;
        tail.transform.localScale = new Vector3(type.bodyRadiusMeters * 1.2f, type.bodyRadiusMeters * 0.8f, type.bodyRadiusMeters * 1.8f);
        DestroyImmediateSafe(tail.GetComponent<Collider>());
        SetColor(tail, Color.Lerp(type.color, Color.black, 0.12f));
    }

    private static void SetColor(GameObject obj, Color color)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;
        renderer.material = new Material(Shader.Find("Standard"));
        renderer.material.color = color;
    }

    private static void DestroyImmediateSafe(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }
}

[RequireComponent(typeof(Rigidbody))]
public class ShipPhysics : MonoBehaviour
{
    private Rigidbody rb;

    [Header("Параметры корабля")]
    public float baseMass = 1000f; // Стартовая масса 1000кг
    [Tooltip("Максимальная взлетная масса, которую допускает корпус, в килограммах.")]
    public float hullMaxTakeoffMassKg = 2000f;
    [HideInInspector] public float cargoMassKg;
    
    [Header("Двигатель")]
    [Tooltip("Мощность, которую двигатель выдает на ручке 100%, в киловаттах.")]
    public float enginePowerKwAt100 = 80f;
    [Tooltip("Тип топлива. Энергоемкость берется из Item.csv по этому id.")]
    public string engineFuelId = "wood";
    [Tooltip("Доля энергии топлива, которая превращается в полезную мощность двигателя.")]
    public float engineFuelEfficiency = 0.32f;
    [HideInInspector] public float engineFuelEnergyKwhPerKg = 4f;
    [HideInInspector] public float engineFuelStockKg = 0f;
    
    [Header("Параметры винта")]
    [Tooltip("Скорость, после которой винт больше не может разгонять корабль, м/с.")]
    public float propellerMaxSpeedMS = 30f;
    [Tooltip("Доля мощности двигателя, которая превращается в полезную тягу винта.")]
    public float propellerEfficiency = 0.8f;
    [Tooltip("Максимальная статическая тяга винта, кгс.")]
    public float propellerMaxThrustKgf = 220f;
    
    [Header("Клавдиевый контур")]
    [Tooltip("Ресурс клавдия в грузовом списке корабля.")]
    public string claudiumResourceId = "claudium";
    [HideInInspector] public float claudiumStock = 0f;
    [Tooltip("Расход клавдия в секунду на одну тонну поддерживаемой массы.")]
    public float claudiumConsumptionPerTonSecond = 0.05f;
    [Tooltip("Сколько килограммов подъема дает один киловатт мощности двигателя.")]
    public float claudiumLiftEfficiency = 10f;
    [Tooltip("Максимальная масса в килограммах, которую контур способен поддерживать.")]
    public float claudiumMaxLiftKg = 1000f;
    [Tooltip("Скорость сглаживания подъемной силы. Чем больше значение, тем быстрее контур выходит на нужную силу.")]
    public float claudiumLiftSmoothing = 2f;
    [HideInInspector] public float claudiumCurrentLiftN;
    [HideInInspector] public float claudiumPowerDrawWatts;
    [HideInInspector] public float claudiumPowerDrawKw;
    [HideInInspector] public float claudiumRequestedLiftKg;

    [Header("Харвестеринг")]
    [Tooltip("Включает установленный газовый харвестер. Модуль работает циклами и кладет целые кг концентрата в общий груз корабля.")]
    public bool gasHarvesterEnabled;
    [Tooltip("Сколько кубометров облака всасывается и конденсируется в секунду.")]
    public float gasHarvesterVolumeM3PerSecond;
    [Tooltip("Сколько мощности харвестер забирает после клавдиевого контура и до винта.")]
    public float gasHarvesterPowerDrawKw;
    [Tooltip("Радиус забора. Добыча возможна, если этот радиус пересекается с облаком.")]
    public float gasHarvesterRadiusMeters;
    [Tooltip("Длительность одного цикла добычи. Контакт с облаком проверяется в начале и в конце цикла.")]
    public float gasHarvesterCycleSeconds = 5f;
    [HideInInspector] public float gasHarvesterPowerDrawActualKw;
    [HideInInspector] public float gasHarvesterCycleProgressSeconds;
    [HideInInspector] public float gasHarvesterBufferKg;
    [HideInInspector] public string gasHarvesterActiveCloudId = "";
    [HideInInspector] public string gasHarvesterLastMessage = "";

    [Header("Майнинг")]
    [Tooltip("Противоударный кузов ловит падающие куски. Пойманная руда складывается в общий груз корабля.")]
    public float miningImpactHoldCapacityKg;
    [Tooltip("Радиус сбора падающих кусков вокруг корабля.")]
    public float miningCatchRadiusMeters = 10f;
    [Tooltip("Дальность временной кнопки выстрела по глыбе.")]
    public float miningManualShotRangeMeters = 180f;
    [HideInInspector] public string miningLastMessage = "";

    [Header("Охота на левиафанов")]
    [Tooltip("Дальность ручного выстрела гарпуном по ближайшему левиафану.")]
    public float harpoonRangeMeters = 280f;
    [Tooltip("Рабочая длина троса. Если цель дальше этой длины, трос натягивается и тянет обе стороны.")]
    public float harpoonRopeLengthMeters = 160f;
    [Tooltip("Максимальное натяжение, которое выдерживает гарпун, в килограммах силы.")]
    public float harpoonMaxTensionKg = 4500f;
    [Tooltip("Жесткость троса: сколько ньютонов появляется за каждый метр растяжения.")]
    public float harpoonStiffnessNPerMeter = 360f;
    [Tooltip("Демпфер троса: гасит рывок, если цель и корабль расходятся.")]
    public float harpoonDampingNsPerMeter = 110f;
    [Tooltip("Постоянная подтяжка лебедкой, когда трос уже натянут.")]
    public float harpoonReelForceN = 600f;
    [Tooltip("Сколько полетоспособности левиафан теряет в секунду при полном натяжении троса.")]
    public float harpoonFatiguePerSecond = 18f;
    [Tooltip("Р Р°РґРёСѓСЃ, РІ РєРѕС‚РѕСЂРѕРј С‚СѓС€Сѓ РјРѕР¶РЅРѕ РїРѕРіСЂСѓР·РёС‚СЊ РІ С‚СЂСЋРј.")]
    public float harpoonCarcassCollectionRadiusMeters = 14f;
    [Tooltip("РЎРєРѕСЂРѕСЃС‚СЊ, СЃ РєРѕС‚РѕСЂРѕР№ Р»РµР±РµРґРєР° СѓРєРѕСЂР°С‡РёРІР°РµС‚ С‚СЂРѕСЃ РїРѕСЃР»Рµ С‚РѕРіРѕ, РєР°Рє С†РµР»СЊ СЃС‚Р°Р»Р° С‚СѓС€РµР№.")]
    public float harpoonCarcassWinchSpeedMS = 7f;
    [Header("Охотничий автопилот")]
    [Tooltip("Если включено, корабль сам подходит к ближайшему левиафану, стреляет гарпуном и забирает тушу после подтяжки.")]
    public bool leviathanHuntAutopilotEnabled;
    [Tooltip("Радиус поиска цели для охотничьего автопилота.")]
    public float leviathanHuntSearchRangeMeters = 900f;
    [Tooltip("На какой дистанции автопилот считает, что можно стрелять гарпуном.")]
    public float leviathanHuntEngageRangeMeters = 240f;
    [Tooltip("На какой дистанции от живой цели автопилот старается остановиться перед выстрелом.")]
    public float leviathanHuntApproachDistanceMeters = 90f;
    [Tooltip("Минимальная высота, ниже которой охотничий автопилот не назначает точку подхода.")]
    public float leviathanHuntMinimumAltitudeMeters = 45f;
    [Tooltip("Пауза между автоматическими выстрелами гарпуном.")]
    public float leviathanHuntShotCooldownSeconds = 4f;
    [Tooltip("На сколько секунд автоохота игнорирует цель, которая только что оборвала трос.")]
    public float leviathanHuntBrokenTargetCooldownSeconds = 25f;
    [HideInInspector] public string leviathanHuntAutopilotMessage = "";
    [HideInInspector] public string harpoonLastMessage = "";
    [HideInInspector] public HarpoonTether activeHarpoon;
    
    [Header("Гироскопический поворот")]
    public float gyroTurnTorque = 12000f; // Максимальный внутренний момент поворота корпуса, Н*м
    public float gyroTurnDamping = 0.8f; // Демпфирование, которое гасит лишнюю угловую скорость
    [HideInInspector] public float currentGyroTurnTorque = 0f;

    [Header("Аэродинамика")]
    public float airDensity = 1.225f; // Плотность воздуха (1.225 на уровне моря)
    public float dragCoefficient = 1.1f; // Коэффициент формы Cd (1.1 для контейнера)
    public float frontalArea = 6.3f; // Лобовая площадь (кв.м)
    public float sideResistance = 2.0f; // Сопротивление боковому сносу (эффект киля)
    public float verticalAreaFactor = 4.0f; // Во сколько раз площадь "пуза" больше лобовой площади
    
    [Header("Лимиты скорости подъема")]
    public float maxStructuralVerticalSpeed = 5.0f; // Предел прочности (конструкционный)
    public float maxAutoVerticalSpeed = 1.0f;        // Лимит автопилота
    
    [Header("Окружающая среда")]
    public Vector3 windVelocity = Vector3.zero; // Глобальный вектор ветра (м/с)

    // Рассчитанный текущий коэффициент сопротивления (используется для физики)
    public float CurrentAeroDrag => 0.5f * airDensity * dragCoefficient * frontalArea;

    [Header("Автопилот и Системы")]
    public bool autoStabilizeAtStart = true; // Новая галочка
    public bool altitudeHold = false;
    public float targetAltitude = 0f;
    public float altStiffness = 0.2f; // P (Было 0.5 - слишком резко)
    public float altDamping = 1.2f;   // D
    public float altDriftTolerance = 0.15f; // Допуск дрейфа (м)

    public bool cruiseControl = false;
    public float targetSpeedMS = 0f;
    [Header("Автопилот курса")]
    public bool headingHold = false;
    public float targetHeading = 0f;      // Целевой курс (градусы 0-360)
    public float headingStiffness = 0.5f; // P-терм (Жесткость реакции на ошибку курса)
    public float headingDamping = 0.5f;   // D-терм (Демпфирование по угловой скорости)
    public float maxAutoTurnRateDeg = 5.0f; // Лимит угловой скорости для автопилота (°/сек)
    public float maxStructuralTurnRateDeg = 15.0f; // Конструкционный лимит угловой скорости (°/сек)
    
    [Header("Путевая машина")]
    public bool routeEnabled = false;
    public System.Collections.Generic.List<Vector3> waypoints = new System.Collections.Generic.List<Vector3>();
    public float waypointRadius = 10f; // Радиус засчитывания точки
    public float routeArrivalSpeedMS = 1f; // Точка засчитывается только если горизонтальная скорость ниже этого порога
    public float routeBrakeAccelerationMS2 = 2.5f; // Расчетное торможение для раннего сброса скорости перед точкой
    [HideInInspector] public int currentWaypointIndex = 0;

    [Header("Удержание позиции")]
    public bool positionHold = false;
    public Vector3 targetHoldPosition = Vector3.zero;
    public float positionHoldRadius = 5f;
    public float positionHoldMaxSpeedMS = 8f;
    public float positionHoldStiffness = 0.35f;
    public float positionHoldDamping = 0.9f;
    
    [Header("Настройки тяги винта")]
    public float propellerPitch = 0f;    // Текущее задание тяги (-1..1)
    public float speedStiffness = 0.8f;  // Насколько активно круиз меняет тягу
    public float speedDamping = 0.3f;    // Демпфирование тяги

    [Header("Текущее управление (для чтения/записи из интерфейса)")]
    [HideInInspector] public float thrustInput; // -1 полный реверс, 0 нет тяги, 1 полный ход вперед
    [HideInInspector] public float turnInput;   // -1 влево, 1 вправо
    [HideInInspector] public float targetTrimMass = 1000f; // Масса для триммирования (кг)
    [HideInInspector] public float liftInput;   // -1 вниз, 1 вверх (Точная доводка +-10%)
    
    // ==========================================
    // МОДУЛИ (Дочерние объекты)
    // ==========================================
    [Header("Установленные модули")]
    
    [HideInInspector] public float currentGasLift; 
    [HideInInspector] public float activeLiftForce; 
    
    [FormerlySerializedAs("targetMainEngineRPM")]
    [HideInInspector] public float enginePowerLever = 0.88f;
    [HideInInspector] public float engineEfficiencyCurrent;
    [HideInInspector] public float engineGeneratedPowerKw;
    [HideInInspector] public float engineFuelConsumptionKgPerSecond;
    [HideInInspector] public float engineMinimumPowerLever;
    [HideInInspector] public bool engineHasFuel = true;
    [HideInInspector] public float propellerInputPowerKw;
    [HideInInspector] public float propellerUsefulPowerKw;
    [HideInInspector] public float propellerCalculatedEfficiency;
    [HideInInspector] public float propellerThrustKgf;
    private bool wasAltitudeHold = false;
    private float altIntegral = 0f; // Память автопилота (I-терм)
    private bool routeWasEnabled = false;
    private bool routePreviousAltitudeHold = false;
    private bool routePreviousCruiseControl = false;
    private bool routePreviousHeadingHold = false;
    private bool routePreviousPositionHold = false;
    private bool positionHoldWasEnabled = false;
    private bool leviathanHuntAutopilotWasEnabled = false;
    private float nextLeviathanHuntShotTime;
    private string leviathanHuntIgnoredTargetId = "";
    private float leviathanHuntIgnoreUntilTime;
    private bool leviathanHuntHasTetherHoldPosition;
    private Vector3 leviathanHuntTetherHoldPosition;
    private string gasHarvesterCycleCloudId = "";
    private MetaGameState cachedMetaGameState;
    
    // Единая ручка управления мощностью (Обороты для CSU / Газ для Manual)
    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.mass = GetTotalMassKg();
        rb.useGravity = true;
        
        rb.angularDamping = 2f; 
        rb.linearDamping = 0f; 
    }

    public void RefreshRuntimeShipSettings()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            rb.mass = GetTotalMassKg();
        }
    }

    public float GetTotalMassKg()
    {
        return Mathf.Max(1f, baseMass + Mathf.Max(0f, cargoMassKg));
    }

    public bool TryCollectMiningFragment(string oreItemId, int amountKg, out string reason)
    {
        reason = "";
        if (string.IsNullOrWhiteSpace(oreItemId) || amountKg <= 0)
        {
            return true;
        }

        if (miningImpactHoldCapacityKg <= 0f)
        {
            reason = "На корабле нет противоударного кузова.";
            miningLastMessage = reason;
            return false;
        }

        MetaGameState meta = ResolveMetaGameState();
        if (meta == null || meta.progress == null)
        {
            reason = "Майнинг ждет MetaGameState.";
            miningLastMessage = reason;
            return false;
        }

        float freeCargoKg = meta.GetRemainingShipCargoCapacityKg();
        if (freeCargoKg + 0.001f < amountKg)
        {
            reason = $"Не хватает грузоподъемности: нужно {amountKg} кг, свободно {Mathf.FloorToInt(freeCargoKg)} кг.";
            miningLastMessage = reason;
            StopRouteForFullMiningHold();
            return false;
        }

        if (!meta.TryAddShipCargoFromRuntime(oreItemId, amountKg, out reason))
        {
            miningLastMessage = reason;
            return false;
        }

        miningLastMessage = "Поймано в груз " + amountKg + " кг: " + oreItemId + ".";
        return true;
    }

    public bool TryFireHarpoonAtNearestLeviathan(out string reason)
    {
        reason = "";
        if (activeHarpoon != null && activeHarpoon.IsAttached)
        {
            reason = "Гарпун уже держит цель.";
            harpoonLastMessage = reason;
            return false;
        }

        Leviathan target = Leviathan.FindNearest(transform.position, Mathf.Max(0f, harpoonRangeMeters), true);
        if (target == null)
        {
            reason = "В радиусе гарпуна нет живого левиафана.";
            harpoonLastMessage = reason;
            return false;
        }

        GameObject tetherObject = new GameObject("Гарпунный трос");
        tetherObject.transform.SetParent(transform, false);
        activeHarpoon = tetherObject.AddComponent<HarpoonTether>();
        activeHarpoon.ropeLengthMeters = Mathf.Max(1f, harpoonRopeLengthMeters);
        activeHarpoon.maxTensionKg = Mathf.Max(1f, harpoonMaxTensionKg);
        activeHarpoon.stiffnessNPerMeter = Mathf.Max(0f, harpoonStiffnessNPerMeter);
        activeHarpoon.dampingNsPerMeter = Mathf.Max(0f, harpoonDampingNsPerMeter);
        activeHarpoon.reelForceN = Mathf.Max(0f, harpoonReelForceN);
        activeHarpoon.fatiguePerSecond = Mathf.Max(0f, harpoonFatiguePerSecond);
        activeHarpoon.collectionRadiusMeters = Mathf.Max(0.1f, harpoonCarcassCollectionRadiusMeters);
        activeHarpoon.carcassWinchSpeedMetersPerSecond = Mathf.Max(0.1f, harpoonCarcassWinchSpeedMS);
        activeHarpoon.Attach(this, target);

        reason = activeHarpoon.lastMessage;
        harpoonLastMessage = reason;
        return activeHarpoon.IsAttached;
    }

    public void DetachHarpoon(string reason = "Гарпун отцеплен.")
    {
        if (activeHarpoon == null)
        {
            harpoonLastMessage = reason;
            return;
        }

        HarpoonTether tether = activeHarpoon;
        activeHarpoon = null;
        tether.Detach(reason);
    }

    public void StopLeviathanHuntForDocking()
    {
        leviathanHuntAutopilotEnabled = false;
        leviathanHuntAutopilotWasEnabled = false;
        leviathanHuntAutopilotMessage = "Охота остановлена стыковкой.";

        if (activeHarpoon != null)
        {
            DetachHarpoon("Гарпун отцеплен перед стыковкой.");
        }

        leviathanHuntHasTetherHoldPosition = false;
        routeEnabled = false;
        routeWasEnabled = false;
        positionHold = true;
        positionHoldWasEnabled = true;
        targetHoldPosition = transform.position;
        targetSpeedMS = 0f;
        thrustInput = 0f;
        turnInput = 0f;
    }

    public void ClearHarpoonReference(HarpoonTether tether, string reason)
    {
        RegisterLeviathanHuntTetherLoss(tether, reason);

        if (activeHarpoon == tether)
        {
            activeHarpoon = null;
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            harpoonLastMessage = reason;
        }
    }

    private void RegisterLeviathanHuntTetherLoss(HarpoonTether tether, string reason)
    {
        if (!leviathanHuntAutopilotEnabled || tether == null) return;

        leviathanHuntHasTetherHoldPosition = false;
        nextLeviathanHuntShotTime = Time.time + Mathf.Max(0.1f, leviathanHuntShotCooldownSeconds);

        Leviathan target = tether.target;
        if (target == null) return;

        string text = string.IsNullOrWhiteSpace(reason) ? "" : reason.ToLowerInvariant();
        bool wasBreak = text.Contains("обор") || text.Contains("break");
        if (!wasBreak) return;

        leviathanHuntIgnoredTargetId = target.leviathanId;
        leviathanHuntIgnoreUntilTime = Time.time + Mathf.Max(0.1f, leviathanHuntBrokenTargetCooldownSeconds);
        leviathanHuntAutopilotMessage = $"Трос оборвался на цели {target.displayName}. Автоохота временно ищет другую цель.";
    }

    public bool TryClaimHarpoonedLeviathan(out string reason)
    {
        reason = "";
        HarpoonTether tether = activeHarpoon;
        Leviathan target = tether != null ? tether.target : null;
        if (target == null)
        {
            reason = "Гарпун не держит левиафана.";
            harpoonLastMessage = reason;
            return false;
        }

        if (!target.CanBeClaimed)
        {
            reason = "Левиафан еще держится в воздухе: ослабь здоровье или полетоспособность.";
            harpoonLastMessage = reason;
            return false;
        }

        if (!tether.TargetInCollectionRadius)
        {
            float distance = tether.DistanceToTargetMeters;
            reason = $"Туша еще далеко: {distance:F1}/{tether.collectionRadiusMeters:F1} м. Лебедка подтягивает ее к кораблю.";
            harpoonLastMessage = reason;
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.carcassItemId))
        {
            reason = "У этого вида не задан ресурс туши.";
            harpoonLastMessage = reason;
            return false;
        }

        MetaGameState meta = ResolveMetaGameState();
        if (meta == null || meta.progress == null)
        {
            reason = "Охота ждет MetaGameState.";
            harpoonLastMessage = reason;
            return false;
        }

        int amountKg = Mathf.Max(1, target.carcassMassKg);
        if (!meta.TryAddShipCargoFromRuntime(target.carcassItemId, amountKg, out reason))
        {
            harpoonLastMessage = reason;
            return false;
        }

        reason = $"Туша погружена: {target.displayName}, {amountKg} кг.";
        harpoonLastMessage = reason;
        tether.Detach("Туша погружена.");
        Destroy(target.gameObject);
        return true;
    }

    public string GetHarpoonStatusRu()
    {
        HarpoonTether tether = activeHarpoon;
        if (tether == null || !tether.IsAttached)
        {
            return string.IsNullOrWhiteSpace(harpoonLastMessage) ? "Гарпун готов." : harpoonLastMessage;
        }

        Leviathan target = tether.target;
        string targetText = target != null ? target.GetStatusRu() : "цель потеряна";
        return $"{targetText}\nТрос: {tether.CurrentTensionKg:F0}/{harpoonMaxTensionKg:F0} кг. {tether.lastMessage}";
    }

    private void UpdateLeviathanHuntAutopilot()
    {
        if (!leviathanHuntAutopilotEnabled)
        {
            if (leviathanHuntAutopilotWasEnabled)
            {
                leviathanHuntHasTetherHoldPosition = false;
                routeEnabled = false;
                routeWasEnabled = false;
                positionHold = true;
                positionHoldWasEnabled = true;
                targetHoldPosition = transform.position;
                targetSpeedMS = 0f;
                thrustInput = 0f;
                turnInput = 0f;
                leviathanHuntAutopilotMessage = "Автоохота выключена, удерживаю текущую позицию.";
            }

            leviathanHuntAutopilotWasEnabled = false;
            return;
        }

        leviathanHuntAutopilotWasEnabled = true;
        if (rb == null) rb = GetComponent<Rigidbody>();

        HarpoonTether tether = activeHarpoon;
        if (tether != null && tether.IsAttached)
        {
            if (!leviathanHuntHasTetherHoldPosition)
            {
                leviathanHuntTetherHoldPosition = transform.position;
                leviathanHuntHasTetherHoldPosition = true;
            }

            routeEnabled = false;
            routeWasEnabled = false;
            positionHold = true;
            positionHoldWasEnabled = true;
            targetHoldPosition = leviathanHuntTetherHoldPosition;
            altitudeHold = true;
            targetAltitude = Mathf.Max(leviathanHuntTetherHoldPosition.y, leviathanHuntMinimumAltitudeMeters);
            cruiseControl = true;
            headingHold = true;

            Leviathan target = tether.target;
            if (target != null && !target.CanBeClaimed)
            {
                Vector3 away = FlattenHorizontal(transform.position - target.transform.position);
                if (away.sqrMagnitude > 0.04f)
                {
                    targetHeading = HeadingFromVector(away);
                }
            }

            if (target != null && target.CanBeClaimed && tether.TargetInCollectionRadius)
            {
                TryClaimHarpoonedLeviathan(out string claimMessage);
                leviathanHuntHasTetherHoldPosition = false;
                leviathanHuntAutopilotMessage = claimMessage;
                return;
            }

            float drift = Vector3.Distance(FlattenHorizontal(transform.position), FlattenHorizontal(leviathanHuntTetherHoldPosition));
            leviathanHuntAutopilotMessage = $"{tether.lastMessage} Удерживаю точку захвата, снос {drift:F1} м.";
            return;
        }

        leviathanHuntHasTetherHoldPosition = false;

        if (!string.IsNullOrWhiteSpace(leviathanHuntIgnoredTargetId) && Time.time >= leviathanHuntIgnoreUntilTime)
        {
            leviathanHuntIgnoredTargetId = "";
        }

        string ignoredTarget = !string.IsNullOrWhiteSpace(leviathanHuntIgnoredTargetId) && Time.time < leviathanHuntIgnoreUntilTime
            ? leviathanHuntIgnoredTargetId
            : "";
        Leviathan nearest = Leviathan.FindNearest(transform.position, Mathf.Max(0f, leviathanHuntSearchRangeMeters), true, ignoredTarget);
        if (nearest == null)
        {
            if (!string.IsNullOrWhiteSpace(ignoredTarget))
            {
                float wait = Mathf.Max(0f, leviathanHuntIgnoreUntilTime - Time.time);
                leviathanHuntAutopilotMessage = $"Цель только что оборвала трос. Жду {wait:F1} сек или ищу другую цель.";
            }
            else
            {
                leviathanHuntAutopilotMessage = "В радиусе поиска нет живого левиафана.";
            }

            return;
        }

        float distance = Vector3.Distance(transform.position, nearest.transform.position);
        float engageDistance = Mathf.Min(Mathf.Max(1f, harpoonRangeMeters), Mathf.Max(1f, leviathanHuntEngageRangeMeters));
        if (distance <= engageDistance)
        {
            if (Time.time < nextLeviathanHuntShotTime)
            {
                float wait = Mathf.Max(0f, nextLeviathanHuntShotTime - Time.time);
                leviathanHuntAutopilotMessage = $"Гарпун перезаряжается: {wait:F1} сек. Цель {nearest.displayName} в {distance:F0} м.";
                return;
            }

            routeEnabled = false;
            routeWasEnabled = false;
            positionHold = true;
            positionHoldWasEnabled = true;
            targetHoldPosition = transform.position;
            altitudeHold = true;
            targetAltitude = Mathf.Max(transform.position.y, leviathanHuntMinimumAltitudeMeters);
            cruiseControl = true;
            headingHold = true;
            TryFireHarpoonAtNearestLeviathan(out string fireMessage);
            leviathanHuntAutopilotMessage = fireMessage;
            return;
        }

        Vector3 fromTargetToShip = FlattenHorizontal(transform.position - nearest.transform.position);
        if (fromTargetToShip.sqrMagnitude < 0.04f)
        {
            fromTargetToShip = FlattenHorizontal(-transform.forward);
            if (fromTargetToShip.sqrMagnitude < 0.04f) fromTargetToShip = Vector3.back;
        }

        float approachDistance = Mathf.Max(10f, leviathanHuntApproachDistanceMeters);
        Vector3 approachPoint = nearest.transform.position + fromTargetToShip.normalized * approachDistance;
        approachPoint.y = Mathf.Max(nearest.transform.position.y, leviathanHuntMinimumAltitudeMeters);

        if (waypoints == null) waypoints = new System.Collections.Generic.List<Vector3>();
        waypoints.Clear();
        waypoints.Add(approachPoint);
        currentWaypointIndex = 0;
        routeEnabled = true;
        positionHold = false;
        altitudeHold = true;
        cruiseControl = true;
        headingHold = true;
        targetAltitude = approachPoint.y;

        leviathanHuntAutopilotMessage = $"Иду к цели {nearest.displayName}: {distance:F0} м, точка подхода {approachDistance:F0} м.";
    }

    private void StopRouteForFullMiningHold()
    {
        if (!routeEnabled) return;

        routeEnabled = false;
        routeWasEnabled = false;
        targetSpeedMS = 0f;
        thrustInput = 0f;
        turnInput = 0f;

        positionHold = true;
        positionHoldWasEnabled = true;
        targetHoldPosition = transform.position;

        altitudeHold = true;
        if (rb != null)
        {
            targetAltitude = rb.position.y;
        }

        cruiseControl = true;
        headingHold = true;
        miningLastMessage += " Маршрут остановлен, удерживаю позицию.";
    }

    public RouteEtaInfo GetCurrentRouteEta()
    {
        RouteEtaInfo info = new RouteEtaInfo
        {
            routeEnabled = routeEnabled,
            etaSeconds = float.PositiveInfinity,
            status = routeEnabled ? "Путевая машина активна." : "Путевая машина выключена, оценка по текущей скорости."
        };

        if (waypoints == null || waypoints.Count == 0)
        {
            info.status = "Точки маршрута не заданы.";
            return info;
        }

        info.waypointCount = waypoints.Count;
        if (currentWaypointIndex >= waypoints.Count)
        {
            info.status = "Маршрут завершен.";
            return info;
        }

        int targetIndex = Mathf.Clamp(currentWaypointIndex, 0, waypoints.Count - 1);
        Rigidbody body = rb != null ? rb : GetComponent<Rigidbody>();
        if (body == null)
        {
            info.status = "Нет Rigidbody для расчета ETA.";
            return info;
        }

        Vector3 target = waypoints[targetIndex];
        Vector3 toTarget = target - transform.position;
        Vector3 horizontalToTarget = FlattenHorizontal(toTarget);
        Vector3 horizontalVelocity = FlattenHorizontal(body.linearVelocity);
        float horizontalDistance = horizontalToTarget.magnitude;
        float verticalError = toTarget.y;
        float verticalTolerance = Mathf.Max(1f, waypointRadius * 0.5f);

        info.hasTarget = true;
        info.waypointIndex = targetIndex;
        info.targetPosition = target;
        info.horizontalDistance = horizontalDistance;
        info.horizontalRemaining = Mathf.Max(0f, horizontalDistance - Mathf.Max(0.1f, waypointRadius));
        info.verticalError = verticalError;
        info.verticalRemaining = Mathf.Max(0f, Mathf.Abs(verticalError) - verticalTolerance);
        info.horizontalSpeed = horizontalVelocity.magnitude;
        info.verticalSpeed = body.linearVelocity.y;

        if (horizontalDistance > 0.001f)
        {
            info.horizontalClosingSpeed = Vector3.Dot(horizontalVelocity, horizontalToTarget / horizontalDistance);
        }

        if (info.verticalRemaining > 0f)
        {
            info.verticalClosingSpeed = body.linearVelocity.y * Mathf.Sign(verticalError);
        }

        float horizontalEta = 0f;
        if (info.horizontalRemaining > 0f)
        {
            horizontalEta = info.horizontalClosingSpeed > 0.05f
                ? info.horizontalRemaining / info.horizontalClosingSpeed
                : float.PositiveInfinity;
        }

        float verticalEta = 0f;
        if (info.verticalRemaining > 0f)
        {
            verticalEta = info.verticalClosingSpeed > 0.05f
                ? info.verticalRemaining / info.verticalClosingSpeed
                : float.PositiveInfinity;
        }

        info.etaSeconds = Mathf.Max(horizontalEta, verticalEta);
        info.canEstimate = !float.IsInfinity(info.etaSeconds) && !float.IsNaN(info.etaSeconds);

        if (info.horizontalRemaining <= 0f && info.verticalRemaining <= 0f)
        {
            info.canEstimate = true;
            info.etaSeconds = 0f;
            info.status = "Корабль уже в зоне текущей точки; осталось погасить скорость для засчитывания.";
        }
        else if (!info.canEstimate)
        {
            info.status = "Нет устойчивой оценки: текущая скорость не ведет к точке по одной из осей.";
        }

        return info;
    }

    void Start()
    {
        if (autoStabilizeAtStart)
        {
            PerformAutoStabilization();
        }
        else
        {
            UpdateEngineThrottles();
        }
    }

    private void PerformAutoStabilization()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        targetTrimMass = rb != null ? rb.mass : baseMass;
        if (altitudeHold && rb != null)
        {
            targetAltitude = rb.position.y;
        }

        currentGasLift = 0f;
        activeLiftForce = claudiumCurrentLiftN;
        UpdateEngineThrottles();
    }

    public void StabilizeForFlightStart(bool holdCurrentAltitude)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        if (holdCurrentAltitude && rb != null)
        {
            altitudeHold = true;
            targetAltitude = rb.position.y;
        }

        thrustInput = 0f;
        turnInput = 0f;
        liftInput = 0f;
        PerformAutoStabilization();
    }

    void FixedUpdate()
    {
        UpdateLeviathanHuntAutopilot();
        UpdateRouteModeState();
        UpdateWaypointNavigation(); // Мастер-автопилот
        UpdateRouteModeState();
        UpdatePositionHold();       // Удержание координат, если маршрут не активен
        UpdateCruiseControl();      // Круиз-контроль (скорость)
        UpdateEngineThrottles();
        UpdateHeadingAutopilot(); // Автопилот курса
        UpdateClaudium(); // Магия Клавдия
        UpdateGasHarvester();
        
        // --- АЭРОДИНАМИКА (с учетом ветра) ---
        Vector3 airVelocity = rb.linearVelocity - windVelocity;
        float airspeed = airVelocity.magnitude;
        
        float aeroMultiplier = 1.0f;
        float currentDrag = CurrentAeroDrag * aeroMultiplier;
        
        if (airspeed > 0.01f)
        {
            Vector3 dragForce = -airVelocity.normalized * (airspeed * airspeed) * currentDrag;
            rb.AddForce(dragForce, ForceMode.Force);
        }

        // --- ПОДЪЕМНАЯ СИЛА ---
        float totalLift = claudiumCurrentLiftN;
        activeLiftForce = totalLift;
        currentGasLift = 0f;
        rb.AddForce(Vector3.up * totalLift, ForceMode.Force);

        ApplyPropellerThrust();

        ApplyGyroTurn();

        // 4. Подавление бокового сноса (Киль сопротивляется воздуху)
        Vector3 localAirVel = transform.InverseTransformDirection(airVelocity);
        Vector3 sideAirVelocity = transform.right * localAirVel.x;
        rb.AddForce(-sideAirVelocity * rb.mass * sideResistance, ForceMode.Force);
    }

    private void ApplyGyroTurn()
    {
        float maxTorque = Mathf.Max(0f, gyroTurnTorque);
        float currentTurnRateDeg = rb.angularVelocity.y * Mathf.Rad2Deg;
        float activeTorque = Mathf.Clamp(turnInput, -1f, 1f) * maxTorque;
        float dampingTorque = -rb.angularVelocity.y * maxTorque * Mathf.Max(0f, gyroTurnDamping);

        float maxSafeDamping = Mathf.Abs(rb.angularVelocity.y) * rb.inertiaTensor.y / Time.fixedDeltaTime;
        dampingTorque = Mathf.Clamp(dampingTorque, -maxSafeDamping, maxSafeDamping);

        float finalTorque = activeTorque + dampingTorque;
        if (maxStructuralTurnRateDeg > 0f && Mathf.Abs(currentTurnRateDeg) > maxStructuralTurnRateDeg)
        {
            if (Mathf.Sign(finalTorque) == Mathf.Sign(currentTurnRateDeg))
            {
                float overspeed = Mathf.Abs(currentTurnRateDeg) - maxStructuralTurnRateDeg;
                finalTorque *= Mathf.Clamp01(1f - overspeed * 0.2f);
            }
        }

        currentGyroTurnTorque = finalTorque;
        rb.AddTorque(transform.up * finalTorque, ForceMode.Force);
    }

    private void ApplyPropellerThrust()
    {
        float residualPowerKw = Mathf.Max(0f, engineGeneratedPowerKw - claudiumPowerDrawKw - gasHarvesterPowerDrawActualKw);
        float thrustDirection = Mathf.Sign(thrustInput);
        float propellerEngagement = Mathf.Clamp01(Mathf.Abs(thrustInput));

        propellerInputPowerKw = residualPowerKw * propellerEngagement;
        propellerCalculatedEfficiency = Mathf.Clamp01(propellerEfficiency);
        propellerUsefulPowerKw = propellerInputPowerKw * propellerCalculatedEfficiency;
        if (propellerMaxSpeedMS <= 0f
            || propellerMaxThrustKgf <= 0f
            || propellerUsefulPowerKw <= 0f
            || Mathf.Approximately(thrustDirection, 0f))
        {
            propellerCalculatedEfficiency = 0f;
            propellerUsefulPowerKw = 0f;
            propellerThrustKgf = 0f;
            return;
        }

        Vector3 thrustAxis = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (thrustAxis.sqrMagnitude < 0.001f)
        {
            thrustAxis = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
        }

        thrustAxis.Normalize();
        Vector3 horizontalAirVelocity = Vector3.ProjectOnPlane(rb.linearVelocity - windVelocity, Vector3.up);
        float signedAirspeedWithThrust = Vector3.Dot(horizontalAirVelocity, thrustAxis) * thrustDirection;
        float speedFactor = signedAirspeedWithThrust >= propellerMaxSpeedMS ? 0f : 1f;
        float maxThrustKgf = propellerMaxThrustKgf * propellerEngagement * speedFactor;
        float powerLimitedThrustN = propellerUsefulPowerKw * 1000f / Mathf.Max(1f, Mathf.Abs(signedAirspeedWithThrust));
        float powerLimitedThrustKgf = powerLimitedThrustN / 9.81f;
        float thrustKgf = Mathf.Min(maxThrustKgf, powerLimitedThrustKgf);

        propellerThrustKgf = thrustKgf * thrustDirection;
        rb.AddForce(thrustAxis * (propellerThrustKgf * 9.81f), ForceMode.Force);
    }

    private void UpdateRouteModeState()
    {
        if (routeEnabled && !routeWasEnabled)
        {
            if (waypoints != null && waypoints.Count > 0)
            {
                if (currentWaypointIndex < 0 || currentWaypointIndex >= waypoints.Count)
                {
                    currentWaypointIndex = 0;
                }
            }

            routePreviousAltitudeHold = altitudeHold;
            routePreviousCruiseControl = cruiseControl;
            routePreviousHeadingHold = headingHold;
            routePreviousPositionHold = positionHold;
            positionHold = false;
            routeWasEnabled = true;
        }
        else if (!routeEnabled && routeWasEnabled)
        {
            altitudeHold = routePreviousAltitudeHold;
            cruiseControl = routePreviousCruiseControl;
            headingHold = routePreviousHeadingHold;
            positionHold = routePreviousPositionHold;
            positionHoldWasEnabled = false;
            targetSpeedMS = 0f;
            turnInput = 0f;
            routeWasEnabled = false;
        }
    }

    private void UpdateHeadingAutopilot()
    {
        if (headingHold)
        {
            float currentHeading = transform.eulerAngles.y;
            float headingError = Mathf.DeltaAngle(currentHeading, targetHeading);
            
            // P-терм: требуемая угловая скорость (градусов в секунду)
            float targetTurnRate = headingError * headingStiffness;
            
            // Ограничение скорости поворота автопилотом
            targetTurnRate = Mathf.Clamp(targetTurnRate, -maxAutoTurnRateDeg, maxAutoTurnRateDeg);
            
            // D-терм: компенсация по текущей угловой скорости
            float currentTurnRate = rb.angularVelocity.y * Mathf.Rad2Deg;
            float rateError = targetTurnRate - currentTurnRate;
            
            // Вывод на штурвал (turnInput)
            float turnCommand = rateError * headingDamping;
            turnInput = Mathf.Clamp(turnCommand, -1f, 1f);
        }
    }

    private void UpdateCruiseControl()
    {
        if (cruiseControl)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.forward;
            }

            Vector3 horizontalVelocity = rb.linearVelocity;
            horizontalVelocity.y = 0f;
            float currentSpeed = Vector3.Dot(horizontalVelocity, forward.normalized);
            float speedError = targetSpeedMS - currentSpeed;
            
            // Жесткость (P-терм)
            float desiredOutput = speedError * speedStiffness;
            desiredOutput = Mathf.Clamp(desiredOutput, -1f, 1f);
            
            float response = 1f - Mathf.Exp(-Mathf.Max(0.01f, speedDamping) * 6f * Time.fixedDeltaTime);
            thrustInput = Mathf.Lerp(thrustInput, desiredOutput, response);
            enginePowerLever = Mathf.Lerp(enginePowerLever, Mathf.Clamp01(Mathf.Abs(desiredOutput)), response);
        }
    }

    private void UpdateWaypointNavigation()
    {
        if (!routeEnabled || waypoints == null || waypoints.Count == 0) return;

        if (currentWaypointIndex >= waypoints.Count)
        {
            CompleteRouteWithPositionHold(waypoints[waypoints.Count - 1]);
            return;
        }

        // Путевая машина берет на себя все системы.
        altitudeHold = true;
        cruiseControl = true;
        headingHold = true;

        Vector3 currentTarget = waypoints[currentWaypointIndex];
        float verticalError = currentTarget.y - transform.position.y;
        Vector3 horizontalError = FlattenHorizontal(currentTarget - transform.position);
        Vector3 horizontalVelocity = FlattenHorizontal(rb.linearVelocity);
        float horizDist = horizontalError.magnitude;
        float horizontalSpeed = horizontalVelocity.magnitude;
        float verticalTolerance = Mathf.Max(1f, waypointRadius * 0.5f);
        float arrivalSpeed = Mathf.Max(0.25f, routeArrivalSpeedMS);
        float arrivalVerticalSpeed = Mathf.Max(0.5f, maxAutoVerticalSpeed);

        bool horizontalStable = horizDist <= waypointRadius && horizontalSpeed <= arrivalSpeed;
        bool verticalStable = Mathf.Abs(verticalError) <= verticalTolerance && Mathf.Abs(rb.linearVelocity.y) <= arrivalVerticalSpeed;
        bool reached = horizontalStable && verticalStable;

        if (reached)
        {
            currentWaypointIndex++;
            if (currentWaypointIndex >= waypoints.Count)
            {
                CompleteRouteWithPositionHold(currentTarget);
                return;
            }
            currentTarget = waypoints[currentWaypointIndex];
            verticalError = currentTarget.y - transform.position.y;
            horizontalError = FlattenHorizontal(currentTarget - transform.position);
            horizDist = horizontalError.magnitude;
        }

        // 1. Задаем высоту.
        targetAltitude = currentTarget.y;

        if (horizDist <= waypointRadius)
        {
            ApplyPositionHoldHorizontalControl(currentTarget, waypointRadius, Mathf.Min(propellerMaxSpeedMS, positionHoldMaxSpeedMS));
            return;
        }

        ApplyRouteHorizontalControl(currentTarget);
    }

    private void CompleteRouteWithPositionHold(Vector3 holdPosition)
    {
        routeEnabled = false;
        routeWasEnabled = false;
        currentWaypointIndex = waypoints != null ? waypoints.Count : currentWaypointIndex;

        altitudeHold = true;
        cruiseControl = true;
        headingHold = true;
        positionHold = true;
        positionHoldWasEnabled = true;

        targetHoldPosition = holdPosition;
        targetAltitude = holdPosition.y;
        targetSpeedMS = 0f;
        thrustInput = 0f;
        turnInput = 0f;
    }

    private void UpdatePositionHold()
    {
        if (routeEnabled) return;

        if (!positionHold)
        {
            positionHoldWasEnabled = false;
            return;
        }

        if (!positionHoldWasEnabled)
        {
            targetHoldPosition = transform.position;
            positionHoldWasEnabled = true;
        }

        cruiseControl = true;
        headingHold = true;
        ApplyPositionHoldHorizontalControl(targetHoldPosition, positionHoldRadius, positionHoldMaxSpeedMS);
    }

    private void ApplyRouteHorizontalControl(Vector3 target)
    {
        Vector3 horizontalError = FlattenHorizontal(target - transform.position);
        float distance = horizontalError.magnitude;
        if (distance <= 0.1f)
        {
            ApplyPositionHoldHorizontalControl(target, waypointRadius, positionHoldMaxSpeedMS);
            return;
        }

        Vector3 directionToTarget = horizontalError / distance;
        float maxSpeed = Mathf.Max(0f, propellerMaxSpeedMS);
        float stopDistance = Mathf.Max(0f, distance - waypointRadius);
        float brakeAcceleration = EstimateRouteBrakeAcceleration();
        float desiredSpeed = Mathf.Min(maxSpeed, Mathf.Sqrt(2f * brakeAcceleration * stopDistance));

        Vector3 desiredGroundVelocity = directionToTarget * desiredSpeed;
        Vector3 requiredAirVelocity = desiredGroundVelocity - FlattenHorizontal(windVelocity);
        Vector3 headingVector = requiredAirVelocity.sqrMagnitude > 0.04f ? requiredAirVelocity : directionToTarget;

        targetHeading = HeadingFromVector(headingVector);

        float headingError = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, targetHeading));
        desiredSpeed *= CalculateRouteHeadingSpeedFactor(headingError);
        targetSpeedMS = Mathf.Clamp(desiredSpeed, 0f, maxSpeed);
    }

    private float EstimateRouteBrakeAcceleration()
    {
        float configuredAcceleration = Mathf.Max(0.25f, routeBrakeAccelerationMS2);
        if (rb == null || propellerMaxThrustKgf <= 0f || propellerEfficiency <= 0f)
        {
            return configuredAcceleration;
        }

        float mass = Mathf.Max(1f, rb.mass);
        float staticThrustN = propellerMaxThrustKgf * 9.81f;
        float staticAcceleration = staticThrustN / mass;

        float liftPowerKw = altitudeHold ? CalculateClaudiumPowerKwForLift(mass) : 0f;
        float modulePowerKw = gasHarvesterEnabled ? Mathf.Max(0f, gasHarvesterPowerDrawKw) : 0f;
        float residualPowerKw = Mathf.Max(0f, enginePowerKwAt100 - liftPowerKw - modulePowerKw);
        float usefulPowerW = residualPowerKw * Mathf.Clamp01(propellerEfficiency) * 1000f;
        float horizontalAirspeed = FlattenHorizontal(rb.linearVelocity - windVelocity).magnitude;
        float powerLimitedThrustN = usefulPowerW > 0f ? usefulPowerW / Mathf.Max(1f, horizontalAirspeed) : 0f;
        float estimatedThrustN = Mathf.Min(staticThrustN, powerLimitedThrustN);
        float estimatedAcceleration = estimatedThrustN / mass;

        // РљРѕСЌС„С„РёС†РёРµРЅС‚ Р·Р°РїР°СЃР° РЅСѓР¶РµРЅ, РїРѕС‚РѕРјСѓ С‡С‚Рѕ РєРѕСЂР°Р±Р»СЊ РµС‰Рµ РґРѕРІРѕСЂР°С‡РёРІР°РµС‚ РЅРѕСЃ РґР»СЏ СЂРµРІРµСЂСЃР°.
        float safeAcceleration = Mathf.Max(0.25f, Mathf.Min(staticAcceleration, estimatedAcceleration) * 0.6f);
        return Mathf.Min(configuredAcceleration, safeAcceleration);
    }

    private void ApplyPositionHoldHorizontalControl(Vector3 target, float radius, float maxSpeed)
    {
        Vector3 horizontalError = FlattenHorizontal(target - transform.position);
        Vector3 horizontalVelocity = FlattenHorizontal(rb.linearVelocity);
        float distance = horizontalError.magnitude;
        float speed = horizontalVelocity.magnitude;
        float holdRadius = Mathf.Max(0.1f, radius);
        float maxHoldSpeed = Mathf.Max(0f, maxSpeed);
        float brakeAcceleration = EstimateRouteBrakeAcceleration();
        float stoppingDistance = speed * speed / Mathf.Max(0.5f, 2f * brakeAcceleration);
        float remainingToHold = Mathf.Max(0f, distance - holdRadius);

        Vector3 headingVector = Vector3.zero;
        float desiredSpeed = 0f;

        if (speed > 0.15f && (distance <= holdRadius || stoppingDistance >= remainingToHold))
        {
            headingVector = -horizontalVelocity;
        }
        else
        {
            Vector3 correctionVelocity = horizontalError * Mathf.Max(0f, positionHoldStiffness)
                - horizontalVelocity * Mathf.Max(0f, positionHoldDamping);
            float approachSpeedLimit = Mathf.Sqrt(2f * brakeAcceleration * remainingToHold);
            correctionVelocity = Vector3.ClampMagnitude(correctionVelocity, Mathf.Min(maxHoldSpeed, approachSpeedLimit));
            headingVector = correctionVelocity;
            desiredSpeed = correctionVelocity.magnitude;
        }

        if (headingVector.sqrMagnitude < 0.04f)
        {
            if (horizontalVelocity.sqrMagnitude > 0.04f)
            {
                headingVector = -horizontalVelocity;
            }
            else
            {
                Vector3 windHorizontal = FlattenHorizontal(windVelocity);
                if (windHorizontal.sqrMagnitude > 0.04f)
                {
                    headingVector = -windHorizontal;
                }
            }
        }

        if (headingVector.sqrMagnitude > 0.04f)
        {
            targetHeading = HeadingFromVector(headingVector);
        }

        if (distance <= holdRadius && speed < 0.35f)
        {
            desiredSpeed = 0f;
        }

        float headingError = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, targetHeading));
        desiredSpeed *= CalculateRouteHeadingSpeedFactor(headingError);
        targetSpeedMS = Mathf.Clamp(desiredSpeed, 0f, Mathf.Max(0f, maxSpeed));
    }

    private static Vector3 FlattenHorizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static float HeadingFromVector(Vector3 value)
    {
        float angle = Mathf.Atan2(value.x, value.z) * Mathf.Rad2Deg;
        return angle < 0f ? angle + 360f : angle;
    }

    private float CalculateRouteHeadingSpeedFactor(float headingErrorDeg)
    {
        float normalized = 1f - Mathf.Clamp01(Mathf.Abs(headingErrorDeg) / 45f);
        return Mathf.SmoothStep(0f, 1f, normalized);
    }

    private void UpdateSimplifiedClaudium()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        currentGasLift = 0f;
        float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float requestedLiftKg = CalculateDesiredClaudiumLiftKg();
        if (claudiumMaxLiftKg > 0f)
        {
            requestedLiftKg = Mathf.Min(requestedLiftKg, claudiumMaxLiftKg);
        }
        else
        {
            requestedLiftKg = 0f;
        }

        bool hasClaudium = claudiumStock > 0f;
        bool canLift = enginePowerKwAt100 > 0f
            && hasClaudium
            && claudiumLiftEfficiency > 0f;

        float requestedPowerKw = canLift ? CalculateClaudiumPowerKwForLift(requestedLiftKg) : 0f;
        float requestedModulePowerKw = CalculatePoweredModulePowerRequestKw(requestedPowerKw);
        UpdateEnginePowerOutput(requestedPowerKw + requestedModulePowerKw);

        float targetLiftN = 0f;
        if (!hasClaudium)
        {
            claudiumCurrentLiftN = 0f;
            claudiumPowerDrawWatts = 0f;
            claudiumPowerDrawKw = 0f;
            claudiumRequestedLiftKg = requestedLiftKg;
            activeLiftForce = 0f;
            return;
        }

        if (canLift)
        {
            float powerForLiftKw = Mathf.Min(requestedPowerKw, engineGeneratedPowerKw);
            targetLiftN = powerForLiftKw * claudiumLiftEfficiency * 9.81f;
        }

        float smoothing = claudiumLiftSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-claudiumLiftSmoothing * dt);
        claudiumCurrentLiftN = Mathf.Lerp(claudiumCurrentLiftN, targetLiftN, smoothing);
        if (claudiumCurrentLiftN < 0.001f)
        {
            claudiumCurrentLiftN = 0f;
        }

        claudiumRequestedLiftKg = requestedLiftKg;
        claudiumPowerDrawKw = claudiumLiftEfficiency > 0f ? CalculateClaudiumPowerKwForLift(claudiumCurrentLiftN / 9.81f) : 0f;
        claudiumPowerDrawWatts = claudiumPowerDrawKw * 1000f;

        float supportedTons = Mathf.Max(0f, claudiumCurrentLiftN / 9.81f) / 1000f;
        float consumption = claudiumConsumptionPerTonSecond * supportedTons * dt;
        if (consumption > 0f)
        {
            if (claudiumStock >= consumption)
            {
                claudiumStock -= consumption;
            }
            else
            {
                float availableFraction = Mathf.Clamp01(claudiumStock / consumption);
                claudiumStock = 0f;
                claudiumCurrentLiftN *= availableFraction;
                claudiumPowerDrawKw *= availableFraction;
                claudiumPowerDrawWatts = claudiumPowerDrawKw * 1000f;
            }
        }

        activeLiftForce = claudiumCurrentLiftN;
    }

    private float CalculatePoweredModulePowerRequestKw(float claudiumPowerKw)
    {
        if (!gasHarvesterEnabled || gasHarvesterVolumeM3PerSecond <= 0f || gasHarvesterPowerDrawKw <= 0f)
        {
            return 0f;
        }

        if (claudiumPowerKw + gasHarvesterPowerDrawKw > enginePowerKwAt100 + 0.001f)
        {
            return 0f;
        }

        return gasHarvesterPowerDrawKw;
    }

    private float CalculateClaudiumPowerKwForLift(float liftKg)
    {
        if (claudiumLiftEfficiency <= 0f) return 0f;
        return Mathf.Max(0f, liftKg) / claudiumLiftEfficiency;
    }

    private void UpdateEnginePowerOutput(float minimumPowerKw)
    {
        float maxLever = 1.2f;
        engineMinimumPowerLever = enginePowerKwAt100 > 0f ? Mathf.Clamp(minimumPowerKw / enginePowerKwAt100, 0f, maxLever) : 0f;
        enginePowerLever = Mathf.Clamp(enginePowerLever, engineMinimumPowerLever, maxLever);

        engineGeneratedPowerKw = Mathf.Max(0f, enginePowerKwAt100) * enginePowerLever;
        engineEfficiencyCurrent = Mathf.Clamp01(engineFuelEfficiency);
        engineHasFuel = engineFuelStockKg > 0f;
        engineFuelConsumptionKgPerSecond = 0f;

        if (engineGeneratedPowerKw <= 0f || engineEfficiencyCurrent <= 0f || engineFuelEnergyKwhPerKg <= 0f)
        {
            engineGeneratedPowerKw = 0f;
            return;
        }

        engineFuelConsumptionKgPerSecond = engineGeneratedPowerKw / engineEfficiencyCurrent / engineFuelEnergyKwhPerKg / 3600f;
        float requestedFuel = engineFuelConsumptionKgPerSecond * Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        if (engineFuelStockKg >= requestedFuel)
        {
            engineFuelStockKg -= requestedFuel;
            return;
        }

        float availableFraction = requestedFuel > 0f ? Mathf.Clamp01(engineFuelStockKg / requestedFuel) : 0f;
        engineFuelStockKg = 0f;
        engineGeneratedPowerKw *= availableFraction;
        engineFuelConsumptionKgPerSecond *= availableFraction;
        engineHasFuel = false;
    }

    private float CalculateDesiredClaudiumLiftKg()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        float mass = rb != null ? rb.mass : baseMass;
        float trimMass = Mathf.Max(1f, targetTrimMass > 0f ? targetTrimMass : mass);

        if (altitudeHold && rb != null)
        {
            if (!wasAltitudeHold)
            {
                targetAltitude = rb.position.y;
                wasAltitudeHold = true;
                altIntegral = 0f;
            }

            targetAltitude += liftInput * 8.0f * Time.fixedDeltaTime;

            float currentAcceleration = (claudiumCurrentLiftN / Mathf.Max(mass, 1f)) - 9.81f;
            float lookAheadTime = 1.2f;
            float predictedHeight = rb.position.y + rb.linearVelocity.y * lookAheadTime + 0.5f * currentAcceleration * lookAheadTime * lookAheadTime;
            float altitudeError = targetAltitude - predictedHeight;

            if (Mathf.Abs(altitudeError) > altDriftTolerance)
            {
                altIntegral += (targetAltitude - rb.position.y) * Time.fixedDeltaTime * 0.05f;
                altIntegral = Mathf.Clamp(altIntegral, -0.25f, 0.25f);
            }

            float targetVerticalSpeed = 0f;
            if (Mathf.Abs(altitudeError) > 0.001f)
            {
                targetVerticalSpeed = Mathf.Sqrt(2f * 0.25f * Mathf.Abs(altitudeError)) * Mathf.Sign(altitudeError);
                targetVerticalSpeed = Mathf.Clamp(targetVerticalSpeed, -maxAutoVerticalSpeed, maxAutoVerticalSpeed);
            }

            float velocityError = targetVerticalSpeed - rb.linearVelocity.y;
            float desiredAcceleration = velocityError * altDamping - currentAcceleration * 1.5f + altIntegral + liftInput * 0.1f;
            float requestedKg = mass * Mathf.Max(0f, 9.81f + desiredAcceleration) / 9.81f;

            if (rb.linearVelocity.y > maxStructuralVerticalSpeed * 0.9f)
            {
                float speedFactor = Mathf.InverseLerp(maxStructuralVerticalSpeed, maxStructuralVerticalSpeed * 0.9f, rb.linearVelocity.y);
                requestedKg *= speedFactor;
            }

            return requestedKg;
        }

        wasAltitudeHold = false;
        altIntegral = 0f;
        return Mathf.Max(0f, trimMass * (1f + liftInput * 0.1f));
    }

    void UpdateClaudium()
    {
        UpdateSimplifiedClaudium();
    }

    private void UpdateGasHarvester()
    {
        gasHarvesterPowerDrawActualKw = 0f;

        if (!gasHarvesterEnabled)
        {
            ResetGasHarvesterCycle();
            return;
        }

        if (gasHarvesterVolumeM3PerSecond <= 0f || gasHarvesterRadiusMeters <= 0f || gasHarvesterPowerDrawKw <= 0f)
        {
            gasHarvesterLastMessage = "Харвестер не установлен или не имеет рабочих характеристик.";
            ResetGasHarvesterCycle();
            return;
        }

        if (claudiumPowerDrawKw + gasHarvesterPowerDrawKw > enginePowerKwAt100 + 0.001f)
        {
            gasHarvesterLastMessage = "Харвестер выключен: после клавдиевого контура не хватает мощности до лимита 100%.";
            ResetGasHarvesterCycle();
            return;
        }

        if (engineGeneratedPowerKw + 0.001f < claudiumPowerDrawKw + gasHarvesterPowerDrawKw || !engineHasFuel)
        {
            gasHarvesterLastMessage = "Харвестер ждет мощность или топливо.";
            ResetGasHarvesterCycle();
            return;
        }

        MetaGameState meta = ResolveMetaGameState();
        if (meta == null || meta.progress == null)
        {
            gasHarvesterLastMessage = "Харвестер ждет MetaGameState.";
            ResetGasHarvesterCycle();
            return;
        }

        if (meta.GetRemainingShipCargoCapacityKg() < 1f)
        {
            gasHarvesterEnabled = false;
            gasHarvesterLastMessage = "Харвестер выключен: не осталось грузоподъемности.";
            ResetGasHarvesterCycle();
            return;
        }

        if (string.IsNullOrWhiteSpace(gasHarvesterCycleCloudId))
        {
            GasCloud startCloud = GasCloud.FindRandomOverlapping(transform.position, gasHarvesterRadiusMeters);
            if (startCloud == null)
            {
                gasHarvesterLastMessage = "Нет облака в радиусе харвестера.";
                ResetGasHarvesterCycle();
                return;
            }

            gasHarvesterCycleCloudId = startCloud.cloudId;
            gasHarvesterActiveCloudId = startCloud.cloudId;
            gasHarvesterCycleProgressSeconds = 0f;
            gasHarvesterLastMessage = "Цикл начат: " + startCloud.displayName;
        }

        gasHarvesterPowerDrawActualKw = gasHarvesterPowerDrawKw;
        gasHarvesterCycleProgressSeconds += Time.fixedDeltaTime;
        float cycleSeconds = Mathf.Max(0.1f, gasHarvesterCycleSeconds);
        if (gasHarvesterCycleProgressSeconds < cycleSeconds)
        {
            return;
        }

        GasCloud endCloud = GasCloud.FindById(gasHarvesterCycleCloudId);
        if (endCloud == null || !endCloud.IntersectsHarvestRadius(transform.position, gasHarvesterRadiusMeters))
        {
            gasHarvesterLastMessage = "Цикл сорван: в конце цикла нет контакта с выбранным облаком.";
            ResetGasHarvesterCycle();
            return;
        }

        float sampledCubicMeters = gasHarvesterVolumeM3PerSecond * cycleSeconds;
        float possibleLiters = Mathf.Min(sampledCubicMeters * Mathf.Max(0.0001f, endCloud.condensateLitersPerCubicMeter), endCloud.remainingVolumeLiters);
        int possibleWholeKg = Mathf.FloorToInt(gasHarvesterBufferKg + possibleLiters + 0.0001f);
        if (possibleWholeKg > 0 && meta.GetRemainingShipCargoCapacityKg() < possibleWholeKg)
        {
            gasHarvesterEnabled = false;
            gasHarvesterLastMessage = "Харвестер выключен: не хватит места для результата цикла.";
            ResetGasHarvesterCycle();
            return;
        }

        float harvestedLiters = endCloud.HarvestLiters(sampledCubicMeters);
        gasHarvesterBufferKg += harvestedLiters;
        int wholeKg = Mathf.FloorToInt(gasHarvesterBufferKg + 0.0001f);
        if (wholeKg > 0)
        {
            if (meta.TryAddShipCargoFromRuntime(endCloud.condensateItemId, wholeKg, out string cargoError))
            {
                gasHarvesterBufferKg -= wholeKg;
                gasHarvesterLastMessage = $"Добыто {wholeKg} кг: {endCloud.condensateItemId}. Остаток облака {endCloud.remainingVolumeLiters:F1} кг.";
            }
            else
            {
                gasHarvesterEnabled = false;
                gasHarvesterLastMessage = cargoError;
            }
        }
        else
        {
            gasHarvesterLastMessage = $"Буфер {gasHarvesterBufferKg:F2} кг. Остаток облака {endCloud.remainingVolumeLiters:F1} кг.";
        }

        ResetGasHarvesterCycle();
    }

    private void ResetGasHarvesterCycle()
    {
        gasHarvesterCycleCloudId = "";
        gasHarvesterActiveCloudId = "";
        gasHarvesterCycleProgressSeconds = 0f;
    }

    private MetaGameState ResolveMetaGameState()
    {
        if (cachedMetaGameState == null)
        {
            cachedMetaGameState = FindFirstObjectByType<MetaGameState>();
        }

        return cachedMetaGameState;
    }

    private void UpdateEngineThrottles()
    {
        enginePowerLever = Mathf.Clamp(enginePowerLever, 0f, 1.2f);
        propellerPitch = Mathf.Clamp(thrustInput, -1f, 1f);
    }

    private void OnDrawGizmos()
    {
        // Визуализация ветра
        if (windVelocity.sqrMagnitude > 0.1f)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.7f); // Пурпурный
            Vector3 startPos = transform.position + Vector3.up * 10f; // Чуть выше корабля
            Gizmos.DrawLine(startPos, startPos + windVelocity);
            Gizmos.DrawWireSphere(startPos + windVelocity, 1f); // Наконечник
        }

        if (positionHold)
        {
            Gizmos.color = new Color(1f, 0.85f, 0f, 0.6f);
            Gizmos.DrawWireSphere(targetHoldPosition, Mathf.Max(0.1f, positionHoldRadius));
        }

        if (waypoints == null || waypoints.Count == 0) return;

        Gizmos.color = new Color(0f, 1f, 1f, 0.5f); // Полупрозрачный голубой
        for (int i = 0; i < waypoints.Count; i++)
        {
            // Рисуем сферу (радиус достижения точки)
            Gizmos.DrawWireSphere(waypoints[i], waypointRadius);
            
            // Соединяем точки линией
            if (i > 0)
            {
                Gizmos.DrawLine(waypoints[i - 1], waypoints[i]);
            }
            else
            {
                // Линия от текущей позиции корабля к первой точке
                Gizmos.DrawLine(transform.position, waypoints[i]);
            }
        }

        // Подсвечиваем текущую цель желтым, если маршрут активен
        if (Application.isPlaying && routeEnabled && currentWaypointIndex < waypoints.Count)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(waypoints[currentWaypointIndex], waypointRadius);
            Gizmos.DrawLine(transform.position, waypoints[currentWaypointIndex]);
        }
    }
}

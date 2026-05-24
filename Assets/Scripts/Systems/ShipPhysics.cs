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
    public float claudiumLiftKg = 5500f;
    public float cruiseSpeedMS = 9f;
    public float wanderRadiusMeters = 220f;
    [Header("Физика плавания")]
    public float forwardThrustKgf = 920f;
    public float omniThrustKgf = 660f;
    public float turnTorqueNm = 1200f;
    public float turnDamping = 0.85f;
    public float stationaryTurnEffectiveness = 0.28f;
    public float fullTurnEffectSpeedMS = 7f;
    public bool autoCalculateDragArea = true;
    public float airDensity = 1.225f;
    public float dragCoefficient = 0.55f;
    public float frontalArea = 12f;
    public float sideAreaMultiplier = 3.2f;
    public float verticalAreaMultiplier = 2.6f;
    public bool bellyDownStabilization = true;
    public float bellyDownTorqueNm = 900f;
    public float bellyDownDamping = 0.65f;
    public float autopilotVelocityGain = 0.85f;
    public float autopilotArrivalRadiusMeters = 8f;
    public float fishLookAheadSeconds = 2.4f;
    public float fishFullArcAngleDeg = 105f;
    [Range(0f, 1f)] public float fishArcForwardBias = 0.35f;
    [Range(0f, 1f)] public float fishTurnSpeedRetention = 0.35f;
    [Range(0f, 1f)] public float fishLateralCorrection = 0.45f;
    [Range(0f, 1f)] public float fishTurnBrakePermission = 0.18f;
    public bool organicSwimMotion = true;
    public float swimSwayFrequency = 0.22f;
    public float swimSwayAngleDeg = 5f;
    public float swimSwaySideForceKgf = 18f;
    public float swimNoiseRadiusMeters = 12f;
    public float swimNoiseFrequency = 0.08f;
    public float swimVerticalNoiseMeters = 4f;
    public float visualBodySwayDeg = 4f;
    public float visualTailSwayMeters = 1.2f;
    public float failureGravityRampSeconds = 20f;
    [Range(0f, 1f)] public float failureStartGravity01 = 0.12f;
    public float headArmorMm = 28f;
    public float bodyArmorMm = 5f;
    public float ramDamageMultiplier = 1f;
    public int carcassMassKg = 2500;
    public bool isCarcass;
    public bool debugLogging;
    public float harpoonStruggleForceMultiplier = 1.15f;
    public float harpoonDiveBias = 0.35f;
    public float harpoonPanicTurnIntervalSeconds = 2.4f;
    [Header("Питание")]
    public float satietyKg = 120f;
    public float maxSatietyKg = 300f;
    public float hungerDecayKgPerSecond = 0.04f;
    public float feedingSearchRadiusMeters = 260f;
    public int feedingBiteKg = 25;
    public float feedingBiteIntervalSeconds = 5f;
    public float feedingApproachDistanceMeters = 16f;
    public float fragmentEatRadiusMeters = 18f;
    [Header("Агрессия")]
    public float passiveAggressionRangeMeters = 170f;
    public float ramChargeForceMultiplier = 1.65f;
    public float harpoonedRamMassRatio = 1.08f;
    public float harpoonedRamRetreatDistanceMeters = 90f;
    public float harpoonedRamChargeDistanceMeters = 32f;
    public float harpoonedRamChargeSeconds = 3.2f;
    public float harpoonedRamRetreatSeconds = 2.8f;
    public float harpoonedSmallRamForceMultiplier = 0.72f;
    public float harpoonAlarmAggressionRangeMultiplier = 3f;
    public float ramMinDamageSpeedMS = 4f;
    public float ramDamageScale = 10f;
    [Header("Тревога")]
    [Range(0f, 1f)] public float alarm01;
    public float alarmNearGainPerSecond = 0.06f;
    public float alarmHarpoonedGainPerSecond = 0.28f;
    public float alarmTensionGainPerSecond = 0.35f;
    public float alarmOtherHarpoonedGainPerSecond = 0.14f;
    public float alarmShotNearAdd = 0.16f;
    public float alarmShotAtSelfAdd = 0.45f;
    public float alarmRamDecaySeconds = 10f;
    [Header("Путевая машинка")]
    public bool routeEnabled;
    public bool routeLoop = true;
    public List<Vector3> routeWaypoints = new List<Vector3>();
    public int routeWaypointIndex;
    public float routeWaypointRadiusMeters = 18f;
    public float routeCruiseSpeedMS = 10f;
    public int routeRandomPointCount = 8;
    public float routeRandomRadiusMeters = 1000f;
    [TextArea(2, 4)] public string routeStatus = "";
    [TextArea(2, 4)] public string behaviorStatus = "";
    [Header("Разведка")]
    [InspectorName("Паспорт разведки")]
    [Tooltip("Автоматически обновляемая отладочная сводка: известность левиафана, прогресс сведений и научной информации.")]
    public SurveyObjectInspectorState survey = new SurveyObjectInspectorState();

    private Rigidbody rb;
    private Vector3 wanderTarget;
    private Vector3 harpoonPanicDirection;
    private float nextHarpoonPanicTurnTime;
    private float nextWanderChangeTime;
    private float nextStatusLogTime;
    private float nextFeedingBiteTime;
    private float ramUntilTime;
    private float lastRamDamageTime;
    private bool harpoonRamCharging;
    private float harpoonRamPhaseUntilTime;
    private bool alarmRamActive;
    private ShipPhysics ramTarget;
    private MiningRock feedingTarget;
    private float carcassStartTime = -1f;
    private Color visualColor = new Color(0.35f, 0.55f, 0.7f, 1f);
    private HarpoonTether activeTether;
    private float swimSeed;
    private Transform visualRoot;
    private Transform visualHead;
    private Transform visualTail;
    private Transform visualDorsal;
    private MetaGameState cachedSurveyMeta;
    private float nextSurveyInspectorRefreshTime;

    public Rigidbody Body
    {
        get
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            return rb;
        }
    }

    public bool CanBeClaimed => isCarcass || health <= 0f;

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
        claudiumLiftKg = type != null ? type.claudiumLiftKg : claudiumLiftKg;
        forwardThrustKgf = type != null ? Mathf.Max(0f, type.forwardThrustKgf) : forwardThrustKgf;
        omniThrustKgf = type != null ? Mathf.Max(0f, type.omniThrustKgf) : omniThrustKgf;
        cruiseSpeedMS = type != null ? type.cruiseSpeedMS : cruiseSpeedMS;
        wanderRadiusMeters = type != null ? type.wanderRadiusMeters : wanderRadiusMeters;
        turnTorqueNm = type != null ? Mathf.Max(0f, type.turnTorque) : turnTorqueNm;
        headArmorMm = type != null ? type.headArmorMm : headArmorMm;
        bodyArmorMm = type != null ? type.bodyArmorMm : bodyArmorMm;
        ramDamageMultiplier = type != null ? type.ramDamageMultiplier : ramDamageMultiplier;
        carcassMassKg = type != null ? type.CarcassMassKg : Mathf.Max(1, Mathf.RoundToInt(massKg * 0.5f));
        visualColor = type != null ? type.color : visualColor;
        homeCenter = zone != null ? zone.center : position;
        zoneRadiusMeters = zone != null ? zone.radiusMeters : zoneRadiusMeters;
        minY = zone != null ? zone.minY : minY;
        maxY = zone != null ? zone.maxY : maxY;
        if (autoCalculateDragArea)
        {
            RecalculateDragAreaFromBody();
        }

        transform.position = position;
        name = displayName;

        rb = GetComponent<Rigidbody>();
        rb.mass = Mathf.Max(1f, massKg);
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.45f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        carcassStartTime = -1f;

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.direction = 2;
            capsule.radius = Mathf.Max(0.5f, bodyRadiusMeters);
            capsule.height = Mathf.Max(bodyRadiusMeters * 2f, bodyLengthMeters);
        }

        PickNewWanderTarget(true);
        EnsureVisual();
        RefreshSurveyInspectorState(true);
    }

    private void Start()
    {
        EnsureVisual();
    }

    private void FixedUpdate()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        EnsureVisual();

        if (health <= 0f)
        {
            BecomeCarcass();
        }

        RefreshSurveyInspectorState();

        if (isCarcass)
        {
            UpdateCarcassFailurePhysics();
            ApplyLeviathanAirResistance();
            return;
        }

        rb.useGravity = false;
        UpdateHunger();
        UpdateAlarm();
        if (activeTether != null && activeTether.IsAttached)
        {
            UpdateHarpoonedStruggle();
        }
        else if (UpdateRamAttack())
        {
        }
        else if (UpdateRouteMachine())
        {
        }
        else if (UpdateFeeding())
        {
        }
        else
        {
            UpdateWander();
        }

        ApplyBellyDownStabilization();
        ApplyLeviathanAirResistance();

        if (debugLogging && Time.time >= nextStatusLogTime)
        {
            nextStatusLogTime = Time.time + 5f;
            Debug.Log("[Левиафан] " + GetStatusRu(), this);
        }
    }

    private void LateUpdate()
    {
        AnimateSwimVisual();
    }

    private void EnsureVisual()
    {
        Transform existingRoot = transform.Find("Визуал левиафана");
        if (existingRoot != null)
        {
            CacheVisualParts(existingRoot);
            return;
        }

        Transform root = new GameObject("Визуал левиафана").transform;
        root.SetParent(transform, false);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Тело";
        body.transform.SetParent(root, false);
        body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        body.transform.localScale = new Vector3(bodyRadiusMeters * 2f, bodyLengthMeters * 0.5f, bodyRadiusMeters * 2f);
        RemoveVisualCollider(body);
        SetVisualColor(body, visualColor);

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Бронированная голова";
        head.transform.SetParent(root, false);
        head.transform.localPosition = Vector3.forward * bodyLengthMeters * 0.48f;
        head.transform.localScale = Vector3.one * bodyRadiusMeters * 2.2f;
        RemoveVisualCollider(head);
        SetVisualColor(head, Color.Lerp(visualColor, Color.white, 0.2f));

        GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tail.name = "Хвост";
        tail.transform.SetParent(root, false);
        tail.transform.localPosition = Vector3.back * bodyLengthMeters * 0.52f;
        tail.transform.localScale = new Vector3(bodyRadiusMeters * 1.2f, bodyRadiusMeters * 0.8f, bodyRadiusMeters * 1.8f);
        RemoveVisualCollider(tail);
        SetVisualColor(tail, Color.Lerp(visualColor, Color.black, 0.12f));

        GameObject dorsal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dorsal.name = "Спинной плавник";
        dorsal.transform.SetParent(root, false);
        dorsal.transform.localPosition = new Vector3(0f, bodyRadiusMeters * 0.75f, -bodyLengthMeters * 0.05f);
        dorsal.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        dorsal.transform.localScale = new Vector3(bodyRadiusMeters * 0.35f, bodyRadiusMeters * 1.2f, bodyLengthMeters * 0.32f);
        RemoveVisualCollider(dorsal);
        SetVisualColor(dorsal, Color.Lerp(visualColor, Color.black, 0.2f));
        CacheVisualParts(root);
    }

    private void CacheVisualParts(Transform root)
    {
        visualRoot = root;
        visualHead = root != null ? root.Find("Бронированная голова") : null;
        visualTail = root != null ? root.Find("Хвост") : null;
        visualDorsal = root != null ? root.Find("Спинной плавник") : null;
    }

    private void EnsureSwimSeed()
    {
        if (swimSeed > 0.001f) return;

        int hash = Mathf.Abs(GetInstanceID());
        swimSeed = 13.37f + hash * 0.173f;
    }

    private float GetSwimSway()
    {
        EnsureSwimSeed();
        float frequency = Mathf.Max(0f, swimSwayFrequency);
        if (frequency <= 0.0001f) return 0f;

        float phase = (Time.time + swimSeed) * frequency * Mathf.PI * 2f;
        return Mathf.Sin(phase) + Mathf.Sin(phase * 0.47f + swimSeed * 1.91f) * 0.35f;
    }

    private float GetSwimNoise(float channel, float frequency)
    {
        EnsureSwimSeed();
        float time = Time.time * Mathf.Max(0f, frequency);
        return Mathf.PerlinNoise(swimSeed + channel * 31.7f, time + channel * 7.3f) * 2f - 1f;
    }

    private void AnimateSwimVisual()
    {
        if (visualRoot == null) CacheVisualParts(transform.Find("Визуал левиафана"));
        if (visualRoot == null) return;
        if (!organicSwimMotion || isCarcass)
        {
            ResetSwimVisual();
            return;
        }

        float speed = rb != null ? rb.linearVelocity.magnitude : Mathf.Max(0.1f, cruiseSpeedMS);
        float speed01 = Mathf.InverseLerp(0.2f, Mathf.Max(0.3f, cruiseSpeedMS), speed);
        float sway = Mathf.Clamp(GetSwimSway(), -1.35f, 1.35f) * speed01;
        float bodyYawDeg = sway * Mathf.Max(0f, visualBodySwayDeg);
        float bodyRollDeg = -bodyYawDeg * 0.35f;

        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.Euler(0f, bodyYawDeg, bodyRollDeg);

        if (visualHead != null)
        {
            visualHead.localPosition = Vector3.forward * bodyLengthMeters * 0.48f + Vector3.right * (-sway * Mathf.Max(0f, visualTailSwayMeters) * 0.25f);
        }

        if (visualTail != null)
        {
            visualTail.localPosition = Vector3.back * bodyLengthMeters * 0.52f + Vector3.right * (-sway * Mathf.Max(0f, visualTailSwayMeters));
        }

        if (visualDorsal != null)
        {
            visualDorsal.localRotation = Quaternion.Euler(0f, 0f, 45f + bodyRollDeg * 0.35f);
        }
    }

    private void ResetSwimVisual()
    {
        if (visualRoot != null)
        {
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
        }

        if (visualHead != null)
        {
            visualHead.localPosition = Vector3.forward * bodyLengthMeters * 0.48f;
        }

        if (visualTail != null)
        {
            visualTail.localPosition = Vector3.back * bodyLengthMeters * 0.52f;
        }

        if (visualDorsal != null)
        {
            visualDorsal.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
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
        ApplyDamage(amount);
    }

    public void SetHarpoonTether(HarpoonTether tether)
    {
        activeTether = tether;
        nextHarpoonPanicTurnTime = 0f;
        harpoonRamCharging = false;
        harpoonRamPhaseUntilTime = 0f;
    }

    public void ClearHarpoonTether(HarpoonTether tether)
    {
        if (activeTether == tether)
        {
            activeTether = null;
            harpoonRamCharging = false;
            harpoonRamPhaseUntilTime = 0f;
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
        string behavior = string.IsNullOrWhiteSpace(behaviorStatus) ? "" : " " + behaviorStatus;
        return $"{displayName}: {state}, здоровье {health:F0}/{maxHealth:F0}, тревога {alarm01:P0}, сытость {satietyKg:F0}/{maxSatietyKg:F0}, масса туши {carcassMassKg} кг.{behavior}";
    }

    private void UpdateHunger()
    {
        maxSatietyKg = Mathf.Max(1f, maxSatietyKg);
        satietyKg = Mathf.Clamp(satietyKg - Mathf.Max(0f, hungerDecayKgPerSecond) * Time.fixedDeltaTime, 0f, maxSatietyKg);
    }

    private void UpdateAlarm()
    {
        if (isCarcass) return;

        if (alarmRamActive)
        {
            alarm01 = Mathf.Max(0f, alarm01 - Time.fixedDeltaTime / Mathf.Max(0.1f, alarmRamDecaySeconds));
            if (alarm01 <= 0.001f || Time.time >= ramUntilTime)
            {
                alarm01 = 0f;
                alarmRamActive = false;
                ramTarget = null;
                harpoonRamCharging = false;
                behaviorStatus = "Тревога выгорела, таран не удался.";
            }

            return;
        }

        ShipPhysics ship = FindFirstObjectByType<ShipPhysics>();
        if (ship != null)
        {
            float distance = Vector3.Distance(transform.position, ship.transform.position);
            if (distance <= Mathf.Max(0f, passiveAggressionRangeMeters))
            {
                AddAlarm(alarmNearGainPerSecond * Time.fixedDeltaTime, "корабль слишком близко", ship);
            }
        }

        if (activeTether != null && activeTether.IsAttached)
        {
            float maxTensionN = Mathf.Max(1f, activeTether.maxTensionKg) * 9.81f;
            float tensionRatio = Mathf.Max(0f, activeTether.currentTensionN / maxTensionN);
            float gain = alarmHarpoonedGainPerSecond + alarmTensionGainPerSecond * tensionRatio;
            AddAlarm(gain * Time.fixedDeltaTime, "гарпун и натяжение троса", activeTether.ownerShip);
        }
        else
        {
            ShipPhysics harpooningShip = FindHarpooningShip();
            if (harpooningShip != null)
            {
                float alarmRange = Mathf.Max(0f, passiveAggressionRangeMeters) * Mathf.Max(1f, harpoonAlarmAggressionRangeMultiplier);
                if (Vector3.Distance(transform.position, harpooningShip.transform.position) <= alarmRange)
                {
                    AddAlarm(alarmOtherHarpoonedGainPerSecond * Time.fixedDeltaTime, "тревога от чужого гарпуна", harpooningShip);
                }
            }
        }
    }

    public void AddAlarm(float amount, string reason, ShipPhysics sourceShip = null)
    {
        if (isCarcass || amount <= 0f || alarmRamActive) return;

        float sourceMultiplier = sourceShip != null ? Mathf.Max(0f, sourceShip.leviathanAlarmGenerationMultiplier) : 1f;
        alarm01 = Mathf.Clamp01(alarm01 + amount * sourceMultiplier);
        if (alarm01 >= 1f)
        {
            StartAlarmRamAttack(sourceShip != null ? sourceShip : FindFirstObjectByType<ShipPhysics>(), reason);
        }
    }

    public static void AddAlarmNear(Vector3 position, float radiusMeters, float amount, string reason, Leviathan primary = null, float primaryAmount = 0f, ShipPhysics sourceShip = null)
    {
        float radiusSqr = Mathf.Max(0f, radiusMeters) * Mathf.Max(0f, radiusMeters);
        for (int i = 0; i < ActiveLeviathans.Count; i++)
        {
            Leviathan leviathan = ActiveLeviathans[i];
            if (leviathan == null || leviathan.isCarcass) continue;

            float sqr = (leviathan.transform.position - position).sqrMagnitude;
            if (sqr <= radiusSqr)
            {
                leviathan.AddAlarm(amount, reason, sourceShip);
            }
        }

        if (primary != null && primaryAmount > 0f)
        {
            primary.AddAlarm(primaryAmount, reason, sourceShip);
        }
    }

    private void StartAlarmRamAttack(ShipPhysics target, string reason)
    {
        if (target == null)
        {
            alarm01 = 0f;
            return;
        }

        ramTarget = target;
        alarmRamActive = true;
        alarm01 = 1f;
        ramUntilTime = Time.time + Mathf.Max(0.1f, alarmRamDecaySeconds);
        harpoonRamCharging = true;
        harpoonRamPhaseUntilTime = Time.time + Mathf.Max(0.2f, harpoonedRamChargeSeconds);
        behaviorStatus = $"Тревога на максимуме: таран из-за {reason}.";
    }

    private bool WantsFood()
    {
        return satietyKg < maxSatietyKg * 0.72f;
    }

    public bool TryEatOre(string oreItemId, int amountKg)
    {
        if (isCarcass || amountKg <= 0 || satietyKg >= maxSatietyKg) return false;

        satietyKg = Mathf.Clamp(satietyKg + amountKg, 0f, Mathf.Max(1f, maxSatietyKg));
        behaviorStatus = $"Перехватил кусок руды {oreItemId}: +{amountKg} кг сытости ({satietyKg:0}/{maxSatietyKg:0}).";
        if (debugLogging)
        {
            Debug.Log("[Левиафан] " + displayName + " " + behaviorStatus, this);
        }

        return true;
    }

    public static Leviathan FindFragmentEater(Vector3 position)
    {
        Leviathan best = null;
        float bestSqr = float.PositiveInfinity;
        for (int i = 0; i < ActiveLeviathans.Count; i++)
        {
            Leviathan leviathan = ActiveLeviathans[i];
            if (leviathan == null || leviathan.isCarcass || !leviathan.WantsFood()) continue;

            float radius = Mathf.Max(0.1f, leviathan.fragmentEatRadiusMeters);
            float sqr = (leviathan.transform.position - position).sqrMagnitude;
            if (sqr > radius * radius || sqr >= bestSqr) continue;

            best = leviathan;
            bestSqr = sqr;
        }

        return best;
    }

    public static ShipPhysics FindHarpooningShip()
    {
        for (int i = 0; i < ActiveLeviathans.Count; i++)
        {
            Leviathan leviathan = ActiveLeviathans[i];
            if (leviathan == null || leviathan.activeTether == null || !leviathan.activeTether.IsAttached) continue;
            if (leviathan.activeTether.ownerShip != null) return leviathan.activeTether.ownerShip;
        }

        return null;
    }

    private bool UpdateFeeding()
    {
        if (!WantsFood()) return false;

        if (feedingTarget == null || feedingTarget.IsDepleted)
        {
            feedingTarget = MiningRock.FindNearestFeedTarget(transform.position, Mathf.Max(0f, feedingSearchRadiusMeters));
        }

        if (feedingTarget == null || feedingTarget.IsDepleted) return false;

        Vector3 targetPosition = feedingTarget.transform.position;
        float distance = Vector3.Distance(rb.worldCenterOfMass, targetPosition);
        float biteDistance = Mathf.Max(bodyRadiusMeters + 1f, feedingApproachDistanceMeters);
        if (distance > biteDistance)
        {
            SteerToward(targetPosition, 1f, Mathf.Max(0.1f, cruiseSpeedMS * 0.75f));
            behaviorStatus = $"Идет к глыбе {feedingTarget.displayName}: {distance:0} м, сытость {satietyKg:0}/{maxSatietyKg:0}.";
            return true;
        }

        if (Time.time >= nextFeedingBiteTime)
        {
            nextFeedingBiteTime = Time.time + Mathf.Max(0.2f, feedingBiteIntervalSeconds);
            if (feedingTarget.TryLeviathanBite(Mathf.Max(1, feedingBiteKg), out int eatenKg, out string oreItemId))
            {
                TryEatOre(oreItemId, eatenKg);
            }
            else
            {
                feedingTarget = null;
            }
        }

        SteerToward(targetPosition, 0.35f, Mathf.Max(0.1f, cruiseSpeedMS * 0.25f));
        return true;
    }

    private bool UpdateRamAttack()
    {
        if (ramTarget == null || Time.time > ramUntilTime)
        {
            ramTarget = null;
            if (alarmRamActive)
            {
                alarmRamActive = false;
                alarm01 = 0f;
                behaviorStatus = "Тревога спала, таран не успел дойти до цели.";
            }

            return false;
        }

        float rangeLimit = Mathf.Max(passiveAggressionRangeMeters * 1.8f, bodyLengthMeters * 4f);
        if (Vector3.Distance(transform.position, ramTarget.transform.position) > rangeLimit)
        {
            ramTarget = null;
            alarmRamActive = false;
            alarm01 = 0f;
            behaviorStatus = "Цель ушла слишком далеко, тревога спала.";
            return false;
        }

        SteerToward(ramTarget.transform.position, Mathf.Max(1f, ramChargeForceMultiplier), Mathf.Max(0.1f, cruiseSpeedMS * 1.15f));
        behaviorStatus = $"Таранит корабль {ramTarget.name}, тревога {alarm01:P0}.";
        return true;
    }

    private void SteerToward(Vector3 targetPosition, float forceMultiplier, float desiredSpeed)
    {
        Vector3 directToTarget = targetPosition - rb.worldCenterOfMass;
        if (directToTarget.sqrMagnitude < 0.01f) return;

        float distance = directToTarget.magnitude;
        float arrivalRadius = Mathf.Max(0.1f, autopilotArrivalRadiusMeters);
        float desiredSpeedAbs = Mathf.Max(0.1f, desiredSpeed);
        Vector3 steeringTarget = GetOrganicSwimTarget(targetPosition, desiredSpeedAbs, distance, arrivalRadius);
        Vector3 toTarget = steeringTarget - rb.worldCenterOfMass;
        if (toTarget.sqrMagnitude < 0.01f) return;

        Vector3 direction = toTarget.normalized;
        float remainingDistance = Mathf.Max(0f, distance - arrivalRadius);
        Vector3 forward = transform.forward;
        if (forward.sqrMagnitude < 0.001f) forward = direction;
        forward.Normalize();

        float turnAngleDeg = Vector3.Angle(forward, direction);
        float fullArcAngle = Mathf.Max(1f, fishFullArcAngleDeg);
        float turn01 = Mathf.InverseLerp(12f, fullArcAngle, turnAngleDeg);
        float far01 = Mathf.InverseLerp(arrivalRadius * 1.5f, arrivalRadius + Mathf.Max(bodyLengthMeters, desiredSpeedAbs * 4f), remainingDistance);
        float fish01 = turn01 * far01;

        Vector3 leadDirection = direction;
        float lookAheadSeconds = Mathf.Max(0f, fishLookAheadSeconds) * fish01;
        if (lookAheadSeconds > 0.001f && rb.linearVelocity.sqrMagnitude > 0.25f)
        {
            Vector3 futureToTarget = targetPosition - (rb.worldCenterOfMass + rb.linearVelocity * lookAheadSeconds);
            if (futureToTarget.sqrMagnitude > 0.01f)
            {
                leadDirection = futureToTarget.normalized;
            }
        }

        if (Vector3.Dot(forward, leadDirection) < -0.985f)
        {
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            if (side.sqrMagnitude < 0.01f) side = transform.right;
            if (side.sqrMagnitude > 0.01f)
            {
                leadDirection = Vector3.Slerp(forward, side.normalized, 0.85f).normalized;
            }
        }

        float arcBlend = Mathf.Lerp(1f, 1f - Mathf.Clamp01(fishArcForwardBias), fish01);
        Vector3 swimDirection = Vector3.Slerp(forward, leadDirection, arcBlend);
        if (swimDirection.sqrMagnitude < 0.001f)
        {
            swimDirection = leadDirection;
        }
        else
        {
            swimDirection.Normalize();
        }

        float maxAcceleration = Mathf.Max(0.1f, (Mathf.Max(0f, forwardThrustKgf) + Mathf.Max(0f, omniThrustKgf)) * 9.81f / Mathf.Max(1f, rb.mass));
        float brakingSpeed = Mathf.Sqrt(2f * maxAcceleration * remainingDistance);
        float speedLimit = Mathf.Min(desiredSpeedAbs, brakingSpeed);
        if (remainingDistance <= 0.001f)
        {
            speedLimit = 0f;
        }
        else
        {
            float retainedTurnSpeed = desiredSpeedAbs * Mathf.Clamp01(fishTurnSpeedRetention) * fish01;
            speedLimit = Mathf.Min(desiredSpeedAbs, Mathf.Max(speedLimit, retainedTurnSpeed));
        }

        Vector3 desiredVelocity = speedLimit <= 0.001f ? Vector3.zero : swimDirection * speedLimit;
        Vector3 velocityError = desiredVelocity - rb.linearVelocity;
        float lateralCorrection = Mathf.Lerp(1f, Mathf.Clamp01(fishLateralCorrection), fish01);
        Vector3 forwardError = Vector3.Project(velocityError, forward);
        Vector3 lateralError = velocityError - forwardError;
        velocityError = forwardError + lateralError * lateralCorrection;

        Vector3 currentVelocity = rb.linearVelocity;
        if (currentVelocity.sqrMagnitude > 0.01f)
        {
            Vector3 velocityAxis = currentVelocity.normalized;
            Vector3 brakingError = Vector3.Project(velocityError, velocityAxis);
            if (Vector3.Dot(brakingError, currentVelocity) < 0f)
            {
                float alignment01 = Mathf.InverseLerp(0.15f, 0.85f, Vector3.Dot(forward, direction));
                float near01 = 1f - Mathf.InverseLerp(arrivalRadius * 2f, arrivalRadius + Mathf.Max(bodyLengthMeters, desiredSpeedAbs * 4f), remainingDistance);
                float brakePermission = Mathf.Lerp(Mathf.Clamp01(fishTurnBrakePermission), 1f, Mathf.Max(alignment01, near01));
                velocityError += brakingError * (brakePermission - 1f);
            }
        }

        Vector3 desiredForce = velocityError * rb.mass * Mathf.Max(0.05f, autopilotVelocityGain);
        ApplyLeviathanThrust(desiredForce, forceMultiplier);
        ApplySwimSideSwayForce(forceMultiplier, far01);
        ApplyLeviathanTurn(swimDirection, forceMultiplier);
    }

    private Vector3 GetOrganicSwimTarget(Vector3 targetPosition, float desiredSpeed, float distance, float arrivalRadius)
    {
        if (!organicSwimMotion || isCarcass || rb == null) return targetPosition;

        float nearFade = Mathf.InverseLerp(arrivalRadius * 1.5f, arrivalRadius + Mathf.Max(bodyLengthMeters, desiredSpeed * 5f), distance);
        if (nearFade <= 0.001f) return targetPosition;

        float speedFade = Mathf.InverseLerp(0.25f, Mathf.Max(0.5f, desiredSpeed * 0.5f), rb.linearVelocity.magnitude);
        float fade = nearFade * Mathf.Lerp(0.35f, 1f, speedFade);
        Vector3 right = transform.right;
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        right.Normalize();

        float angleOffsetMeters = Mathf.Tan(Mathf.Max(0f, swimSwayAngleDeg) * Mathf.Deg2Rad)
            * Mathf.Min(distance, Mathf.Max(bodyLengthMeters, desiredSpeed * 4f));
        float swayOffset = GetSwimSway() * angleOffsetMeters;
        float noiseFrequency = Mathf.Max(0f, swimNoiseFrequency);
        float noiseOffset = GetSwimNoise(1f, noiseFrequency) * Mathf.Max(0f, swimNoiseRadiusMeters);
        float verticalOffset = GetSwimNoise(2f, noiseFrequency * 0.73f + 0.0001f) * Mathf.Max(0f, swimVerticalNoiseMeters);

        return targetPosition + right * ((swayOffset + noiseOffset) * fade) + Vector3.up * (verticalOffset * fade);
    }

    private void ApplySwimSideSwayForce(float forceMultiplier, float fade)
    {
        if (!organicSwimMotion || isCarcass || rb == null) return;

        float sideForce = Mathf.Max(0f, swimSwaySideForceKgf) * 9.81f;
        if (sideForce <= 0.001f) return;

        float speedFade = Mathf.InverseLerp(0.25f, Mathf.Max(0.5f, cruiseSpeedMS * 0.5f), rb.linearVelocity.magnitude);
        float sway = Mathf.Clamp(GetSwimSway(), -1.35f, 1.35f);
        rb.AddForce(transform.right * (sway * sideForce * Mathf.Max(0f, forceMultiplier) * Mathf.Clamp01(fade) * speedFade), ForceMode.Force);
    }

    private bool UpdateRouteMachine()
    {
        if (!routeEnabled)
        {
            return false;
        }

        if (routeWaypoints == null || routeWaypoints.Count == 0)
        {
            routeStatus = "Маршрут пуст.";
            behaviorStatus = routeStatus;
            return false;
        }

        routeWaypointIndex = Mathf.Clamp(routeWaypointIndex, 0, routeWaypoints.Count - 1);
        Vector3 target = routeWaypoints[routeWaypointIndex];
        float distance = Vector3.Distance(rb.worldCenterOfMass, target);
        float arrivalRadius = Mathf.Max(0.5f, routeWaypointRadiusMeters);

        if (distance <= arrivalRadius)
        {
            routeWaypointIndex++;
            if (routeWaypointIndex >= routeWaypoints.Count)
            {
                if (routeLoop)
                {
                    routeWaypointIndex = 0;
                }
                else
                {
                    routeWaypointIndex = routeWaypoints.Count - 1;
                    routeStatus = "Маршрут завершён, держит последнюю точку.";
                    behaviorStatus = routeStatus;
                    SteerToward(target, 1f, Mathf.Max(0.1f, routeCruiseSpeedMS * 0.2f));
                    return true;
                }
            }

            target = routeWaypoints[routeWaypointIndex];
            distance = Vector3.Distance(rb.worldCenterOfMass, target);
        }

        float speed = routeCruiseSpeedMS > 0f ? routeCruiseSpeedMS : cruiseSpeedMS;
        speed = Mathf.Max(0.1f, speed);
        SteerToward(target, 1f, speed);
        routeStatus = $"Идёт к точке {routeWaypointIndex + 1}/{routeWaypoints.Count}: {distance:0} м.";
        behaviorStatus = routeStatus;
        return true;
    }

    public void GenerateRandomRoute(float radiusMeters, int pointCount)
    {
        if (routeWaypoints == null)
        {
            routeWaypoints = new List<Vector3>();
        }

        routeWaypoints.Clear();
        int count = Mathf.Clamp(pointCount, 1, 64);
        float radius = Mathf.Max(1f, radiusMeters);
        float minHeight = Mathf.Min(minY, maxY);
        float maxHeight = Mathf.Max(minY, maxY);
        float verticalSpread = Mathf.Max(20f, radius * 0.18f);
        Vector3 center = transform.position;

        for (int i = 0; i < count; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            float y = Mathf.Clamp(center.y + Random.Range(-verticalSpread, verticalSpread), minHeight, maxHeight);
            routeWaypoints.Add(new Vector3(center.x + offset.x, y, center.z + offset.y));
        }

        routeWaypointIndex = 0;
        routeEnabled = routeWaypoints.Count > 0;
        routeRandomRadiusMeters = radius;
        routeRandomPointCount = count;
        routeStatus = $"Сгенерировано {count} точек в радиусе {radius:0} м.";
        behaviorStatus = routeStatus;
    }

    public void RestartRoute()
    {
        routeWaypointIndex = 0;
        routeEnabled = routeWaypoints != null && routeWaypoints.Count > 0;
        routeStatus = routeEnabled ? "Маршрут запущен с первой точки." : "Маршрут пуст.";
        behaviorStatus = routeStatus;
    }

    public void StopRoute()
    {
        routeEnabled = false;
        routeStatus = "Маршрут остановлен.";
        behaviorStatus = routeStatus;
    }

    public void ClearRoute()
    {
        if (routeWaypoints == null)
        {
            routeWaypoints = new List<Vector3>();
        }

        routeWaypoints.Clear();
        routeWaypointIndex = 0;
        routeEnabled = false;
        routeStatus = "Маршрут очищен.";
        behaviorStatus = routeStatus;
    }

    private void UpdateWander()
    {
        if (Time.time >= nextWanderChangeTime || Vector3.Distance(rb.position, wanderTarget) < bodyLengthMeters)
        {
            PickNewWanderTarget(false);
        }

        SteerToward(wanderTarget, 1f, Mathf.Max(0.1f, cruiseSpeedMS));
    }

    private void ApplyLeviathanThrust(Vector3 desiredForce, float forceMultiplier)
    {
        if (isCarcass || rb == null) return;

        float multiplier = Mathf.Max(0f, forceMultiplier);
        float maxForward = Mathf.Max(0f, forwardThrustKgf) * 9.81f * multiplier;
        float maxOmni = Mathf.Max(0f, omniThrustKgf) * 9.81f * multiplier;
        Vector3 forward = transform.forward;
        Vector3 desiredDirection = desiredForce.sqrMagnitude > 0.001f ? desiredForce.normalized : forward;
        float forwardAlignment = Mathf.Clamp01(Vector3.Dot(desiredDirection, forward));
        float effectiveForwardLimit = maxForward * forwardAlignment;
        float forwardAmount = Mathf.Clamp(Vector3.Dot(desiredForce, forward), 0f, effectiveForwardLimit);
        Vector3 forwardForce = forward * forwardAmount;
        Vector3 omniForce = Vector3.ClampMagnitude(desiredForce - forwardForce, maxOmni);
        rb.AddForce(forwardForce + omniForce, ForceMode.Force);
    }

    private void ApplyLeviathanTurn(Vector3 desiredDirection, float forceMultiplier)
    {
        if (isCarcass || rb == null || desiredDirection.sqrMagnitude < 0.001f) return;

        Vector3 direction = desiredDirection.normalized;
        Quaternion targetRotation = Quaternion.FromToRotation(transform.forward, direction) * rb.rotation;
        Quaternion delta = targetRotation * Quaternion.Inverse(rb.rotation);
        delta.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        if (axis.sqrMagnitude < 0.001f) return;

        float speedEffect = Mathf.InverseLerp(0f, Mathf.Max(0.1f, fullTurnEffectSpeedMS), rb.linearVelocity.magnitude);
        float livingTurnEffectiveness = Mathf.Lerp(Mathf.Clamp01(stationaryTurnEffectiveness), 1f, speedEffect);
        Vector3 torque = axis.normalized * (angleDeg * Mathf.Deg2Rad * Mathf.Max(0f, turnTorqueNm) * Mathf.Max(0f, forceMultiplier) * livingTurnEffectiveness)
            - rb.angularVelocity * Mathf.Max(0f, turnDamping) * Mathf.Max(0f, turnTorqueNm);
        rb.AddTorque(Vector3.ClampMagnitude(torque, Mathf.Max(0f, turnTorqueNm) * Mathf.Max(0.1f, forceMultiplier) * Mathf.Max(0.1f, livingTurnEffectiveness)), ForceMode.Force);
    }

    private void ApplyLeviathanAirResistance()
    {
        if (rb == null) return;

        Vector3 velocity = rb.linearVelocity;
        if (velocity.sqrMagnitude < 0.0001f) return;

        ApplyAxisDrag(transform.forward, Vector3.Dot(velocity, transform.forward), Mathf.Max(0.01f, frontalArea));
        ApplyAxisDrag(transform.right, Vector3.Dot(velocity, transform.right), Mathf.Max(0.01f, frontalArea * Mathf.Max(0.01f, sideAreaMultiplier)));
        ApplyAxisDrag(transform.up, Vector3.Dot(velocity, transform.up), Mathf.Max(0.01f, frontalArea * Mathf.Max(0.01f, verticalAreaMultiplier)));
    }

    private void ApplyAxisDrag(Vector3 axis, float axisSpeed, float area)
    {
        if (Mathf.Abs(axisSpeed) < 0.001f) return;

        float force = 0.5f * Mathf.Max(0f, airDensity) * Mathf.Max(0f, dragCoefficient) * area * axisSpeed * axisSpeed;
        rb.AddForce(-axis.normalized * Mathf.Sign(axisSpeed) * force, ForceMode.Force);
    }

    private void RecalculateDragAreaFromBody()
    {
        float radius = Mathf.Max(0.1f, bodyRadiusMeters);
        float length = Mathf.Max(radius * 2f, bodyLengthMeters);
        frontalArea = Mathf.PI * radius * radius;
        float sideArea = length * radius * 2f;
        float areaRatio = sideArea / Mathf.Max(0.01f, frontalArea);
        sideAreaMultiplier = Mathf.Max(1f, areaRatio);
        verticalAreaMultiplier = Mathf.Max(1f, areaRatio * 0.85f);
    }

    private void OnValidate()
    {
        if (autoCalculateDragArea)
        {
            RecalculateDragAreaFromBody();
        }
    }

    private void ApplyBellyDownStabilization()
    {
        if (!bellyDownStabilization || isCarcass || rb == null) return;

        Vector3 forward = transform.forward;
        if (forward.sqrMagnitude < 0.001f) return;

        forward.Normalize();
        Vector3 desiredUp = Vector3.ProjectOnPlane(Vector3.up, forward);
        Vector3 currentUp = Vector3.ProjectOnPlane(transform.up, forward);
        if (desiredUp.sqrMagnitude < 0.001f || currentUp.sqrMagnitude < 0.001f) return;

        desiredUp.Normalize();
        currentUp.Normalize();
        float rollErrorDeg = Vector3.SignedAngle(currentUp, desiredUp, forward);
        float rollAngularVelocity = Vector3.Dot(rb.angularVelocity, forward);
        float torque = rollErrorDeg * Mathf.Deg2Rad * Mathf.Max(0f, bellyDownTorqueNm)
            - rollAngularVelocity * Mathf.Max(0f, bellyDownDamping) * Mathf.Max(0f, bellyDownTorqueNm);
        rb.AddTorque(forward * torque, ForceMode.Force);
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

        Vector3 toShip = activeTether.ShipPoint - rb.worldCenterOfMass;
        Vector3 awayFromShip = -toShip;
        Vector3 flatToShip = Vector3.ProjectOnPlane(toShip, Vector3.up);
        Vector3 flatAway = -flatToShip;
        if (flatAway.sqrMagnitude < 0.1f)
        {
            flatAway = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
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

        ShipPhysics ship = activeTether.ownerShip;
        float shipMass = ship != null ? ship.GetTotalMassKg() : 1000f;
        if (!alarmRamActive)
        {
            Vector3 calmDirection = flatAway.normalized + harpoonPanicDirection * 0.16f + dive * 0.3f;
            if (calmDirection.sqrMagnitude < 0.1f)
            {
                calmDirection = transform.forward;
            }

            calmDirection.Normalize();
            float maxTensionN = Mathf.Max(1f, activeTether.maxTensionKg) * 9.81f;
            float tensionRatio = Mathf.Clamp01(activeTether.CurrentTensionKg * 9.81f / maxTensionN);
            float baseSwimForce = Mathf.Max(0f, Mathf.Max(forwardThrustKgf, omniThrustKgf)) * 9.81f;
            float calmForce = baseSwimForce
                * Mathf.Max(0f, harpoonStruggleForceMultiplier)
                * Mathf.Lerp(0.55f, 0.9f, tensionRatio);
            ApplyLeviathanThrust(calmDirection * calmForce, 1f);
            ApplyLeviathanTurn(calmDirection, 1f);
            behaviorStatus = $"На гарпуне тянет прочь, тревога {alarm01:P0}, натяжение {activeTether.CurrentTensionKg:0}/{activeTether.maxTensionKg:0} кг.";

            return;
        }

        float distanceToShip = toShip.magnitude;
        float retreatDistance = Mathf.Max(bodyLengthMeters * 1.8f, harpoonedRamRetreatDistanceMeters);
        float chargeDistance = Mathf.Max(bodyRadiusMeters * 2.5f, harpoonedRamChargeDistanceMeters);
        if (harpoonRamPhaseUntilTime <= 0f)
        {
            harpoonRamCharging = false;
            harpoonRamPhaseUntilTime = Time.time + Mathf.Max(0.2f, harpoonedRamRetreatSeconds);
        }

        if (harpoonRamCharging)
        {
            if (distanceToShip <= chargeDistance || Time.time >= harpoonRamPhaseUntilTime)
            {
                harpoonRamCharging = false;
                harpoonRamPhaseUntilTime = Time.time + Mathf.Max(0.2f, harpoonedRamRetreatSeconds);
            }
        }
        else if (distanceToShip >= retreatDistance || Time.time >= harpoonRamPhaseUntilTime)
        {
            harpoonRamCharging = true;
            harpoonRamPhaseUntilTime = Time.time + Mathf.Max(0.2f, harpoonedRamChargeSeconds);
        }

        Vector3 direction = harpoonRamCharging
            ? flatToShip.normalized + harpoonPanicDirection * 0.06f
            : flatAway.normalized + harpoonPanicDirection * 0.12f + dive * 0.25f;
        if (direction.sqrMagnitude < 0.1f)
        {
            direction = transform.forward;
        }

        direction.Normalize();

        float panicFromTension = activeTether.maxTensionKg > 0f
            ? Mathf.Clamp01(activeTether.CurrentTensionKg / activeTether.maxTensionKg)
            : 0f;
        float panic = Mathf.Lerp(0.8f, 1.12f, panicFromTension);
        float massRatio = shipMass > 0.001f ? massKg / shipMass : 1f;
        float massRamFactor = Mathf.Lerp(
            Mathf.Max(0.1f, harpoonedSmallRamForceMultiplier),
            1f,
            Mathf.Clamp01(massRatio / Mathf.Max(0.1f, harpoonedRamMassRatio)));
        float ramMultiplier = harpoonRamCharging
            ? Mathf.Max(0.1f, ramChargeForceMultiplier) * massRamFactor
            : Mathf.Max(0.25f, harpoonStruggleForceMultiplier);
        float force = Mathf.Max(0f, Mathf.Max(forwardThrustKgf, omniThrustKgf)) * 9.81f * panic * ramMultiplier;
        ApplyLeviathanThrust(direction * force, 1f);
        ApplyLeviathanTurn(direction, ramMultiplier);
        behaviorStatus = harpoonRamCharging
            ? $"На гарпуне идет в таран: дистанция {distanceToShip:0}/{chargeDistance:0} м, масса {massKg:0}/{shipMass:0} кг, натяжение {activeTether.CurrentTensionKg:0}/{activeTether.maxTensionKg:0} кг."
            : $"На гарпуне отходит для нового тарана: дистанция {distanceToShip:0}/{retreatDistance:0} м, натяжение {activeTether.CurrentTensionKg:0}/{activeTether.maxTensionKg:0} кг.";
    }

    private void UpdateCarcassFailurePhysics()
    {
        if (rb == null) return;

        rb.useGravity = false;
        float elapsed = carcassStartTime >= 0f ? Time.time - carcassStartTime : 0f;
        float ramp = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, failureGravityRampSeconds));
        float gravity01 = Mathf.Lerp(Mathf.Clamp01(failureStartGravity01), 1f, ramp);
        rb.AddForce(Physics.gravity * rb.mass * gravity01, ForceMode.Force);
    }

    private void BecomeCarcass()
    {
        if (isCarcass) return;
        isCarcass = true;
        health = 0f;
        carcassStartTime = Time.time;
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.12f;
        behaviorStatus = $"Системы отказали: гравитация растет до полной за {failureGravityRampSeconds:0} сек.";
        Debug.Log("[Левиафан] " + displayName + " потерял здоровье: тяга и поворот отказали, гравитация возвращается постепенно.", this);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isCarcass || Time.time < lastRamDamageTime + 1.2f) return;

        ShipPhysics ship = collision.collider.GetComponentInParent<ShipPhysics>();
        if (ship == null) return;

        if (activeTether != null && activeTether.IsAttached && activeTether.ownerShip == ship)
        {
            harpoonRamCharging = false;
            harpoonRamPhaseUntilTime = Time.time + Mathf.Max(0.2f, harpoonedRamRetreatSeconds);
        }

        float speed = collision.relativeVelocity.magnitude;
        if (speed < Mathf.Max(0f, ramMinDamageSpeedMS)) return;

        Rigidbody shipBody = ship.GetComponent<Rigidbody>();
        float targetMass = shipBody != null ? Mathf.Max(1f, shipBody.mass) : Mathf.Max(1f, ship.GetTotalMassKg());
        float sourceMass = Body != null ? Mathf.Max(1f, Body.mass) : Mathf.Max(1f, massKg);
        float reducedMass = sourceMass * targetMass / Mathf.Max(1f, sourceMass + targetMass);
        float energyKJ = 0.5f * reducedMass * speed * speed / 1000f;
        ContactPoint contact = collision.GetContact(0);

        DamageHitContext context = new DamageHitContext
        {
            shellType = DamageShellType.Impact,
            shellName = "Таран левиафана",
            sourceName = displayName,
            impactEnergyKJ = energyKJ,
            impactDamagePerKJ = Mathf.Max(0f, ramDamageScale),
            impactSpeedMS = speed,
            impactSourceMassKg = sourceMass,
            impactTargetMassKg = targetMass,
            impactSourceDamageMultiplier = Mathf.Max(0f, ramDamageMultiplier),
            hitPoint = contact.point,
            hitNormal = contact.normal,
            incomingDirection = collision.relativeVelocity.sqrMagnitude > 0.001f ? collision.relativeVelocity.normalized : -contact.normal,
            velocity = collision.relativeVelocity
        };

        DamageableShip damageableShip = ship.GetComponentInParent<DamageableShip>();
        if (damageableShip != null)
        {
            ArmorZone zone = collision.collider.GetComponentInParent<ArmorZone>();
            if (zone != null)
            {
                zone.ReceiveHit(context);
            }
            else
            {
                damageableShip.ApplyHit(new ArmorSurface
                {
                    zoneId = "leviathan_ram",
                    displayNameRu = "таран левиафана",
                    armorMm = 0f,
                    baseArmorMm = 0f,
                    armorIntegrity01 = 1f,
                    ricochetAngleDeg = 89f,
                    overmatchCaliberMultiplier = 1f,
                    structureDamageMultiplier = 1f,
                    highExplosiveSurfaceDamageMultiplier = 1f,
                    ramDamageMultiplier = 1f
                }, context);
            }
        }

        float selfDamage = Mathf.Sqrt(Mathf.Max(0f, energyKJ)) * Mathf.Max(0f, ramDamageScale) * 0.12f;
        ApplyDamage(selfDamage);
        lastRamDamageTime = Time.time;
        alarm01 = 0f;
        alarmRamActive = false;
        ramTarget = null;
        behaviorStatus = $"Столкнулся с кораблем: скорость {speed:0.0} м/с, энергия {energyKJ:0} кДж.";
        if (debugLogging)
        {
            Debug.Log("[Левиафан] " + displayName + " " + behaviorStatus, this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (routeWaypoints == null || routeWaypoints.Count == 0) return;

        Gizmos.color = new Color(0.9f, 0.25f, 1f, 0.9f);
        for (int i = 0; i < routeWaypoints.Count; i++)
        {
            Vector3 point = routeWaypoints[i];
            float radius = Mathf.Max(1f, routeWaypointRadiusMeters);
            Gizmos.DrawWireSphere(point, radius);

            if (i < routeWaypoints.Count - 1)
            {
                Gizmos.DrawLine(point, routeWaypoints[i + 1]);
            }
            else if (routeLoop && routeWaypoints.Count > 1)
            {
                Gizmos.DrawLine(point, routeWaypoints[0]);
            }
        }

        if (routeEnabled && routeWaypointIndex >= 0 && routeWaypointIndex < routeWaypoints.Count)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, routeWaypoints[routeWaypointIndex]);
            Gizmos.DrawWireSphere(routeWaypoints[routeWaypointIndex], Mathf.Max(2f, routeWaypointRadiusMeters * 1.3f));
        }
    }

    private void RefreshSurveyInspectorState(bool force = false)
    {
        if (survey == null)
        {
            survey = new SurveyObjectInspectorState();
        }

        if (!force && Application.isPlaying && Time.unscaledTime < nextSurveyInspectorRefreshTime)
        {
            return;
        }

        nextSurveyInspectorRefreshTime = Time.unscaledTime + 0.5f;
        MetaGameState meta = ResolveSurveyMeta();
        float potentialKg = SurveySystem.CalculateLeviathanInformationPotentialKg(massKg);

        SurveyObjectRuntimeInfo info = SurveySystem.BuildObjectRuntimeInfo(
            meta != null ? meta.WorldConfig : null,
            meta != null ? meta.progress : null,
            ScoutedObjectKind.Leviathan,
            leviathanId,
            displayName,
            transform.position,
            potentialKg);

        survey.Apply(info);
    }

    private MetaGameState ResolveSurveyMeta()
    {
        if (cachedSurveyMeta == null)
        {
            cachedSurveyMeta = FindFirstObjectByType<MetaGameState>();
        }

        return cachedSurveyMeta;
    }

    public static Leviathan FindNearest(Vector3 position, float rangeMeters, bool requireAlive, string ignoredLeviathanId = "", float maxMassKg = 0f)
    {
        Leviathan best = null;
        float bestSqr = Mathf.Max(0f, rangeMeters) * Mathf.Max(0f, rangeMeters);
        for (int i = 0; i < ActiveLeviathans.Count; i++)
        {
            Leviathan leviathan = ActiveLeviathans[i];
            if (leviathan == null) continue;
            if (requireAlive && leviathan.CanBeClaimed) continue;
            if (!string.IsNullOrWhiteSpace(ignoredLeviathanId) && leviathan.leviathanId == ignoredLeviathanId) continue;
            if (maxMassKg > 0f && leviathan.massKg > maxMassKg) continue;

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

    public static void GetActiveLeviathans(List<Leviathan> results, bool requireAlive = false)
    {
        if (results == null) return;
        results.Clear();

        for (int i = 0; i < ActiveLeviathans.Count; i++)
        {
            Leviathan leviathan = ActiveLeviathans[i];
            if (leviathan == null) continue;
            if (requireAlive && leviathan.CanBeClaimed) continue;
            results.Add(leviathan);
        }
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
    public float maxTensionLifetimeSeconds = 5f;
    public float currentTensionN;
    [Range(0f, 1f)] public float wear01;
    public string lastMessage = "";

    private Rigidbody shipBody;
    private Rigidbody targetBody;
    private LineRenderer lineRenderer;
    private bool attached;

    public bool IsAttached => attached && ownerShip != null && target != null;
    public float CurrentTensionKg => currentTensionN / 9.81f;
    public float WearPercent => Mathf.Clamp01(wear01) * 100f;
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

        if (ownerShip != null && !ownerShip.TrySpendHarpoonUpkeep(Time.fixedDeltaTime, out string upkeepReason))
        {
            Detach(upkeepReason);
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
        if (currentTensionN > 0.001f)
        {
            float tensionRatio = currentTensionN / maxTensionN;
            wear01 += tensionRatio * Time.fixedDeltaTime / Mathf.Max(0.1f, maxTensionLifetimeSeconds);
            if (wear01 >= 1f)
            {
                Detach($"Гарпун изношен и оборвался: натяжение {CurrentTensionKg:F0}/{maxTensionKg:F0} кг, износ 100%.");
                return;
            }
        }

        float appliedTensionN = Mathf.Min(currentTensionN, maxTensionN * 2.5f);

        if (winchingCarcass)
        {
            if (appliedTensionN > 0f)
            {
                shipBody.AddForce(direction * appliedTensionN, ForceMode.Force);
                targetBody.AddForce(-direction * appliedTensionN, ForceMode.Force);
            }

            lastMessage = TargetInCollectionRadius
                ? $"Туша в радиусе сбора: {distance:F1}/{collectionRadiusMeters:F1} м. Можно грузить."
                : $"Лебедка тянет тушу: {distance:F1}/{collectionRadiusMeters:F1} м, трос {ropeLengthMeters:F1} м, натяжение {CurrentTensionKg:F0}/{maxTensionKg:F0} кг, износ {WearPercent:F0}%.";
            UpdateLine(shipPoint, targetPoint);
            return;
        }

        if (appliedTensionN > 0f)
        {
            shipBody.AddForce(direction * appliedTensionN, ForceMode.Force);
            targetBody.AddForce(-direction * appliedTensionN, ForceMode.Force);

            float fatigueScale = Mathf.Clamp01(currentTensionN / maxTensionN);
            target.ApplyHarpoonFatigue(Mathf.Max(0f, fatiguePerSecond) * fatigueScale * Time.fixedDeltaTime);
            lastMessage = $"Натяжение {CurrentTensionKg:F0}/{maxTensionKg:F0} кг, износ {WearPercent:F0}%, здоровье цели {target.health:F0}/{target.maxHealth:F0}.";
        }
        else
        {
            lastMessage = $"Трос провис, натяжения нет. Износ {WearPercent:F0}%.";
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
        body.useGravity = false;

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
    private const float EngineAfterburnerPowerMultiplier = 1.2f;

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
    public string engineFuelId = "charcoal";
    [Tooltip("Доля энергии топлива, которая превращается в полезную мощность двигателя.")]
    public float engineFuelEfficiency = 0.32f;
    [Tooltip("Разрешает двигателю использовать форсажную зону сверх номинальных 100% мощности.")]
    public bool engineAfterburnerEnabled;
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
    [Tooltip("Если включено, харвестер грубо фильтрует любой конденсат в воду и теряет остальные фракции.")]
    public bool gasHarvesterWaterOnly;
    [HideInInspector] public float gasHarvesterPowerDrawActualKw;
    [HideInInspector] public float gasHarvesterCycleProgressSeconds;
    [HideInInspector] public float gasHarvesterBufferKg;
    [HideInInspector] public string gasHarvesterActiveCloudId = "";
    [HideInInspector] public string gasHarvesterLastMessage = "";

    [Header("Майнинг")]
    [Tooltip("Противоударный кузов ловит падающие куски. Пойманная руда складывается в общий груз корабля.")]
    public float miningImpactHoldCapacityKg;
    [Tooltip("Множитель урона от пойманных падающих рудных глыб. 0.5 = противоударный кузов получает вдвое меньше.")]
    public float miningImpactDamageTakenMultiplier = 1f;
    public float miningImpactMinDamageSpeedMS = 4f;
    public float miningImpactDamageScale = 10f;
    [Tooltip("Радиус сбора падающих кусков вокруг корабля.")]
    public float miningCatchRadiusMeters = 10f;
    [Tooltip("Дальность временной кнопки выстрела по глыбе.")]
    public float miningManualShotRangeMeters = 180f;
    [HideInInspector] public string miningLastMessage = "";

    [Header("Разведка")]
    [Tooltip("Базовый радиус, в котором любой корабль замечает координаты и постепенно собирает сведения без специальных приборов.")]
    public float baseObservationRadiusMeters = 100f;
    [Tooltip("Текущий радиус наблюдения после модулей. Если приборов нет, используется базовый радиус корабля.")]
    public float observationRadiusMeters = 100f;
    [Tooltip("Сколько единиц сведений в секунду собирается на половине радиуса наблюдения. У центра быстрее, у края медленнее.")]
    public float observationFactsAtHalfRadiusPerSecond = 1f;
    [Tooltip("Доля потенциальной научной информации о глыбах, которую прибор может снять, перерабатывая бумагу.")]
    [Range(0f, 1f)] public float observationRockInfoEfficiency;
    [Tooltip("Доля потенциальной научной информации об облаках, которую прибор может снять, перерабатывая бумагу.")]
    [Range(0f, 1f)] public float observationCloudInfoEfficiency;
    [Tooltip("Доля потенциальной научной информации о левиафанах, которую прибор может снять, перерабатывая бумагу.")]
    [Range(0f, 1f)] public float observationLeviathanInfoEfficiency;
    [Tooltip("Сколько единиц информации получается из 1 единицы бумаги. 0.2 = 5 бумаги на 1 опыт.")]
    [Range(0.01f, 1f)] public float surveyPaperToInfoEfficiency = 1f;
    [Tooltip("Множитель тревоги левиафанов от этого корабля. 0.5 = тревога растёт вдвое медленнее.")]
    [Min(0f)] public float leviathanAlarmGenerationMultiplier = 1f;
    [HideInInspector] public string surveyLastMessage = "";

    [Header("Оружие")]
    [Tooltip("Ресурс в грузовом списке корабля, который тратится на ручные выстрелы.")]
    public string weaponResourceId = "weapon";
    [Tooltip("Сколько килограммов оружия тратит один ручной выстрел.")]
    public float weaponShotCostKg = 0.1f;
    [Tooltip("Сколько здоровья левиафан теряет от одного ручного выстрела оружием.")]
    public float leviathanWeaponShotFlightDamage = 25f;
    [Tooltip("В каком радиусе выстрел тревожит ближайших левиафанов.")]
    public float weaponShotAlarmRadiusMeters = 450f;
    [HideInInspector] public string weaponLastMessage = "";

    [Header("Охота на левиафанов")]
    [Tooltip("Дальность ручного выстрела гарпуном по ближайшему левиафану.")]
    public float harpoonRangeMeters = 280f;
    [Tooltip("Рабочая длина троса. Если цель дальше этой длины, трос натягивается и тянет обе стороны.")]
    public float harpoonRopeLengthMeters = 160f;
    [Tooltip("Максимальное натяжение, которое выдерживает гарпун, в килограммах силы.")]
    public float harpoonMaxTensionKg = 4500f;
    [Tooltip("Сколько секунд трос живет при натяжении ровно на пределе прочности. При 10% натяжения живет в 10 раз дольше, при 200% - вдвое меньше.")]
    public float harpoonMaxTensionLifetimeSeconds = 5f;
    [Tooltip("Жесткость троса: сколько ньютонов появляется за каждый метр растяжения.")]
    public float harpoonStiffnessNPerMeter = 360f;
    [Tooltip("Демпфер троса: гасит рывок, если цель и корабль расходятся.")]
    public float harpoonDampingNsPerMeter = 110f;
    [Tooltip("Постоянная подтяжка лебедкой, когда трос уже натянут.")]
    public float harpoonReelForceN = 600f;
    [Tooltip("Сколько здоровья левиафан теряет в секунду при полном натяжении троса.")]
    public float harpoonFatiguePerSecond = 18f;
    [Tooltip("Радиус, в котором тушу можно погрузить в трюм.")]
    public float harpoonCarcassCollectionRadiusMeters = 14f;
    [Tooltip("Скорость, с которой лебедка укорачивает трос после того, как цель стала тушей.")]
    public float harpoonCarcassWinchSpeedMS = 7f;
    [Tooltip("Сколько оружия в минуту расходуется, пока гарпун держит цель.")]
    public float harpoonWeaponCostPerMinute;
    [Tooltip("Максимальная масса туши, которую этот гарпун может удержать и поднять. 0 = без ограничения.")]
    public float harpoonMaxCarcassMassKg;
    private float harpoonWeaponSpendBufferKg;

    [Header("Холодильник туш")]
    [Tooltip("Вместимость холодильника для туш, л. 1000 л = 1 м3.")]
    public float refrigeratedHoldCapacityLiters;
    [Tooltip("Сколько мощности холодильник забирает при включении независимо от заполнения.")]
    public float refrigeratedHoldPowerDrawKw;
    [Tooltip("Холодильник активен. Если выключить, туши считаются нестабильным грузом.")]
    public bool refrigeratedHoldEnabled = true;
    [HideInInspector] public float refrigeratedHoldPowerDrawActualKw;
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
    [Tooltip("Максимальная масса левиафана для автоохоты. 0 или меньше - без ограничения.")]
    public float leviathanHuntMaxTargetMassKg = 7000f;
    [Tooltip("Максимальный радиус отхода автоохоты от точки захвата при натяжении троса.")]
    public float leviathanHuntMaxTetherDriftMeters = 1500f;
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

    [Header("Боковая всенаправленная тяга")]
    [Tooltip("Максимальная боковая сила для ручного скольжения A/D, в килограмм-силах.")]
    public float lateralOmniThrustKgf = 260f;

    [Header("Лимиты скорости подъема")]
    public float maxStructuralVerticalSpeed = 5.0f; // Предел прочности (конструкционный)
    public float maxAutoVerticalSpeed = 1.0f;        // Лимит автопилота

    [Header("Окружающая среда")]
    public Vector3 windVelocity = Vector3.zero; // Глобальный вектор ветра (м/с)

    // Рассчитанный текущий коэффициент сопротивления (используется для физики)
    public float CurrentAeroDrag => 0.5f * airDensity * dragCoefficient * frontalArea;
    public float CurrentWindAerodynamicFactor => Mathf.Max(0f, dragCoefficient);
    public Vector3 EffectiveWindVelocity => GetEffectiveWindVelocity();
    public float EnginePowerLeverLimit => engineAfterburnerEnabled ? EngineAfterburnerPowerMultiplier : 1f;
    public float EnginePowerCapacityKw => Mathf.Max(0f, enginePowerKwAt100) * EnginePowerLeverLimit;

    public float CalculateEnginePowerLeverForPropellerEngagement(float propellerEngagement)
    {
        float maxLever = EnginePowerLeverLimit;
        if (enginePowerKwAt100 <= 0f)
        {
            return 0f;
        }

        float supportLever = Mathf.Clamp(CalculateCurrentSupportPowerDrawKw() / enginePowerKwAt100, 0f, maxLever);
        float remainingLever = Mathf.Max(0f, maxLever - supportLever);
        return Mathf.Clamp(supportLever + Mathf.Clamp01(propellerEngagement) * remainingLever, supportLever, maxLever);
    }

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
    [HideInInspector] public float sideInput;   // -1 скольжение влево, 1 скольжение вправо
    [HideInInspector] public float turnInput;   // -1 влево, 1 вправо
    [HideInInspector] public float targetTrimMass = 1000f; // Масса для триммирования (кг)
    [HideInInspector] public float liftInput;   // Legacy: altitude is target-driven; kept at 0 by flight controls.

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
    private bool leviathanHuntHasCapturePosition;
    private Vector3 leviathanHuntCapturePosition;
    private Vector3 leviathanHuntTetherDirection;
    private string gasHarvesterCycleCloudId = "";
    private float surveyTickAccumulator;
    private MetaGameState cachedMetaGameState;
    private readonly List<Leviathan> huntTargetBuffer = new List<Leviathan>();

    // Единая ручка управления мощностью (Обороты для CSU / Газ для Manual)
    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.mass = GetTotalMassKg();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.useGravity = true;
        ConfigureYawOnlyRigidbody();
        EnforceYawOnlyRotation(true);

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
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            ConfigureYawOnlyRigidbody();
            EnforceYawOnlyRotation(true);
        }
    }

    private void ConfigureYawOnlyRigidbody()
    {
        if (rb == null) return;

        RigidbodyConstraints preservedConstraints = rb.constraints & ~RigidbodyConstraints.FreezeRotationY;
        rb.constraints = preservedConstraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void EnforceYawOnlyRotation(bool immediate)
    {
        if (rb == null) return;

        ConfigureYawOnlyRigidbody();

        Vector3 angularVelocity = rb.angularVelocity;
        if (Mathf.Abs(angularVelocity.x) > 0.0001f || Mathf.Abs(angularVelocity.z) > 0.0001f)
        {
            rb.angularVelocity = new Vector3(0f, angularVelocity.y, 0f);
        }

        Quaternion yawOnlyRotation = Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);
        if (Quaternion.Angle(rb.rotation, yawOnlyRotation) <= 0.01f) return;

        if (immediate)
        {
            rb.rotation = yawOnlyRotation;
            transform.rotation = yawOnlyRotation;
        }
        else
        {
            rb.MoveRotation(yawOnlyRotation);
        }
    }

    public float GetTotalMassKg()
    {
        return Mathf.Max(1f, baseMass + Mathf.Max(0f, cargoMassKg));
    }

    public bool TryCollectMiningFragment(string oreItemId, int amountKg, out string reason)
    {
        return TryCollectMiningFragment(oreItemId, amountKg, 0f, out reason);
    }

    public bool TryCollectMiningFragment(string oreItemId, int amountKg, float impactSpeedMS, out string reason)
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

        ApplyMiningImpactDamage(amountKg, impactSpeedMS);

        if (amountKg > miningImpactHoldCapacityKg + 0.001f)
        {
            reason = $"Глыба слишком тяжёлая для противоударного кузова: {amountKg}/{miningImpactHoldCapacityKg:0} кг.";
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

    private void ApplyMiningImpactDamage(int amountKg, float impactSpeedMS)
    {
        float speed = Mathf.Max(0f, impactSpeedMS);
        if (amountKg <= 0 || speed < Mathf.Max(0f, miningImpactMinDamageSpeedMS)) return;

        DamageableShip damageableShip = GetComponentInParent<DamageableShip>();
        if (damageableShip == null) return;

        float targetMass = GetTotalMassKg();
        float sourceMass = Mathf.Max(1f, amountKg);
        float reducedMass = sourceMass * targetMass / Mathf.Max(1f, sourceMass + targetMass);
        float energyKJ = 0.5f * reducedMass * speed * speed / 1000f;
        DamageHitContext context = new DamageHitContext
        {
            shellType = DamageShellType.Impact,
            shellName = "Падающая рудная глыба",
            sourceName = "MiningRockImpact",
            impactEnergyKJ = energyKJ,
            impactDamagePerKJ = Mathf.Max(0f, miningImpactDamageScale),
            impactSpeedMS = speed,
            impactSourceMassKg = sourceMass,
            impactTargetMassKg = targetMass,
            impactSourceDamageMultiplier = Mathf.Max(0f, miningImpactDamageTakenMultiplier),
            hitPoint = transform.position,
            hitNormal = Vector3.up,
            incomingDirection = Vector3.down,
            velocity = Vector3.down * speed
        };

        damageableShip.ApplyHit(new ArmorSurface
        {
            zoneId = "mining_impact_hold",
            displayNameRu = "противоударный кузов",
            armorMm = 0f,
            baseArmorMm = 0f,
            armorIntegrity01 = 1f,
            ricochetAngleDeg = 90f,
            overmatchCaliberMultiplier = 3f,
            structureDamageMultiplier = 1f,
            highExplosiveSurfaceDamageMultiplier = 1f,
            ramDamageMultiplier = 1f
        }, context);
    }

    public bool TryShootNearestMiningRock(out string reason)
    {
        MiningRock target = MiningRock.FindNearestShootTarget(transform.position, Mathf.Max(0f, miningManualShotRangeMeters));
        if (target == null)
        {
            reason = "Нет глыбы в дальности выстрела.";
            miningLastMessage = reason;
            return false;
        }

        if (!TrySpendWeaponForShot(out reason))
        {
            miningLastMessage = reason;
            return false;
        }

        bool success = target.BreakOffByShot(out string shotMessage);
        Leviathan.AddAlarmNear(
            target.transform.position,
            Mathf.Max(0f, weaponShotAlarmRadiusMeters),
            0.16f,
            "выстрел по глыбе",
            null,
            0f,
            this);
        reason = shotMessage + $" Оружие -{Mathf.Max(0f, weaponShotCostKg):0.0} кг.";
        miningLastMessage = reason;
        weaponLastMessage = reason;
        return success;
    }

    public bool TryShootLeviathan(out string reason)
    {
        Leviathan target = activeHarpoon != null && activeHarpoon.IsAttached ? activeHarpoon.target : null;
        if (target == null || target.CanBeClaimed)
        {
            float range = Mathf.Max(Mathf.Max(0f, harpoonRangeMeters), Mathf.Max(0f, leviathanHuntEngageRangeMeters));
            target = Leviathan.FindNearest(transform.position, range, true);
        }

        if (target == null)
        {
            reason = "Нет живого левиафана в дальности выстрела.";
            harpoonLastMessage = reason;
            weaponLastMessage = reason;
            return false;
        }

        if (!TrySpendWeaponForShot(out reason))
        {
            harpoonLastMessage = reason;
            return false;
        }

        float damage = Mathf.Max(0f, leviathanWeaponShotFlightDamage);
        target.ApplyHarpoonFatigue(damage);
        Leviathan.AddAlarmNear(
            target.transform.position,
            Mathf.Max(0f, weaponShotAlarmRadiusMeters),
            Mathf.Max(0f, target.alarmShotNearAdd),
            "выстрел рядом",
            target,
            Mathf.Max(0f, target.alarmShotAtSelfAdd),
            this);
        reason = $"Выстрел по {target.displayName}: здоровье -{damage:0.#}. Оружие -{Mathf.Max(0f, weaponShotCostKg):0.0} кг.";
        if (target.CanBeClaimed)
        {
            reason += " Цель потеряла здоровье и стала добычей.";
        }

        harpoonLastMessage = reason;
        weaponLastMessage = reason;
        return true;
    }

    private bool TrySpendWeaponForShot(out string reason)
    {
        reason = "";
        MetaGameState meta = ResolveMetaGameState();
        if (meta == null || meta.progress == null)
        {
            reason = "Стрельба ждет MetaGameState.";
            weaponLastMessage = reason;
            return false;
        }

        string resourceId = string.IsNullOrWhiteSpace(weaponResourceId) ? "weapon" : weaponResourceId;
        float costKg = Mathf.Max(0f, weaponShotCostKg);
        bool spent = meta.TrySpendFractionalShipCargoFromRuntime(resourceId, costKg, ref meta.progress.shipWeaponSpendBufferKg, out reason);
        if (!spent)
        {
            weaponLastMessage = reason;
        }

        return spent;
    }

    public bool TryFireHarpoonAtNearestLeviathan(out string reason, float maxTargetMassKg = 0f, bool requireSurveyed = false)
    {
        reason = "";
        if (activeHarpoon != null && activeHarpoon.IsAttached)
        {
            reason = "Гарпун уже держит цель.";
            harpoonLastMessage = reason;
            return false;
        }

        Leviathan target = FindNearestHarpoonTarget(Mathf.Max(0f, harpoonRangeMeters), "", maxTargetMassKg, requireSurveyed);
        if (target == null)
        {
            if (harpoonMaxCarcassMassKg > 0f)
            {
                reason = $"В радиусе гарпуна нет живого левиафана с тушей не тяжелее {harpoonMaxCarcassMassKg:0} кг.";
            }
            else
            {
                reason = maxTargetMassKg > 0f
                    ? $"В радиусе гарпуна нет живого левиафана не тяжелее {maxTargetMassKg:0} кг."
                    : "В радиусе гарпуна нет живого левиафана.";
            }

            harpoonLastMessage = reason;
            return false;
        }

        GameObject tetherObject = new GameObject("Гарпунный трос");
        tetherObject.transform.SetParent(transform, false);
        activeHarpoon = tetherObject.AddComponent<HarpoonTether>();
        activeHarpoon.ropeLengthMeters = Mathf.Max(1f, harpoonRopeLengthMeters);
        activeHarpoon.maxTensionKg = Mathf.Max(1f, harpoonMaxTensionKg);
        activeHarpoon.maxTensionLifetimeSeconds = Mathf.Max(0.1f, harpoonMaxTensionLifetimeSeconds);
        activeHarpoon.stiffnessNPerMeter = Mathf.Max(0f, harpoonStiffnessNPerMeter);
        activeHarpoon.dampingNsPerMeter = Mathf.Max(0f, harpoonDampingNsPerMeter);
        activeHarpoon.reelForceN = Mathf.Max(0f, harpoonReelForceN);
        activeHarpoon.fatiguePerSecond = Mathf.Max(0f, harpoonFatiguePerSecond);
        activeHarpoon.collectionRadiusMeters = Mathf.Max(0.1f, harpoonCarcassCollectionRadiusMeters);
        activeHarpoon.carcassWinchSpeedMetersPerSecond = Mathf.Max(0.1f, harpoonCarcassWinchSpeedMS);
        activeHarpoon.Attach(this, target);
        if (activeHarpoon.IsAttached)
        {
            float alarmRange = Mathf.Max(harpoonRangeMeters, target.passiveAggressionRangeMeters * Mathf.Max(1f, target.harpoonAlarmAggressionRangeMultiplier));
            Leviathan.AddAlarmNear(
                target.transform.position,
                alarmRange,
                Mathf.Max(0f, target.alarmOtherHarpoonedGainPerSecond) * 1.5f,
                "гарпун рядом",
                target,
                Mathf.Max(0.1f, target.alarmHarpoonedGainPerSecond) * 1.5f,
                this);
        }

        reason = activeHarpoon.lastMessage;
        harpoonLastMessage = reason;
        return activeHarpoon.IsAttached;
    }

    private Leviathan FindNearestHarpoonTarget(float rangeMeters, string ignoredLeviathanId, float maxTargetMassKg, bool requireSurveyed)
    {
        PlayerProgress progress = null;
        if (requireSurveyed)
        {
            MetaGameState meta = ResolveMetaGameState();
            progress = meta != null ? meta.progress : null;
            if (progress == null) return null;
        }

        Leviathan best = null;
        float bestSqr = Mathf.Max(0f, rangeMeters) * Mathf.Max(0f, rangeMeters);
        Leviathan.GetActiveLeviathans(huntTargetBuffer, true);
        for (int i = 0; i < huntTargetBuffer.Count; i++)
        {
            Leviathan leviathan = huntTargetBuffer[i];
            if (leviathan == null) continue;
            if (!string.IsNullOrWhiteSpace(ignoredLeviathanId) && leviathan.leviathanId == ignoredLeviathanId) continue;
            if (maxTargetMassKg > 0f && leviathan.massKg > maxTargetMassKg) continue;
            if (harpoonMaxCarcassMassKg > 0f && leviathan.carcassMassKg > harpoonMaxCarcassMassKg) continue;
            if (requireSurveyed && !progress.HasObjectFacts(ScoutedObjectKind.Leviathan, leviathan.leviathanId)) continue;

            float sqr = (leviathan.transform.position - transform.position).sqrMagnitude;
            if (sqr > bestSqr) continue;

            best = leviathan;
            bestSqr = sqr;
        }

        return best;
    }

    private Leviathan FindNearestSurveyedLeviathan(float rangeMeters, string ignoredLeviathanId, float maxMassKg)
    {
        MetaGameState meta = ResolveMetaGameState();
        PlayerProgress progress = meta != null ? meta.progress : null;
        if (progress == null) return null;

        Leviathan best = null;
        float bestSqr = Mathf.Max(0f, rangeMeters) * Mathf.Max(0f, rangeMeters);
        Leviathan.GetActiveLeviathans(huntTargetBuffer, true);
        for (int i = 0; i < huntTargetBuffer.Count; i++)
        {
            Leviathan leviathan = huntTargetBuffer[i];
            if (leviathan == null) continue;
            if (!string.IsNullOrWhiteSpace(ignoredLeviathanId) && leviathan.leviathanId == ignoredLeviathanId) continue;
            if (maxMassKg > 0f && leviathan.massKg > maxMassKg) continue;
            if (!progress.HasObjectFacts(ScoutedObjectKind.Leviathan, leviathan.leviathanId)) continue;

            float sqr = (leviathan.transform.position - transform.position).sqrMagnitude;
            if (sqr > bestSqr) continue;

            best = leviathan;
            bestSqr = sqr;
        }

        return best;
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

        ClearLeviathanHuntTetherHold();
        routeEnabled = false;
        routeWasEnabled = false;
        positionHold = true;
        positionHoldWasEnabled = true;
        targetHoldPosition = transform.position;
        targetSpeedMS = 0f;
        thrustInput = 0f;
        sideInput = 0f;
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

    private void ClearLeviathanHuntTetherHold()
    {
        leviathanHuntHasTetherHoldPosition = false;
        leviathanHuntHasCapturePosition = false;
        leviathanHuntTetherDirection = Vector3.zero;
    }

    private void RegisterLeviathanHuntTetherLoss(HarpoonTether tether, string reason)
    {
        if (!leviathanHuntAutopilotEnabled || tether == null) return;

        ClearLeviathanHuntTetherHold();
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
            reason = "Левиафан еще держится в воздухе: ослабь здоровье.";
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

        if (refrigeratedHoldCapacityLiters <= 0f)
        {
            reason = "На корабле нет холодильника для туш.";
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
        if (harpoonMaxCarcassMassKg > 0f && amountKg > harpoonMaxCarcassMassKg)
        {
            reason = $"Гарпун не удержит такую тушу: {amountKg}/{harpoonMaxCarcassMassKg:0} кг.";
            harpoonLastMessage = reason;
            return false;
        }

        if (amountKg > refrigeratedHoldCapacityLiters + 0.001f)
        {
            reason = $"Холодильник мал для туши: {amountKg}/{refrigeratedHoldCapacityLiters:0} кг.";
            harpoonLastMessage = reason;
            return false;
        }

        if (refrigeratedHoldPowerDrawKw > 0f && enginePowerKwAt100 > 0f &&
            claudiumPowerDrawKw + refrigeratedHoldPowerDrawKw > enginePowerKwAt100 + 0.001f)
        {
            reason = "Холодильнику туш не хватает мощности после клавдиевого контура.";
            harpoonLastMessage = reason;
            return false;
        }

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

    public bool TrySpendHarpoonUpkeep(float deltaSeconds, out string reason)
    {
        reason = "";
        float cost = Mathf.Max(0f, harpoonWeaponCostPerMinute) * Mathf.Max(0f, deltaSeconds) / 60f;
        if (cost <= 0f) return true;

        MetaGameState meta = ResolveMetaGameState();
        if (meta == null || meta.progress == null)
        {
            reason = "Гарпун ждёт MetaGameState для списания оружия.";
            harpoonLastMessage = reason;
            return false;
        }

        string resourceId = string.IsNullOrWhiteSpace(weaponResourceId) ? "weapon" : weaponResourceId;
        bool spent = meta.TrySpendFractionalShipCargoFromRuntime(resourceId, cost, ref harpoonWeaponSpendBufferKg, out reason);
        if (!spent)
        {
            harpoonLastMessage = "Гарпун сорвался: " + reason;
        }

        return spent;
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
                ClearLeviathanHuntTetherHold();
                routeEnabled = false;
                routeWasEnabled = false;
                positionHold = true;
                positionHoldWasEnabled = true;
                targetHoldPosition = transform.position;
                targetSpeedMS = 0f;
                thrustInput = 0f;
                sideInput = 0f;
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
            Leviathan target = tether.target;
            if (!leviathanHuntHasTetherHoldPosition)
            {
                leviathanHuntTetherHoldPosition = transform.position;
                leviathanHuntCapturePosition = transform.position;
                leviathanHuntHasTetherHoldPosition = true;
                leviathanHuntHasCapturePosition = true;

                Vector3 initialDirection = target != null
                    ? FlattenHorizontal(transform.position - target.transform.position)
                    : FlattenHorizontal(transform.forward);
                if (initialDirection.sqrMagnitude < 0.04f) initialDirection = FlattenHorizontal(transform.forward);
                if (initialDirection.sqrMagnitude < 0.04f) initialDirection = Vector3.forward;
                leviathanHuntTetherDirection = initialDirection.normalized;
            }

            routeEnabled = false;
            routeWasEnabled = false;
            positionHold = true;
            positionHoldWasEnabled = true;
            altitudeHold = true;
            cruiseControl = true;
            headingHold = true;

            if (target != null && !target.CanBeClaimed)
            {
                Vector3 anchor = leviathanHuntHasCapturePosition ? leviathanHuntCapturePosition : transform.position;
                Vector3 sideDirection = leviathanHuntTetherDirection;
                if (sideDirection.sqrMagnitude < 0.04f)
                {
                    sideDirection = FlattenHorizontal(transform.position - target.transform.position);
                }

                if (sideDirection.sqrMagnitude < 0.04f)
                {
                    sideDirection = FlattenHorizontal(transform.forward);
                }

                if (sideDirection.sqrMagnitude < 0.04f)
                {
                    sideDirection = Vector3.forward;
                }

                sideDirection.Normalize();

                Vector3 targetToAnchor = FlattenHorizontal(anchor - target.transform.position);
                if (targetToAnchor.sqrMagnitude > 25f)
                {
                    float turnToAnchor = Mathf.Clamp01(Time.fixedDeltaTime * 0.28f);
                    Vector3 blended = Vector3.Slerp(sideDirection, targetToAnchor.normalized, turnToAnchor);
                    if (blended.sqrMagnitude > 0.04f)
                    {
                        sideDirection = blended.normalized;
                    }
                }

                leviathanHuntTetherDirection = sideDirection;
                targetHeading = HeadingFromVector(sideDirection);

                float ropeLength = Mathf.Max(1f, tether.ropeLengthMeters);
                float desiredDistance = ropeLength * 1.12f;
                Vector3 desiredHoldPosition = target.transform.position + sideDirection * desiredDistance;
                Vector3 offsetFromCapture = FlattenHorizontal(desiredHoldPosition - anchor);
                float maxDrift = Mathf.Max(desiredDistance, leviathanHuntMaxTetherDriftMeters);
                if (offsetFromCapture.magnitude > maxDrift)
                {
                    desiredHoldPosition = anchor + offsetFromCapture.normalized * maxDrift;
                }

                desiredHoldPosition.y = Mathf.Max(leviathanHuntMinimumAltitudeMeters, Mathf.Lerp(transform.position.y, target.transform.position.y, 0.2f));
                leviathanHuntTetherHoldPosition = desiredHoldPosition;
            }

            leviathanHuntTetherHoldPosition.y = Mathf.Max(leviathanHuntTetherHoldPosition.y, leviathanHuntMinimumAltitudeMeters);
            targetHoldPosition = leviathanHuntTetherHoldPosition;
            targetAltitude = Mathf.Max(leviathanHuntTetherHoldPosition.y, leviathanHuntMinimumAltitudeMeters);

            if (target != null && target.CanBeClaimed && tether.TargetInCollectionRadius)
            {
                TryClaimHarpoonedLeviathan(out string claimMessage);
                ClearLeviathanHuntTetherHold();
                leviathanHuntAutopilotMessage = claimMessage;
                return;
            }

            float drift = Vector3.Distance(FlattenHorizontal(transform.position), FlattenHorizontal(leviathanHuntTetherHoldPosition));
            float captureDrift = leviathanHuntHasCapturePosition
                ? Vector3.Distance(FlattenHorizontal(transform.position), FlattenHorizontal(leviathanHuntCapturePosition))
                : 0f;
            float driftLimit = Mathf.Max(tether.ropeLengthMeters * 1.12f, leviathanHuntMaxTetherDriftMeters);
            leviathanHuntAutopilotMessage = $"{tether.lastMessage} Держу трос натянутым, снос от расчетной точки {drift:F1} м, от точки захвата {captureDrift:F0}/{driftLimit:F0} м.";
            return;
        }

        ClearLeviathanHuntTetherHold();

        if (!string.IsNullOrWhiteSpace(leviathanHuntIgnoredTargetId) && Time.time >= leviathanHuntIgnoreUntilTime)
        {
            leviathanHuntIgnoredTargetId = "";
        }

        string ignoredTarget = !string.IsNullOrWhiteSpace(leviathanHuntIgnoredTargetId) && Time.time < leviathanHuntIgnoreUntilTime
            ? leviathanHuntIgnoredTargetId
            : "";
        float maxAutoTargetMassKg = Mathf.Max(0f, leviathanHuntMaxTargetMassKg);
        Leviathan nearest = FindNearestHarpoonTarget(Mathf.Max(0f, leviathanHuntSearchRangeMeters), ignoredTarget, maxAutoTargetMassKg, true);
        if (nearest == null)
        {
            routeEnabled = false;
            routeWasEnabled = false;
            positionHold = true;
            positionHoldWasEnabled = true;
            targetHoldPosition = transform.position;
            altitudeHold = true;
            cruiseControl = true;
            headingHold = true;
            targetAltitude = transform.position.y;
            targetSpeedMS = 0f;
            thrustInput = 0f;
            sideInput = 0f;
            turnInput = 0f;

            if (!string.IsNullOrWhiteSpace(ignoredTarget))
            {
                float wait = Mathf.Max(0f, leviathanHuntIgnoreUntilTime - Time.time);
                leviathanHuntAutopilotMessage = $"Цель только что оборвала трос. Жду {wait:F1} сек или ищу другую цель.";
            }
            else
            {
                leviathanHuntAutopilotMessage = maxAutoTargetMassKg > 0f
                    ? $"В радиусе поиска нет живого левиафана не тяжелее {maxAutoTargetMassKg:0} кг."
                    : "В радиусе поиска нет живого левиафана.";
            }

            return;
        }

        float distance = Vector3.Distance(transform.position, nearest.transform.position);
        float engageDistance = Mathf.Min(Mathf.Max(1f, harpoonRangeMeters), Mathf.Max(1f, leviathanHuntEngageRangeMeters));
        if (distance <= engageDistance)
        {
            routeEnabled = false;
            routeWasEnabled = false;
            positionHold = true;
            positionHoldWasEnabled = true;
            targetHoldPosition = transform.position;
            altitudeHold = true;
            targetAltitude = Mathf.Max(transform.position.y, leviathanHuntMinimumAltitudeMeters);
            cruiseControl = true;
            headingHold = true;

            if (Time.time < nextLeviathanHuntShotTime)
            {
                float wait = Mathf.Max(0f, nextLeviathanHuntShotTime - Time.time);
                leviathanHuntAutopilotMessage = $"Гарпун перезаряжается: {wait:F1} сек. Цель {nearest.displayName} в {distance:F0} м.";
                return;
            }

            TryFireHarpoonAtNearestLeviathan(out string fireMessage, maxAutoTargetMassKg, true);
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
        sideInput = 0f;
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
            PrimeClaudiumLiftForHover(rb.mass);
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
        sideInput = 0f;
        turnInput = 0f;
        liftInput = 0f;
        PerformAutoStabilization();
    }

    void FixedUpdate()
    {
        EnforceYawOnlyRotation(false);
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
        UpdateSurvey();

        // --- АЭРОДИНАМИКА (с учетом ветра) ---
        Vector3 effectiveWindVelocity = GetEffectiveWindVelocity();
        Vector3 airVelocity = rb.linearVelocity - effectiveWindVelocity;
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
        ApplyLateralOmniThrust();

        ApplyGyroTurn();

        // 4. Подавление бокового сноса (Киль сопротивляется воздуху)
        Vector3 localAirVel = transform.InverseTransformDirection(airVelocity);
        Vector3 sideAirVelocity = transform.right * localAirVel.x;
        rb.AddForce(-sideAirVelocity * rb.mass * sideResistance, ForceMode.Force);
        EnforceYawOnlyRotation(false);
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
        rb.AddTorque(Vector3.up * finalTorque, ForceMode.Force);
    }

    private void ApplyPropellerThrust()
    {
        float thrustDirection = Mathf.Sign(thrustInput);
        float propellerEngagement = Mathf.Clamp01(Mathf.Abs(thrustInput));
        float supportPowerKw = CalculateCurrentSupportPowerDrawKw();
        float residualPowerKw = Mathf.Max(0f, engineGeneratedPowerKw - supportPowerKw);

        propellerInputPowerKw = Mathf.Min(residualPowerKw, CalculatePropellerPowerRequestKw(propellerEngagement, supportPowerKw));
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
        Vector3 horizontalAirVelocity = Vector3.ProjectOnPlane(rb.linearVelocity - GetEffectiveWindVelocity(), Vector3.up);
        float signedAirspeedWithThrust = Vector3.Dot(horizontalAirVelocity, thrustAxis) * thrustDirection;
        float speedFactor = signedAirspeedWithThrust >= propellerMaxSpeedMS ? 0f : 1f;
        float maxThrustKgf = propellerMaxThrustKgf * propellerEngagement * speedFactor;
        float powerLimitedThrustN = propellerUsefulPowerKw * 1000f / Mathf.Max(1f, Mathf.Abs(signedAirspeedWithThrust));
        float powerLimitedThrustKgf = powerLimitedThrustN / 9.81f;
        float thrustKgf = Mathf.Min(maxThrustKgf, powerLimitedThrustKgf);

        propellerThrustKgf = thrustKgf * thrustDirection;
        rb.AddForce(thrustAxis * (propellerThrustKgf * 9.81f), ForceMode.Force);
    }

    private float CalculateCurrentSupportPowerDrawKw()
    {
        float modulePowerKw = Mathf.Max(0f, refrigeratedHoldPowerDrawActualKw);
        if (gasHarvesterEnabled && gasHarvesterVolumeM3PerSecond > 0f && gasHarvesterPowerDrawKw > 0f)
        {
            modulePowerKw += Mathf.Max(gasHarvesterPowerDrawActualKw, gasHarvesterPowerDrawKw);
        }

        return Mathf.Clamp(
            Mathf.Max(0f, claudiumPowerDrawKw) + modulePowerKw,
            0f,
            EnginePowerCapacityKw);
    }

    private float CalculatePropellerPowerRequestKw(float propellerEngagement, float supportPowerKw)
    {
        float availablePowerKw = Mathf.Max(0f, EnginePowerCapacityKw - Mathf.Max(0f, supportPowerKw));
        return availablePowerKw * Mathf.Clamp01(propellerEngagement);
    }

    private void ApplyLateralOmniThrust()
    {
        float input = Mathf.Clamp(sideInput, -1f, 1f);
        float forceN = Mathf.Abs(input) * Mathf.Max(0f, lateralOmniThrustKgf) * 9.81f;
        if (forceN <= 0.001f)
        {
            return;
        }

        Vector3 sideAxis = Vector3.ProjectOnPlane(transform.right, Vector3.up);
        if (sideAxis.sqrMagnitude < 0.001f)
        {
            sideAxis = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * Vector3.right;
        }

        rb.AddForce(sideAxis.normalized * (forceN * Mathf.Sign(input)), ForceMode.Force);
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
            sideInput = 0f;
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
            enginePowerLever = Mathf.Lerp(enginePowerLever, CalculateEnginePowerLeverForPropellerEngagement(Mathf.Abs(desiredOutput)), response);
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
        sideInput = 0f;
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
        Vector3 requiredAirVelocity = desiredGroundVelocity - FlattenHorizontal(GetEffectiveWindVelocity());
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
        float horizontalAirspeed = FlattenHorizontal(rb.linearVelocity - GetEffectiveWindVelocity()).magnitude;
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
            desiredSpeed = Mathf.Clamp(speed, 0f, maxHoldSpeed);
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
                Vector3 windHorizontal = FlattenHorizontal(GetEffectiveWindVelocity());
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

    private Vector3 GetEffectiveWindVelocity()
    {
        return windVelocity * CurrentWindAerodynamicFactor;
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
        float requestKw = 0f;
        refrigeratedHoldPowerDrawActualKw = 0f;

        float refrigeratorKw = GetRefrigeratedHoldPowerRequestKw();
        if (refrigeratorKw > 0f && claudiumPowerKw + refrigeratorKw <= enginePowerKwAt100 + 0.001f)
        {
            refrigeratedHoldPowerDrawActualKw = refrigeratorKw;
            requestKw += refrigeratorKw;
        }

        if (gasHarvesterEnabled && gasHarvesterVolumeM3PerSecond > 0f && gasHarvesterPowerDrawKw > 0f &&
            claudiumPowerKw + requestKw + gasHarvesterPowerDrawKw <= enginePowerKwAt100 + 0.001f)
        {
            requestKw += gasHarvesterPowerDrawKw;
        }

        return requestKw;
    }

    private float GetRefrigeratedHoldPowerRequestKw()
    {
        return refrigeratedHoldEnabled && refrigeratedHoldCapacityLiters > 0f
            ? Mathf.Max(0f, refrigeratedHoldPowerDrawKw)
            : 0f;
    }

    private float CalculateClaudiumPowerKwForLift(float liftKg)
    {
        if (claudiumLiftEfficiency <= 0f) return 0f;
        return Mathf.Max(0f, liftKg) / claudiumLiftEfficiency;
    }

    private void UpdateEnginePowerOutput(float minimumPowerKw)
    {
        float maxLever = EnginePowerLeverLimit;
        engineMinimumPowerLever = enginePowerKwAt100 > 0f ? Mathf.Clamp(minimumPowerKw / enginePowerKwAt100, 0f, maxLever) : 0f;
        float propellerLever = Mathf.Clamp01(Mathf.Abs(thrustInput)) * Mathf.Max(0f, maxLever - engineMinimumPowerLever);
        float requestedLever = engineMinimumPowerLever + propellerLever;
        enginePowerLever = Mathf.Clamp(Mathf.Max(enginePowerLever, requestedLever), engineMinimumPowerLever, maxLever);

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
                if (targetAltitude <= 0f)
                {
                    targetAltitude = rb.position.y;
                }

                wasAltitudeHold = true;
                altIntegral = 0f;
                if (Mathf.Abs(targetAltitude - rb.position.y) <= Mathf.Max(2f, altDriftTolerance * 2f) &&
                    Mathf.Abs(rb.linearVelocity.y) <= 0.5f)
                {
                    PrimeClaudiumLiftForHover(mass);
                }
            }

            targetAltitude = Mathf.Max(0f, targetAltitude);

            float altitudeError = targetAltitude - rb.position.y;
            float driftTolerance = Mathf.Max(0f, altDriftTolerance);
            float absError = Mathf.Abs(altitudeError);
            float correctedError = absError > driftTolerance
                ? altitudeError - Mathf.Sign(altitudeError) * driftTolerance
                : 0f;

            if (Mathf.Abs(correctedError) > 0.001f)
            {
                altIntegral += correctedError * Time.fixedDeltaTime * 0.05f;
                altIntegral = Mathf.Clamp(altIntegral, -0.25f, 0.25f);
            }
            else
            {
                altIntegral = Mathf.MoveTowards(altIntegral, 0f, Time.fixedDeltaTime * 0.25f);
            }

            float targetVerticalSpeed = 0f;
            if (Mathf.Abs(correctedError) > 0.001f)
            {
                targetVerticalSpeed = correctedError * Mathf.Max(0f, altStiffness);
                targetVerticalSpeed = Mathf.Clamp(targetVerticalSpeed, -maxAutoVerticalSpeed, maxAutoVerticalSpeed);
            }

            float velocityError = targetVerticalSpeed - rb.linearVelocity.y;
            float desiredAcceleration = velocityError * Mathf.Max(0f, altDamping) + altIntegral;
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
        return Mathf.Max(0f, trimMass);
    }

    private void PrimeClaudiumLiftForHover(float mass)
    {
        if (claudiumMaxLiftKg <= 0f || enginePowerKwAt100 <= 0f || claudiumLiftEfficiency <= 0f || claudiumStock <= 0f)
        {
            return;
        }

        float hoverLiftKg = Mathf.Max(0f, mass);
        float powerLimitedLiftKg = EnginePowerCapacityKw * claudiumLiftEfficiency;
        hoverLiftKg = Mathf.Min(hoverLiftKg, claudiumMaxLiftKg, Mathf.Max(0f, powerLimitedLiftKg));

        claudiumCurrentLiftN = hoverLiftKg * 9.81f;
        claudiumRequestedLiftKg = hoverLiftKg;
        claudiumPowerDrawKw = claudiumLiftEfficiency > 0f ? CalculateClaudiumPowerKwForLift(hoverLiftKg) : 0f;
        claudiumPowerDrawWatts = claudiumPowerDrawKw * 1000f;
        activeLiftForce = claudiumCurrentLiftN;
    }

    void UpdateClaudium()
    {
        UpdateSimplifiedClaudium();
    }

    private void UpdateSurvey()
    {
        surveyTickAccumulator += Time.fixedDeltaTime;
        if (surveyTickAccumulator < 1f) return;

        float deltaSeconds = surveyTickAccumulator;
        surveyTickAccumulator = 0f;

        MetaGameState meta = ResolveMetaGameState();
        if (meta == null || meta.progress == null || meta.WorldConfig == null)
        {
            surveyLastMessage = "Разведка ждет MetaGameState.";
            return;
        }

        float radius = Mathf.Max(0f, observationRadiusMeters, baseObservationRadiusMeters);
        float speed = Mathf.Max(0.01f, observationFactsAtHalfRadiusPerSecond);
        int changed = SurveySystem.ObserveAndExtractWorldFromPoint(
            meta.WorldConfig,
            meta.progress,
            transform.position,
            radius,
            speed,
            deltaSeconds,
            meta.CurrentProcessUtcNow.Ticks,
            meta.progress.shipCargo,
            observationRockInfoEfficiency,
            observationCloudInfoEfficiency,
            observationLeviathanInfoEfficiency,
            surveyPaperToInfoEfficiency);

        bool hasScientificGear = observationRockInfoEfficiency > 0f
            || observationCloudInfoEfficiency > 0f
            || observationLeviathanInfoEfficiency > 0f;
        surveyLastMessage = changed > 0
            ? "Разведка обновила сведения или научную информацию."
            : hasScientificGear
                ? "Разведка активна. Для научной информации нужна бумага и объект в радиусе."
                : "Пассивная разведка активна: собирает координаты и сведения в базовом радиусе.";
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

        float refrigeratorKw = refrigeratedHoldPowerDrawActualKw;
        if (claudiumPowerDrawKw + refrigeratorKw + gasHarvesterPowerDrawKw > enginePowerKwAt100 + 0.001f)
        {
            gasHarvesterLastMessage = "Харвестер выключен: после клавдиевого контура не хватает мощности до лимита 100%.";
            ResetGasHarvesterCycle();
            return;
        }

        if (engineGeneratedPowerKw + 0.001f < claudiumPowerDrawKw + refrigeratorKw + gasHarvesterPowerDrawKw || !engineHasFuel)
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
            string outputItemId = gasHarvesterWaterOnly ? "water" : endCloud.condensateItemId;
            if (meta.TryAddShipCargoFromRuntime(outputItemId, wholeKg, out string cargoError))
            {
                gasHarvesterBufferKg -= wholeKg;
                gasHarvesterLastMessage = gasHarvesterWaterOnly
                    ? $"Добыто {wholeKg} кг воды грубой фильтрацией. Остаток облака {endCloud.remainingVolumeLiters:F1} кг."
                    : $"Добыто {wholeKg} кг: {endCloud.condensateItemId}. Остаток облака {endCloud.remainingVolumeLiters:F1} кг.";
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
        enginePowerLever = Mathf.Clamp(enginePowerLever, 0f, EnginePowerLeverLimit);
        propellerPitch = Mathf.Clamp(thrustInput, -1f, 1f);
    }

    private void OnDrawGizmos()
    {
        // Визуализация ветра
        Vector3 effectiveWindVelocity = GetEffectiveWindVelocity();
        if (effectiveWindVelocity.sqrMagnitude > 0.1f)
        {
            Gizmos.color = new Color(1f, 0f, 1f, 0.7f); // Пурпурный
            Vector3 startPos = transform.position + Vector3.up * 10f; // Чуть выше корабля
            Gizmos.DrawLine(startPos, startPos + effectiveWindVelocity);
            Gizmos.DrawWireSphere(startPos + effectiveWindVelocity, 1f); // Наконечник
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

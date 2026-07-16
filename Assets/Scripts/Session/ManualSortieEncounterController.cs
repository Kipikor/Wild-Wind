using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ManualSortieEncounterController : MonoBehaviour
{
    private const string ControllerObjectName = "Manual Sortie Encounter Controller";
    private const string EncounterRootName = "Manual Sortie Encounter";
    private const int MinimumPlayableResourceNodeAmount = 48;

    [InspectorName("Meta State")]
    public MetaGameState metaGameState;
    [InspectorName("Target Ship")]
    public ShipPhysics targetShip;
    [InspectorName("Max Resource Nodes")]
    public int maxResourceNodeCount = 4;
    [InspectorName("Max Enemy Count")]
    public int maxEnemyCount = 5;
    [InspectorName("Resource Ring Radius, m")]
    public float resourceRingRadiusMeters = 185f;
    [InspectorName("Enemy Ring Radius, m")]
    public float enemyRingRadiusMeters = 320f;

    private string activeSortieKey = "";
    private Transform encounterRoot;

    public int ActiveResourceNodeCount => encounterRoot != null
        ? encounterRoot.GetComponentsInChildren<ManualSortieResourceDeposit>().Length
        : 0;

    public int ActiveEnemyCount => encounterRoot != null
        ? encounterRoot.GetComponentsInChildren<ManualSortieEnemyDrone>().Length
        : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallManualSortieEncounterBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureControllerForGameplayScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureControllerForGameplayScene();
    }

    private static void EnsureControllerForGameplayScene()
    {
        if (!WildWindSessionFlow.IsGameplaySceneLoaded())
        {
            return;
        }

        if (FindFirstObjectByType<ManualSortieEncounterController>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject(ControllerObjectName);
        controllerObject.AddComponent<ManualSortieEncounterController>();
    }

    private void Update()
    {
        RefreshNow();
    }

    public void RefreshNow()
    {
        MetaGameState meta = ResolveMeta();
        if (meta == null || meta.CurrentMode != GameSessionMode.Flight || !meta.HasActiveSortie)
        {
            ClearEncounter();
            return;
        }

        SortieSessionState sortie = meta.ActiveSortie;
        SortieZoneDefinition zone = sortie != null ? sortie.zone : null;
        if (zone == null)
        {
            ClearEncounter();
            return;
        }

        zone.Normalize();
        if (!ShouldRunEncounter(zone))
        {
            ClearEncounter();
            return;
        }

        string key = sortie.startedUtcTicks + ":" + zone.sortieId + ":" + zone.missionSeed;
        if (activeSortieKey != key)
        {
            SpawnEncounter(meta, sortie, key);
        }
    }

    private static bool ShouldRunEncounter(SortieZoneDefinition zone)
    {
        return zone != null
            && zone.sortieId == SessionExtractionConstants.QuickAdaptiveManualSortieId;
    }

    private MetaGameState ResolveMeta()
    {
        if (metaGameState == null)
        {
            metaGameState = FindFirstObjectByType<MetaGameState>();
        }

        return metaGameState;
    }

    private ShipPhysics ResolveShip()
    {
        MetaGameState meta = ResolveMeta();
        ShipPhysics loaderShip = meta != null && meta.shipLoader != null
            ? meta.shipLoader.targetShip
            : null;
        if (IsUsableShip(loaderShip))
        {
            targetShip = loaderShip;
            return targetShip;
        }

        if (!IsUsableShip(targetShip))
        {
            targetShip = FindFirstObjectByType<ShipPhysics>();
        }

        return targetShip;
    }

    private static bool IsUsableShip(ShipPhysics ship)
    {
        return ship != null && ship.gameObject.activeInHierarchy;
    }

    private void SpawnEncounter(MetaGameState meta, SortieSessionState sortie, string key)
    {
        ClearEncounter();
        if (meta == null || sortie == null || sortie.zone == null)
        {
            return;
        }

        SortieZoneDefinition zone = sortie.zone;
        zone.Normalize();
        activeSortieKey = key ?? "";

        GameObject rootObject = new GameObject(EncounterRootName);
        encounterRoot = rootObject.transform;

        System.Random random = new System.Random(StableSeed(activeSortieKey));
        ShipTreeEntryConfig sourceShip = ResolveSourceShip(meta, zone);
        ShipPhysics ship = ResolveShip();

        SpawnResourceDeposits(zone, random);
        SpawnEnemyDrones(zone, sourceShip, ship, random);
    }

    private void SpawnResourceDeposits(SortieZoneDefinition zone, System.Random random)
    {
        if (zone == null || zone.payloadRewards == null || zone.payloadRewards.Count == 0)
        {
            return;
        }

        int payloadCount = zone.payloadRewards.Count;
        int count = Mathf.Max(1, maxResourceNodeCount);
        float radius = Mathf.Clamp(resourceRingRadiusMeters, 40f, Mathf.Max(40f, zone.radiusMeters * 0.45f));
        float baseAngle = NextFloat(random, 0f, Mathf.PI * 2f);
        float altitude = Mathf.Max(zone.entryPosition.y + 35f, zone.stormFloorY + 95f);

        for (int i = 0; i < count; i++)
        {
            SortiePayloadRewardLine payload = CreatePlayablePayload(zone.payloadRewards[i % payloadCount]);
            if (payload == null)
            {
                continue;
            }

            payload.Normalize();
            if (string.IsNullOrWhiteSpace(payload.itemId) || payload.amount <= 0)
            {
                continue;
            }

            float angle = baseAngle + i * Mathf.PI * 2f / count + NextFloat(random, -0.16f, 0.16f);
            Vector3 position = new Vector3(
                zone.centerPosition.x + Mathf.Cos(angle) * radius,
                altitude + NextFloat(random, -18f, 18f),
                zone.centerPosition.z + Mathf.Sin(angle) * radius);
            SpawnResourceDeposit(zone, payload, position, i, random);
        }
    }

    private static SortiePayloadRewardLine CreatePlayablePayload(SortiePayloadRewardLine source)
    {
        if (source == null)
        {
            return null;
        }

        SortiePayloadRewardLine payload = new SortiePayloadRewardLine
        {
            itemId = source.itemId,
            displayNameRu = source.displayNameRu,
            amount = Mathf.Max(source.amount, MinimumPlayableResourceNodeAmount),
            color = source.color
        };
        payload.Normalize();
        return payload;
    }

    private void SpawnResourceDeposit(
        SortieZoneDefinition zone,
        SortiePayloadRewardLine payload,
        Vector3 position,
        int index,
        System.Random random)
    {
        GameObject depositObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        depositObject.name = payload.itemId + " manual deposit " + (index + 1).ToString();
        depositObject.transform.SetParent(encounterRoot, true);
        depositObject.transform.position = position;
        float scale = NextFloat(random, 13f, 21f);
        depositObject.transform.localScale = new Vector3(scale, scale * NextFloat(random, 0.62f, 0.82f), scale * NextFloat(random, 0.84f, 1.16f));

        Renderer renderer = depositObject.GetComponent<Renderer>();
        MiningFragment.ApplyRendererColor(renderer, payload.color);

        DamageableShip damageable = depositObject.AddComponent<DamageableShip>();
        damageable.shipId = "manual_resource_" + payload.itemId + "_" + index.ToString();
        damageable.displayNameRu = payload.displayNameRu;
        damageable.maxStructureHp = Mathf.Clamp(80f + payload.amount * 1.6f, 90f, 460f);
        damageable.ResetDamageState();

        ArmorZone armor = depositObject.AddComponent<ArmorZone>();
        armor.zoneId = "resource_body";
        armor.displayNameRu = "Resource body";
        armor.armorMm = 0f;
        armor.SetResistances(DamageResistanceUtility.DefaultOreResistances);
        armor.ricochetAngleDeg = 88f;
        armor.structureDamageMultiplier = 1f;
        armor.receiveRamDamage = false;

        ManualSortieResourceDeposit deposit = depositObject.AddComponent<ManualSortieResourceDeposit>();
        deposit.Initialize(zone, payload, StableSeed(activeSortieKey + ":payload:" + index.ToString()));
    }

    private void SpawnEnemyDrones(
        SortieZoneDefinition zone,
        ShipTreeEntryConfig sourceShip,
        ShipPhysics ship,
        System.Random random)
    {
        if (zone == null)
        {
            return;
        }

        int rank = Mathf.Clamp(sourceShip != null && sourceShip.rank > 0 ? sourceShip.rank : sourceShip != null ? sourceShip.treeTier : 1, 1, 10);
        int warfare = Mathf.Clamp(sourceShip != null && sourceShip.warfareRating >= 0 ? sourceShip.warfareRating : sourceShip != null ? sourceShip.firepower : 15, 0, 100);
        bool combatLike = string.Equals(zone.primaryActivity, "combat", System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(zone.primaryActivity, "salvage", System.StringComparison.OrdinalIgnoreCase);
        int desiredCount = combatLike
            ? 2 + warfare / 34 + rank / 3
            : 2 + warfare / 46 + rank / 5;
        int count = Mathf.Clamp(desiredCount, 1, Mathf.Max(1, maxEnemyCount));

        float radius = Mathf.Clamp(enemyRingRadiusMeters, 90f, Mathf.Max(90f, zone.radiusMeters * 0.62f));
        float baseAngle = NextFloat(random, 0f, Mathf.PI * 2f);
        float altitude = Mathf.Max(zone.entryPosition.y + 25f, zone.stormFloorY + 110f);

        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + i * Mathf.PI * 2f / count + NextFloat(random, -0.28f, 0.28f);
            Vector3 position = new Vector3(
                zone.centerPosition.x + Mathf.Cos(angle) * radius,
                altitude + NextFloat(random, -28f, 28f),
                zone.centerPosition.z + Mathf.Sin(angle) * radius);
            SpawnEnemyDrone(zone, ship, rank, warfare, position, i, random);
        }
    }

    private void SpawnEnemyDrone(
        SortieZoneDefinition zone,
        ShipPhysics ship,
        int rank,
        int warfare,
        Vector3 position,
        int index,
        System.Random random)
    {
        GameObject droneObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        droneObject.name = "Manual Sortie Drone " + (index + 1).ToString();
        droneObject.transform.SetParent(encounterRoot, true);
        droneObject.transform.position = position;
        droneObject.transform.localScale = new Vector3(7f, 4.5f, 7f);

        Renderer renderer = droneObject.GetComponent<Renderer>();
        MiningFragment.ApplyRendererColor(renderer, new Color(0.70f, 0.27f, 0.22f, 1f));

        Rigidbody body = droneObject.AddComponent<Rigidbody>();
        body.mass = Mathf.Max(250f, 420f + rank * 55f);
        body.useGravity = false;
        body.linearDamping = 0.45f;
        body.angularDamping = 1.6f;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        DamageableShip damageable = droneObject.AddComponent<DamageableShip>();
        damageable.shipId = "manual_drone_" + index.ToString();
        damageable.displayNameRu = "Sortie drone";
        damageable.maxStructureHp = Mathf.Clamp(110f + rank * 25f + warfare * 0.9f, 120f, 360f);
        damageable.ResetDamageState();

        ArmorZone armor = droneObject.AddComponent<ArmorZone>();
        armor.zoneId = "drone_hull";
        armor.displayNameRu = "Drone hull";
        armor.armorMm = Mathf.Clamp(5f + rank * 1.5f, 5f, 24f);
        armor.SetResistances(DamageResistanceUtility.DefaultAutomatonResistances);
        armor.ricochetAngleDeg = 82f;
        armor.structureDamageMultiplier = 1f;
        armor.receiveRamDamage = true;

        ManualSortieEnemyDrone drone = droneObject.AddComponent<ManualSortieEnemyDrone>();
        drone.Initialize(zone, ship, rank, warfare, StableSeed(activeSortieKey + ":enemy:" + index.ToString()));
    }

    private static ShipTreeEntryConfig ResolveSourceShip(MetaGameState meta, SortieZoneDefinition zone)
    {
        if (meta == null || meta.SessionConfig == null || zone == null || string.IsNullOrWhiteSpace(zone.sourceShipId))
        {
            return null;
        }

        return meta.SessionConfig.GetShipTreeEntry(zone.sourceShipId);
    }

    private void ClearEncounter()
    {
        activeSortieKey = "";
        if (encounterRoot == null)
        {
            return;
        }

        Destroy(encounterRoot.gameObject);
        encounterRoot = null;
    }

    private static int StableSeed(string value)
    {
        unchecked
        {
            int hash = 17;
            string text = value ?? "";
            for (int i = 0; i < text.Length; i++)
            {
                hash = hash * 31 + text[i];
            }

            return hash;
        }
    }

    private static float NextFloat(System.Random random, float min, float max)
    {
        random ??= new System.Random(1);
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}

public sealed class ManualSortieResourceDeposit : MonoBehaviour
{
    private SortieZoneDefinition zone;
    private string itemId = "";
    private string displayName = "";
    private Color color = Color.white;
    private int remainingAmount;
    private int chunkMin;
    private int chunkMax;
    private float baseShedIntervalSeconds;
    private float acceleratedShedIntervalSeconds;
    private float acceleratedUntil;
    private float nextShedTime;
    private float fallSpeedMS;
    private int lastHitCount;
    private bool shattered;
    private System.Random random;
    private DamageableShip damageable;

    public void Initialize(SortieZoneDefinition definition, SortiePayloadRewardLine payload, int seed)
    {
        zone = definition != null ? definition.Clone() : null;
        if (zone != null)
        {
            zone.Normalize();
        }

        payload ??= new SortiePayloadRewardLine();
        payload.Normalize();
        itemId = payload.itemId;
        displayName = payload.displayNameRu;
        color = payload.color;
        remainingAmount = Mathf.Max(0, payload.amount);
        chunkMax = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(1, remainingAmount) / 14f), 1, 36);
        chunkMin = Mathf.Max(1, Mathf.Min(chunkMax, Mathf.FloorToInt(chunkMax * 0.45f)));
        baseShedIntervalSeconds = zone != null ? Mathf.Max(0.35f, zone.starterResourceShedIntervalSeconds * 1.35f) : 2.4f;
        acceleratedShedIntervalSeconds = Mathf.Clamp(baseShedIntervalSeconds * 0.28f, 0.16f, 0.75f);
        fallSpeedMS = ResolveFallSpeed(itemId);
        random = new System.Random(seed);
        damageable = GetComponent<DamageableShip>();
        lastHitCount = damageable != null ? damageable.hitCount : 0;
        nextShedTime = Time.time + NextFloat(0.3f, 1.1f);
    }

    private void Update()
    {
        if (zone == null || string.IsNullOrWhiteSpace(itemId) || remainingAmount <= 0)
        {
            Destroy(gameObject);
            return;
        }

        if (damageable == null)
        {
            damageable = GetComponent<DamageableShip>();
        }

        if (damageable != null && damageable.hitCount > lastHitCount)
        {
            int newHits = damageable.hitCount - lastHitCount;
            lastHitCount = damageable.hitCount;
            acceleratedUntil = Mathf.Max(acceleratedUntil, Time.time + 7.5f);
            for (int i = 0; i < newHits; i++)
            {
                SpawnFragment(Mathf.Max(chunkMin, chunkMax));
            }
        }

        if (damageable != null && damageable.structureHp <= 0.001f)
        {
            ShatterDeposit();
            return;
        }

        if (Time.time >= nextShedTime)
        {
            SpawnFragment();
            float interval = Time.time < acceleratedUntil ? acceleratedShedIntervalSeconds : baseShedIntervalSeconds;
            nextShedTime = Time.time + interval;
        }
    }

    private void ShatterDeposit()
    {
        if (shattered)
        {
            return;
        }

        shattered = true;
        int burstCount = Mathf.Clamp(Mathf.CeilToInt(remainingAmount / (float)Mathf.Max(1, chunkMax)), 1, 12);
        for (int i = 0; i < burstCount && remainingAmount > 0; i++)
        {
            int requested = Mathf.CeilToInt(remainingAmount / (float)(burstCount - i));
            SpawnFragment(Mathf.Max(chunkMin, requested));
        }

        Destroy(gameObject);
    }

    private void SpawnFragment()
    {
        SpawnFragment(RandomRange(chunkMin, chunkMax + 1));
    }

    private void SpawnFragment(int requestedAmount)
    {
        if (remainingAmount <= 0 || !MiningFragment.CanSpawnMore)
        {
            return;
        }

        int amount = Mathf.Clamp(requestedAmount, 1, remainingAmount);
        remainingAmount -= amount;

        GameObject fragmentObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fragmentObject.name = itemId + "_manual_fragment";
        fragmentObject.transform.position = transform.position
            + Vector3.down * NextFloat(4f, 8f)
            + RandomUnitSphere() * NextFloat(1.5f, 5.5f);
        fragmentObject.transform.localScale = Vector3.one * Mathf.Lerp(0.48f, 1.35f, Mathf.Clamp01(amount / (float)Mathf.Max(1, chunkMax)));

        Collider collider = fragmentObject.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        MiningFragment fragment = fragmentObject.AddComponent<MiningFragment>();
        fragment.Initialize(itemId, amount, fallSpeedMS, zone.stormFloorY, color);
    }

    private static float ResolveFallSpeed(string resourceId)
    {
        string normalized = (resourceId ?? "").ToLowerInvariant();
        if (normalized.Contains("gas") || normalized.Contains("mist") || normalized.Contains("condensate"))
        {
            return 2.15f;
        }

        if (normalized.Contains("ore") || normalized.Contains("shale") || normalized.Contains("stone"))
        {
            return 4.2f;
        }

        return 3.2f;
    }

    private int RandomRange(int minInclusive, int maxExclusive)
    {
        if (random == null)
        {
            random = new System.Random(1);
        }

        return random.Next(minInclusive, Mathf.Max(minInclusive + 1, maxExclusive));
    }

    private float NextFloat(float min, float max)
    {
        if (random == null)
        {
            random = new System.Random(1);
        }

        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }

    private Vector3 RandomUnitSphere()
    {
        return new Vector3(NextFloat(-1f, 1f), NextFloat(-1f, 1f), NextFloat(-1f, 1f)).normalized;
    }
}

public sealed class ManualSortieEnemyDrone : MonoBehaviour
{
    private SortieZoneDefinition zone;
    private ShipPhysics targetShip;
    private Rigidbody body;
    private DamageableShip damageable;
    private System.Random random;
    private float moveSpeedMS;
    private float preferredRangeMeters;
    private float weaponRangeMeters;
    private float projectileSpeedMS;
    private float fireIntervalSeconds;
    private float nextFireTime;
    private float altitudeOffset;
    private int warfareRating;

    public void Initialize(SortieZoneDefinition definition, ShipPhysics target, int rank, int warfare, int seed)
    {
        zone = definition != null ? definition.Clone() : null;
        if (zone != null)
        {
            zone.Normalize();
        }

        targetShip = target;
        body = GetComponent<Rigidbody>();
        damageable = GetComponent<DamageableShip>();
        random = new System.Random(seed);
        warfareRating = Mathf.Clamp(warfare, 0, 100);
        moveSpeedMS = Mathf.Clamp(22f + rank * 1.8f + warfareRating * 0.10f, 18f, 46f);
        preferredRangeMeters = Mathf.Clamp(175f + rank * 14f, 150f, 310f);
        weaponRangeMeters = Mathf.Clamp(520f + rank * 35f + warfareRating * 2.5f, 420f, 900f);
        projectileSpeedMS = Mathf.Clamp(165f + rank * 8f + warfareRating * 1.2f, 150f, 310f);
        fireIntervalSeconds = Mathf.Clamp(3.8f - warfareRating * 0.012f, 1.9f, 4.2f);
        altitudeOffset = NextFloat(-18f, 18f);
        nextFireTime = Time.time + NextFloat(1.0f, 3.2f);
    }

    private void Update()
    {
        if (damageable == null)
        {
            damageable = GetComponent<DamageableShip>();
        }

        if (damageable != null && damageable.structureHp <= 0.001f)
        {
            Destroy(gameObject);
            return;
        }

        if (targetShip == null || !targetShip.gameObject.activeInHierarchy)
        {
            targetShip = FindFirstObjectByType<ShipPhysics>();
        }

        if (targetShip == null)
        {
            return;
        }

        MoveDrone(Time.deltaTime);
        TryFireAtTarget();
    }

    private void MoveDrone(float deltaSeconds)
    {
        Vector3 target = targetShip.transform.position + Vector3.up * altitudeOffset;
        Vector3 toTarget = target - transform.position;
        Vector3 flatToTarget = new Vector3(toTarget.x, 0f, toTarget.z);
        float flatDistance = flatToTarget.magnitude;
        Vector3 desiredVelocity = Vector3.zero;

        if (flatDistance > preferredRangeMeters * 1.18f)
        {
            desiredVelocity += flatToTarget.normalized * moveSpeedMS;
        }
        else if (flatDistance < preferredRangeMeters * 0.62f)
        {
            desiredVelocity -= flatToTarget.normalized * (moveSpeedMS * 0.72f);
        }
        else if (flatDistance > 0.001f)
        {
            Vector3 tangent = Vector3.Cross(Vector3.up, flatToTarget.normalized);
            desiredVelocity += tangent * (moveSpeedMS * 0.64f);
        }

        if (zone != null)
        {
            Vector3 centerDelta = new Vector3(transform.position.x - zone.centerPosition.x, 0f, transform.position.z - zone.centerPosition.z);
            float distanceFromCenter = centerDelta.magnitude;
            float leashRadius = Mathf.Max(100f, zone.radiusMeters * 0.78f);
            if (distanceFromCenter > leashRadius)
            {
                desiredVelocity += (-centerDelta.normalized) * moveSpeedMS;
            }
        }

        desiredVelocity.y = Mathf.Clamp(toTarget.y * 0.45f, -moveSpeedMS * 0.55f, moveSpeedMS * 0.55f);

        if (body != null)
        {
            body.linearVelocity = Vector3.Lerp(body.linearVelocity, desiredVelocity, Mathf.Clamp01(deltaSeconds * 1.8f));
        }
        else
        {
            transform.position += desiredVelocity * deltaSeconds;
        }

        Vector3 look = flatToTarget.sqrMagnitude > 0.001f ? flatToTarget.normalized : transform.forward;
        if (look.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(look, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 120f * deltaSeconds);
        }
    }

    private void TryFireAtTarget()
    {
        if (Time.time < nextFireTime || targetShip == null)
        {
            return;
        }

        Vector3 targetPoint = targetShip.transform.position + Vector3.up * 1.5f;
        Vector3 delta = targetPoint - transform.position;
        float distance = delta.magnitude;
        if (distance > weaponRangeMeters || distance <= 0.001f)
        {
            nextFireTime = Time.time + 0.45f;
            return;
        }

        SpawnProjectile(delta.normalized, distance);
        nextFireTime = Time.time + fireIntervalSeconds * NextFloat(0.78f, 1.22f);
    }

    private void SpawnProjectile(Vector3 direction, float distance)
    {
        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = "Sortie Drone Projectile";
        projectileObject.transform.position = transform.position + direction * 5.5f;
        projectileObject.transform.localScale = Vector3.one * 0.22f;

        Renderer renderer = projectileObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(1f, 0.45f, 0.26f, 1f);
        }

        Collider projectileCollider = projectileObject.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
            Collider[] ownColliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < ownColliders.Length; i++)
            {
                if (ownColliders[i] != null)
                {
                    Physics.IgnoreCollision(projectileCollider, ownColliders[i], true);
                }
            }
        }

        Rigidbody projectileBody = projectileObject.AddComponent<Rigidbody>();
        projectileBody.mass = 1.8f;
        projectileBody.useGravity = false;
        projectileBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        projectileBody.interpolation = RigidbodyInterpolation.Interpolate;
        projectileBody.linearVelocity = direction * projectileSpeedMS;

        DamageProjectile projectile = projectileObject.AddComponent<DamageProjectile>();
        projectile.Initialize(
            BuildDroneShell(),
            "Sortie drone",
            null,
            direction * projectileSpeedMS,
            Mathf.Max(weaponRangeMeters, distance + 50f),
            Mathf.Max(2.5f, weaponRangeMeters / Mathf.Max(1f, projectileSpeedMS) * 1.8f),
            0f);
    }

    private DamageShellPreset BuildDroneShell()
    {
        return new DamageShellPreset
        {
            displayNameRu = "Drone bolt",
            shellType = DamageShellType.ArmorPiercing,
            damageType = CoreTacticalDamageType.Kinetic,
            resistanceIgnorePercent = Mathf.Clamp(3f + warfareRating * 0.08f, 3f, 11f),
            caliberMm = 18f,
            damagePoints = Mathf.Clamp(11f + warfareRating * 0.12f, 10f, 24f),
            hullDamageOnPenetration = Mathf.Clamp(11f + warfareRating * 0.12f, 10f, 24f),
            penetrationMm = Mathf.Clamp(16f + warfareRating * 0.16f, 14f, 34f),
            penetrationAtMaxRangeMultiplier = 0.62f,
            velocityRetentionAtMaxRange = 0.70f,
            normalizationDegrees = 1f,
            penetrationRollSpread = 0.16f,
            projectileColor = new Color(1f, 0.45f, 0.26f, 1f)
        };
    }

    private float NextFloat(float min, float max)
    {
        if (random == null)
        {
            random = new System.Random(1);
        }

        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}

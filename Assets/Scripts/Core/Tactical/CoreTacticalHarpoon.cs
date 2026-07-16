using System.Collections.Generic;
using UnityEngine;

public sealed class CoreTacticalHarpoonLauncher : MonoBehaviour
{
    private const float DefaultProjectileRadiusMeters = 8f;
    private const float DefaultFlightCableMaxLengthMeters = 400f;
    private const float DefaultCableLifetimeSeconds = 10f;
    private const float DefaultCableMinimumLengthMeters = 50f;
    private const float DefaultCableWinchSpeedMS = 10f;
    private const float DefaultProjectileReturnSpeedMS = 100f;

    public CoreTacticalShipMotor owner;
    public int sideSign = 1;
    public string displayName = "Harpoon cannon";
    public CoreTacticalWeaponGroup weaponGroup = CoreTacticalWeaponGroup.Torpedoes;
    public string visualLauncherRole = "harpoon";
    public float rangeMeters = 1800f;
    public float projectileSpeedMS = 360f;
    public float reloadSeconds = 5f;
    public float pierceDamage = 55f;
    public float manualAimSectorDegrees = 185f;
    public float targetScatterMinMeters = 4f;
    public float targetScatterPerMeter = 0.035f;
    public float targetScatterMaxMeters = 90f;
    public float cableDurabilitySeconds = DefaultCableLifetimeSeconds;
    public float cableTowForceKg = 280f;
    public float cableWinchSpeedMS = DefaultCableWinchSpeedMS;
    public float projectileReturnSpeedMS = DefaultProjectileReturnSpeedMS;
    public float flightCableMaxLengthMeters = DefaultFlightCableMaxLengthMeters;

    private float nextLaunchTime;
    private bool reloadCooldownStarted;
    private CoreTacticalHarpoonLink activeLink;
    private CoreTacticalWeaponControl weaponControl;
    private CoreTacticalVisualWeaponHandle visualLauncherHandle;
    private bool visualLauncherHandleResolved;
    private CoreTacticalHarpoonProjectile activeProjectile;
    private Transform designatedTargetTransform;
    private Rigidbody designatedTargetBody;
    private CoreTacticalPrototypeHealth designatedTargetHealth;
    private CoreTacticalAutomatonWreck designatedTargetWreck;
    private GameObject launcherObject;
    private Material launcherMaterial;
    private Material projectileMaterial;
    private Material cableMaterial;

    public bool HasActiveLink => activeLink != null;
    public bool HasDesignatedTarget => designatedTargetTransform != null;
    public bool HasFlyingProjectile => activeProjectile != null;
    public CoreTacticalHarpoonLink ActiveLink => activeLink;
    public CoreTacticalHarpoonProjectile ActiveProjectileForTests => activeProjectile;
    public float ManualRangeMeters => Mathf.Max(1f, rangeMeters);
    public float CableRangeMeters => Mathf.Max(1f, flightCableMaxLengthMeters);
    public float ManualAimSectorDegrees => Mathf.Clamp(manualAimSectorDegrees, 1f, 185f);
    public float ReloadCooldownRemainingSeconds => reloadCooldownStarted ? Mathf.Max(0f, nextLaunchTime - Time.time) : 0f;

    private void Awake()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        weaponControl = ResolveWeaponControl();
        EnsureLauncherVisual();
    }

    private void Update()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        EnsureLauncherVisual();
        ApplyLauncherTransform();
        TryLaunchAtDesignatedTarget();
    }

    private void OnDestroy()
    {
        ReleaseActiveLink("launcher destroyed");
        if (launcherObject != null) Destroy(launcherObject);
        if (launcherMaterial != null) Destroy(launcherMaterial);
        if (projectileMaterial != null) Destroy(projectileMaterial);
        if (cableMaterial != null) Destroy(cableMaterial);
    }

    public void Configure(
        CoreTacticalShipMotor newOwner,
        int newSideSign,
        string newDisplayName,
        float newRangeMeters,
        float newPierceDamage,
        float newProjectileSpeedMS,
        float newReloadSeconds,
        float newCableTowForceKg)
    {
        owner = newOwner;
        sideSign = newSideSign < 0 ? -1 : 1;
        displayName = string.IsNullOrWhiteSpace(newDisplayName) ? "Harpoon cannon" : newDisplayName.Trim();
        rangeMeters = Mathf.Max(1f, newRangeMeters);
        pierceDamage = Mathf.Max(0f, newPierceDamage);
        projectileSpeedMS = Mathf.Max(1f, newProjectileSpeedMS);
        reloadSeconds = Mathf.Max(0.2f, newReloadSeconds);
        manualAimSectorDegrees = 185f;
        cableDurabilitySeconds = DefaultCableLifetimeSeconds;
        cableTowForceKg = Mathf.Max(1f, newCableTowForceKg);
        cableWinchSpeedMS = DefaultCableWinchSpeedMS;
        projectileReturnSpeedMS = DefaultProjectileReturnSpeedMS;
        flightCableMaxLengthMeters = Mathf.Max(1f, DefaultFlightCableMaxLengthMeters);
        weaponControl = ResolveWeaponControl();
        visualLauncherHandleResolved = false;
    }

    public bool TryLaunchManualHarpoon(Vector3 worldAimPoint)
    {
        if (owner == null || activeLink != null || activeProjectile != null || Time.time < nextLaunchTime || !CanFireWeapon())
        {
            return false;
        }

        Vector3 launchPosition = GetManualLaunchPosition();
        Vector3 direction = worldAimPoint - launchPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f || !IsDirectionInsideManualSector(direction))
        {
            return false;
        }

        Vector3 targetPoint = ResolveScatteredAimPoint(launchPosition, worldAimPoint);
        activeProjectile = CoreTacticalHarpoonProjectile.Create(
            this,
            owner,
            launchPosition,
            targetPoint,
            projectileSpeedMS,
            DefaultProjectileRadiusMeters,
            flightCableMaxLengthMeters,
            projectileReturnSpeedMS,
            GetProjectileMaterial(),
            GetCableMaterial());

        nextLaunchTime = Time.time + Mathf.Max(0.2f, reloadSeconds);
        reloadCooldownStarted = true;
        return true;
    }

    public bool DesignateTarget(
        Transform targetTransform,
        Rigidbody targetBody,
        CoreTacticalPrototypeHealth targetHealth,
        CoreTacticalAutomatonWreck targetWreck)
    {
        if (owner == null || targetTransform == null)
        {
            return false;
        }

        if (targetTransform == owner.transform || targetTransform.IsChildOf(owner.transform))
        {
            return false;
        }

        if (!IsCatchTargetValid(targetTransform, targetHealth, targetWreck))
        {
            return false;
        }

        if (!CanLaunchAtTarget(targetTransform, targetBody))
        {
            return false;
        }

        designatedTargetTransform = targetTransform;
        designatedTargetBody = targetBody;
        designatedTargetHealth = targetHealth;
        designatedTargetWreck = targetWreck;
        return true;
    }

    public void ClearDesignatedTarget()
    {
        designatedTargetTransform = null;
        designatedTargetBody = null;
        designatedTargetHealth = null;
        designatedTargetWreck = null;
    }

    public bool TryLaunchAtDesignatedTarget()
    {
        if (owner == null
            || activeLink != null
            || activeProjectile != null
            || designatedTargetTransform == null
            || Time.time < nextLaunchTime
            || !CanFireWeapon())
        {
            return false;
        }

        if (!IsCatchTargetValid(designatedTargetTransform, designatedTargetHealth, designatedTargetWreck))
        {
            ClearDesignatedTarget();
            return false;
        }

        Vector3 launchPosition = GetManualLaunchPosition();
        if (!TryBuildLaunchSolution(
                launchPosition,
                designatedTargetTransform,
                designatedTargetBody,
                out Vector3 targetPoint))
        {
            ClearDesignatedTarget();
            return false;
        }

        activeProjectile = CoreTacticalHarpoonProjectile.CreateTargeted(
            this,
            owner,
            designatedTargetTransform,
            designatedTargetBody,
            designatedTargetHealth,
            designatedTargetWreck,
            launchPosition,
            targetPoint,
            projectileSpeedMS,
            DefaultProjectileRadiusMeters,
            flightCableMaxLengthMeters,
            projectileReturnSpeedMS,
            GetProjectileMaterial(),
            GetCableMaterial());

        nextLaunchTime = Time.time + Mathf.Max(0.2f, reloadSeconds);
        reloadCooldownStarted = true;
        return activeProjectile != null;
    }

    public void NotifyProjectileEnded(CoreTacticalHarpoonProjectile projectile, bool attached)
    {
        if (activeProjectile == projectile)
        {
            activeProjectile = null;
        }

        if (attached)
        {
            ClearDesignatedTarget();
        }
    }

    public void ReleaseActiveLink(string reason = "")
    {
        if (activeLink == null)
        {
            return;
        }

        CoreTacticalHarpoonLink link = activeLink;
        activeLink = null;
        link.BreakLink(reason);
    }

    public void NotifyLinkEnded(CoreTacticalHarpoonLink link)
    {
        if (activeLink == link)
        {
            activeLink = null;
        }
    }

    public bool TryAttachFromProjectile(
        Transform targetTransform,
        Rigidbody targetBody,
        CoreTacticalPrototypeHealth targetHealth,
        CoreTacticalAutomatonWreck targetWreck,
        Vector3 hitPoint,
        Vector3 incomingDirection)
    {
        if (owner == null || activeLink != null || targetTransform == null)
        {
            return false;
        }

        Vector3 cableAnchor = GetCableAnchorPosition();
        float initialDistance = Vector3.Distance(cableAnchor, hitPoint);
        if (initialDistance > CableRangeMeters)
        {
            return false;
        }

        ApplyPierceDamage(targetHealth, targetWreck, hitPoint, incomingDirection);
        float initialRestLength = Mathf.Clamp(initialDistance, 1f, CableRangeMeters);
        float minimumRestLength = Mathf.Min(CalculateMinimumRestLengthMeters(targetTransform), initialRestLength);
        activeLink = CoreTacticalHarpoonLink.Create(
            this,
            owner,
            targetTransform,
            targetBody,
            targetHealth,
            targetWreck,
            hitPoint,
            initialRestLength,
            minimumRestLength,
            CableRangeMeters,
            cableDurabilitySeconds,
            cableTowForceKg,
            cableWinchSpeedMS,
            GetCableMaterial());
        if (activeLink != null)
        {
            ClearDesignatedTarget();
        }

        return activeLink != null;
    }

    public Vector3 GetCableAnchorPosition()
    {
        return GetManualLaunchPosition();
    }

    public bool CanLaunchAtTarget(Transform targetTransform, Rigidbody targetBody)
    {
        if (owner == null || targetTransform == null)
        {
            return false;
        }

        return TryBuildLaunchSolution(GetManualLaunchPosition(), targetTransform, targetBody, out _);
    }

    public void SetReloadCooldownRemainingSecondsForTests(float seconds)
    {
        nextLaunchTime = Time.time + Mathf.Max(0f, seconds);
        reloadCooldownStarted = seconds > 0.05f;
    }

    public Vector3 GetManualLaunchPosition()
    {
        if (TryGetVisualLauncherMuzzle(out Vector3 muzzlePosition))
        {
            return muzzlePosition;
        }

        return GetFallbackLauncherWorldPosition() + GetLauncherWorldRotation() * Vector3.forward * 15f;
    }

    public Vector3 GetManualAimCenterDirection()
    {
        Transform ownerTransform = owner != null ? owner.transform : transform;
        int safeSide = sideSign < 0 ? -1 : 1;
        return FlattenDirection(ownerTransform.right * safeSide, ownerTransform.forward);
    }

    public float GetManualAimDeltaDegrees(Vector3 worldDirection)
    {
        Vector3 aimDirection = FlattenDirection(worldDirection, GetManualAimCenterDirection());
        return Mathf.Abs(Vector3.SignedAngle(GetManualAimCenterDirection(), aimDirection, Vector3.up));
    }

    public bool IsDirectionInsideManualSector(Vector3 worldDirection)
    {
        return GetManualAimDeltaDegrees(worldDirection) <= ManualAimSectorDegrees * 0.5f;
    }

    public float GetScatterRadiusForTests(float distanceMeters)
    {
        return CalculateScatterRadius(distanceMeters);
    }

    public float GetManualScatterAngleDegrees(Vector3 worldDirection)
    {
        Vector3 flatDirection = worldDirection;
        flatDirection.y = 0f;
        float distance = Mathf.Clamp(flatDirection.magnitude, 1f, ManualRangeMeters);
        float scatter = CalculateScatterRadius(distance);
        return Mathf.Clamp(Mathf.Atan2(scatter, distance) * Mathf.Rad2Deg * 2f, 2f, 18f);
    }

    private Vector3 ResolveScatteredAimPoint(Vector3 launchPosition, Vector3 worldAimPoint)
    {
        Vector3 flatAim = worldAimPoint;
        flatAim.y = launchPosition.y;
        Vector3 toAim = flatAim - launchPosition;
        float distance = Mathf.Clamp(toAim.magnitude, 1f, ManualRangeMeters);
        Vector3 direction = FlattenDirection(toAim, GetManualAimCenterDirection());
        Vector2 random = Random.insideUnitCircle * CalculateScatterRadius(distance);
        Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
        return launchPosition
            + direction * distance
            + side * random.x
            + Vector3.up * random.y * 0.1f;
    }

    private float CalculateScatterRadius(float distanceMeters)
    {
        return Mathf.Clamp(
            Mathf.Max(0f, targetScatterMinMeters) + Mathf.Max(0f, targetScatterPerMeter) * Mathf.Max(0f, distanceMeters),
            0f,
            Mathf.Max(0f, targetScatterMaxMeters));
    }

    private static Vector3 GetTargetAimPoint(Transform targetTransform)
    {
        return targetTransform != null ? targetTransform.position : Vector3.zero;
    }

    private Vector3 CalculateLeadTargetPoint(Vector3 launchPosition, Transform targetTransform, Rigidbody targetBody)
    {
        Vector3 targetPoint = GetTargetAimPoint(targetTransform);
        if (targetBody == null || projectileSpeedMS <= 0.001f)
        {
            return targetPoint;
        }

        Vector3 targetVelocity = targetBody.linearVelocity;
        Vector3 flatDelta = targetPoint - launchPosition;
        flatDelta.y = 0f;
        Vector3 flatVelocity = targetVelocity;
        flatVelocity.y = 0f;
        float speed = Mathf.Max(1f, projectileSpeedMS);
        float a = Vector3.Dot(flatVelocity, flatVelocity) - speed * speed;
        float b = 2f * Vector3.Dot(flatDelta, flatVelocity);
        float c = Vector3.Dot(flatDelta, flatDelta);
        float leadSeconds = 0f;
        if (Mathf.Abs(a) < 0.001f)
        {
            leadSeconds = Mathf.Abs(b) > 0.001f ? Mathf.Max(0f, -c / b) : 0f;
        }
        else
        {
            float discriminant = b * b - 4f * a * c;
            if (discriminant > 0f)
            {
                float sqrt = Mathf.Sqrt(discriminant);
                float t0 = (-b - sqrt) / (2f * a);
                float t1 = (-b + sqrt) / (2f * a);
                if (t0 > 0f && t1 > 0f)
                {
                    leadSeconds = Mathf.Min(t0, t1);
                }
                else
                {
                    leadSeconds = Mathf.Max(t0, t1);
                }
            }
        }

        if (leadSeconds <= 0f)
        {
            leadSeconds = flatDelta.magnitude / speed;
        }

        leadSeconds = Mathf.Clamp(leadSeconds, 0f, Mathf.Max(0.5f, ManualRangeMeters / speed * 1.35f));
        return targetPoint + targetVelocity * leadSeconds;
    }

    private bool TryBuildLaunchSolution(
        Vector3 launchPosition,
        Transform targetTransform,
        Rigidbody targetBody,
        out Vector3 targetPoint)
    {
        targetPoint = default;
        if (targetTransform == null)
        {
            return false;
        }

        Vector3 currentTargetPoint = GetTargetAimPoint(targetTransform);
        Vector3 directAim = currentTargetPoint - launchPosition;
        directAim.y = 0f;
        if (directAim.sqrMagnitude <= 0.01f
            || directAim.magnitude > ManualRangeMeters
            || !IsDirectionInsideManualSector(directAim))
        {
            return false;
        }

        targetPoint = CalculateLeadTargetPoint(launchPosition, targetTransform, targetBody);
        Vector3 flatAim = targetPoint - launchPosition;
        flatAim.y = 0f;
        if (flatAim.sqrMagnitude <= 0.01f)
        {
            return false;
        }

        if (flatAim.magnitude > ManualRangeMeters)
        {
            targetPoint = launchPosition + flatAim.normalized * ManualRangeMeters;
            targetPoint.y = currentTargetPoint.y;
            flatAim = targetPoint - launchPosition;
            flatAim.y = 0f;
        }

        return IsDirectionInsideManualSector(flatAim);
    }

    public static bool IsCatchTargetValid(
        Transform targetTransform,
        CoreTacticalPrototypeHealth targetHealth,
        CoreTacticalAutomatonWreck targetWreck)
    {
        if (targetTransform == null)
        {
            return false;
        }

        if (targetWreck != null)
        {
            return targetWreck.currentHealth > 0.001f;
        }

        bool isLeviathan = targetTransform.GetComponent<CoreTacticalLeviathanController>() != null;
        if (targetHealth != null && targetHealth.currentHealth <= 0f && !isLeviathan)
        {
            return false;
        }

        return true;
    }

    private float CalculateMinimumRestLengthMeters(Transform targetTransform)
    {
        return DefaultCableMinimumLengthMeters;
    }

    private void ApplyPierceDamage(
        CoreTacticalPrototypeHealth targetHealth,
        CoreTacticalAutomatonWreck targetWreck,
        Vector3 hitPoint,
        Vector3 incomingDirection)
    {
        if (pierceDamage <= 0f)
        {
            return;
        }

        if (targetWreck != null)
        {
            targetWreck.ApplyProjectileDamage(pierceDamage, "harpoon pierce", hitPoint);
            return;
        }

        if (targetHealth != null)
        {
            targetHealth.ApplyDamage(CoreTacticalDamageRequest.Create(
                CoreTacticalDamageType.Kinetic,
                pierceDamage,
                "harpoon pierce",
                0f,
                0f,
                0f,
                hitPoint,
                incomingDirection));
        }
    }

    private bool CanFireWeapon()
    {
        weaponControl ??= ResolveWeaponControl();
        return weaponControl == null || weaponControl.CanFire(weaponGroup);
    }

    private CoreTacticalWeaponControl ResolveWeaponControl()
    {
        if (owner != null)
        {
            return owner.GetComponent<CoreTacticalWeaponControl>();
        }

        return GetComponent<CoreTacticalWeaponControl>();
    }

    private void EnsureLauncherVisual()
    {
        if (owner != null && owner.hideRuntimeWeaponVisuals)
        {
            TryGetVisualLauncherHandle(out _);
            if (launcherObject != null)
            {
                Destroy(launcherObject);
                launcherObject = null;
            }

            return;
        }

        if (launcherObject != null || owner == null)
        {
            return;
        }

        launcherObject = new GameObject("Core Tactical Harpoon Cannon " + (sideSign < 0 ? "Port" : "Starboard"));
        launcherObject.transform.SetPositionAndRotation(GetFallbackLauncherWorldPosition(), GetLauncherWorldRotation());
        launcherMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(new Color(0.18f, 0.28f, 0.32f, 1f), 1.7f);

        GameObject carriage = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carriage.name = "Harpoon Carriage";
        carriage.transform.SetParent(launcherObject.transform, false);
        carriage.transform.localScale = new Vector3(6.5f, 3.2f, 8.5f);
        AssignRendererMaterial(carriage, launcherMaterial);
        DestroyPrimitiveCollider(carriage);

        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrel.name = "Harpoon Pressure Barrel";
        barrel.transform.SetParent(launcherObject.transform, false);
        barrel.transform.localPosition = new Vector3(0f, 1.8f, 5.5f);
        barrel.transform.localScale = new Vector3(2.2f, 2.0f, 14f);
        AssignRendererMaterial(barrel, launcherMaterial);
        DestroyPrimitiveCollider(barrel);

        GameObject spear = GameObject.CreatePrimitive(PrimitiveType.Cube);
        spear.name = "Loaded Harpoon Bolt";
        spear.transform.SetParent(launcherObject.transform, false);
        spear.transform.localPosition = new Vector3(0f, 2.2f, 12.8f);
        spear.transform.localScale = new Vector3(0.75f, 0.75f, 6f);
        AssignRendererMaterial(spear, GetProjectileMaterial());
        DestroyPrimitiveCollider(spear);
    }

    private void ApplyLauncherTransform()
    {
        if (launcherObject != null)
        {
            launcherObject.transform.SetPositionAndRotation(GetFallbackLauncherWorldPosition(), GetLauncherWorldRotation());
        }
    }

    private Vector3 GetFallbackLauncherWorldPosition()
    {
        Vector3 hullSize = owner != null ? owner.hullSizeMeters : transform.localScale;
        Transform ownerTransform = owner != null ? owner.transform : transform;
        int safeSide = sideSign < 0 ? -1 : 1;
        return ownerTransform.position
            + ownerTransform.right * (safeSide * hullSize.x * 0.70f)
            + ownerTransform.up * (hullSize.y * 0.58f + 3f)
            + ownerTransform.forward * (hullSize.z * 0.18f);
    }

    private Quaternion GetLauncherWorldRotation()
    {
        Transform ownerTransform = owner != null ? owner.transform : transform;
        int safeSide = sideSign < 0 ? -1 : 1;
        return ownerTransform.rotation * Quaternion.Euler(0f, safeSide > 0 ? 90f : -90f, 0f);
    }

    private bool TryGetVisualLauncherMuzzle(out Vector3 muzzlePosition)
    {
        muzzlePosition = default;
        if (!TryGetVisualLauncherHandle(out CoreTacticalVisualWeaponHandle handle))
        {
            return false;
        }

        Vector3 fireDirection = GetLauncherWorldRotation() * Vector3.forward;
        muzzlePosition = CoreTacticalShipVisualWeaponBinding.GetMuzzlePosition(handle, GetFallbackLauncherWorldPosition(), fireDirection);
        return true;
    }

    private bool TryGetVisualLauncherHandle(out CoreTacticalVisualWeaponHandle handle)
    {
        if (visualLauncherHandleResolved)
        {
            handle = visualLauncherHandle;
            return handle.IsValid;
        }

        visualLauncherHandleResolved = true;
        visualLauncherHandle = default;
        if (owner == null)
        {
            handle = default;
            return false;
        }

        CoreTacticalShipVisualWeaponBinding binding = owner.GetComponent<CoreTacticalShipVisualWeaponBinding>();
        if (binding == null || !binding.TryBindMissileLauncher(sideSign, visualLauncherRole, out visualLauncherHandle))
        {
            handle = default;
            return false;
        }

        handle = visualLauncherHandle;
        return true;
    }

    private Material GetProjectileMaterial()
    {
        if (projectileMaterial == null)
        {
            projectileMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(new Color(0.54f, 0.84f, 0.94f, 1f), 2.0f);
        }

        return projectileMaterial;
    }

    private Material GetCableMaterial()
    {
        if (cableMaterial == null)
        {
            cableMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(new Color(0.25f, 0.68f, 1f, 0.92f));
        }

        return cableMaterial;
    }

    private static Vector3 FlattenDirection(Vector3 direction, Vector3 fallback)
    {
        direction.y = 0f;
        fallback.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    private static void DestroyPrimitiveCollider(GameObject gameObject)
    {
        Collider collider = gameObject != null ? gameObject.GetComponent<Collider>() : null;
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private static void AssignRendererMaterial(GameObject gameObject, Material material)
    {
        Renderer renderer = gameObject != null ? gameObject.GetComponent<Renderer>() : null;
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}

public sealed class CoreTacticalHarpoonProjectile : MonoBehaviour
{
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[24];
    private static readonly Collider[] OverlapBuffer = new Collider[24];

    private CoreTacticalHarpoonLauncher launcher;
    private CoreTacticalShipMotor owner;
    private Vector3 startPoint;
    private Vector3 targetPoint;
    private Vector3 previousPoint;
    private float speedMS;
    private float radiusMeters;
    private float maxFlightCableLengthMeters;
    private float returnSpeedMS;
    private float flightTime;
    private float elapsedSeconds;
    private GameObject bodyObject;
    private LineRenderer flightCableLine;
    private bool notifiedLauncher;
    private bool returningToLauncher;

    public bool HasFlightCableForTests => flightCableLine != null && flightCableLine.positionCount == 2;
    public float MaxFlightCableLengthMetersForTests => maxFlightCableLengthMeters;
    public Vector3 TargetPointForTests => targetPoint;
    public bool IsReturningForTests => returningToLauncher;

    public static CoreTacticalHarpoonProjectile Create(
        CoreTacticalHarpoonLauncher launcher,
        CoreTacticalShipMotor owner,
        Vector3 start,
        Vector3 target,
        float speedMS,
        float radiusMeters,
        float maxFlightCableLengthMeters,
        float returnSpeedMS,
        Material projectileMaterial,
        Material trailMaterial)
    {
        GameObject projectileObject = new GameObject("Core Tactical Harpoon Projectile");
        CoreTacticalHarpoonProjectile projectile = projectileObject.AddComponent<CoreTacticalHarpoonProjectile>();
        projectile.Initialize(launcher, owner, null, null, null, null, start, target, speedMS, radiusMeters, maxFlightCableLengthMeters, returnSpeedMS, projectileMaterial, trailMaterial);
        return projectile;
    }

    public static CoreTacticalHarpoonProjectile CreateTargeted(
        CoreTacticalHarpoonLauncher launcher,
        CoreTacticalShipMotor owner,
        Transform targetTransform,
        Rigidbody targetBody,
        CoreTacticalPrototypeHealth targetHealth,
        CoreTacticalAutomatonWreck targetWreck,
        Vector3 start,
        Vector3 target,
        float speedMS,
        float radiusMeters,
        float maxFlightCableLengthMeters,
        float returnSpeedMS,
        Material projectileMaterial,
        Material trailMaterial)
    {
        GameObject projectileObject = new GameObject("Core Tactical Harpoon Projectile");
        CoreTacticalHarpoonProjectile projectile = projectileObject.AddComponent<CoreTacticalHarpoonProjectile>();
        projectile.Initialize(launcher, owner, targetTransform, targetBody, targetHealth, targetWreck, start, target, speedMS, radiusMeters, maxFlightCableLengthMeters, returnSpeedMS, projectileMaterial, trailMaterial);
        return projectile;
    }

    private void Initialize(
        CoreTacticalHarpoonLauncher newLauncher,
        CoreTacticalShipMotor newOwner,
        Transform newIntendedTarget,
        Rigidbody newIntendedTargetBody,
        CoreTacticalPrototypeHealth newIntendedTargetHealth,
        CoreTacticalAutomatonWreck newIntendedTargetWreck,
        Vector3 start,
        Vector3 target,
        float newSpeedMS,
        float newRadiusMeters,
        float newMaxFlightCableLengthMeters,
        float newReturnSpeedMS,
        Material projectileMaterial,
        Material trailMaterial)
    {
        launcher = newLauncher;
        owner = newOwner;
        startPoint = start;
        targetPoint = target;
        previousPoint = start;
        speedMS = Mathf.Max(1f, newSpeedMS);
        radiusMeters = Mathf.Max(0.25f, newRadiusMeters);
        maxFlightCableLengthMeters = Mathf.Max(1f, newMaxFlightCableLengthMeters);
        returnSpeedMS = Mathf.Max(1f, newReturnSpeedMS);
        float flatDistance = Vector3.Distance(new Vector3(start.x, 0f, start.z), new Vector3(target.x, 0f, target.z));
        flightTime = Mathf.Max(0.15f, flatDistance / speedMS);
        transform.position = start;

        bodyObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bodyObject.name = "Harpoon Bolt";
        bodyObject.transform.SetParent(transform, false);
        bodyObject.transform.localScale = new Vector3(1.2f, 4.2f, 1.2f);
        AssignRendererMaterial(bodyObject, projectileMaterial);
        DestroyPrimitiveCollider(bodyObject);

        flightCableLine = gameObject.AddComponent<LineRenderer>();
        flightCableLine.sharedMaterial = trailMaterial;
        flightCableLine.useWorldSpace = true;
        flightCableLine.positionCount = 2;
        flightCableLine.startWidth = 1.2f;
        flightCableLine.endWidth = 0.75f;
        flightCableLine.startColor = new Color(0.24f, 0.60f, 0.82f, 0.78f);
        flightCableLine.endColor = new Color(0.24f, 0.60f, 0.82f, 0.72f);
        flightCableLine.numCapVertices = 2;
        flightCableLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flightCableLine.receiveShadows = false;
        UpdateVisual(start, start);
    }

    private void Update()
    {
        if (launcher == null || owner == null)
        {
            FinishProjectile(false);
            return;
        }

        if (returningToLauncher)
        {
            UpdateReturn(Time.deltaTime);
            return;
        }

        elapsedSeconds += Mathf.Max(0f, Time.deltaTime);
        float t = Mathf.Clamp01(elapsedSeconds / Mathf.Max(0.001f, flightTime));
        Vector3 nextPoint = EvaluatePosition(t);
        if (TryHitBetween(previousPoint, nextPoint, out Transform target, out Rigidbody targetBody, out CoreTacticalPrototypeHealth targetHealth, out CoreTacticalAutomatonWreck wreck, out Vector3 hitPoint))
        {
            Vector3 incoming = (nextPoint - previousPoint).sqrMagnitude > 0.001f ? (nextPoint - previousPoint).normalized : (targetPoint - startPoint).normalized;
            bool attached = launcher.TryAttachFromProjectile(target, targetBody, targetHealth, wreck, hitPoint, incoming);
            FinishOrReturn(attached);
            return;
        }

        transform.position = nextPoint;
        UpdateVisual(previousPoint, nextPoint);
        previousPoint = nextPoint;

        if (t >= 0.999f)
        {
            bool attached = false;
            if (TryOverlapAt(nextPoint, out target, out targetBody, out targetHealth, out wreck, out hitPoint))
            {
                Vector3 incoming = (targetPoint - startPoint).sqrMagnitude > 0.001f ? (targetPoint - startPoint).normalized : Vector3.forward;
                attached = launcher.TryAttachFromProjectile(target, targetBody, targetHealth, wreck, hitPoint, incoming);
            }

            FinishOrReturn(attached);
        }
    }

    private void OnDestroy()
    {
        if (!notifiedLauncher && launcher != null)
        {
            launcher.NotifyProjectileEnded(this, false);
        }
    }

    private Vector3 EvaluatePosition(float t)
    {
        Vector3 flat = Vector3.Lerp(startPoint, targetPoint, t);
        float distance = Vector3.Distance(new Vector3(startPoint.x, 0f, startPoint.z), new Vector3(targetPoint.x, 0f, targetPoint.z));
        float arcHeight = Mathf.Clamp(distance * 0.06f, 6f, 55f);
        flat.y = Mathf.Lerp(startPoint.y, targetPoint.y, t) + Mathf.Sin(t * Mathf.PI) * arcHeight;
        return flat;
    }

    public void BeginReturnForTests()
    {
        BeginReturnToLauncher();
    }

    public void StepReturnForTests(float deltaSeconds)
    {
        UpdateReturn(Mathf.Max(0f, deltaSeconds));
    }

    private void BeginReturnToLauncher()
    {
        returningToLauncher = true;
        previousPoint = transform.position;
    }

    private void UpdateReturn(float deltaSeconds)
    {
        if (!returningToLauncher)
        {
            return;
        }

        if (launcher == null)
        {
            FinishProjectile(false);
            return;
        }

        Vector3 current = transform.position;
        Vector3 anchor = launcher.GetCableAnchorPosition();
        Vector3 toAnchor = anchor - current;
        float distance = toAnchor.magnitude;
        float step = Mathf.Max(0f, returnSpeedMS) * Mathf.Max(0f, deltaSeconds);
        if (distance <= Mathf.Max(0.25f, step))
        {
            transform.position = anchor;
            UpdateVisual(current, anchor);
            FinishProjectile(false);
            return;
        }

        Vector3 next = current + toAnchor / distance * step;
        transform.position = next;
        UpdateVisual(current, next);
        previousPoint = next;
    }

    private void UpdateVisual(Vector3 previous, Vector3 next)
    {
        Vector3 velocity = next - previous;
        if (velocity.sqrMagnitude > 0.001f && bodyObject != null)
        {
            bodyObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, velocity.normalized);
        }

        if (flightCableLine != null)
        {
            flightCableLine.SetPosition(0, launcher != null ? launcher.GetCableAnchorPosition() : startPoint);
            flightCableLine.SetPosition(1, next);
        }
    }

    private void FinishProjectile(bool attached)
    {
        if (!notifiedLauncher && launcher != null)
        {
            notifiedLauncher = true;
            launcher.NotifyProjectileEnded(this, attached);
        }

        Destroy(gameObject);
    }

    private void FinishOrReturn(bool attached)
    {
        if (attached)
        {
            FinishProjectile(true);
            return;
        }

        BeginReturnToLauncher();
    }

    private bool TryHitBetween(
        Vector3 from,
        Vector3 to,
        out Transform target,
        out Rigidbody targetBody,
        out CoreTacticalPrototypeHealth targetHealth,
        out CoreTacticalAutomatonWreck wreck,
        out Vector3 hitPoint)
    {
        target = null;
        targetBody = null;
        targetHealth = null;
        wreck = null;
        hitPoint = to;

        Vector3 segment = to - from;
        float distance = segment.magnitude;
        if (distance <= 0.001f)
        {
            return false;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            from,
            radiusMeters,
            segment / distance,
            HitBuffer,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;
        int bestIndex = -1;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = HitBuffer[i];
            if (hit.collider == null || hit.distance >= bestDistance)
            {
                continue;
            }

            if (!TryResolveTarget(hit.collider, out Transform resolvedTarget, out Rigidbody resolvedBody, out CoreTacticalPrototypeHealth resolvedHealth, out CoreTacticalAutomatonWreck resolvedWreck))
            {
                continue;
            }

            bestDistance = hit.distance;
            bestIndex = i;
            target = resolvedTarget;
            targetBody = resolvedBody;
            targetHealth = resolvedHealth;
            wreck = resolvedWreck;
            hitPoint = hit.point;
        }

        return bestIndex >= 0;
    }

    private bool TryOverlapAt(
        Vector3 point,
        out Transform target,
        out Rigidbody targetBody,
        out CoreTacticalPrototypeHealth targetHealth,
        out CoreTacticalAutomatonWreck wreck,
        out Vector3 hitPoint)
    {
        target = null;
        targetBody = null;
        targetHealth = null;
        wreck = null;
        hitPoint = point;

        int hitCount = Physics.OverlapSphereNonAlloc(point, radiusMeters * 1.2f, OverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = OverlapBuffer[i];
            if (collider != null
                && TryResolveTarget(collider, out target, out targetBody, out targetHealth, out wreck))
            {
                hitPoint = collider.ClosestPoint(point);
                return true;
            }
        }

        return false;
    }

    private bool TryResolveTarget(
        Collider collider,
        out Transform target,
        out Rigidbody targetBody,
        out CoreTacticalPrototypeHealth targetHealth,
        out CoreTacticalAutomatonWreck wreck)
    {
        target = null;
        targetBody = null;
        targetHealth = null;
        wreck = null;
        if (collider == null)
        {
            return false;
        }

        Transform hitTransform = collider.transform;
        if (owner != null && (hitTransform == owner.transform || hitTransform.IsChildOf(owner.transform)))
        {
            return false;
        }

        wreck = collider.GetComponentInParent<CoreTacticalAutomatonWreck>();
        if (wreck != null)
        {
            if (wreck.currentHealth <= 0.001f)
            {
                return false;
            }

            target = wreck.transform;
            targetBody = wreck.GetComponent<Rigidbody>();
            return true;
        }

        CoreTacticalShipMotor targetShip = collider.GetComponentInParent<CoreTacticalShipMotor>();
        if (targetShip != null)
        {
            if (targetShip == owner)
            {
                return false;
            }

            targetHealth = targetShip.GetComponent<CoreTacticalPrototypeHealth>();
            if (targetHealth != null
                && targetHealth.currentHealth <= 0f
                && targetShip.GetComponent<CoreTacticalLeviathanController>() == null)
            {
                return false;
            }

            target = targetShip.transform;
            targetBody = targetShip.Body != null ? targetShip.Body : targetShip.GetComponent<Rigidbody>();
            return true;
        }

        targetHealth = collider.GetComponentInParent<CoreTacticalPrototypeHealth>();
        if (targetHealth != null && targetHealth.currentHealth > 0f)
        {
            target = targetHealth.transform;
            targetBody = targetHealth.GetComponent<Rigidbody>();
            return true;
        }

        return false;
    }

    private static void DestroyPrimitiveCollider(GameObject gameObject)
    {
        Collider collider = gameObject != null ? gameObject.GetComponent<Collider>() : null;
        if (collider != null)
        {
            Destroy(collider);
        }
    }

    private static void AssignRendererMaterial(GameObject gameObject, Material material)
    {
        Renderer renderer = gameObject != null ? gameObject.GetComponent<Renderer>() : null;
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}

public sealed class CoreTacticalHarpoonLink : MonoBehaviour
{
    private const float GravityMS2 = 9.81f;
    private static readonly List<CoreTacticalHarpoonLink> ActiveLinks = new List<CoreTacticalHarpoonLink>(16);

    private CoreTacticalHarpoonLauncher launcher;
    private CoreTacticalShipMotor owner;
    private Rigidbody ownerBody;
    private Transform targetTransform;
    private Rigidbody targetBody;
    private CoreTacticalPrototypeHealth targetHealth;
    private CoreTacticalAutomatonWreck targetWreck;
    private Vector3 targetLocalAnchor;
    private float restLengthMeters;
    private float minimumRestLengthMeters;
    private float maximumCableLengthMeters;
    private float remainingDurabilitySeconds;
    private float maximumDurabilitySeconds;
    private float towForceKg;
    private float winchSpeedMS;
    private LineRenderer cableLine;
    private Material cableMaterial;
    private bool ending;

    public float CurrentTensionKg { get; private set; }
    public float Stress01 { get; private set; }
    public float RemainingDurabilitySeconds => Mathf.Max(0f, remainingDurabilitySeconds);
    public float RestLengthMeters => Mathf.Max(0f, restLengthMeters);
    public float MaximumCableLengthMeters => Mathf.Max(0f, maximumCableLengthMeters);

    public static CoreTacticalHarpoonLink Create(
        CoreTacticalHarpoonLauncher launcher,
        CoreTacticalShipMotor owner,
        Transform target,
        Rigidbody targetBody,
        CoreTacticalPrototypeHealth targetHealth,
        CoreTacticalAutomatonWreck targetWreck,
        Vector3 hitPoint,
        float initialRestLengthMeters,
        float minimumRestLengthMeters,
        float maximumCableLengthMeters,
        float durabilitySeconds,
        float towForceKg,
        float winchSpeedMS,
        Material cableMaterial)
    {
        if (launcher == null || owner == null || target == null)
        {
            return null;
        }

        GameObject linkObject = new GameObject("Core Tactical Harpoon Cable");
        CoreTacticalHarpoonLink link = linkObject.AddComponent<CoreTacticalHarpoonLink>();
        link.Initialize(
            launcher,
            owner,
            target,
            targetBody,
            targetHealth,
            targetWreck,
            hitPoint,
            initialRestLengthMeters,
            minimumRestLengthMeters,
            maximumCableLengthMeters,
            durabilitySeconds,
            towForceKg,
            winchSpeedMS,
            cableMaterial);
        return link;
    }

    public static bool HasActiveLinkForShip(CoreTacticalShipMotor ship)
    {
        if (ship == null)
        {
            return false;
        }

        for (int i = ActiveLinks.Count - 1; i >= 0; i--)
        {
            CoreTacticalHarpoonLink link = ActiveLinks[i];
            if (link == null)
            {
                ActiveLinks.RemoveAt(i);
                continue;
            }

            if (link.owner == ship)
            {
                return true;
            }

            CoreTacticalShipMotor targetShip = link.targetTransform != null
                ? link.targetTransform.GetComponent<CoreTacticalShipMotor>()
                : null;
            if (targetShip == ship)
            {
                return true;
            }
        }

        return false;
    }

    private void Initialize(
        CoreTacticalHarpoonLauncher newLauncher,
        CoreTacticalShipMotor newOwner,
        Transform newTarget,
        Rigidbody newTargetBody,
        CoreTacticalPrototypeHealth newTargetHealth,
        CoreTacticalAutomatonWreck newTargetWreck,
        Vector3 hitPoint,
        float newRestLengthMeters,
        float newMinimumRestLengthMeters,
        float newMaximumCableLengthMeters,
        float newDurabilitySeconds,
        float newTowForceKg,
        float newWinchSpeedMS,
        Material newCableMaterial)
    {
        launcher = newLauncher;
        owner = newOwner;
        ownerBody = owner != null ? owner.Body : null;
        targetTransform = newTarget;
        targetBody = newTargetBody;
        targetHealth = newTargetHealth;
        targetWreck = newTargetWreck;
        targetLocalAnchor = newTarget != null ? newTarget.InverseTransformPoint(hitPoint) : Vector3.zero;
        maximumCableLengthMeters = Mathf.Max(1f, newMaximumCableLengthMeters);
        restLengthMeters = Mathf.Clamp(newRestLengthMeters, 1f, maximumCableLengthMeters);
        minimumRestLengthMeters = Mathf.Clamp(newMinimumRestLengthMeters, 1f, restLengthMeters);
        maximumDurabilitySeconds = Mathf.Max(1f, newDurabilitySeconds);
        remainingDurabilitySeconds = maximumDurabilitySeconds;
        towForceKg = Mathf.Max(1f, newTowForceKg);
        winchSpeedMS = Mathf.Max(0f, newWinchSpeedMS);
        cableMaterial = newCableMaterial;
        ActiveLinks.Add(this);
        EnsureLine();
        UpdateLine();
    }

    private void FixedUpdate()
    {
        Tick(Time.fixedDeltaTime);
    }

    private void LateUpdate()
    {
        UpdateLine();
    }

    private void OnDestroy()
    {
        ActiveLinks.Remove(this);
        if (!ending && launcher != null)
        {
            launcher.NotifyLinkEnded(this);
        }
    }

    public void BreakLink(string reason = "")
    {
        if (ending)
        {
            return;
        }

        ending = true;
        if (launcher != null)
        {
            launcher.NotifyLinkEnded(this);
        }

        Destroy(gameObject);
    }

    public void ApplyTensionForTests(float deltaSeconds, float forcedTensionKg)
    {
        Tick(deltaSeconds);
        CurrentTensionKg = Mathf.Max(CurrentTensionKg, forcedTensionKg);
    }

    private void Tick(float deltaSeconds)
    {
        deltaSeconds = Mathf.Max(0f, deltaSeconds);
        if (deltaSeconds <= 0f)
        {
            return;
        }

        if (!IsLinkStillValid())
        {
            BreakLink("invalid target");
            return;
        }

        remainingDurabilitySeconds = Mathf.Max(0f, remainingDurabilitySeconds - deltaSeconds);
        if (remainingDurabilitySeconds <= 0.001f)
        {
            BreakLink("cable timed out");
            return;
        }

        restLengthMeters = Mathf.MoveTowards(restLengthMeters, minimumRestLengthMeters, winchSpeedMS * deltaSeconds);
        Vector3 ownerAnchor = GetOwnerAnchor();
        Vector3 targetAnchor = GetTargetAnchor();
        Vector3 delta = targetAnchor - ownerAnchor;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            CurrentTensionKg = 0f;
            Stress01 = 0f;
            UpdateLine();
            return;
        }

        Vector3 direction = delta / distance;
        EnforceMaximumCableLength(ownerAnchor, targetAnchor, direction, distance);
        ownerAnchor = GetOwnerAnchor();
        targetAnchor = GetTargetAnchor();
        delta = targetAnchor - ownerAnchor;
        distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            CurrentTensionKg = 0f;
            Stress01 = 0f;
            UpdateLine();
            return;
        }

        direction = delta / distance;
        float stretch = Mathf.Max(0f, distance - restLengthMeters);
        float relativeSeparatingSpeed = Mathf.Max(0f, Vector3.Dot(GetTargetVelocity() - GetOwnerVelocity(), direction));
        float tensionKg = stretch > 0.001f || relativeSeparatingSpeed > 0.001f ? towForceKg : 0f;
        CurrentTensionKg = tensionKg;
        Stress01 = 0f;
        ApplyPull(direction, tensionKg, deltaSeconds);
        UpdateLine();
    }

    private void ApplyPull(Vector3 ownerToTargetDirection, float tensionKg, float deltaSeconds)
    {
        if (tensionKg <= 0.001f)
        {
            return;
        }

        float appliedLoadKg = Mathf.Min(Mathf.Max(0f, towForceKg), tensionKg);
        ApplyVelocityDelta(ownerBody, ownerToTargetDirection, appliedLoadKg, owner != null ? owner.massKg : 1f, deltaSeconds);
        if (targetWreck != null)
        {
            targetWreck.SetCaptured(true, owner != null ? owner.name : "Harpoon", 0.1f);
            float towDistance = CalculateTowDistanceForMass(targetWreck.massKg, appliedLoadKg, deltaSeconds);
            targetWreck.PullToward(GetOwnerAnchor(), towDistance, false);
            return;
        }

        ApplyVelocityDelta(targetBody, -ownerToTargetDirection, appliedLoadKg, ResolveTargetMassKg(), deltaSeconds);
    }

    private void EnforceMaximumCableLength(Vector3 ownerAnchor, Vector3 targetAnchor, Vector3 ownerToTargetDirection, float distance)
    {
        float overLength = distance - maximumCableLengthMeters;
        if (overLength <= 0.001f)
        {
            return;
        }

        if (targetWreck != null)
        {
            Vector3 allowedAnchor = ownerAnchor + ownerToTargetDirection * maximumCableLengthMeters;
            targetWreck.transform.position += allowedAnchor - targetAnchor;
            return;
        }

        bool ownerCanMove = ownerBody != null && !ownerBody.isKinematic;
        bool targetCanMove = targetBody != null && !targetBody.isKinematic;
        if (!ownerCanMove && !targetCanMove)
        {
            if (targetTransform != null)
            {
                Vector3 allowedAnchor = ownerAnchor + ownerToTargetDirection * maximumCableLengthMeters;
                targetTransform.position += allowedAnchor - targetAnchor;
            }

            return;
        }

        float ownerMass = Mathf.Max(1f, owner != null ? owner.massKg : ownerBody != null ? ownerBody.mass : 1f);
        float targetMass = ResolveTargetMassKg();
        float totalMass = ownerMass + targetMass;
        if (ownerCanMove && targetCanMove)
        {
            MoveBody(ownerBody, ownerToTargetDirection * (overLength * targetMass / totalMass), ownerToTargetDirection);
            MoveBody(targetBody, -ownerToTargetDirection * (overLength * ownerMass / totalMass), -ownerToTargetDirection);
        }
        else if (ownerCanMove)
        {
            MoveBody(ownerBody, ownerToTargetDirection * overLength, ownerToTargetDirection);
        }
        else
        {
            MoveBody(targetBody, -ownerToTargetDirection * overLength, -ownerToTargetDirection);
        }
    }

    private bool IsLinkStillValid()
    {
        if (owner == null || targetTransform == null)
        {
            return false;
        }

        CoreTacticalPrototypeHealth ownerHealth = owner.GetComponent<CoreTacticalPrototypeHealth>();
        if (ownerHealth != null && ownerHealth.currentHealth <= 0f)
        {
            return false;
        }

        if (targetWreck != null && targetWreck.currentHealth <= 0.001f)
        {
            return false;
        }

        return true;
    }

    private Vector3 GetOwnerAnchor()
    {
        if (owner == null)
        {
            return transform.position;
        }

        if (launcher != null)
        {
            return launcher.GetCableAnchorPosition();
        }

        return owner.transform.position + owner.transform.up * (owner.hullSizeMeters.y * 0.4f + 2f);
    }

    private Vector3 GetTargetAnchor()
    {
        return targetTransform != null ? targetTransform.TransformPoint(targetLocalAnchor) : GetOwnerAnchor();
    }

    private Vector3 GetOwnerVelocity()
    {
        return ownerBody != null ? ownerBody.linearVelocity : Vector3.zero;
    }

    private Vector3 GetTargetVelocity()
    {
        return targetBody != null && !targetBody.isKinematic ? targetBody.linearVelocity : Vector3.zero;
    }

    private float ResolveTargetMassKg()
    {
        if (targetWreck != null)
        {
            return Mathf.Max(1f, targetWreck.massKg);
        }

        CoreTacticalShipMotor targetShip = targetTransform != null ? targetTransform.GetComponent<CoreTacticalShipMotor>() : null;
        if (targetShip != null)
        {
            return Mathf.Max(1f, targetShip.massKg);
        }

        return targetBody != null ? Mathf.Max(1f, targetBody.mass) : 1f;
    }

    private static void ApplyVelocityDelta(Rigidbody body, Vector3 direction, float loadKg, float fallbackMassKg, float deltaSeconds)
    {
        if (body == null || body.isKinematic || loadKg <= 0f)
        {
            return;
        }

        float mass = Mathf.Max(1f, body.mass > 0f ? body.mass : fallbackMassKg);
        body.linearVelocity += direction * (loadKg * GravityMS2 / mass) * Mathf.Max(0f, deltaSeconds);
    }

    private static void MoveBody(Rigidbody body, Vector3 deltaPosition, Vector3 allowedDirection)
    {
        if (body == null || body.isKinematic)
        {
            return;
        }

        body.position += deltaPosition;
        body.transform.position = body.position;
        Vector3 velocity = body.linearVelocity;
        float separatingSpeed = Vector3.Dot(velocity, -allowedDirection);
        if (separatingSpeed > 0f)
        {
            body.linearVelocity = velocity + allowedDirection * separatingSpeed;
        }
    }

    private static float CalculateTowDistanceForMass(float massKg, float loadKg, float deltaSeconds)
    {
        float mass = Mathf.Max(1f, massKg);
        float speedMS = Mathf.Clamp(120f * Mathf.Max(0f, loadKg) / mass, 0.05f, 90f);
        return speedMS * Mathf.Max(0f, deltaSeconds);
    }

    private void EnsureLine()
    {
        if (cableLine != null)
        {
            return;
        }

        cableLine = gameObject.AddComponent<LineRenderer>();
        cableLine.sharedMaterial = cableMaterial != null
            ? cableMaterial
            : CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(new Color(0.25f, 0.68f, 1f, 0.92f));
        cableLine.useWorldSpace = true;
        cableLine.positionCount = 2;
        cableLine.startWidth = 1.55f;
        cableLine.endWidth = 1.1f;
        cableLine.numCapVertices = 3;
        cableLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cableLine.receiveShadows = false;
    }

    private void UpdateLine()
    {
        if (cableLine == null || owner == null || targetTransform == null)
        {
            return;
        }

        Color color = Color.Lerp(new Color(0.20f, 0.72f, 1f, 0.92f), new Color(1f, 0.14f, 0.08f, 0.96f), Stress01);
        cableLine.startColor = color;
        cableLine.endColor = color;
        Vector3 ownerAnchor = GetOwnerAnchor();
        Vector3 targetAnchor = GetTargetAnchor();
        Vector3 delta = targetAnchor - ownerAnchor;
        float distance = delta.magnitude;
        if (distance > maximumCableLengthMeters && distance > 0.001f)
        {
            targetAnchor = ownerAnchor + delta / distance * maximumCableLengthMeters;
        }

        cableLine.SetPosition(0, ownerAnchor);
        cableLine.SetPosition(1, targetAnchor);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct CoreTacticalVisualWeaponHandle
{
    public Transform pivot;
    public Quaternion initialLocalRotation;
    public float baselineYawDegrees;
    public Renderer[] renderers;

    public bool IsValid => pivot != null;
}

internal static class CoreTacticalWeaponVisualMaterialUtility
{
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int TintColorPropertyId = Shader.PropertyToID("_TintColor");
    private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");

    public static Material CreateVisibleMaterial(Color color, float emissionMultiplier)
    {
        Material material = new Material(FindVisibleShader());
        ApplyVisibleColor(material, color, emissionMultiplier);
        return material;
    }

    public static Material CreateTransparentMaterial(Color color, float emissionMultiplier)
    {
        Material material = new Material(FindVisibleShader());
        ConfigureTransparentMaterial(material);
        ApplyVisibleColor(material, color, emissionMultiplier);
        return material;
    }

    public static Material CreateVertexColorTransparentMaterial(Color fallbackTint)
    {
        Material material = new Material(FindVertexColorShader());
        ConfigureTransparentMaterial(material);
        // Mesh and LineRenderer weapon trails carry their real color in vertex/line colors.
        // Keep the material white so the shader does not multiply those colors into darkness.
        ApplyVisibleColor(material, Color.white, 0f);
        if (material.HasProperty(TintColorPropertyId)) material.SetColor(TintColorPropertyId, Color.white);
        material.name = "Core Tactical Weapon Vertex Color Transparent " + ColorUtility.ToHtmlStringRGBA(fallbackTint);
        return material;
    }

    public static void ApplyVisibleColor(Material material, Color color, float emissionMultiplier)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(BaseColorPropertyId)) material.SetColor(BaseColorPropertyId, color);
        if (material.HasProperty(ColorPropertyId)) material.SetColor(ColorPropertyId, color);
        if (material.HasProperty(TintColorPropertyId)) material.SetColor(TintColorPropertyId, color);
        if (emissionMultiplier > 0f && material.HasProperty(EmissionColorPropertyId))
        {
            material.SetColor(EmissionColorPropertyId, color * emissionMultiplier);
            material.EnableKeyword("_EMISSION");
        }

        material.color = color;
    }

    public static void ConfigureTransparentMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    public static Color ResolveVisibleColor(Material material, Color fallback)
    {
        Color color = fallback;
        if (material != null)
        {
            if (material.HasProperty(BaseColorPropertyId))
            {
                color = material.GetColor(BaseColorPropertyId);
            }
            else if (material.HasProperty(ColorPropertyId))
            {
                color = material.GetColor(ColorPropertyId);
            }
            else if (material.HasProperty(TintColorPropertyId))
            {
                color = material.GetColor(TintColorPropertyId);
            }
            else
            {
                color = material.color;
            }
        }

        float luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        return luminance <= 0.04f ? fallback : color;
    }

    private static Shader FindVisibleShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return shader;
    }

    private static Shader FindVertexColorShader()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return shader;
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalShipVisualWeaponBinding : MonoBehaviour
{
    private readonly List<Transform> scratchTransforms = new List<Transform>(32);
    private CoreTacticalShipMotor owner;
    private Transform visualRoot;

    public void Initialize(CoreTacticalShipMotor ship, Transform importedVisualRoot)
    {
        owner = ship;
        visualRoot = importedVisualRoot;
    }

    public bool TryBindFrigateMainMount(string displayName, float baselineYawDegrees, out CoreTacticalVisualWeaponHandle handle)
    {
        handle = default;
        if (visualRoot == null)
        {
            return false;
        }

        Transform pivot = null;
        string normalizedName = Normalize(displayName);
        if (normalizedName.Contains("fore"))
        {
            pivot = FindDeepChildExact("Korshun_Turret_76mm_Twin_Fore")
                ?? FindProjectedExtreme("Turret", front: true);
        }
        else if (normalizedName.Contains("aft"))
        {
            pivot = FindDeepChildExact("Korshun_Turret_76mm_Twin_Aft")
                ?? FindProjectedExtreme("Turret", front: false);
        }

        return TryCreateHandle(pivot, baselineYawDegrees, out handle);
    }

    public bool TryBindCapitalMainTurret(string displayName, float baselineYawDegrees, out CoreTacticalVisualWeaponHandle handle)
    {
        handle = default;
        if (visualRoot == null)
        {
            return false;
        }

        CollectDeepChildrenContaining("Turret", scratchTransforms, excludeToken: "Base");
        if (scratchTransforms.Count == 0)
        {
            return false;
        }

        SortByForwardProjection(scratchTransforms);
        string normalizedName = Normalize(displayName);
        int index = 0;
        if (normalizedName.Contains("turret b")) index = Mathf.Min(1, scratchTransforms.Count - 1);
        else if (normalizedName.Contains("turret x")) index = Mathf.Max(0, scratchTransforms.Count - 2);
        else if (normalizedName.Contains("turret y")) index = scratchTransforms.Count - 1;

        return TryCreateHandle(scratchTransforms[index], baselineYawDegrees, out handle);
    }

    public bool TryBindMissileLauncher(int sideSign, out CoreTacticalVisualWeaponHandle handle)
    {
        return TryBindMissileLauncher(sideSign, "torpedo", out handle);
    }

    public bool TryBindMissileLauncher(int sideSign, string visualRole, out CoreTacticalVisualWeaponHandle handle)
    {
        handle = default;
        if (visualRoot == null)
        {
            return false;
        }

        string normalizedRole = Normalize(visualRole);
        bool mainRocket = normalizedRole.Contains("main") && (normalizedRole.Contains("rocket") || normalizedRole.Contains("nurs") || normalizedRole.Contains("missile"));
        bool sideRocket = normalizedRole.Contains("side") && (normalizedRole.Contains("rocket") || normalizedRole.Contains("nurs") || normalizedRole.Contains("missile"));
        bool sideHarpoon = normalizedRole.Contains("harpoon");
        Transform pivot = null;
        float baselineYawDegrees = sideSign < 0 ? -90f : 90f;

        if (mainRocket)
        {
            pivot = sideSign < 0
                ? FindDeepChildExact("Korshun_CombatMain_RocketLauncher_Pod_Fore")
                    ?? FindDeepChildExact("Korshun_PortPreview_RocketLauncher_Pod_Fore")
                : FindDeepChildExact("Korshun_CombatMain_RocketLauncher_Pod_Aft")
                    ?? FindDeepChildExact("Korshun_PortPreview_RocketLauncher_Pod_Aft");
            baselineYawDegrees = 0f;
        }
        else if (sideRocket)
        {
            pivot = sideSign < 0
                ? FindDeepChildExact("Korshun_CombatAux_RocketLauncher_Pod_Left")
                    ?? FindDeepChildExact("Korshun_PortAuxPreview_RocketLauncher_Pod_Left")
                : FindDeepChildExact("Korshun_CombatAux_RocketLauncher_Pod_Right")
                    ?? FindDeepChildExact("Korshun_PortAuxPreview_RocketLauncher_Pod_Right");
        }
        else if (sideHarpoon)
        {
            pivot = sideSign < 0
                ? FindDeepChildExact("Korshun_CombatAux_HarpoonCannon_Left")
                    ?? FindDeepChildExact("Korshun_PortAuxPreview_HarpoonCannon_Left")
                    ?? FindDeepChildExact("Barbet_CombatSmall_HarpoonCannon_Left")
                    ?? FindDeepChildExact("Barbet_PortSmallPreview_HarpoonCannon_Left")
                : FindDeepChildExact("Korshun_CombatAux_HarpoonCannon_Right")
                    ?? FindDeepChildExact("Korshun_PortAuxPreview_HarpoonCannon_Right")
                    ?? FindDeepChildExact("Barbet_CombatSmall_HarpoonCannon_Right")
                    ?? FindDeepChildExact("Barbet_PortSmallPreview_HarpoonCannon_Right");
        }

        if (pivot == null)
        {
            pivot = sideSign < 0
                ? FindDeepChildExact("Korshun_TorpedoLauncher_3Tube_Left")
                : FindDeepChildExact("Korshun_TorpedoLauncher_3Tube_Right");
        }

        if (pivot == null)
        {
            pivot = sideRocket
                ? FindProjectedSide("RocketLauncher", sideSign)
                : sideHarpoon
                    ? FindProjectedSide("Harpoon", sideSign)
                    : FindProjectedSide("TorpedoLauncher", sideSign);
        }

        return TryCreateHandle(pivot, baselineYawDegrees, out handle);
    }

    public bool TryBindUtilityModule(string utilityKind, int sideSign, out CoreTacticalVisualWeaponHandle handle)
    {
        handle = default;
        if (visualRoot == null)
        {
            return false;
        }

        string normalizedKind = Normalize(utilityKind);
        string token = "Utility";
        Transform pivot = null;
        if (normalizedKind.Contains("magnet"))
        {
            token = "Magnet";
            pivot = sideSign < 0
                ? FindDeepChildExact("Korshun_CombatAux_Magnet_Left")
                    ?? FindDeepChildExact("Korshun_PortAuxPreview_Magnet_Left")
                    ?? FindDeepChildExact("Barbet_CombatSmall_Magnet_Left")
                    ?? FindDeepChildExact("Barbet_PortSmallPreview_Magnet_Left")
                : FindDeepChildExact("Korshun_CombatAux_Magnet_Right")
                    ?? FindDeepChildExact("Korshun_PortAuxPreview_Magnet_Right")
                    ?? FindDeepChildExact("Barbet_CombatSmall_Magnet_Right")
                    ?? FindDeepChildExact("Barbet_PortSmallPreview_Magnet_Right");
        }
        else if (normalizedKind.Contains("siphon"))
        {
            token = "GasSiphon";
            pivot = sideSign < 0
                ? FindDeepChildExact("Korshun_CombatAux_GasSiphon_Left")
                    ?? FindDeepChildExact("Korshun_PortAuxPreview_GasSiphon_Left")
                    ?? FindDeepChildExact("Barbet_CombatSmall_GasSiphon_Left")
                    ?? FindDeepChildExact("Barbet_PortSmallPreview_GasSiphon_Left")
                : FindDeepChildExact("Korshun_CombatAux_GasSiphon_Right")
                    ?? FindDeepChildExact("Korshun_PortAuxPreview_GasSiphon_Right")
                    ?? FindDeepChildExact("Barbet_CombatSmall_GasSiphon_Right")
                    ?? FindDeepChildExact("Barbet_PortSmallPreview_GasSiphon_Right");
        }
        else if (normalizedKind.Contains("repair"))
        {
            token = "RepairBeam";
            pivot = sideSign < 0
                ? FindDeepChildExact("Korshun_CombatAux_RepairBeam_Left")
                    ?? FindDeepChildExact("Korshun_PortAuxPreview_RepairBeam_Left")
                : FindDeepChildExact("Korshun_CombatAux_RepairBeam_Right")
                    ?? FindDeepChildExact("Korshun_PortAuxPreview_RepairBeam_Right");
        }
        else if (normalizedKind.Contains("scanner") || normalizedKind.Contains("hacker"))
        {
            token = "HackingDish";
            pivot = sideSign < 0
                ? FindDeepChildExact("Korshun_CombatAux_HackingDish_Left")
                    ?? FindDeepChildExact("Korshun_PortAuxPreview_HackingDish_Left")
                : FindDeepChildExact("Korshun_CombatAux_HackingDish_Right")
                    ?? FindDeepChildExact("Korshun_PortAuxPreview_HackingDish_Right");
        }

        if (pivot == null)
        {
            pivot = FindProjectedSide(token, sideSign);
        }

        return TryCreateHandle(pivot, sideSign < 0 ? -90f : 90f, out handle);
    }

    public static void ApplyYaw(CoreTacticalVisualWeaponHandle handle, float currentYawDegrees)
    {
        if (!handle.IsValid)
        {
            return;
        }

        float deltaYaw = Mathf.DeltaAngle(handle.baselineYawDegrees, currentYawDegrees);
        handle.pivot.localRotation = Quaternion.AngleAxis(deltaYaw, Vector3.up) * handle.initialLocalRotation;
    }

    public static float CalculateLocalYawDegrees(Transform ownerTransform, Vector3 worldDirection, float fallbackYawDegrees)
    {
        Vector3 flatDirection = worldDirection;
        flatDirection.y = 0f;
        if (flatDirection.sqrMagnitude <= 0.0001f)
        {
            return fallbackYawDegrees;
        }

        Vector3 ownerForward = ownerTransform != null ? ownerTransform.forward : Vector3.forward;
        ownerForward.y = 0f;
        if (ownerForward.sqrMagnitude <= 0.0001f)
        {
            ownerForward = Vector3.forward;
        }

        return Mathf.DeltaAngle(0f, Vector3.SignedAngle(ownerForward.normalized, flatDirection.normalized, Vector3.up));
    }

    public static void ApplyYawToward(CoreTacticalVisualWeaponHandle handle, Transform ownerTransform, Vector3 worldDirection, float fallbackYawDegrees)
    {
        ApplyYaw(handle, CalculateLocalYawDegrees(ownerTransform, worldDirection, fallbackYawDegrees));
    }

    public static Vector3 GetMuzzlePosition(CoreTacticalVisualWeaponHandle handle, Vector3 fallbackPosition, Vector3 fireDirection)
    {
        if (!handle.IsValid)
        {
            return fallbackPosition;
        }

        Vector3 direction = fireDirection.sqrMagnitude > 0.0001f ? fireDirection.normalized : handle.pivot.forward;
        if (TryGetBounds(handle, out Bounds bounds))
        {
            return GetProjectedBoundsTip(bounds, direction);
        }

        return handle.pivot.position + direction * 4f;
    }

    private bool TryCreateHandle(Transform pivot, float baselineYawDegrees, out CoreTacticalVisualWeaponHandle handle)
    {
        handle = default;
        if (pivot == null)
        {
            return false;
        }

        handle = new CoreTacticalVisualWeaponHandle
        {
            pivot = pivot,
            initialLocalRotation = pivot.localRotation,
            baselineYawDegrees = baselineYawDegrees,
            renderers = pivot.GetComponentsInChildren<Renderer>(true)
        };
        return true;
    }

    private Transform FindDeepChildExact(string objectName)
    {
        if (visualRoot == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform[] children = visualRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && string.Equals(child.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private Transform FindProjectedExtreme(string requiredToken, bool front)
    {
        CollectDeepChildrenContaining(requiredToken, scratchTransforms, excludeToken: "Base");
        if (scratchTransforms.Count == 0)
        {
            return null;
        }

        SortByForwardProjection(scratchTransforms);
        return front ? scratchTransforms[scratchTransforms.Count - 1] : scratchTransforms[0];
    }

    private Transform FindProjectedSide(string requiredToken, int sideSign)
    {
        CollectDeepChildrenContaining(requiredToken, scratchTransforms);
        if (scratchTransforms.Count == 0)
        {
            return null;
        }

        Vector3 right = owner != null ? owner.transform.right : visualRoot.right;
        Transform best = null;
        float bestScore = sideSign < 0 ? float.PositiveInfinity : float.NegativeInfinity;
        for (int i = 0; i < scratchTransforms.Count; i++)
        {
            Transform candidate = scratchTransforms[i];
            if (candidate == null) continue;
            Vector3 origin = owner != null ? owner.transform.position : visualRoot.position;
            float score = Vector3.Dot(candidate.position - origin, right);
            if ((sideSign < 0 && score < bestScore) || (sideSign >= 0 && score > bestScore))
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private void CollectDeepChildrenContaining(string requiredToken, List<Transform> results, string excludeToken = "")
    {
        results.Clear();
        if (visualRoot == null || string.IsNullOrWhiteSpace(requiredToken))
        {
            return;
        }

        string required = Normalize(requiredToken);
        string excluded = Normalize(excludeToken);
        Transform[] children = visualRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == visualRoot)
            {
                continue;
            }

            string name = Normalize(child.name);
            if (!name.Contains(required))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(excluded) && name.Contains(excluded))
            {
                continue;
            }

            results.Add(child);
        }
    }

    private void SortByForwardProjection(List<Transform> transforms)
    {
        Vector3 forward = owner != null ? owner.transform.forward : visualRoot.forward;
        transforms.Sort((a, b) => Vector3.Dot(a.position, forward).CompareTo(Vector3.Dot(b.position, forward)));
    }

    private static bool TryGetBounds(CoreTacticalVisualWeaponHandle handle, out Bounds bounds)
    {
        bounds = default;
        if (handle.renderers == null)
        {
            return false;
        }

        bool found = false;
        for (int i = 0; i < handle.renderers.Length; i++)
        {
            Renderer renderer = handle.renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    private static Vector3 GetProjectedBoundsTip(Bounds bounds, Vector3 direction)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float bestProjection = float.NegativeInfinity;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    float projection = Vector3.Dot(corner, direction);
                    if (projection > bestProjection)
                    {
                        bestProjection = projection;
                    }
                }
            }
        }

        float centerProjection = Vector3.Dot(center, direction);
        return center + direction * (bestProjection - centerProjection);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
    }
}

public static class CoreTacticalPrototypeBootstrap
{
    public const string PrototypeSceneName = "WildWindCoreTacticalPrototype";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsurePrototypeScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsurePrototypeScene();
    }

    private static void EnsurePrototypeScene()
    {
        if (SceneManager.GetActiveScene().name != PrototypeSceneName)
        {
            return;
        }

        if (Object.FindFirstObjectByType<CoreTacticalFleetController>() != null)
        {
            return;
        }

        BuildPrototypeScene();
    }

    private static void BuildPrototypeScene()
    {
        GameObject root = new GameObject("Core Tactical Prototype");
        CoreTacticalFleetController fleet = root.AddComponent<CoreTacticalFleetController>();
        fleet.commandPlaneAltitudeMeters = 80f;
        fleet.formationSpacingMeters = 430f;
        fleet.gridHalfSizeMeters = 14000f;
        fleet.gridStepMeters = 500f;

        Camera camera = CreateCamera(fleet);
        CreateLight();
        CreateGroundReference();
        CreateObstacleField(fleet.commandPlaneAltitudeMeters);

        CoreTacticalShipMotor battleship = CreatePrototypeShip(
            fleet,
            "heavy_battleship",
            "Heavy Battleship",
            new Vector3(-620f, fleet.commandPlaneAltitudeMeters, 120f),
            Quaternion.Euler(0f, 8f, 0f),
            new Vector3(62f, 20f, 350f),
            46f,
            5.4f,
            9.6f,
            9f,
            10f,
            3.2f,
            14000000f);
        ConfigureBattleshipWeaponControl(battleship);
        CreatePrototypeShip(
            fleet,
            "cruiser",
            "Cruiser",
            new Vector3(80f, fleet.commandPlaneAltitudeMeters, 60f),
            Quaternion.Euler(0f, 0f, 0f),
            new Vector3(32f, 12f, 150f),
            99f,
            12.6f,
            19.2f,
            18f,
            21f,
            9.0f,
            3200000f);
        CoreTacticalShipMotor frigate = CreatePrototypeShip(
            fleet,
            "frigate",
            "Frigate",
            new Vector3(300f, fleet.commandPlaneAltitudeMeters, -80f),
            Quaternion.Euler(0f, -10f, 0f),
            new Vector3(15f, 7f, 60f),
            460f,
            75f,
            95f,
            360f,
            90f,
            52f,
            520000f);
        ConfigureFrigateAutocannons(frigate, null);
        ConfigureBattleshipMainBattery(battleship, null);
        ConfigureBattleshipSecondaryBattery(battleship, null);
        ConfigureBattleshipMissileLauncher(battleship, null);

        CoreTacticalEnemyCruiserSpawner spawner = root.AddComponent<CoreTacticalEnemyCruiserSpawner>();
        spawner.fleet = fleet;
        spawner.playerShip = battleship;
        CoreTacticalEnemyFrigateSpawner frigateSpawner = root.AddComponent<CoreTacticalEnemyFrigateSpawner>();
        frigateSpawner.fleet = fleet;
        frigateSpawner.playerShip = battleship;

        if (camera != null)
        {
            camera.transform.position = fleet.GetFleetCenter() + new Vector3(-150f, 125f, -165f);
        }
    }

    private static Camera CreateCamera(CoreTacticalFleetController fleet)
    {
        GameObject cameraObject = new GameObject("Core Tactical Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.fieldOfView = 42f;
        camera.nearClipPlane = 0.2f;
        camera.farClipPlane = 60000f;
        camera.clearFlags = CameraClearFlags.Skybox;
        if (fleet != null)
        {
            fleet.SetInputCamera(camera);
        }

        CoreTacticalCameraRig rig = cameraObject.AddComponent<CoreTacticalCameraRig>();
        rig.fleet = fleet;
        rig.targetCamera = camera;
        return camera;
    }

    private static void CreateLight()
    {
        GameObject lightObject = new GameObject("Core Tactical Sun");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.color = new Color(1f, 0.94f, 0.84f, 1f);
        lightObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
    }

    private static void CreateGroundReference()
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "Storm Floor Reference";
        plane.transform.position = Vector3.zero;
        plane.transform.localScale = new Vector3(300f, 1f, 300f);
        Collider collider = plane.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        Renderer renderer = plane.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateRuntimeMaterial(new Color(0.08f, 0.10f, 0.11f, 1f));
        }
    }

    private static void CreateObstacleField(float altitudeMeters)
    {
        Material obstacleMaterial = CreateRuntimeMaterial(new Color(0.35f, 0.38f, 0.42f, 1f));
        CreateBoxObstacle(
            "Square Obstacle - North Gate",
            new Vector3(-90f, altitudeMeters, 250f),
            new Vector3(130f, 90f, 130f),
            obstacleMaterial);
        CreateBoxObstacle(
            "Square Obstacle - West Block",
            new Vector3(-430f, altitudeMeters, -170f),
            new Vector3(170f, 105f, 120f),
            obstacleMaterial);
        CreateCylinderObstacle(
            "Round Obstacle - Center Stack",
            new Vector3(-90f, altitudeMeters, -120f),
            95f,
            95f,
            obstacleMaterial);
        CreateCylinderObstacle(
            "Round Obstacle - East Stack",
            new Vector3(310f, altitudeMeters, 170f),
            75f,
            85f,
            obstacleMaterial);
    }

    private static void CreateBoxObstacle(string obstacleName, Vector3 position, Vector3 size, Material material)
    {
        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = obstacleName;
        obstacle.transform.position = position;
        obstacle.transform.localScale = size;
        obstacle.AddComponent<CoreTacticalObstacle>();
        Renderer renderer = obstacle.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static void CreateCylinderObstacle(string obstacleName, Vector3 position, float radius, float height, Material material)
    {
        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        obstacle.name = obstacleName;
        obstacle.transform.position = position;
        obstacle.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        obstacle.AddComponent<CoreTacticalObstacle>();
        Renderer renderer = obstacle.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    public static CoreTacticalShipMotor CreatePrototypeShip(
        CoreTacticalFleetController fleet,
        string shipId,
        string displayName,
        Vector3 position,
        Quaternion rotation,
        Vector3 size,
        float maxForwardSpeedMS,
        float forwardAccelerationMS2,
        float brakingAccelerationMS2,
        float maxYawRateDegPerSecond,
        float maxReverseSpeedMS,
        float maxLateralSpeedMS,
        float massKg,
        bool registerWithFleet = true,
        Color? normalColor = null)
    {
        GameObject shipObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shipObject.name = "Core Tactical Ship - " + displayName;
        shipObject.transform.SetPositionAndRotation(position, rotation);
        shipObject.transform.localScale = size;
        Renderer renderer = shipObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateRuntimeMaterial(Color.white);
        }

        Rigidbody body = shipObject.AddComponent<Rigidbody>();
        body.useGravity = false;

        CoreTacticalShipMotor motor = shipObject.AddComponent<CoreTacticalShipMotor>();
        motor.massKg = massKg;
        motor.maxForwardSpeedMS = maxForwardSpeedMS;
        motor.maxReverseSpeedMS = maxReverseSpeedMS;
        motor.maxLateralSpeedMS = maxLateralSpeedMS;
        motor.forwardAccelerationMS2 = forwardAccelerationMS2;
        motor.lateralAccelerationMS2 = Mathf.Max(0.5f, forwardAccelerationMS2 * 0.45f);
        motor.brakingAccelerationMS2 = brakingAccelerationMS2;
        motor.arrivalRadiusMeters = Mathf.Clamp(size.z * 0.14f, 8f, 42f);
        motor.arrivalLockSpeedMS = Mathf.Max(1.5f, maxForwardSpeedMS * 0.05f);
        motor.slowdownDistanceMeters = Mathf.Max(motor.slowdownDistanceMeters, size.z * 0.8f);
        motor.maxYawRateDegPerSecond = maxYawRateDegPerSecond;
        motor.noseFirstYawReadyDeg = 95f;
        motor.noseFirstYawHardGateDeg = 170f;
        motor.obstacleAvoidanceLookAheadMeters = Mathf.Max(size.z * 0.9f, maxForwardSpeedMS * 7f);
        motor.obstacleAvoidanceMarginMeters = Mathf.Max(8f, size.x * 0.35f);
        motor.obstacleAvoidanceStrength = 1.55f;
        motor.InitializePrototypeShip(shipId, displayName, size, normalColor ?? new Color(0.16f, 0.38f, 0.86f, 1f));
        motor.SetCommand(position, rotation * Vector3.forward);

        CoreTacticalCombatant combatant = shipObject.AddComponent<CoreTacticalCombatant>();
        combatant.team = CoreTacticalCombatTeam.Friendly;
        combatant.ship = motor;

        CoreTacticalPrototypeHealth health = shipObject.AddComponent<CoreTacticalPrototypeHealth>();
        health.maxHealth = Mathf.Max(100f, massKg / 10000f);
        health.ResetHealth();

        CoreTacticalDamageProfile damageProfile = shipObject.AddComponent<CoreTacticalDamageProfile>();
        string classId = ResolveDefaultDamageClassId(shipId, size);
        damageProfile.ConfigureDefense(
            classId,
            health.maxHealth,
            CoreTacticalDamageProfile.ResolveClassBaselineResistances(classId),
            0f,
            0f);

        if (registerWithFleet && fleet != null)
        {
            fleet.RegisterShip(motor);
        }

        return motor;
    }

    private static string ResolveDefaultDamageClassId(string shipId, Vector3 size)
    {
        string id = string.IsNullOrWhiteSpace(shipId) ? "" : shipId.Trim().ToLowerInvariant();
        if (id.Contains("battleship") || size.z >= 260f) return "battleship";
        if (id.Contains("cruiser") || size.z >= 110f) return "cruiser";
        if (id.Contains("frigate") || size.z >= 38f) return "frigate";
        return "small";
    }

    public static CoreTacticalWeaponControl ConfigureFrigateAutocannonLoadout(
        CoreTacticalShipMotor frigate,
        CoreTacticalShipMotor target = null,
        CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy)
    {
        ConfigureFrigateAutocannons(frigate, target, targetTeam);
        return frigate != null ? frigate.GetComponent<CoreTacticalWeaponControl>() : null;
    }

    public static CoreTacticalWeaponControl ConfigureCruiserArtilleryLoadout(
        CoreTacticalShipMotor cruiser,
        CoreTacticalShipMotor target = null,
        CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy)
    {
        if (cruiser == null)
        {
            return null;
        }

        CoreTacticalWeaponControl weaponControl = ConfigureCruiserWeaponControl(cruiser);

        CoreTacticalMainBattery mainBattery = GetOrAddComponent<CoreTacticalMainBattery>(cruiser);
        mainBattery.owner = cruiser;
        mainBattery.target = target;
        mainBattery.targetTeam = targetTeam;
        mainBattery.reloadSeconds = 13f;
        mainBattery.maxRangeMeters = 15000f;
        mainBattery.muzzleVelocityMS = 820f;
        mainBattery.dispersionAtMaxRangeMeters = 80f;
        mainBattery.shellVisualScale = 7.2f;
        mainBattery.burstRadiusMeters = 36f;
        mainBattery.shellResistanceIgnorePercent = 70f;
        mainBattery.shellCaliberMm = 203f;
        mainBattery.shellDirectImpactFuseThresholdMeters = 0f;
        mainBattery.shellFireChancePercent = 12f;
        mainBattery.detonationMode = CoreTacticalProjectileDetonationMode.AirBurst;

        CoreTacticalSecondaryMountBattery secondaryBattery = GetOrAddComponent<CoreTacticalSecondaryMountBattery>(cruiser);
        secondaryBattery.owner = cruiser;
        secondaryBattery.target = target;
        secondaryBattery.targetTeam = targetTeam;
        secondaryBattery.maxRangeMeters = 9500f;
        secondaryBattery.suppressFireWithinRangeMeters = 0f;

        CoreTacticalMachineGunMountBattery machineGunBattery = GetOrAddComponent<CoreTacticalMachineGunMountBattery>(cruiser);
        machineGunBattery.owner = cruiser;
        machineGunBattery.target = target;
        machineGunBattery.targetTeam = targetTeam;
        machineGunBattery.radiusMeters = 1800f;
        return weaponControl;
    }

    public static CoreTacticalWeaponControl ConfigureBattleshipFullLoadout(
        CoreTacticalShipMotor battleship,
        CoreTacticalShipMotor target = null,
        CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy)
    {
        if (battleship == null)
        {
            return null;
        }

        CoreTacticalWeaponControl weaponControl = ConfigureBattleshipWeaponControl(battleship);
        ConfigureBattleshipMainBattery(battleship, target, targetTeam);
        ConfigureBattleshipSecondaryBattery(battleship, target, targetTeam);
        ConfigureBattleshipMissileLauncher(battleship, target, targetTeam);
        return weaponControl;
    }

    public static void RetargetLoadout(
        CoreTacticalShipMotor owner,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam)
    {
        if (owner == null)
        {
            return;
        }

        CoreTacticalMainBattery[] mainBatteries = owner.GetComponents<CoreTacticalMainBattery>();
        for (int i = 0; i < mainBatteries.Length; i++)
        {
            mainBatteries[i].target = target;
            mainBatteries[i].targetTeam = targetTeam;
        }

        CoreTacticalSecondaryMountBattery[] secondaryBatteries = owner.GetComponents<CoreTacticalSecondaryMountBattery>();
        for (int i = 0; i < secondaryBatteries.Length; i++)
        {
            secondaryBatteries[i].target = target;
            secondaryBatteries[i].targetTeam = targetTeam;
        }

        CoreTacticalMachineGunMountBattery[] machineGunBatteries = owner.GetComponents<CoreTacticalMachineGunMountBattery>();
        for (int i = 0; i < machineGunBatteries.Length; i++)
        {
            machineGunBatteries[i].target = target;
            machineGunBatteries[i].targetTeam = targetTeam;
        }

        CoreTacticalFrigateAutocannonBattery[] autocannonBatteries = owner.GetComponents<CoreTacticalFrigateAutocannonBattery>();
        for (int i = 0; i < autocannonBatteries.Length; i++)
        {
            autocannonBatteries[i].target = target;
            autocannonBatteries[i].targetTeam = targetTeam;
        }

        CoreTacticalMissileLauncher[] missileLaunchers = owner.GetComponents<CoreTacticalMissileLauncher>();
        for (int i = 0; i < missileLaunchers.Length; i++)
        {
            missileLaunchers[i].target = target;
            missileLaunchers[i].targetTeam = targetTeam;
        }
    }

    private static CoreTacticalWeaponControl ConfigureBattleshipWeaponControl(CoreTacticalShipMotor battleship)
    {
        if (battleship == null)
        {
            return null;
        }

        CoreTacticalWeaponControl weaponControl = battleship.GetComponent<CoreTacticalWeaponControl>();
        if (weaponControl == null)
        {
            weaponControl = battleship.gameObject.AddComponent<CoreTacticalWeaponControl>();
        }

        if (battleship.GetComponent<CoreTacticalPriorityTargetControl>() == null)
        {
            battleship.gameObject.AddComponent<CoreTacticalPriorityTargetControl>();
        }

        weaponControl.ConfigureBattleshipDefaults();
        return weaponControl;
    }

    private static CoreTacticalWeaponControl ConfigureFrigateWeaponControl(CoreTacticalShipMotor frigate)
    {
        if (frigate == null)
        {
            return null;
        }

        CoreTacticalWeaponControl weaponControl = frigate.GetComponent<CoreTacticalWeaponControl>();
        if (weaponControl == null)
        {
            weaponControl = frigate.gameObject.AddComponent<CoreTacticalWeaponControl>();
        }

        if (frigate.GetComponent<CoreTacticalPriorityTargetControl>() == null)
        {
            frigate.gameObject.AddComponent<CoreTacticalPriorityTargetControl>();
        }

        weaponControl.ConfigureFrigateDefaults();
        return weaponControl;
    }

    private static CoreTacticalWeaponControl ConfigureCruiserWeaponControl(CoreTacticalShipMotor cruiser)
    {
        if (cruiser == null)
        {
            return null;
        }

        CoreTacticalWeaponControl weaponControl = cruiser.GetComponent<CoreTacticalWeaponControl>();
        if (weaponControl == null)
        {
            weaponControl = cruiser.gameObject.AddComponent<CoreTacticalWeaponControl>();
        }

        if (cruiser.GetComponent<CoreTacticalPriorityTargetControl>() == null)
        {
            cruiser.gameObject.AddComponent<CoreTacticalPriorityTargetControl>();
        }

        weaponControl.ConfigureArtilleryCruiserDefaults();
        return weaponControl;
    }

    private static void ConfigureFrigateAutocannons(
        CoreTacticalShipMotor frigate,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy)
    {
        if (frigate == null)
        {
            return;
        }

        ConfigureFrigateWeaponControl(frigate);
        CoreTacticalFrigateAutocannonBattery battery = GetOrAddComponent<CoreTacticalFrigateAutocannonBattery>(frigate);
        battery.owner = frigate;
        battery.target = target;
        battery.targetTeam = targetTeam;
        battery.shellResistanceIgnorePercent = 45f;
        battery.shellCaliberMm = 57f;
        battery.shellDirectImpactFuseThresholdMeters = 4f;
        battery.shellFireChancePercent = 0f;
        battery.detonationMode = CoreTacticalProjectileDetonationMode.DirectImpact;
    }

    private static void ConfigureBattleshipSecondaryBattery(
        CoreTacticalShipMotor battleship,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy)
    {
        if (battleship == null)
        {
            return;
        }

        ConfigureBattleshipWeaponControl(battleship);
        CoreTacticalSecondaryMountBattery battery = GetOrAddComponent<CoreTacticalSecondaryMountBattery>(battleship);
        battery.owner = battleship;
        battery.target = target;
        battery.targetTeam = targetTeam;
        battery.suppressFireWithinRangeMeters = 0f;
    }

    private static void ConfigureBattleshipMainBattery(
        CoreTacticalShipMotor battleship,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy)
    {
        if (battleship == null)
        {
            return;
        }

        ConfigureBattleshipWeaponControl(battleship);
        CoreTacticalMainBattery battery = GetOrAddComponent<CoreTacticalMainBattery>(battleship);
        battery.owner = battleship;
        battery.target = target;
        battery.targetTeam = targetTeam;
        battery.shellResistanceIgnorePercent = 406f;
        battery.shellCaliberMm = 406f;
        battery.shellDirectImpactFuseThresholdMeters = 28f;
        battery.shellFireChancePercent = 0f;
        battery.detonationMode = CoreTacticalProjectileDetonationMode.DirectImpact;
    }

    private static void ConfigureBattleshipMissileLauncher(
        CoreTacticalShipMotor battleship,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy)
    {
        if (battleship == null)
        {
            return;
        }

        ConfigureBattleshipWeaponControl(battleship);
        CoreTacticalMissileLauncher[] existingLaunchers = battleship.GetComponents<CoreTacticalMissileLauncher>();
        if (existingLaunchers.Length == 0)
        {
            AddMissileLauncher(battleship, target, targetTeam, -1, 0.23f, CoreTacticalMissileGuidanceMode.PredictedIntercept, 0.75f);
            AddMissileLauncher(battleship, target, targetTeam, -1, -0.23f, CoreTacticalMissileGuidanceMode.DirectChase, 1.75f);
            AddMissileLauncher(battleship, target, targetTeam, 1, 0.23f, CoreTacticalMissileGuidanceMode.PredictedIntercept, 1.25f);
            AddMissileLauncher(battleship, target, targetTeam, 1, -0.23f, CoreTacticalMissileGuidanceMode.DirectChase, 2.25f);
        }
        else
        {
            for (int i = 0; i < existingLaunchers.Length; i++)
            {
                existingLaunchers[i].target = target;
                existingLaunchers[i].targetTeam = targetTeam;
            }
        }

        ConfigureBattleshipMachineGunAura(battleship, target, targetTeam);
    }

    private static void AddMissileLauncher(
        CoreTacticalShipMotor battleship,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam,
        int sideSign,
        float localZScale,
        CoreTacticalMissileGuidanceMode guidanceMode,
        float initialDelaySeconds)
    {
        CoreTacticalMissileLauncher launcher = battleship.gameObject.AddComponent<CoreTacticalMissileLauncher>();
        launcher.owner = battleship;
        launcher.target = target;
        launcher.targetTeam = targetTeam;
        launcher.sideSign = sideSign;
        launcher.localZScale = localZScale;
        launcher.missileName = guidanceMode == CoreTacticalMissileGuidanceMode.PredictedIntercept
            ? "Core Tactical Smart Missile"
            : "Core Tactical Dumb Missile";
        launcher.guidanceMode = guidanceMode;
        launcher.initialLaunchDelaySeconds = initialDelaySeconds;
        launcher.missileColor = guidanceMode == CoreTacticalMissileGuidanceMode.PredictedIntercept
            ? new Color(0.72f, 0.95f, 1f, 1f)
            : new Color(1f, 0.18f, 0.12f, 1f);
        launcher.trailColor = guidanceMode == CoreTacticalMissileGuidanceMode.PredictedIntercept
            ? new Color(0.48f, 0.66f, 0.72f, 1f)
            : new Color(0.78f, 0.24f, 0.18f, 1f);
    }

    private static void ConfigureBattleshipMachineGunAura(
        CoreTacticalShipMotor battleship,
        CoreTacticalShipMotor target,
        CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy)
    {
        if (battleship == null)
        {
            return;
        }

        ConfigureBattleshipWeaponControl(battleship);
        CoreTacticalMachineGunMountBattery aura = GetOrAddComponent<CoreTacticalMachineGunMountBattery>(battleship);
        aura.owner = battleship;
        aura.target = target;
        aura.targetTeam = targetTeam;
    }

    private static T GetOrAddComponent<T>(CoreTacticalShipMotor ship) where T : Component
    {
        if (ship == null)
        {
            return null;
        }

        T component = ship.GetComponent<T>();
        return component != null ? component : ship.gameObject.AddComponent<T>();
    }

    private static Material CreateRuntimeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        material.color = color;
        return material;
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalObstacle : MonoBehaviour
{
}

public enum CoreTacticalCombatTeam
{
    Friendly,
    Enemy
}

[DisallowMultipleComponent]
public sealed class CoreTacticalCombatant : MonoBehaviour
{
    public CoreTacticalCombatTeam team = CoreTacticalCombatTeam.Friendly;
    public CoreTacticalShipMotor ship;

    private CoreTacticalPrototypeHealth health;

    public bool IsAlive
    {
        get
        {
            ship ??= GetComponent<CoreTacticalShipMotor>();
            health ??= GetComponent<CoreTacticalPrototypeHealth>();
            return ship != null && (health == null || health.currentHealth > 0f);
        }
    }

    private void Awake()
    {
        ship ??= GetComponent<CoreTacticalShipMotor>();
        health = GetComponent<CoreTacticalPrototypeHealth>();
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalPriorityTargetControl : MonoBehaviour
{
    public CoreTacticalShipMotor priorityTarget;

    public bool TryGetPriorityTarget(CoreTacticalCombatTeam targetTeam, out CoreTacticalShipMotor target)
    {
        target = null;
        if (!IsValidPriorityTarget(priorityTarget, targetTeam))
        {
            priorityTarget = null;
            return false;
        }

        target = priorityTarget;
        return true;
    }

    public void SetPriorityTarget(CoreTacticalShipMotor target)
    {
        priorityTarget = target;
    }

    public void ClearPriorityTarget()
    {
        priorityTarget = null;
    }

    public static bool IsValidPriorityTarget(CoreTacticalShipMotor candidate, CoreTacticalCombatTeam targetTeam)
    {
        return CoreTacticalOreTargetingRules.IsValidExplicitTarget(candidate, targetTeam);
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalWeaponTargetCoordinator : MonoBehaviour
{
    public CoreTacticalShipMotor owner;
    public CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy;
    public float retargetIntervalSeconds = 0.2f;
    public float maxTargetRangeMeters = 22000f;

    private CoreTacticalShipMotor currentTarget;
    private float nextRetargetTime;

    private void Update()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        if (owner == null)
        {
            return;
        }

        if (Time.time < nextRetargetTime)
        {
            return;
        }

        nextRetargetTime = Time.time + Mathf.Max(0.03f, retargetIntervalSeconds);
        CoreTacticalShipMotor target = FindBestTarget();
        if (target != currentTarget)
        {
            currentTarget = target;
            ApplyTargetToWeapons(target);
        }
    }

    private CoreTacticalShipMotor FindBestTarget()
    {
        CoreTacticalCombatant[] combatants = FindObjectsByType<CoreTacticalCombatant>(FindObjectsSortMode.None);
        CoreTacticalShipMotor bestTarget = null;
        float bestDistanceSqr = Mathf.Max(1f, maxTargetRangeMeters) * Mathf.Max(1f, maxTargetRangeMeters);
        Vector3 ownerPosition = owner.transform.position;
        for (int i = 0; i < combatants.Length; i++)
        {
            CoreTacticalCombatant combatant = combatants[i];
            if (!CoreTacticalOreTargetingRules.IsAutomaticCombatTarget(combatant, targetTeam) || combatant.ship == owner)
            {
                continue;
            }

            float distanceSqr = (combatant.ship.transform.position - ownerPosition).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestTarget = combatant.ship;
            }
        }

        return bestTarget;
    }

    private void ApplyTargetToWeapons(CoreTacticalShipMotor target)
    {
        CoreTacticalMainBattery[] mainBatteries = owner.GetComponents<CoreTacticalMainBattery>();
        for (int i = 0; i < mainBatteries.Length; i++)
        {
            mainBatteries[i].target = target;
        }

        CoreTacticalSecondaryMountBattery[] secondaryBatteries = owner.GetComponents<CoreTacticalSecondaryMountBattery>();
        for (int i = 0; i < secondaryBatteries.Length; i++)
        {
            secondaryBatteries[i].target = target;
        }

        CoreTacticalMachineGunMountBattery[] machineGunBatteries = owner.GetComponents<CoreTacticalMachineGunMountBattery>();
        for (int i = 0; i < machineGunBatteries.Length; i++)
        {
            machineGunBatteries[i].target = target;
        }

        CoreTacticalFrigateAutocannonBattery[] autocannonBatteries = owner.GetComponents<CoreTacticalFrigateAutocannonBattery>();
        for (int i = 0; i < autocannonBatteries.Length; i++)
        {
            autocannonBatteries[i].target = target;
        }

        CoreTacticalMissileLauncher[] missileLaunchers = owner.GetComponents<CoreTacticalMissileLauncher>();
        for (int i = 0; i < missileLaunchers.Length; i++)
        {
            missileLaunchers[i].target = target;
        }
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalEnemyCruiserSpawner : MonoBehaviour
{
    public CoreTacticalFleetController fleet;
    public CoreTacticalShipMotor playerShip;
    public int initialSpawnCount = 4;
    public int maxAliveCruisers = 10;
    public float spawnIntervalSeconds = 6f;
    public float spawnRadiusMeters = 9000f;
    public float spawnJitterMeters = 600f;

    private int nextCruiserIndex;
    private float nextSpawnTime;

    private void Start()
    {
        for (int i = 0; i < initialSpawnCount; i++)
        {
            SpawnCruiser();
        }

        nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnIntervalSeconds);
    }

    private void Update()
    {
        if (playerShip == null || fleet == null)
        {
            return;
        }

        if (CountAliveEnemyCruisers() >= Mathf.Max(0, maxAliveCruisers) || Time.time < nextSpawnTime)
        {
            return;
        }

        SpawnCruiser();
        nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnIntervalSeconds);
    }

    private void SpawnCruiser()
    {
        if (playerShip == null || fleet == null)
        {
            return;
        }

        float angle = nextCruiserIndex * 57.5f * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
        Vector2 jitter = Random.insideUnitCircle * Mathf.Max(0f, spawnJitterMeters);
        Vector3 position = playerShip.transform.position
            + direction * Mathf.Max(1000f, spawnRadiusMeters)
            + new Vector3(jitter.x, 0f, jitter.y);
        position.y = fleet.CommandPlaneAltitudeMeters;

        Vector3 toPlayer = playerShip.transform.position - position;
        toPlayer.y = 0f;
        Quaternion rotation = toPlayer.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toPlayer.normalized, Vector3.up)
            : Quaternion.identity;

        int cruiserNumber = ++nextCruiserIndex;
        CoreTacticalShipMotor cruiser = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
            fleet,
            "enemy_cruiser_" + cruiserNumber,
            "Enemy Cruiser " + cruiserNumber,
            position,
            rotation,
            new Vector3(32f, 12f, 150f),
            102f,
            12.0f,
            19.2f,
            18f,
            21f,
            7.8f,
            3200000f,
            false,
            new Color(0.82f, 0.12f, 0.09f, 1f));

        CoreTacticalCombatant combatant = cruiser.GetComponent<CoreTacticalCombatant>();
        if (combatant != null)
        {
            combatant.team = CoreTacticalCombatTeam.Enemy;
            combatant.ship = cruiser;
        }

        CoreTacticalPrototypeHealth health = cruiser.GetComponent<CoreTacticalPrototypeHealth>();
        if (health != null)
        {
            health.maxHealth = 420f;
            health.ResetHealth();
        }

        CoreTacticalEnemyCruiserBrain brain = cruiser.gameObject.AddComponent<CoreTacticalEnemyCruiserBrain>();
        brain.target = playerShip;
    }

    private int CountAliveEnemyCruisers()
    {
        CoreTacticalCombatant[] combatants = FindObjectsByType<CoreTacticalCombatant>(FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < combatants.Length; i++)
        {
            CoreTacticalCombatant combatant = combatants[i];
            if (combatant != null
                && combatant.team == CoreTacticalCombatTeam.Enemy
                && combatant.IsAlive
                && combatant.GetComponent<CoreTacticalEnemyCruiserBrain>() != null)
            {
                count++;
            }
        }

        return count;
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalEnemyCruiserBrain : MonoBehaviour
{
    public CoreTacticalShipMotor target;
    public float commandRefreshIntervalSeconds = 0.75f;

    private CoreTacticalShipMotor ship;
    private float nextCommandTime;

    private void Awake()
    {
        ship = GetComponent<CoreTacticalShipMotor>();
    }

    private void Update()
    {
        if (ship == null || target == null || Time.time < nextCommandTime)
        {
            return;
        }

        nextCommandTime = Time.time + Mathf.Max(0.05f, commandRefreshIntervalSeconds);
        Vector3 targetPosition = target.transform.position;
        Vector3 approachForward = targetPosition - ship.transform.position;
        approachForward.y = 0f;
        if (approachForward.sqrMagnitude <= 0.0001f)
        {
            approachForward = ship.transform.forward;
        }

        ship.SetCommand(targetPosition, approachForward.normalized);
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalEnemyFrigateSpawner : MonoBehaviour
{
    private static readonly float[] OrbitRadiiMeters = { 1500f, 2000f, 2500f, 3000f };

    public CoreTacticalFleetController fleet;
    public CoreTacticalShipMotor playerShip;
    public int initialSpawnCount = 10;
    public int maxAliveFrigates = 30;
    public float spawnIntervalSeconds = 1.35f;
    public float spawnRadiusMeters = 5600f;
    public float spawnJitterMeters = 850f;

    private int nextFrigateIndex;
    private float nextSpawnTime;

    private void Start()
    {
        for (int i = 0; i < initialSpawnCount; i++)
        {
            SpawnFrigate();
        }

        nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnIntervalSeconds);
    }

    private void Update()
    {
        if (playerShip == null || fleet == null)
        {
            return;
        }

        if (CountAliveEnemyFrigates() >= Mathf.Max(0, maxAliveFrigates) || Time.time < nextSpawnTime)
        {
            return;
        }

        SpawnFrigate();
        nextSpawnTime = Time.time + Mathf.Max(0.1f, spawnIntervalSeconds);
    }

    private void SpawnFrigate()
    {
        if (playerShip == null || fleet == null)
        {
            return;
        }

        int frigateNumber = ++nextFrigateIndex;
        float angle = frigateNumber * 43.0f * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)).normalized;
        Vector2 jitter = Random.insideUnitCircle * Mathf.Max(0f, spawnJitterMeters);
        Vector3 position = playerShip.transform.position
            + direction * Mathf.Max(1000f, spawnRadiusMeters)
            + new Vector3(jitter.x, 0f, jitter.y);
        position.y = fleet.CommandPlaneAltitudeMeters;

        Vector3 toPlayer = playerShip.transform.position - position;
        toPlayer.y = 0f;
        Quaternion rotation = toPlayer.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toPlayer.normalized, Vector3.up)
            : Quaternion.identity;

        CoreTacticalShipMotor frigate = CoreTacticalPrototypeBootstrap.CreatePrototypeShip(
            fleet,
            "enemy_frigate_" + frigateNumber,
            "Enemy Frigate " + frigateNumber,
            position,
            rotation,
            new Vector3(15f, 7f, 60f),
            230f,
            37.5f,
            47.5f,
            180f,
            45f,
            26f,
            520000f,
            false,
            new Color(0.95f, 0.11f, 0.08f, 1f));

        CoreTacticalCombatant combatant = frigate.GetComponent<CoreTacticalCombatant>();
        if (combatant != null)
        {
            combatant.team = CoreTacticalCombatTeam.Enemy;
            combatant.ship = frigate;
        }

        CoreTacticalPrototypeHealth health = frigate.GetComponent<CoreTacticalPrototypeHealth>();
        if (health != null)
        {
            health.maxHealth = 115f;
            health.ResetHealth();
        }

        CoreTacticalEnemyFrigateOrbitBrain brain = frigate.gameObject.AddComponent<CoreTacticalEnemyFrigateOrbitBrain>();
        brain.target = playerShip;
        brain.orbitRadiusMeters = OrbitRadiiMeters[(frigateNumber - 1) % OrbitRadiiMeters.Length];
        brain.orbitDirection = frigateNumber % 2 == 0 ? -1f : 1f;
        brain.commandRefreshIntervalSeconds = Random.Range(0.18f, 0.34f);
        brain.orbitLeadDegrees = Random.Range(20f, 38f);
    }

    private int CountAliveEnemyFrigates()
    {
        CoreTacticalCombatant[] combatants = FindObjectsByType<CoreTacticalCombatant>(FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < combatants.Length; i++)
        {
            CoreTacticalCombatant combatant = combatants[i];
            if (combatant != null
                && combatant.team == CoreTacticalCombatTeam.Enemy
                && combatant.IsAlive
                && combatant.GetComponent<CoreTacticalEnemyFrigateOrbitBrain>() != null)
            {
                count++;
            }
        }

        return count;
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalEnemyFrigateOrbitBrain : MonoBehaviour
{
    public CoreTacticalShipMotor target;
    public float orbitRadiusMeters = 2000f;
    public float orbitDirection = 1f;
    public float orbitLeadDegrees = 28f;
    public float commandRefreshIntervalSeconds = 0.25f;

    private CoreTacticalShipMotor ship;
    private float nextCommandTime;

    private void Awake()
    {
        ship = GetComponent<CoreTacticalShipMotor>();
        orbitDirection = orbitDirection < 0f ? -1f : 1f;
    }

    private void Update()
    {
        if (ship == null || target == null || Time.time < nextCommandTime)
        {
            return;
        }

        nextCommandTime = Time.time + Mathf.Max(0.05f, commandRefreshIntervalSeconds);
        Vector3 center = target.transform.position;
        Vector3 radial = ship.transform.position - center;
        radial.y = 0f;
        if (radial.sqrMagnitude <= 0.0001f)
        {
            radial = ship.transform.right;
            radial.y = 0f;
        }

        radial.Normalize();
        float safeRadius = Mathf.Max(300f, orbitRadiusMeters);
        float distance = GetFlatDistance(ship.transform.position, center);
        float radiusError = distance - safeRadius;
        float leadDegrees = Mathf.Clamp(orbitLeadDegrees + Mathf.Abs(radiusError) * 0.004f, 12f, 70f);
        Vector3 commandRadial = RotateFlat(radial, orbitDirection * leadDegrees);
        Vector3 commandPosition = center + commandRadial * safeRadius;
        commandPosition.y = center.y;

        Vector3 tangent = new Vector3(-commandRadial.z, 0f, commandRadial.x) * orbitDirection;
        if (tangent.sqrMagnitude <= 0.0001f)
        {
            tangent = ship.transform.forward;
            tangent.y = 0f;
        }

        ship.SetCommand(commandPosition, tangent.normalized);
    }

    private static Vector3 RotateFlat(Vector3 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);
        return new Vector3(
            vector.x * cos - vector.z * sin,
            0f,
            vector.x * sin + vector.z * cos).normalized;
    }

    private static float GetFlatDistance(Vector3 first, Vector3 second)
    {
        Vector3 delta = second - first;
        delta.y = 0f;
        return delta.magnitude;
    }
}

public static class CoreTacticalWeaponOcclusion
{
    private const int MaxHits = 32;
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[MaxHits];

    public static bool HasObstacleBetween(Vector3 from, Vector3 to, float radiusMeters)
    {
        return SegmentHitsObstacle(from, to, radiusMeters, out _);
    }

    public static bool SegmentHitsObstacle(Vector3 from, Vector3 to, float radiusMeters, out Vector3 hitPosition)
    {
        hitPosition = to;
        Vector3 segment = to - from;
        float distance = segment.magnitude;
        if (distance <= 0.001f)
        {
            return false;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            from,
            Mathf.Max(0.01f, radiusMeters),
            segment / distance,
            HitBuffer,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        return TryGetNearestObstacleHit(hitCount, out RaycastHit nearestHit, out hitPosition);
    }

    public static bool BallisticPathHitsObstacle(
        Vector3 startPosition,
        Vector3 initialVelocity,
        float gravityMS2,
        float linearDragK,
        float flightTime,
        float radiusMeters,
        out Vector3 hitPosition)
    {
        hitPosition = startPosition;
        float clampedFlightTime = Mathf.Max(0.01f, flightTime);
        int stepCount = Mathf.Clamp(Mathf.CeilToInt(clampedFlightTime / 0.14f), 4, 120);
        Vector3 previousPosition = startPosition;
        for (int i = 1; i <= stepCount; i++)
        {
            float sampleTime = clampedFlightTime * i / stepCount;
            Vector3 nextPosition = EvaluateProjectilePosition(startPosition, initialVelocity, gravityMS2, linearDragK, sampleTime);
            if (SegmentHitsObstacle(previousPosition, nextPosition, radiusMeters, out hitPosition))
            {
                return true;
            }

            previousPosition = nextPosition;
        }

        return false;
    }

    public static bool TryGetNearestObstacleAhead(
        Vector3 origin,
        Vector3 direction,
        float radiusMeters,
        float distanceMeters,
        out RaycastHit nearestHit)
    {
        nearestHit = default;
        if (direction.sqrMagnitude <= 0.0001f || distanceMeters <= 0.001f)
        {
            return false;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.01f, radiusMeters),
            direction.normalized,
            HitBuffer,
            distanceMeters,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        Vector3 unusedPosition;
        return TryGetNearestObstacleHit(hitCount, out nearestHit, out unusedPosition);
    }

    public static bool IsObstacleCollider(Collider candidate)
    {
        return candidate != null && candidate.GetComponentInParent<CoreTacticalObstacle>() != null;
    }

    private static bool TryGetNearestObstacleHit(int hitCount, out RaycastHit nearestHit, out Vector3 hitPosition)
    {
        nearestHit = default;
        hitPosition = Vector3.zero;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = HitBuffer[i];
            if (!IsObstacleCollider(hit.collider))
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
                hitPosition = hit.point;
            }
        }

        return nearestHit.collider != null;
    }

    private static Vector3 EvaluateProjectilePosition(
        Vector3 startPosition,
        Vector3 initialVelocity,
        float gravityMS2,
        float linearDragK,
        float elapsed)
    {
        if (linearDragK <= 0.0001f)
        {
            return startPosition + initialVelocity * elapsed + 0.5f * Vector3.down * gravityMS2 * elapsed * elapsed;
        }

        Vector3 gravity = Vector3.down * gravityMS2;
        float dragTravel = (1f - Mathf.Exp(-linearDragK * Mathf.Max(0f, elapsed))) / linearDragK;
        return startPosition
            + gravity * (elapsed / linearDragK)
            + (initialVelocity - gravity / linearDragK) * dragTravel;
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalPrototypeHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public float totalDamageTaken;
    public string lastDamageSource = "";
    public float lastDamageTime;
    public bool destroyOnDeath = true;
    public bool createDeathExplosionVisual = true;

    private bool deathHandled;
    private CoreTacticalDamageProfile damageProfile;

    public void ResetHealth()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = maxHealth;
        totalDamageTaken = 0f;
        lastDamageSource = "";
        lastDamageTime = 0f;
        deathHandled = false;
        damageProfile = GetComponent<CoreTacticalDamageProfile>();
        if (damageProfile != null)
        {
            damageProfile.ResetRuntimeState();
        }
    }

    public void ApplyDamage(float amount, string source)
    {
        ApplyDamage(CoreTacticalDamageRequest.Kinetic(amount, source));
    }

    public void ApplyDamage(CoreTacticalDamageRequest request)
    {
        float amount = Mathf.Max(0f, request.damage);
        if (amount <= 0f)
        {
            return;
        }

        damageProfile ??= GetComponent<CoreTacticalDamageProfile>();
        if (damageProfile != null)
        {
            float before = currentHealth;
            CoreTacticalDamageResolution resolution = damageProfile.ResolveDamage(request);
            ApplyResolvedDamage(resolution.hullDamage, request.source);
            NotifyOreBoulderDamage(request, before - currentHealth);
            return;
        }

        float currentBefore = currentHealth;
        ApplyResolvedDamage(amount, request.source);
        NotifyOreBoulderDamage(request, currentBefore - currentHealth);
    }

    private void NotifyOreBoulderDamage(CoreTacticalDamageRequest request, float appliedHullDamage)
    {
        if (appliedHullDamage <= 0f)
        {
            return;
        }

        CoreTacticalOreBoulder boulder = GetComponent<CoreTacticalOreBoulder>();
        if (boulder != null)
        {
            boulder.NotifyAppliedDamage(request, appliedHullDamage);
        }
    }

    public void ApplyResolvedDamage(float amount, string source)
    {
        amount = Mathf.Max(0f, amount);
        if (amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        totalDamageTaken += amount;
        lastDamageSource = source;
        lastDamageTime = Time.time;
        if (currentHealth <= 0f)
        {
            HandleDeath();
        }
    }

    private void HandleDeath()
    {
        if (deathHandled)
        {
            return;
        }

        deathHandled = true;
        CoreTacticalAutomatonWreckSpawner wreckSpawner = GetComponent<CoreTacticalAutomatonWreckSpawner>();
        if (wreckSpawner != null)
        {
            wreckSpawner.SpawnWreck();
        }

        if (createDeathExplosionVisual && GetComponent<CoreTacticalOreBoulder>() == null)
        {
            CoreTacticalFlakBurstVisual.Create(transform.position, 95f);
        }

        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }
}

public enum CoreTacticalWeaponGroup
{
    MainBattery = 0,
    Secondary76mm = 1,
    Secondary152mm = 2,
    Missiles = 3,
    MachineGuns = 4,
    Autocannon30mm = 5,
    Torpedoes = 6
}

[DisallowMultipleComponent]
public sealed class CoreTacticalWeaponControl : MonoBehaviour
{
    public const int WeaponGroupCount = 7;

    [SerializeField] private bool configured;
    [SerializeField] private bool[] fireEnabled = new bool[WeaponGroupCount];
    [SerializeField] private bool[] weaponGroupActive = new bool[WeaponGroupCount];
    [SerializeField] private string[] runtimeDisplayNames = new string[WeaponGroupCount];
    [SerializeField] private string[] runtimeIcons = new string[WeaponGroupCount];
    public bool fireSuppressed;

    private void Awake()
    {
        EnsureArrays();
        if (!configured)
        {
            for (int i = 0; i < WeaponGroupCount; i++)
            {
                fireEnabled[i] = true;
            }
        }
    }

    public void ConfigureBattleshipDefaults()
    {
        if (configured)
        {
            return;
        }

        EnsureArrays();
        SetGroupActive(CoreTacticalWeaponGroup.MainBattery);
        SetGroupActive(CoreTacticalWeaponGroup.Secondary76mm);
        SetGroupActive(CoreTacticalWeaponGroup.Secondary152mm);
        SetGroupActive(CoreTacticalWeaponGroup.Missiles);
        SetGroupActive(CoreTacticalWeaponGroup.MachineGuns);
        configured = true;
    }

    public void ConfigureArtilleryCruiserDefaults()
    {
        if (configured)
        {
            return;
        }

        EnsureArrays();
        SetGroupActive(CoreTacticalWeaponGroup.MainBattery);
        SetGroupActive(CoreTacticalWeaponGroup.Secondary76mm);
        SetGroupActive(CoreTacticalWeaponGroup.Secondary152mm);
        SetGroupActive(CoreTacticalWeaponGroup.MachineGuns);
        configured = true;
    }

    public void ConfigureFrigateDefaults()
    {
        if (configured)
        {
            return;
        }

        EnsureArrays();
        SetGroupActive(CoreTacticalWeaponGroup.Autocannon30mm);
        configured = true;
    }

    public void SetRuntimeWeaponGroupActive(CoreTacticalWeaponGroup group, bool active = true)
    {
        EnsureArrays();
        int index = ToIndex(group);
        weaponGroupActive[index] = active;
        if (active)
        {
            fireEnabled[index] = true;
        }

        configured = true;
    }

    public void ClearRuntimeWeaponGroups()
    {
        EnsureArrays();
        for (int i = 0; i < WeaponGroupCount; i++)
        {
            weaponGroupActive[i] = false;
            fireEnabled[i] = true;
            runtimeDisplayNames[i] = "";
            runtimeIcons[i] = "";
        }

        configured = true;
    }

    public void SetRuntimeGroupPresentation(CoreTacticalWeaponGroup group, string displayName, string icon)
    {
        EnsureArrays();
        int index = ToIndex(group);
        runtimeDisplayNames[index] = string.IsNullOrWhiteSpace(displayName) ? "" : displayName.Trim();
        runtimeIcons[index] = string.IsNullOrWhiteSpace(icon) ? "" : icon.Trim();
    }

    public bool IsFireEnabled(CoreTacticalWeaponGroup group)
    {
        EnsureArrays();
        int index = ToIndex(group);
        return fireEnabled[index];
    }

    public void SetFireEnabled(CoreTacticalWeaponGroup group, bool value)
    {
        EnsureArrays();
        fireEnabled[ToIndex(group)] = value;
    }

    public void ToggleFireEnabled(CoreTacticalWeaponGroup group)
    {
        EnsureArrays();
        int index = ToIndex(group);
        fireEnabled[index] = !fireEnabled[index];
    }

    public bool IsWeaponGroupActive(CoreTacticalWeaponGroup group)
    {
        EnsureArrays();
        return weaponGroupActive[ToIndex(group)];
    }

    public bool CanFire(CoreTacticalWeaponGroup group)
    {
        EnsureArrays();
        int index = ToIndex(group);
        return !fireSuppressed && fireEnabled[index] && weaponGroupActive[index];
    }

    public string GetRuntimeDisplayName(CoreTacticalWeaponGroup group)
    {
        EnsureArrays();
        string value = runtimeDisplayNames[ToIndex(group)];
        return string.IsNullOrWhiteSpace(value) ? GetDisplayName(group) : value;
    }

    public string GetRuntimeIcon(CoreTacticalWeaponGroup group)
    {
        EnsureArrays();
        return runtimeIcons[ToIndex(group)];
    }

    public static string GetDisplayName(CoreTacticalWeaponGroup group)
    {
        return group switch
        {
            CoreTacticalWeaponGroup.MainBattery => "Main battery",
            CoreTacticalWeaponGroup.Secondary76mm => "PMK 76 mm",
            CoreTacticalWeaponGroup.Secondary152mm => "PMK 152 mm",
            CoreTacticalWeaponGroup.Missiles => "Missiles",
            CoreTacticalWeaponGroup.Torpedoes => "Torpedoes",
            CoreTacticalWeaponGroup.MachineGuns => "Machine guns",
            CoreTacticalWeaponGroup.Autocannon30mm => "Autocannon 33 mm",
            _ => group.ToString()
        };
    }

    private void SetGroupActive(CoreTacticalWeaponGroup group)
    {
        int index = ToIndex(group);
        weaponGroupActive[index] = true;
        fireEnabled[index] = true;
    }

    private void EnsureArrays()
    {
        if (fireEnabled == null || fireEnabled.Length != WeaponGroupCount)
        {
            bool[] previous = fireEnabled;
            fireEnabled = new bool[WeaponGroupCount];
            for (int i = 0; i < fireEnabled.Length; i++)
            {
                fireEnabled[i] = previous == null || i >= previous.Length || previous[i];
            }
        }

        if (weaponGroupActive == null || weaponGroupActive.Length != WeaponGroupCount)
        {
            bool[] previous = weaponGroupActive;
            weaponGroupActive = new bool[WeaponGroupCount];
            if (previous != null)
            {
                for (int i = 0; i < Mathf.Min(previous.Length, weaponGroupActive.Length); i++)
                {
                    weaponGroupActive[i] = previous[i];
                }
            }
        }

        if (runtimeDisplayNames == null || runtimeDisplayNames.Length != WeaponGroupCount)
        {
            string[] previous = runtimeDisplayNames;
            runtimeDisplayNames = new string[WeaponGroupCount];
            if (previous != null)
            {
                for (int i = 0; i < Mathf.Min(previous.Length, runtimeDisplayNames.Length); i++)
                {
                    runtimeDisplayNames[i] = previous[i] ?? "";
                }
            }
        }

        if (runtimeIcons == null || runtimeIcons.Length != WeaponGroupCount)
        {
            string[] previous = runtimeIcons;
            runtimeIcons = new string[WeaponGroupCount];
            if (previous != null)
            {
                for (int i = 0; i < Mathf.Min(previous.Length, runtimeIcons.Length); i++)
                {
                    runtimeIcons[i] = previous[i] ?? "";
                }
            }
        }
    }

    private static int ToIndex(CoreTacticalWeaponGroup group)
    {
        return Mathf.Clamp((int)group, 0, WeaponGroupCount - 1);
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalFrigateAutocannonBattery : MonoBehaviour
{
    private const int AimPredictionIterations = 2;
    private const float ReloadRandomSpread = 0.16f;
    private const float MountBaseHalfHeightMeters = 0.34f;
    private const float MountBaseDiameterMeters = 3.6f;
    private const float MountHousingWidthMeters = 2.6f;
    private const float MountHousingHeightMeters = 1.3f;
    private const float MountHousingLengthMeters = 3.2f;
    private const float BarrelLengthMeters = 4.8f;
    private const float BarrelWidthMeters = 0.32f;
    private const float BarrelHeightMeters = 0.24f;

    public CoreTacticalShipMotor owner;
    public CoreTacticalShipMotor target;
    public CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy;
    public float targetRefreshIntervalSeconds = 0.12f;
    public float maxRangeMeters = 4200f;
    public float targetAwarenessRangeMeters = 6400f;
    public float mountYawRateDegPerSecond = 150f;
    public float aimToleranceDegrees = 4.2f;
    public float reloadSeconds = 0.18f;
    public float muzzleVelocityMS = 780f;
    public float gravityMS2 = 9.81f;
    public float projectileLinearDragK = 0.006f;
    public float dispersionAtMaxRangeMeters = 62f;
    public float shellVisualScale = 0.72f;
    public float tracerTrailSeconds = 0.30f;
    public float burstRadiusMeters = 4.2f;
    public float shellDamage = 2.4f;
    public CoreTacticalDamageType shellDamageType = CoreTacticalDamageType.Kinetic;
    public float shellResistanceIgnorePercent = 45f;
    public float shellCaliberMm = 57f;
    public float shellDirectImpactFuseThresholdMeters = 4f;
    public float shellFireChancePercent = 0f;
    public int barrelsPerMount = 1;
    public float barrelShotSpacingSeconds = 0.08f;
    public float barrelSpacingMeters = 1.1f;
    public CoreTacticalProjectileDetonationMode detonationMode = CoreTacticalProjectileDetonationMode.AirBurst;

    private AutocannonMount[] mounts;
    private readonly List<PendingAutocannonShot> pendingShots = new List<PendingAutocannonShot>(16);
    private Material mountMaterial;
    private Material barrelMaterial;
    private Material shellMaterial;
    private Material tracerMaterial;
    private CoreTacticalWeaponControl weaponControl;
    private CoreTacticalCombatant[] targetCandidates = new CoreTacticalCombatant[0];
    private float nextTargetRefreshTime;

    private sealed class AutocannonMount
    {
        public string displayName;
        public float localZ;
        public float baseLocalYawDegrees;
        public float sectorHalfAngleDegrees;
        public float currentLocalYawDegrees;
        public float desiredLocalYawDegrees;
        public float nextReadyTime;
        public bool hasFired;
        public bool targetInsideSector;
        public bool aimed;
        public CoreTacticalShipMotor target;
        public GameObject rootObject;
        public Transform barrelPivot;
        public CoreTacticalVisualWeaponHandle visualHandle;
    }

    private struct PendingAutocannonShot
    {
        public AutocannonMount mount;
        public int barrelIndex;
        public CoreTacticalShipMotor target;
        public float fireTime;
    }

    private void Awake()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        weaponControl = ResolveWeaponControl();
    }

    private void OnDestroy()
    {
        if (mounts != null)
        {
            for (int i = 0; i < mounts.Length; i++)
            {
                if (mounts[i]?.rootObject != null)
                {
                    Destroy(mounts[i].rootObject);
                }
            }
        }

        if (mountMaterial != null) Destroy(mountMaterial);
        if (barrelMaterial != null) Destroy(barrelMaterial);
        if (shellMaterial != null) Destroy(shellMaterial);
        if (tracerMaterial != null) Destroy(tracerMaterial);
    }

    private void Update()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        if (owner == null)
        {
            return;
        }

        EnsureMounts();
        float now = Time.time;
        float deltaSeconds = Mathf.Max(0.001f, Time.deltaTime);
        RefreshTargetCandidates(now);
        RefreshMountTargets();
        UpdateMounts(deltaSeconds);
        TryFireReadyMounts(now);
        ProcessPendingShots(now);
    }

    public float GetReloadCooldownRemainingSecondsForHud()
    {
        if (mounts == null)
        {
            return 0f;
        }

        float remaining = 0f;
        float now = Time.time;
        for (int i = 0; i < mounts.Length; i++)
        {
            AutocannonMount mount = mounts[i];
            if (mount != null && mount.hasFired)
            {
                remaining = Mathf.Max(remaining, mount.nextReadyTime - now);
            }
        }

        return Mathf.Max(0f, remaining);
    }

    private void EnsureMounts()
    {
        if (mounts != null || owner == null)
        {
            return;
        }

        mountMaterial = CreateMaterial(new Color(0.18f, 0.19f, 0.19f, 1f));
        barrelMaterial = CreateMaterial(new Color(0.07f, 0.075f, 0.08f, 1f));
        shellMaterial = CreateMaterial(new Color(1f, 0.76f, 0.22f, 1f));
        tracerMaterial = CreateTransparentMaterial(new Color(0.80f, 0.86f, 0.88f, 0.30f));

        Vector3 hullSize = owner.hullSizeMeters;
        mounts = new[]
        {
            CreateMount("33 mm Fore Autocannon", hullSize.z * 0.24f, 0f, 155f, 0f),
            CreateMount("33 mm Aft Autocannon", -hullSize.z * 0.24f, 180f, 155f, reloadSeconds * 0.45f)
        };
    }

    private AutocannonMount CreateMount(
        string displayName,
        float localZ,
        float baseLocalYawDegrees,
        float sectorHalfAngleDegrees,
        float initialReadyDelaySeconds)
    {
        AutocannonMount mount = new AutocannonMount
        {
            displayName = displayName,
            localZ = localZ,
            baseLocalYawDegrees = baseLocalYawDegrees,
            sectorHalfAngleDegrees = Mathf.Clamp(sectorHalfAngleDegrees, 1f, 180f),
            currentLocalYawDegrees = baseLocalYawDegrees,
            desiredLocalYawDegrees = baseLocalYawDegrees,
            nextReadyTime = Time.time + Mathf.Max(0f, initialReadyDelaySeconds)
        };

        if (owner != null && owner.hideRuntimeWeaponVisuals)
        {
            CoreTacticalShipVisualWeaponBinding binding = owner.GetComponent<CoreTacticalShipVisualWeaponBinding>();
            if (binding != null && binding.TryBindFrigateMainMount(displayName, mount.baseLocalYawDegrees, out CoreTacticalVisualWeaponHandle visualHandle))
            {
                mount.visualHandle = visualHandle;
            }

            return mount;
        }

        mount.rootObject = new GameObject("Core Tactical " + displayName);
        mount.rootObject.transform.SetPositionAndRotation(GetMountWorldPosition(mount), GetMountWorldRotation(mount.currentLocalYawDegrees));

        GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObject.name = displayName + " Base";
        baseObject.transform.SetParent(mount.rootObject.transform, false);
        baseObject.transform.localScale = new Vector3(MountBaseDiameterMeters, MountBaseHalfHeightMeters, MountBaseDiameterMeters);
        AssignRendererMaterial(baseObject, mountMaterial);
        DestroyPrimitiveCollider(baseObject);

        GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        housing.name = displayName + " Housing";
        housing.transform.SetParent(mount.rootObject.transform, false);
        housing.transform.localPosition = new Vector3(0f, 0.92f, 0.55f);
        housing.transform.localScale = new Vector3(MountHousingWidthMeters, MountHousingHeightMeters, MountHousingLengthMeters);
        AssignRendererMaterial(housing, mountMaterial);
        DestroyPrimitiveCollider(housing);

        GameObject pivotObject = new GameObject(displayName + " Barrel Pivot");
        pivotObject.transform.SetParent(mount.rootObject.transform, false);
        pivotObject.transform.localPosition = new Vector3(0f, 1.15f, 1.45f);
        mount.barrelPivot = pivotObject.transform;

        int barrelCount = Mathf.Max(1, barrelsPerMount);
        for (int barrelIndex = 0; barrelIndex < barrelCount; barrelIndex++)
        {
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = displayName + " Barrel " + (barrelIndex + 1).ToString();
            barrel.transform.SetParent(mount.barrelPivot, false);
            barrel.transform.localPosition = new Vector3(GetBarrelOffsetMeters(barrelIndex), 0f, BarrelLengthMeters * 0.5f);
            barrel.transform.localScale = new Vector3(BarrelWidthMeters, BarrelHeightMeters, BarrelLengthMeters);
            AssignRendererMaterial(barrel, barrelMaterial);
            DestroyPrimitiveCollider(barrel);
        }

        return mount;
    }

    private void RefreshTargetCandidates(float now)
    {
        if (now < nextTargetRefreshTime)
        {
            return;
        }

        targetCandidates = FindObjectsByType<CoreTacticalCombatant>(FindObjectsSortMode.None);
        nextTargetRefreshTime = now + Mathf.Max(0.03f, targetRefreshIntervalSeconds);
    }

    private void RefreshMountTargets()
    {
        if (mounts == null)
        {
            return;
        }

        float engagementRange = Mathf.Max(1f, maxRangeMeters);
        float awarenessRange = Mathf.Max(engagementRange, targetAwarenessRangeMeters);
        for (int i = 0; i < mounts.Length; i++)
        {
            AutocannonMount mount = mounts[i];
            if (mount == null)
            {
                continue;
            }

            CoreTacticalShipMotor bestTarget = FindBestTargetForMount(mount, true, engagementRange);
            if (bestTarget == null)
            {
                bestTarget = FindBestTargetForMount(mount, false, awarenessRange);
            }

            mount.target = bestTarget;
        }
    }

    private CoreTacticalShipMotor FindBestTargetForMount(AutocannonMount mount, bool requireInsideSector, float rangeMeters)
    {
        CoreTacticalShipMotor bestTarget = null;
        float bestScore = float.PositiveInfinity;
        if (requireInsideSector)
        {
            ConsiderTargetForMount(GetPriorityTarget(), mount, true, rangeMeters, ref bestTarget, ref bestScore);
            if (bestTarget != null)
            {
                return bestTarget;
            }
        }

        if (targetCandidates != null)
        {
            for (int i = 0; i < targetCandidates.Length; i++)
            {
                CoreTacticalCombatant combatant = targetCandidates[i];
                if (!CoreTacticalOreTargetingRules.IsAutomaticCombatTarget(combatant, targetTeam))
                {
                    continue;
                }

                ConsiderTargetForMount(combatant.ship, mount, requireInsideSector, rangeMeters, ref bestTarget, ref bestScore);
            }
        }

        ConsiderTargetForMount(target, mount, requireInsideSector, rangeMeters, ref bestTarget, ref bestScore);
        return bestTarget;
    }

    private CoreTacticalShipMotor GetPriorityTarget()
    {
        CoreTacticalPriorityTargetControl priorityControl = owner != null ? owner.GetComponent<CoreTacticalPriorityTargetControl>() : null;
        return priorityControl != null && priorityControl.TryGetPriorityTarget(targetTeam, out CoreTacticalShipMotor priorityTarget)
            ? priorityTarget
            : null;
    }

    private void ConsiderTargetForMount(
        CoreTacticalShipMotor candidate,
        AutocannonMount mount,
        bool requireInsideSector,
        float rangeMeters,
        ref CoreTacticalShipMotor bestTarget,
        ref float bestScore)
    {
        if (!IsValidTarget(candidate))
        {
            return;
        }

        Vector3 mountPosition = GetMountWorldPosition(mount);
        float flatDistance = GetFlatDistance(mountPosition, candidate.transform.position);
        if (flatDistance > Mathf.Max(1f, rangeMeters))
        {
            return;
        }

        float desiredYaw = GetDesiredLocalYawDegrees(mount, candidate);
        float sectorDelta = Mathf.Abs(Mathf.DeltaAngle(mount.baseLocalYawDegrees, desiredYaw));
        bool insideSector = sectorDelta <= mount.sectorHalfAngleDegrees;
        if (requireInsideSector && !insideSector)
        {
            return;
        }

        float currentAimDelta = Mathf.Abs(Mathf.DeltaAngle(mount.currentLocalYawDegrees, desiredYaw));
        float score = flatDistance + currentAimDelta * 9f;
        if (!insideSector)
        {
            score += 12000f + sectorDelta * 55f;
        }

        if (score < bestScore)
        {
            bestScore = score;
            bestTarget = candidate;
        }
    }

    private bool IsValidTarget(CoreTacticalShipMotor candidate)
    {
        if (candidate == null || candidate == owner)
        {
            return false;
        }

        CoreTacticalCombatant combatant = candidate.GetComponent<CoreTacticalCombatant>();
        if (combatant != null && candidate != target && combatant.team != targetTeam)
        {
            return false;
        }

        CoreTacticalPrototypeHealth health = candidate.GetComponent<CoreTacticalPrototypeHealth>();
        return health == null || health.currentHealth > 0f;
    }

    private void UpdateMounts(float deltaSeconds)
    {
        if (mounts == null)
        {
            return;
        }

        for (int i = 0; i < mounts.Length; i++)
        {
            AutocannonMount mount = mounts[i];
            CoreTacticalShipMotor mountTarget = mount.target;
            if (IsValidTarget(mountTarget))
            {
                float desiredYaw = GetDesiredLocalYawDegrees(mount, mountTarget);
                mount.targetInsideSector = TryResolveSectorYaw(mount, desiredYaw, out float resolvedYaw);
                mount.desiredLocalYawDegrees = resolvedYaw;
                mount.aimed = mount.targetInsideSector
                    && Mathf.Abs(Mathf.DeltaAngle(mount.currentLocalYawDegrees, mount.desiredLocalYawDegrees)) <= Mathf.Max(0.1f, aimToleranceDegrees);
            }
            else
            {
                mount.target = null;
                mount.targetInsideSector = false;
                mount.aimed = false;
                mount.desiredLocalYawDegrees = mount.baseLocalYawDegrees;
            }

            mount.currentLocalYawDegrees = Mathf.MoveTowardsAngle(
                mount.currentLocalYawDegrees,
                mount.desiredLocalYawDegrees,
                Mathf.Max(0.01f, mountYawRateDegPerSecond) * deltaSeconds);

            ApplyMountTransform(mount);
            UpdateBarrelElevation(mount, mount.target);
        }
    }

    private void TryFireReadyMounts(float now)
    {
        if (mounts == null || !CanFireWeapon(CoreTacticalWeaponGroup.Autocannon30mm))
        {
            return;
        }

        for (int i = 0; i < mounts.Length; i++)
        {
            AutocannonMount mount = mounts[i];
            CoreTacticalShipMotor mountTarget = mount?.target;
            if (mount == null
                || !IsValidTarget(mountTarget)
                || now < mount.nextReadyTime
                || !mount.aimed
                || !IsTargetInFireRange(mount, mountTarget)
                || !HasClearIdealShot(mount, mountTarget))
            {
                continue;
            }

            ScheduleMountSalvo(mount, mountTarget, now);
            mount.nextReadyTime = now + Mathf.Max(0.03f, reloadSeconds) * Random.Range(1f - ReloadRandomSpread, 1f + ReloadRandomSpread);
            mount.hasFired = true;
        }
    }

    private void ScheduleMountSalvo(AutocannonMount mount, CoreTacticalShipMotor shotTarget, float now)
    {
        int barrelCount = Mathf.Max(1, barrelsPerMount);
        float spacing = Mathf.Max(0.01f, barrelShotSpacingSeconds);
        for (int barrelIndex = 0; barrelIndex < barrelCount; barrelIndex++)
        {
            pendingShots.Add(new PendingAutocannonShot
            {
                mount = mount,
                barrelIndex = barrelIndex,
                target = shotTarget,
                fireTime = now + spacing * barrelIndex
            });
        }
    }

    private void ProcessPendingShots(float now)
    {
        if (pendingShots.Count == 0)
        {
            return;
        }

        for (int i = pendingShots.Count - 1; i >= 0; i--)
        {
            PendingAutocannonShot shot = pendingShots[i];
            if (now < shot.fireTime)
            {
                continue;
            }

            pendingShots.RemoveAt(i);
            if (shot.mount == null
                || !IsValidTarget(shot.target)
                || !shot.mount.aimed
                || !IsTargetInFireRange(shot.mount, shot.target)
                || !CanFireWeapon(CoreTacticalWeaponGroup.Autocannon30mm))
            {
                continue;
            }

            FireGun(shot.mount, shot.barrelIndex, shot.target);
        }
    }

    private bool FireGun(AutocannonMount mount, int barrelIndex, CoreTacticalShipMotor shotTarget)
    {
        Vector3 muzzlePosition = GetMuzzlePosition(mount, barrelIndex);
        if (!TryGetAimingSolution(
                mount,
                shotTarget,
                muzzlePosition,
                out Vector3 predictedAimPoint,
                out Vector3 idealVelocity,
                out float idealFlightTime,
                out float predictedFlatDistance))
        {
            return false;
        }

        float obstacleRadius = Mathf.Max(0.35f, shellVisualScale * 0.45f);
        if (CoreTacticalWeaponOcclusion.BallisticPathHitsObstacle(
                muzzlePosition,
                idealVelocity,
                Mathf.Max(0.01f, gravityMS2),
                Mathf.Max(0f, projectileLinearDragK),
                idealFlightTime,
                obstacleRadius,
                out _))
        {
            return false;
        }

        Vector3 aimedPosition = ApplyDispersion(predictedAimPoint, predictedFlatDistance);
        if (!TrySolveBallisticVelocity(
                muzzlePosition,
                aimedPosition,
                Mathf.Max(1f, muzzleVelocityMS),
                Mathf.Max(0.01f, gravityMS2),
                Mathf.Max(0f, projectileLinearDragK),
                out Vector3 initialVelocity,
                out float flightTime))
        {
            return false;
        }

        if (!CanFireWeapon(CoreTacticalWeaponGroup.Autocannon30mm))
        {
            return false;
        }

        CoreTacticalProjectileVisual.Create(
            mount.displayName + " Shell",
            muzzlePosition,
            initialVelocity,
            Mathf.Max(0.01f, gravityMS2),
            Mathf.Max(0f, projectileLinearDragK),
            Mathf.Max(100f, maxRangeMeters),
            shellVisualScale,
            tracerTrailSeconds,
            flightTime,
            burstRadiusMeters,
            shellMaterial,
            tracerMaterial,
            shotTarget,
            shellDamage,
            mount.displayName,
            detonationMode,
            shellDamageType,
            shellResistanceIgnorePercent,
            shellCaliberMm,
            shellDirectImpactFuseThresholdMeters,
            shellFireChancePercent);
        CoreTacticalLeviathanController.NotifyArtilleryReport(muzzlePosition, maxRangeMeters, owner);
        return true;
    }

    private bool IsTargetInFireRange(AutocannonMount mount, CoreTacticalShipMotor fireTarget)
    {
        return IsValidTarget(fireTarget)
            && GetFlatDistance(GetMountWorldPosition(mount), fireTarget.transform.position) <= Mathf.Max(1f, maxRangeMeters);
    }

    private bool HasClearIdealShot(AutocannonMount mount, CoreTacticalShipMotor shotTarget)
    {
        Vector3 muzzlePosition = GetMuzzleCenterPosition(mount);
        if (!TryGetAimingSolution(mount, shotTarget, muzzlePosition, out _, out Vector3 initialVelocity, out float flightTime, out _))
        {
            return false;
        }

        return !CoreTacticalWeaponOcclusion.BallisticPathHitsObstacle(
            muzzlePosition,
            initialVelocity,
            Mathf.Max(0.01f, gravityMS2),
            Mathf.Max(0f, projectileLinearDragK),
            flightTime,
            Mathf.Max(0.35f, shellVisualScale * 0.45f),
            out _);
    }

    private bool TryGetAimingSolution(
        AutocannonMount mount,
        CoreTacticalShipMotor shotTarget,
        Vector3 muzzlePosition,
        out Vector3 predictedAimPoint,
        out Vector3 initialVelocity,
        out float flightTime,
        out float predictedFlatDistance)
    {
        predictedAimPoint = shotTarget != null ? shotTarget.transform.position : muzzlePosition + transform.forward * 100f;
        initialVelocity = Vector3.zero;
        flightTime = 0f;
        predictedFlatDistance = 0f;
        if (!IsValidTarget(shotTarget))
        {
            return false;
        }

        Vector3 targetPosition = shotTarget.transform.position;
        Vector3 targetVelocity = shotTarget.Body != null ? shotTarget.Body.linearVelocity : Vector3.zero;
        Vector3 aimPoint = targetPosition;
        bool predictiveSolutionValid = true;
        for (int i = 0; i < AimPredictionIterations; i++)
        {
            if (!TrySolveBallisticVelocity(
                    muzzlePosition,
                    aimPoint,
                    Mathf.Max(1f, muzzleVelocityMS),
                    Mathf.Max(0.01f, gravityMS2),
                    Mathf.Max(0f, projectileLinearDragK),
                    out _,
                    out float iterationFlightTime))
            {
                predictiveSolutionValid = false;
                break;
            }

            aimPoint = targetPosition + targetVelocity * iterationFlightTime;
        }

        if (predictiveSolutionValid)
        {
            predictedFlatDistance = GetFlatDistance(muzzlePosition, aimPoint);
            if (predictedFlatDistance <= Mathf.Max(1f, maxRangeMeters)
                && TrySolveBallisticVelocity(
                    muzzlePosition,
                    aimPoint,
                    Mathf.Max(1f, muzzleVelocityMS),
                    Mathf.Max(0.01f, gravityMS2),
                    Mathf.Max(0f, projectileLinearDragK),
                    out initialVelocity,
                    out flightTime))
            {
                predictedAimPoint = aimPoint;
                return true;
            }
        }

        // Autocannons should keep suppressing a maneuvering target even when the
        // lead point is temporarily outside a clean ballistic solution.
        predictedAimPoint = targetPosition;
        predictedFlatDistance = GetFlatDistance(muzzlePosition, predictedAimPoint);
        return TrySolveBallisticVelocity(
            muzzlePosition,
            predictedAimPoint,
            Mathf.Max(1f, muzzleVelocityMS),
            Mathf.Max(0.01f, gravityMS2),
            Mathf.Max(0f, projectileLinearDragK),
            out initialVelocity,
            out flightTime);
    }

    private void UpdateBarrelElevation(AutocannonMount mount, CoreTacticalShipMotor mountTarget)
    {
        if (mount?.barrelPivot == null)
        {
            return;
        }

        if (!IsValidTarget(mountTarget)
            || !TryGetAimingSolution(mount, mountTarget, GetMuzzleCenterPosition(mount), out _, out Vector3 initialVelocity, out _, out _))
        {
            mount.barrelPivot.localRotation = Quaternion.identity;
            return;
        }

        Vector3 localDirection = Quaternion.Inverse(GetMountWorldRotation(mount.currentLocalYawDegrees)) * initialVelocity.normalized;
        float elevationDegrees = Mathf.Asin(Mathf.Clamp(localDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
        mount.barrelPivot.localRotation = Quaternion.Euler(-elevationDegrees, 0f, 0f);
    }

    private Vector3 GetMountWorldPosition(AutocannonMount mount)
    {
        Vector3 hullSize = owner != null ? owner.hullSizeMeters : new Vector3(15f, 7f, 60f);
        Transform ownerTransform = owner != null ? owner.transform : transform;
        return ownerTransform.position
            + ownerTransform.up * (hullSize.y * 0.5f + 1.1f)
            + ownerTransform.forward * mount.localZ;
    }

    private Quaternion GetMountWorldRotation(float localYawDegrees)
    {
        Transform ownerTransform = owner != null ? owner.transform : transform;
        return ownerTransform.rotation * Quaternion.Euler(0f, localYawDegrees, 0f);
    }

    private void ApplyMountTransform(AutocannonMount mount)
    {
        if (mount != null && mount.visualHandle.IsValid)
        {
            CoreTacticalShipVisualWeaponBinding.ApplyYaw(mount.visualHandle, mount.currentLocalYawDegrees);
            return;
        }

        if (mount?.rootObject == null)
        {
            return;
        }

        mount.rootObject.transform.SetPositionAndRotation(GetMountWorldPosition(mount), GetMountWorldRotation(mount.currentLocalYawDegrees));
    }

    private Vector3 GetMuzzleCenterPosition(AutocannonMount mount)
    {
        Vector3 fallbackPosition = GetMountWorldPosition(mount);
        if (mount != null && mount.visualHandle.IsValid)
        {
            Vector3 fireDirection = GetMountWorldRotation(mount.currentLocalYawDegrees) * Vector3.forward;
            return CoreTacticalShipVisualWeaponBinding.GetMuzzlePosition(mount.visualHandle, fallbackPosition, fireDirection);
        }

        if (mount?.barrelPivot == null)
        {
            return fallbackPosition;
        }

        return mount.barrelPivot.TransformPoint(new Vector3(0f, 0f, BarrelLengthMeters));
    }

    private Vector3 GetMuzzlePosition(AutocannonMount mount, int barrelIndex)
    {
        Vector3 center = GetMuzzleCenterPosition(mount);
        if (mount == null || barrelsPerMount <= 1)
        {
            return center;
        }

        Vector3 localRight = GetMountWorldRotation(mount.currentLocalYawDegrees) * Vector3.right;
        return center + localRight * GetBarrelOffsetMeters(barrelIndex);
    }

    private float GetBarrelOffsetMeters(int barrelIndex)
    {
        int barrelCount = Mathf.Max(1, barrelsPerMount);
        if (barrelCount <= 1)
        {
            return 0f;
        }

        int clampedIndex = Mathf.Clamp(barrelIndex, 0, barrelCount - 1);
        float spacing = Mathf.Max(0.05f, barrelSpacingMeters);
        return (clampedIndex - (barrelCount - 1) * 0.5f) * spacing;
    }

    private float GetDesiredLocalYawDegrees(AutocannonMount mount, CoreTacticalShipMotor aimTarget)
    {
        Vector3 toTarget = aimTarget != null ? aimTarget.transform.position - GetMountWorldPosition(mount) : transform.forward;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return mount.currentLocalYawDegrees;
        }

        Vector3 ownerForward = owner != null ? owner.transform.forward : transform.forward;
        ownerForward.y = 0f;
        return Mathf.DeltaAngle(0f, Vector3.SignedAngle(ownerForward.normalized, toTarget.normalized, Vector3.up));
    }

    private static bool TryResolveSectorYaw(AutocannonMount mount, float desiredYaw, out float resolvedYaw)
    {
        float sectorDelta = Mathf.DeltaAngle(mount.baseLocalYawDegrees, desiredYaw);
        bool insideSector = Mathf.Abs(sectorDelta) <= mount.sectorHalfAngleDegrees;
        float clampedDelta = Mathf.Clamp(sectorDelta, -mount.sectorHalfAngleDegrees, mount.sectorHalfAngleDegrees);
        resolvedYaw = mount.baseLocalYawDegrees + clampedDelta;
        return insideSector;
    }

    private Vector3 ApplyDispersion(Vector3 targetPosition, float flatDistance)
    {
        float dispersionRadius = dispersionAtMaxRangeMeters * Mathf.Clamp01(flatDistance / Mathf.Max(1f, maxRangeMeters));
        Vector2 randomOffset = Random.insideUnitCircle * dispersionRadius;
        return targetPosition + new Vector3(randomOffset.x, 0f, randomOffset.y);
    }

    private bool CanFireWeapon(CoreTacticalWeaponGroup group)
    {
        CoreTacticalWeaponControl control = ResolveWeaponControl();
        return control == null || control.CanFire(group);
    }

    private CoreTacticalWeaponControl ResolveWeaponControl()
    {
        if (weaponControl != null)
        {
            return weaponControl;
        }

        if (owner != null)
        {
            weaponControl = owner.GetComponent<CoreTacticalWeaponControl>();
        }

        if (weaponControl == null)
        {
            weaponControl = GetComponent<CoreTacticalWeaponControl>();
        }

        return weaponControl;
    }

    private static float GetFlatDistance(Vector3 first, Vector3 second)
    {
        Vector3 flatDelta = second - first;
        flatDelta.y = 0f;
        return flatDelta.magnitude;
    }

    private static bool TrySolveBallisticVelocity(
        Vector3 start,
        Vector3 end,
        float speed,
        float gravity,
        float linearDragK,
        out Vector3 initialVelocity,
        out float flightTime)
    {
        initialVelocity = Vector3.zero;
        flightTime = 0f;

        Vector3 delta = end - start;
        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        float flatDistance = flatDelta.magnitude;
        if (flatDistance <= 0.01f || speed <= 0.01f)
        {
            return false;
        }

        if (linearDragK <= 0.0001f)
        {
            return TrySolveVacuumBallisticVelocity(start, end, speed, gravity, out initialVelocity, out flightTime);
        }

        float speedSquared = speed * speed;
        float previousTime = 0.02f;
        float previousError = GetLinearDragRequiredSpeedSqr(delta, previousTime, gravity, linearDragK) - speedSquared;
        bool hasBracket = false;
        float lowerTime = previousTime;
        float upperTime = previousTime;
        const float searchStepSeconds = 0.035f;
        const float maxSearchSeconds = 18f;

        for (float time = previousTime + searchStepSeconds; time <= maxSearchSeconds; time += searchStepSeconds)
        {
            float error = GetLinearDragRequiredSpeedSqr(delta, time, gravity, linearDragK) - speedSquared;
            if (previousError > 0f && error <= 0f)
            {
                lowerTime = previousTime;
                upperTime = time;
                hasBracket = true;
                break;
            }

            previousTime = time;
            previousError = error;
        }

        if (!hasBracket)
        {
            return false;
        }

        for (int i = 0; i < 30; i++)
        {
            float midTime = (lowerTime + upperTime) * 0.5f;
            float midError = GetLinearDragRequiredSpeedSqr(delta, midTime, gravity, linearDragK) - speedSquared;
            if (midError > 0f)
            {
                lowerTime = midTime;
            }
            else
            {
                upperTime = midTime;
            }
        }

        flightTime = (lowerTime + upperTime) * 0.5f;
        float dragTravel = GetLinearDragTravelFactor(flightTime, linearDragK);
        Vector3 flatVelocity = flatDelta / Mathf.Max(0.0001f, dragTravel);
        float verticalVelocity = (delta.y + gravity * flightTime / linearDragK) / Mathf.Max(0.0001f, dragTravel)
            - gravity / linearDragK;
        initialVelocity = flatVelocity + Vector3.up * verticalVelocity;
        return flightTime > 0.01f;
    }

    private static bool TrySolveVacuumBallisticVelocity(
        Vector3 start,
        Vector3 end,
        float speed,
        float gravity,
        out Vector3 initialVelocity,
        out float flightTime)
    {
        initialVelocity = Vector3.zero;
        flightTime = 0f;

        Vector3 delta = end - start;
        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        float flatDistance = flatDelta.magnitude;
        if (flatDistance <= 0.01f || speed <= 0.01f)
        {
            return false;
        }

        float speedSquared = speed * speed;
        float underRoot = speedSquared * speedSquared
            - gravity * (gravity * flatDistance * flatDistance + 2f * delta.y * speedSquared);
        if (underRoot < 0f)
        {
            return false;
        }

        float lowAngleTangent = (speedSquared - Mathf.Sqrt(underRoot)) / (gravity * flatDistance);
        float angle = Mathf.Atan(lowAngleTangent);
        Vector3 flatDirection = flatDelta / flatDistance;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        initialVelocity = flatDirection * (cos * speed) + Vector3.up * (sin * speed);
        flightTime = flatDistance / Mathf.Max(0.01f, cos * speed);
        return flightTime > 0.01f;
    }

    private static float GetLinearDragRequiredSpeedSqr(Vector3 delta, float time, float gravity, float linearDragK)
    {
        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        float dragTravel = GetLinearDragTravelFactor(time, linearDragK);
        if (dragTravel <= 0.0001f)
        {
            return float.PositiveInfinity;
        }

        Vector3 flatVelocity = flatDelta / dragTravel;
        float verticalVelocity = (delta.y + gravity * time / linearDragK) / dragTravel - gravity / linearDragK;
        return flatVelocity.sqrMagnitude + verticalVelocity * verticalVelocity;
    }

    private static float GetLinearDragTravelFactor(float time, float linearDragK)
    {
        return (1f - Mathf.Exp(-linearDragK * Mathf.Max(0f, time))) / linearDragK;
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

    private static Material CreateMaterial(Color color)
    {
        return CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(color, 1.8f);
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        return CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(color);
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalMainBattery : MonoBehaviour
{
    private const int TurretCount = 4;
    private const int GunsPerTurret = 3;
    private const int AimPredictionIterations = 3;
    private const float TurretBarbetteHalfHeightMeters = 1.8f;
    private const float TurretBarbetteDiameterMeters = 25f;
    private const float TurretHousingWidthMeters = 23f;
    private const float TurretHousingHeightMeters = 5.8f;
    private const float TurretHousingLengthMeters = 21f;
    private const float TurretBarrelLengthMeters = 28f;
    private const float TurretBarrelWidthMeters = 1.9f;
    private const float TurretBarrelHeightMeters = 1.55f;
    private const float TurretGunSpacingMeters = 4.1f;

    public CoreTacticalShipMotor owner;
    public CoreTacticalShipMotor target;
    public CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy;
    public float targetAwarenessRangeMeters = 22000f;
    public float targetRefreshIntervalSeconds = 0.2f;
    public float turretYawRateDegPerSecond = 6f;
    public float aimToleranceDegrees = 1.7f;
    public float reloadSeconds = 18f;
    public float salvoGunSpacingSeconds = 0.13f;
    public float maxRangeMeters = 18000f;
    public float muzzleVelocityMS = 860f;
    public float gravityMS2 = 9.81f;
    public float projectileLinearDragK = 0.012f;
    public float dispersionAtMaxRangeMeters = 95f;
    public float shellVisualScale = 11f;
    public float tracerTrailSeconds = 0.78f;
    public float burstRadiusMeters = 55f;
    public float shellDamage = 135f;
    public CoreTacticalDamageType shellDamageType = CoreTacticalDamageType.Explosive;
    public float shellResistanceIgnorePercent = 406f;
    public float shellCaliberMm = 406f;
    public float shellDirectImpactFuseThresholdMeters = 28f;
    public float shellFireChancePercent = 0f;
    public CoreTacticalProjectileDetonationMode detonationMode = CoreTacticalProjectileDetonationMode.AirBurst;

    private readonly List<PendingMainGunShot> pendingShots = new List<PendingMainGunShot>(24);
    private MainTurret[] turrets;
    private Material turretMaterial;
    private Material barrelMaterial;
    private Material shellMaterial;
    private Material tracerMaterial;
    private CoreTacticalWeaponControl weaponControl;
    private CoreTacticalCombatant[] targetCandidates = new CoreTacticalCombatant[0];
    private float nextTargetRefreshTime;

    private struct PendingMainGunShot
    {
        public int turretIndex;
        public int gunIndex;
        public float fireTime;
        public CoreTacticalShipMotor target;
    }

    private sealed class MainTurret
    {
        public string displayName;
        public float localZ;
        public bool forwardTurret;
        public float currentLocalYawDegrees;
        public float desiredLocalYawDegrees;
        public float nextReadyTime;
        public bool hasFired;
        public bool targetInsideSector;
        public bool aimed;
        public CoreTacticalShipMotor target;
        public GameObject rootObject;
        public Transform barrelPivot;
        public CoreTacticalVisualWeaponHandle visualHandle;
    }

    private void Awake()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        weaponControl = ResolveWeaponControl();
    }

    private void OnDestroy()
    {
        if (turrets != null)
        {
            for (int i = 0; i < turrets.Length; i++)
            {
                if (turrets[i]?.rootObject != null)
                {
                    Destroy(turrets[i].rootObject);
                }
            }
        }

        if (turretMaterial != null) Destroy(turretMaterial);
        if (barrelMaterial != null) Destroy(barrelMaterial);
        if (shellMaterial != null) Destroy(shellMaterial);
        if (tracerMaterial != null) Destroy(tracerMaterial);
    }

    private void Update()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        if (owner == null)
        {
            return;
        }

        EnsureTurrets();
        float deltaSeconds = Mathf.Max(0.001f, Time.deltaTime);
        float now = Time.time;
        RefreshTargetCandidates(now);
        RefreshTurretTargets();
        UpdateTurrets(deltaSeconds);
        TryScheduleReadySalvos(now);
        ProcessPendingShots(now);
    }

    public float GetReloadCooldownRemainingSecondsForHud()
    {
        if (turrets == null)
        {
            return 0f;
        }

        float remaining = 0f;
        float now = Time.time;
        for (int i = 0; i < turrets.Length; i++)
        {
            MainTurret turret = turrets[i];
            if (turret != null && turret.hasFired)
            {
                remaining = Mathf.Max(remaining, turret.nextReadyTime - now);
            }
        }

        return Mathf.Max(0f, remaining);
    }

    private void EnsureTurrets()
    {
        if (turrets != null)
        {
            return;
        }

        turretMaterial = CreateMaterial(new Color(0.31f, 0.32f, 0.33f, 1f));
        barrelMaterial = CreateMaterial(new Color(0.11f, 0.12f, 0.13f, 1f));
        shellMaterial = CreateMaterial(new Color(1f, 0.88f, 0.42f, 1f));
        tracerMaterial = CreateTransparentMaterial(new Color(0.88f, 0.91f, 0.92f, 0.30f));

        Vector3 hullSize = owner != null ? owner.hullSizeMeters : new Vector3(62f, 20f, 350f);
        turrets = new[]
        {
            CreateTurret("406 mm Turret A", hullSize.z * 0.33f, true, 0f, 0f),
            CreateTurret("406 mm Turret B", hullSize.z * 0.18f, true, 0f, reloadSeconds * 0.18f),
            CreateTurret("406 mm Turret X", -hullSize.z * 0.18f, false, 180f, reloadSeconds * 0.36f),
            CreateTurret("406 mm Turret Y", -hullSize.z * 0.33f, false, 180f, reloadSeconds * 0.54f)
        };
    }

    private MainTurret CreateTurret(
        string displayName,
        float localZ,
        bool forwardTurret,
        float initialLocalYawDegrees,
        float initialReadyDelaySeconds)
    {
        MainTurret turret = new MainTurret
        {
            displayName = displayName,
            localZ = localZ,
            forwardTurret = forwardTurret,
            currentLocalYawDegrees = initialLocalYawDegrees,
            desiredLocalYawDegrees = initialLocalYawDegrees,
            nextReadyTime = Time.time + Mathf.Max(0f, initialReadyDelaySeconds)
        };

        if (owner != null && owner.hideRuntimeWeaponVisuals)
        {
            CoreTacticalShipVisualWeaponBinding binding = owner.GetComponent<CoreTacticalShipVisualWeaponBinding>();
            if (binding != null && binding.TryBindCapitalMainTurret(displayName, initialLocalYawDegrees, out CoreTacticalVisualWeaponHandle visualHandle))
            {
                turret.visualHandle = visualHandle;
            }

            return turret;
        }

        turret.rootObject = new GameObject("Core Tactical " + displayName);
        turret.rootObject.transform.SetPositionAndRotation(
            GetTurretWorldPosition(turret),
            GetTurretWorldRotation(turret.currentLocalYawDegrees));

        GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObject.name = displayName + " Barbette";
        baseObject.transform.SetParent(turret.rootObject.transform, false);
        baseObject.transform.localPosition = Vector3.zero;
        baseObject.transform.localScale = new Vector3(
            TurretBarbetteDiameterMeters,
            TurretBarbetteHalfHeightMeters,
            TurretBarbetteDiameterMeters);
        AssignRendererMaterial(baseObject, turretMaterial);
        DestroyPrimitiveCollider(baseObject);

        GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        housing.name = displayName + " Housing";
        housing.transform.SetParent(turret.rootObject.transform, false);
        housing.transform.localPosition = new Vector3(0f, 3.7f, 2.3f);
        housing.transform.localScale = new Vector3(
            TurretHousingWidthMeters,
            TurretHousingHeightMeters,
            TurretHousingLengthMeters);
        AssignRendererMaterial(housing, turretMaterial);
        DestroyPrimitiveCollider(housing);

        GameObject pivotObject = new GameObject(displayName + " Barrel Pivot");
        pivotObject.transform.SetParent(turret.rootObject.transform, false);
        pivotObject.transform.localPosition = new Vector3(0f, 5.6f, 7.2f);
        pivotObject.transform.localRotation = Quaternion.identity;
        turret.barrelPivot = pivotObject.transform;

        for (int i = 0; i < GunsPerTurret; i++)
        {
            float xOffset = GetGunLateralOffset(i);
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = displayName + " Barrel " + (i + 1);
            barrel.transform.SetParent(turret.barrelPivot, false);
            barrel.transform.localPosition = new Vector3(xOffset, 0f, GetBarrelLengthMeters() * 0.5f);
            barrel.transform.localScale = new Vector3(
                TurretBarrelWidthMeters,
                TurretBarrelHeightMeters,
                GetBarrelLengthMeters());
            AssignRendererMaterial(barrel, barrelMaterial);
            DestroyPrimitiveCollider(barrel);
        }

        return turret;
    }

    private void RefreshTargetCandidates(float now)
    {
        if (now < nextTargetRefreshTime)
        {
            return;
        }

        targetCandidates = FindObjectsByType<CoreTacticalCombatant>(FindObjectsSortMode.None);
        nextTargetRefreshTime = now + Mathf.Max(0.03f, targetRefreshIntervalSeconds);
    }

    private void RefreshTurretTargets()
    {
        if (turrets == null)
        {
            return;
        }

        float engagementRange = Mathf.Max(1f, maxRangeMeters);
        float awarenessRange = Mathf.Max(engagementRange, targetAwarenessRangeMeters);
        for (int i = 0; i < turrets.Length; i++)
        {
            MainTurret turret = turrets[i];
            if (turret == null)
            {
                continue;
            }

            CoreTacticalShipMotor bestTarget = FindBestTargetForTurret(turret, true, engagementRange);
            if (bestTarget == null)
            {
                bestTarget = FindBestTargetForTurret(turret, false, awarenessRange);
            }

            turret.target = bestTarget;
        }
    }

    private CoreTacticalShipMotor FindBestTargetForTurret(MainTurret turret, bool requireInsideSector, float rangeMeters)
    {
        CoreTacticalShipMotor bestTarget = null;
        float bestScore = float.PositiveInfinity;
        if (requireInsideSector)
        {
            ConsiderTargetForTurret(GetPriorityTarget(), turret, true, rangeMeters, ref bestTarget, ref bestScore);
            if (bestTarget != null)
            {
                return bestTarget;
            }
        }

        if (targetCandidates != null)
        {
            for (int i = 0; i < targetCandidates.Length; i++)
            {
                CoreTacticalCombatant combatant = targetCandidates[i];
                if (!CoreTacticalOreTargetingRules.IsAutomaticCombatTarget(combatant, targetTeam))
                {
                    continue;
                }

                ConsiderTargetForTurret(combatant.ship, turret, requireInsideSector, rangeMeters, ref bestTarget, ref bestScore);
            }
        }

        ConsiderTargetForTurret(target, turret, requireInsideSector, rangeMeters, ref bestTarget, ref bestScore);
        return bestTarget;
    }

    private CoreTacticalShipMotor GetPriorityTarget()
    {
        CoreTacticalPriorityTargetControl priorityControl = owner != null ? owner.GetComponent<CoreTacticalPriorityTargetControl>() : null;
        return priorityControl != null && priorityControl.TryGetPriorityTarget(targetTeam, out CoreTacticalShipMotor priorityTarget)
            ? priorityTarget
            : null;
    }

    private void ConsiderTargetForTurret(
        CoreTacticalShipMotor candidate,
        MainTurret turret,
        bool requireInsideSector,
        float rangeMeters,
        ref CoreTacticalShipMotor bestTarget,
        ref float bestScore)
    {
        if (!IsValidTarget(candidate))
        {
            return;
        }

        float flatDistance = GetFlatDistance(GetTurretWorldPosition(turret), candidate.transform.position);
        if (flatDistance > Mathf.Max(1f, rangeMeters))
        {
            return;
        }

        float desiredYaw = GetDesiredLocalYawDegrees(turret, candidate);
        bool insideSector = TryResolveSectorYaw(turret, desiredYaw, out _);
        if (requireInsideSector && !insideSector)
        {
            return;
        }

        float currentAimDelta = Mathf.Abs(Mathf.DeltaAngle(turret.currentLocalYawDegrees, desiredYaw));
        float score = flatDistance + currentAimDelta * 18f;
        if (!insideSector)
        {
            score += 50000f + currentAimDelta * 130f;
        }

        if (score < bestScore)
        {
            bestScore = score;
            bestTarget = candidate;
        }
    }

    private bool IsValidTarget(CoreTacticalShipMotor candidate)
    {
        if (candidate == null || candidate == owner)
        {
            return false;
        }

        CoreTacticalCombatant combatant = candidate.GetComponent<CoreTacticalCombatant>();
        if (combatant != null && candidate != target && combatant.team != targetTeam)
        {
            return false;
        }

        CoreTacticalPrototypeHealth health = candidate.GetComponent<CoreTacticalPrototypeHealth>();
        return health == null || health.currentHealth > 0f;
    }

    private bool IsTargetInFireRange(MainTurret turret, CoreTacticalShipMotor fireTarget)
    {
        return IsValidTarget(fireTarget)
            && GetFlatDistance(GetTurretWorldPosition(turret), fireTarget.transform.position) <= Mathf.Max(1f, maxRangeMeters);
    }

    private void UpdateTurrets(float deltaSeconds)
    {
        if (turrets == null)
        {
            return;
        }

        for (int i = 0; i < turrets.Length; i++)
        {
            MainTurret turret = turrets[i];
            if (turret == null)
            {
                continue;
            }

            CoreTacticalShipMotor turretTarget = turret.target;
            if (IsValidTarget(turretTarget))
            {
                float desiredSignedYaw = GetDesiredLocalYawDegrees(turret, turretTarget);
                turret.targetInsideSector = TryResolveSectorYaw(turret, desiredSignedYaw, out float sectorYaw);
                turret.desiredLocalYawDegrees = sectorYaw;
                turret.aimed = turret.targetInsideSector
                    && Mathf.Abs(turret.currentLocalYawDegrees - turret.desiredLocalYawDegrees) <= Mathf.Max(0.1f, aimToleranceDegrees);
            }
            else
            {
                turret.target = null;
                turret.targetInsideSector = false;
                turret.aimed = false;
                turret.desiredLocalYawDegrees = turret.forwardTurret ? 0f : 180f;
            }

            turret.currentLocalYawDegrees = Mathf.MoveTowards(
                turret.currentLocalYawDegrees,
                turret.desiredLocalYawDegrees,
                Mathf.Max(0.01f, turretYawRateDegPerSecond) * deltaSeconds);

            ApplyTurretTransform(turret);
            UpdateBarrelElevation(turret, turret.target);
        }
    }

    private void TryScheduleReadySalvos(float now)
    {
        if (turrets == null)
        {
            return;
        }

        if (!CanFireWeapon(CoreTacticalWeaponGroup.MainBattery))
        {
            return;
        }

        for (int i = 0; i < turrets.Length; i++)
        {
            MainTurret turret = turrets[i];
            CoreTacticalShipMotor turretTarget = turret?.target;
            if (turret == null
                || !IsValidTarget(turretTarget)
                || now < turret.nextReadyTime
                || !turret.aimed
                || !IsTargetInFireRange(turret, turretTarget))
            {
                continue;
            }

            if (!HasClearIdealShot(turret, turretTarget))
            {
                continue;
            }

            for (int gunIndex = 0; gunIndex < GunsPerTurret; gunIndex++)
            {
                pendingShots.Add(new PendingMainGunShot
                {
                    turretIndex = i,
                    gunIndex = gunIndex,
                    fireTime = now + gunIndex * Mathf.Max(0f, salvoGunSpacingSeconds),
                    target = turretTarget
                });
            }

            turret.nextReadyTime = now + Mathf.Max(0.5f, reloadSeconds) * Random.Range(0.96f, 1.06f);
            turret.hasFired = true;
        }
    }

    private void ProcessPendingShots(float now)
    {
        for (int i = pendingShots.Count - 1; i >= 0; i--)
        {
            PendingMainGunShot shot = pendingShots[i];
            if (now < shot.fireTime)
            {
                continue;
            }

            FireGun(shot.turretIndex, shot.gunIndex, shot.target);
            pendingShots.RemoveAt(i);
        }
    }

    private void FireGun(int turretIndex, int gunIndex, CoreTacticalShipMotor shotTarget)
    {
        if (!IsValidTarget(shotTarget) || turrets == null || turretIndex < 0 || turretIndex >= turrets.Length)
        {
            return;
        }

        MainTurret turret = turrets[turretIndex];
        if (turret == null || !turret.targetInsideSector)
        {
            return;
        }

        Vector3 muzzlePosition = GetMuzzlePosition(turret, gunIndex);
        if (!TryGetAimingSolution(
                shotTarget,
                muzzlePosition,
                out Vector3 predictedAimPoint,
                out Vector3 idealInitialVelocity,
                out float idealFlightTime,
                out float predictedFlatDistance))
        {
            return;
        }

        float shellObstacleRadius = Mathf.Max(3.5f, shellVisualScale * 0.45f);
        if (CoreTacticalWeaponOcclusion.BallisticPathHitsObstacle(
                muzzlePosition,
                idealInitialVelocity,
                Mathf.Max(0.01f, gravityMS2),
                Mathf.Max(0f, projectileLinearDragK),
                idealFlightTime,
                shellObstacleRadius,
                out _))
        {
            return;
        }

        Vector3 aimedPosition = ApplyDispersion(predictedAimPoint, predictedFlatDistance);
        if (!TrySolveBallisticVelocity(
                muzzlePosition,
                aimedPosition,
                Mathf.Max(1f, muzzleVelocityMS),
                Mathf.Max(0.01f, gravityMS2),
                Mathf.Max(0f, projectileLinearDragK),
                out Vector3 initialVelocity,
                out float flightTime))
        {
            return;
        }

        if (!CanFireWeapon(CoreTacticalWeaponGroup.MainBattery))
        {
            return;
        }

        CoreTacticalProjectileVisual.Create(
            "406 mm Main Battery Shell",
            muzzlePosition,
            initialVelocity,
            Mathf.Max(0.01f, gravityMS2),
            Mathf.Max(0f, projectileLinearDragK),
            Mathf.Max(100f, maxRangeMeters),
            shellVisualScale,
            tracerTrailSeconds,
            flightTime,
            burstRadiusMeters,
            shellMaterial,
            tracerMaterial,
            shotTarget,
            Mathf.Max(0f, shellDamage),
            "406 mm main battery",
            detonationMode,
            shellDamageType,
            shellResistanceIgnorePercent,
            shellCaliberMm,
            shellDirectImpactFuseThresholdMeters,
            shellFireChancePercent);
        CoreTacticalLeviathanController.NotifyArtilleryReport(muzzlePosition, maxRangeMeters, owner);
    }

    private bool HasClearIdealShot(MainTurret turret, CoreTacticalShipMotor shotTarget)
    {
        Vector3 muzzlePosition = GetMuzzlePosition(turret, 1);
        if (!TryGetAimingSolution(shotTarget, muzzlePosition, out _, out Vector3 initialVelocity, out float flightTime, out _))
        {
            return false;
        }

        return !CoreTacticalWeaponOcclusion.BallisticPathHitsObstacle(
            muzzlePosition,
            initialVelocity,
            Mathf.Max(0.01f, gravityMS2),
            Mathf.Max(0f, projectileLinearDragK),
            flightTime,
            Mathf.Max(3.5f, shellVisualScale * 0.45f),
            out _);
    }

    private bool CanFireWeapon(CoreTacticalWeaponGroup group)
    {
        CoreTacticalWeaponControl control = ResolveWeaponControl();
        return control == null || control.CanFire(group);
    }

    private CoreTacticalWeaponControl ResolveWeaponControl()
    {
        if (weaponControl != null)
        {
            return weaponControl;
        }

        if (owner != null)
        {
            weaponControl = owner.GetComponent<CoreTacticalWeaponControl>();
        }

        if (weaponControl == null)
        {
            weaponControl = GetComponent<CoreTacticalWeaponControl>();
        }

        return weaponControl;
    }

    private void UpdateBarrelElevation(MainTurret turret, CoreTacticalShipMotor turretTarget)
    {
        if (turret == null || turret.barrelPivot == null || !IsValidTarget(turretTarget))
        {
            return;
        }

        Vector3 muzzlePosition = GetMuzzlePosition(turret, 1);
        if (!TryGetAimingSolution(turretTarget, muzzlePosition, out _, out Vector3 initialVelocity, out _, out _))
        {
            return;
        }

        Vector3 localVelocity = Quaternion.Inverse(turret.rootObject.transform.rotation) * initialVelocity.normalized;
        float elevationDegrees = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(localVelocity.y, -0.95f, 0.95f)) * Mathf.Rad2Deg, 0f, 34f);
        turret.barrelPivot.localRotation = Quaternion.Euler(-elevationDegrees, 0f, 0f);
    }

    private bool TryGetAimingSolution(
        CoreTacticalShipMotor shotTarget,
        Vector3 muzzlePosition,
        out Vector3 predictedAimPoint,
        out Vector3 initialVelocity,
        out float flightTime,
        out float predictedFlatDistance)
    {
        predictedAimPoint = shotTarget != null ? shotTarget.transform.position : muzzlePosition + transform.forward * 100f;
        initialVelocity = Vector3.zero;
        flightTime = 0f;
        predictedFlatDistance = 0f;
        if (!IsValidTarget(shotTarget))
        {
            return false;
        }

        Vector3 targetPosition = shotTarget.transform.position;
        Vector3 targetVelocity = shotTarget.Body != null ? shotTarget.Body.linearVelocity : Vector3.zero;
        Vector3 aimPoint = targetPosition;
        for (int i = 0; i < AimPredictionIterations; i++)
        {
            if (!TrySolveBallisticVelocity(
                    muzzlePosition,
                    aimPoint,
                    Mathf.Max(1f, muzzleVelocityMS),
                    Mathf.Max(0.01f, gravityMS2),
                    Mathf.Max(0f, projectileLinearDragK),
                    out _,
                    out float iterationFlightTime))
            {
                return false;
            }

            aimPoint = targetPosition + targetVelocity * iterationFlightTime;
        }

        predictedFlatDistance = GetFlatDistance(muzzlePosition, aimPoint);
        if (predictedFlatDistance > Mathf.Max(1f, maxRangeMeters))
        {
            return false;
        }

        predictedAimPoint = aimPoint;
        return TrySolveBallisticVelocity(
            muzzlePosition,
            predictedAimPoint,
            Mathf.Max(1f, muzzleVelocityMS),
            Mathf.Max(0.01f, gravityMS2),
            Mathf.Max(0f, projectileLinearDragK),
            out initialVelocity,
            out flightTime);
    }

    private void ApplyTurretTransform(MainTurret turret)
    {
        if (turret != null && turret.visualHandle.IsValid)
        {
            CoreTacticalShipVisualWeaponBinding.ApplyYaw(turret.visualHandle, turret.currentLocalYawDegrees);
            return;
        }

        if (turret?.rootObject == null)
        {
            return;
        }

        turret.rootObject.transform.SetPositionAndRotation(
            GetTurretWorldPosition(turret),
            GetTurretWorldRotation(turret.currentLocalYawDegrees));
    }

    private Vector3 GetTurretWorldPosition(MainTurret turret)
    {
        Vector3 hullSize = owner != null ? owner.hullSizeMeters : new Vector3(62f, 20f, 350f);
        Transform ownerTransform = owner != null ? owner.transform : transform;
        return ownerTransform.position
            + ownerTransform.up * (hullSize.y * 0.5f + TurretBarbetteHalfHeightMeters)
            + ownerTransform.forward * turret.localZ;
    }

    private Quaternion GetTurretWorldRotation(float localYawDegrees)
    {
        Transform ownerTransform = owner != null ? owner.transform : transform;
        return ownerTransform.rotation * Quaternion.Euler(0f, localYawDegrees, 0f);
    }

    private Vector3 GetMuzzlePosition(MainTurret turret, int gunIndex)
    {
        Vector3 fallbackPosition = GetTurretWorldPosition(turret);
        if (turret != null && turret.visualHandle.IsValid)
        {
            Vector3 fireDirection = GetTurretWorldRotation(turret.currentLocalYawDegrees) * Vector3.forward;
            return CoreTacticalShipVisualWeaponBinding.GetMuzzlePosition(turret.visualHandle, fallbackPosition, fireDirection);
        }

        if (turret?.barrelPivot == null)
        {
            return fallbackPosition;
        }

        return turret.barrelPivot.TransformPoint(new Vector3(GetGunLateralOffset(gunIndex), 0f, GetBarrelLengthMeters()));
    }

    private float GetDesiredLocalYawDegrees(MainTurret turret, CoreTacticalShipMotor aimTarget)
    {
        Vector3 turretPosition = GetTurretWorldPosition(turret);
        Vector3 toTarget = aimTarget != null ? aimTarget.transform.position - turretPosition : transform.forward;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return turret.currentLocalYawDegrees;
        }

        Vector3 ownerForward = owner != null ? owner.transform.forward : transform.forward;
        ownerForward.y = 0f;
        return Mathf.DeltaAngle(0f, Vector3.SignedAngle(ownerForward.normalized, toTarget.normalized, Vector3.up));
    }

    private static bool TryResolveSectorYaw(MainTurret turret, float desiredSignedYaw, out float resolvedYaw)
    {
        if (turret.forwardTurret)
        {
            float sectorDelta = Mathf.DeltaAngle(0f, desiredSignedYaw);
            bool insideSector = Mathf.Abs(sectorDelta) <= 135f;
            resolvedYaw = Mathf.Clamp(sectorDelta, -135f, 135f);
            return insideSector;
        }

        float rearSectorDelta = Mathf.DeltaAngle(180f, desiredSignedYaw);
        bool targetInsideRearSector = Mathf.Abs(rearSectorDelta) <= 135f;
        resolvedYaw = Mathf.Repeat(180f + Mathf.Clamp(rearSectorDelta, -135f, 135f), 360f);
        return targetInsideRearSector;
    }

    private Vector3 ApplyDispersion(Vector3 targetPosition, float flatDistance)
    {
        float dispersionRadius = dispersionAtMaxRangeMeters * Mathf.Clamp01(flatDistance / Mathf.Max(1f, maxRangeMeters));
        Vector2 randomOffset = Random.insideUnitCircle * dispersionRadius;
        return targetPosition + new Vector3(randomOffset.x, 0f, randomOffset.y);
    }

    private static float GetGunLateralOffset(int gunIndex)
    {
        return (Mathf.Clamp(gunIndex, 0, GunsPerTurret - 1) - 1) * TurretGunSpacingMeters;
    }

    private static float GetBarrelLengthMeters()
    {
        return TurretBarrelLengthMeters;
    }

    private static float GetFlatDistance(Vector3 first, Vector3 second)
    {
        Vector3 flatDelta = second - first;
        flatDelta.y = 0f;
        return flatDelta.magnitude;
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

    private static Material CreateMaterial(Color color)
    {
        return CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(color, 1.7f);
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        return CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(color);
    }

    private static bool TrySolveBallisticVelocity(
        Vector3 start,
        Vector3 end,
        float speed,
        float gravity,
        float linearDragK,
        out Vector3 initialVelocity,
        out float flightTime)
    {
        initialVelocity = Vector3.zero;
        flightTime = 0f;

        Vector3 delta = end - start;
        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        float flatDistance = flatDelta.magnitude;
        if (flatDistance <= 0.01f || speed <= 0.01f)
        {
            return false;
        }

        if (linearDragK <= 0.0001f)
        {
            return TrySolveVacuumBallisticVelocity(start, end, speed, gravity, out initialVelocity, out flightTime);
        }

        float speedSquared = speed * speed;
        float previousTime = 0.02f;
        float previousError = GetLinearDragRequiredSpeedSqr(delta, previousTime, gravity, linearDragK) - speedSquared;
        bool hasBracket = false;
        float lowerTime = previousTime;
        float upperTime = previousTime;
        const float searchStepSeconds = 0.08f;
        const float maxSearchSeconds = 90f;

        for (float time = previousTime + searchStepSeconds; time <= maxSearchSeconds; time += searchStepSeconds)
        {
            float error = GetLinearDragRequiredSpeedSqr(delta, time, gravity, linearDragK) - speedSquared;
            if (previousError > 0f && error <= 0f)
            {
                lowerTime = previousTime;
                upperTime = time;
                hasBracket = true;
                break;
            }

            previousTime = time;
            previousError = error;
        }

        if (!hasBracket)
        {
            return false;
        }

        for (int i = 0; i < 36; i++)
        {
            float midTime = (lowerTime + upperTime) * 0.5f;
            float midError = GetLinearDragRequiredSpeedSqr(delta, midTime, gravity, linearDragK) - speedSquared;
            if (midError > 0f)
            {
                lowerTime = midTime;
            }
            else
            {
                upperTime = midTime;
            }
        }

        flightTime = (lowerTime + upperTime) * 0.5f;
        float dragTravel = GetLinearDragTravelFactor(flightTime, linearDragK);
        Vector3 flatVelocity = flatDelta / Mathf.Max(0.0001f, dragTravel);
        float verticalVelocity = (delta.y + gravity * flightTime / linearDragK) / Mathf.Max(0.0001f, dragTravel)
            - gravity / linearDragK;
        initialVelocity = flatVelocity + Vector3.up * verticalVelocity;
        return flightTime > 0.01f;
    }

    private static bool TrySolveVacuumBallisticVelocity(
        Vector3 start,
        Vector3 end,
        float speed,
        float gravity,
        out Vector3 initialVelocity,
        out float flightTime)
    {
        initialVelocity = Vector3.zero;
        flightTime = 0f;

        Vector3 delta = end - start;
        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        float flatDistance = flatDelta.magnitude;
        if (flatDistance <= 0.01f || speed <= 0.01f)
        {
            return false;
        }

        float speedSquared = speed * speed;
        float underRoot = speedSquared * speedSquared
            - gravity * (gravity * flatDistance * flatDistance + 2f * delta.y * speedSquared);
        if (underRoot < 0f)
        {
            return false;
        }

        float lowAngleTangent = (speedSquared - Mathf.Sqrt(underRoot)) / (gravity * flatDistance);
        float angle = Mathf.Atan(lowAngleTangent);
        Vector3 flatDirection = flatDelta / flatDistance;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        initialVelocity = flatDirection * (cos * speed) + Vector3.up * (sin * speed);
        flightTime = flatDistance / Mathf.Max(0.01f, cos * speed);
        return flightTime > 0.01f;
    }

    private static float GetLinearDragRequiredSpeedSqr(Vector3 delta, float time, float gravity, float linearDragK)
    {
        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        float dragTravel = GetLinearDragTravelFactor(time, linearDragK);
        if (dragTravel <= 0.0001f)
        {
            return float.PositiveInfinity;
        }

        Vector3 flatVelocity = flatDelta / dragTravel;
        float verticalVelocity = (delta.y + gravity * time / linearDragK) / dragTravel - gravity / linearDragK;
        return flatVelocity.sqrMagnitude + verticalVelocity * verticalVelocity;
    }

    private static float GetLinearDragTravelFactor(float time, float linearDragK)
    {
        return (1f - Mathf.Exp(-linearDragK * Mathf.Max(0f, time))) / linearDragK;
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalSecondaryMountBattery : MonoBehaviour
{
    private const int AimPredictionIterations = 3;
    private const float FireRateMultiplier = 1.35f;
    private const float ReloadRandomSpread = 0.1f;
    private const float SectorHalfAngleDegrees = 85f;

    public CoreTacticalShipMotor owner;
    public CoreTacticalShipMotor target;
    public CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy;
    public float targetAwarenessRangeMeters = 22000f;
    public float targetRefreshIntervalSeconds = 0.2f;
    public float maxRangeMeters = 12000f;
    public float suppressFireWithinRangeMeters;
    public float turretYawRateDegPerSecond = 38f;
    public float aimToleranceDegrees = 2.4f;
    public float dispersionAtMaxRangeMeters = 200f;
    public float gravityMS2 = 9.81f;
    // Same cheap drag model as the old PMK: long shots lose horizontal speed and land steeper.
    public float projectileLinearDragK = 0.025f;
    public bool include76mmMounts = true;
    public bool include152mmMounts = true;
    public float reloadSeconds76mm = 3.6f / FireRateMultiplier;
    public float reloadSeconds152mm = 7f / FireRateMultiplier;
    public float muzzleVelocity76mmMS = 520f;
    public float muzzleVelocity152mmMS = 650f;
    public float shellDamage76mm = 9f;
    public float shellDamage152mm = 34f;
    public CoreTacticalDamageType shellDamageType76mm = CoreTacticalDamageType.Explosive;
    public CoreTacticalDamageType shellDamageType152mm = CoreTacticalDamageType.Explosive;
    public float shellResistanceIgnore76mm = 40f;
    public float shellResistanceIgnore152mm = 55f;
    public float shellCaliber76mm = 76f;
    public float shellCaliber152mm = 152f;
    public float shellDirectImpactFuseThreshold76mm = 5f;
    public float shellDirectImpactFuseThreshold152mm = 11f;
    public float shellFireChance76mm = 8f;
    public float shellFireChance152mm = 12f;
    public float burstRadius76mmMeters = 10f;
    public float burstRadius152mmMeters = 20f;
    public CoreTacticalProjectileDetonationMode detonationMode = CoreTacticalProjectileDetonationMode.AirBurst;

    private readonly List<PendingShot> pendingShots = new List<PendingShot>(64);
    private SecondaryMount[] mounts;
    private Material mountMaterial76mm;
    private Material mountMaterial152mm;
    private Material barrelMaterial;
    private Material shellMaterial76mm;
    private Material shellMaterial152mm;
    private Material tracerMaterial;
    private CoreTacticalWeaponControl weaponControl;
    private CoreTacticalCombatant[] targetCandidates = new CoreTacticalCombatant[0];
    private float nextTargetRefreshTime;

    private struct PendingShot
    {
        public int mountIndex;
        public int gunIndex;
        public float fireTime;
        public CoreTacticalShipMotor target;
    }

    private sealed class SecondaryMount
    {
        public string displayName;
        public int sideSign;
        public float localX;
        public float localZ;
        public float baseLocalYawDegrees;
        public float currentLocalYawDegrees;
        public float desiredLocalYawDegrees;
        public float nextReadyTime;
        public bool hasFired;
        public float reloadSeconds;
        public float muzzleVelocityMS;
        public float shellVisualScale;
        public float burstRadiusMeters;
        public float shellDamage;
        public CoreTacticalDamageType shellDamageType;
        public float shellResistanceIgnorePercent;
        public float shellCaliberMm;
        public float shellDirectImpactFuseThresholdMeters;
        public float shellFireChancePercent;
        public CoreTacticalProjectileDetonationMode detonationMode;
        public float tracerTrailSeconds;
        public float barrelLengthMeters;
        public float gunSpacingMeters;
        public CoreTacticalWeaponGroup weaponGroup;
        public bool targetInsideSector;
        public bool aimed;
        public GameObject rootObject;
        public Transform barrelPivot;
        public Material shellMaterial;
        public Material tracerMaterial;
        public CoreTacticalShipMotor target;
    }

    private void Awake()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        weaponControl = ResolveWeaponControl();
    }

    private void OnDestroy()
    {
        if (mounts != null)
        {
            for (int i = 0; i < mounts.Length; i++)
            {
                if (mounts[i]?.rootObject != null)
                {
                    Destroy(mounts[i].rootObject);
                }
            }
        }

        if (mountMaterial76mm != null) Destroy(mountMaterial76mm);
        if (mountMaterial152mm != null) Destroy(mountMaterial152mm);
        if (barrelMaterial != null) Destroy(barrelMaterial);
        if (shellMaterial76mm != null) Destroy(shellMaterial76mm);
        if (shellMaterial152mm != null) Destroy(shellMaterial152mm);
        if (tracerMaterial != null) Destroy(tracerMaterial);
    }

    private void Update()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        if (owner == null)
        {
            return;
        }

        EnsureMounts();
        float deltaSeconds = Mathf.Max(0.001f, Time.deltaTime);
        float now = Time.time;
        RefreshTargetCandidates(now);
        RefreshMountTargets();
        UpdateMounts(deltaSeconds);
        TryScheduleReadyMounts(now);
        ProcessPendingShots(now);
    }

    public float GetReloadCooldownRemainingSecondsForHud(CoreTacticalWeaponGroup group)
    {
        if (mounts == null)
        {
            return 0f;
        }

        float remaining = 0f;
        float now = Time.time;
        for (int i = 0; i < mounts.Length; i++)
        {
            SecondaryMount mount = mounts[i];
            if (mount != null && mount.hasFired && mount.weaponGroup == group)
            {
                remaining = Mathf.Max(remaining, mount.nextReadyTime - now);
            }
        }

        return Mathf.Max(0f, remaining);
    }

    private void EnsureMounts()
    {
        if (mounts != null)
        {
            return;
        }

        mountMaterial76mm = CreateMaterial(new Color(0.22f, 0.24f, 0.25f, 1f));
        mountMaterial152mm = CreateMaterial(new Color(0.28f, 0.28f, 0.29f, 1f));
        barrelMaterial = CreateMaterial(new Color(0.09f, 0.10f, 0.105f, 1f));
        shellMaterial76mm = CreateMaterial(new Color(1f, 0.72f, 0.24f, 1f));
        shellMaterial152mm = CreateMaterial(new Color(1f, 0.94f, 0.48f, 1f));
        tracerMaterial = CreateTransparentMaterial(new Color(0.82f, 0.86f, 0.88f, 0.28f));

        List<SecondaryMount> createdMounts = new List<SecondaryMount>(24);
        if (include76mmMounts)
        {
            AddSecondaryMountSet(createdMounts, "76 mm PMK", 8, 0.64f, -0.42f, 0.42f, reloadSeconds76mm, muzzleVelocity76mmMS, 2.2f, burstRadius76mmMeters, shellDamage76mm, shellDamageType76mm, shellResistanceIgnore76mm, shellCaliber76mm, shellDirectImpactFuseThreshold76mm, shellFireChance76mm, 0.48f, 5.2f, 1.35f, CoreTacticalWeaponGroup.Secondary76mm, mountMaterial76mm, shellMaterial76mm);
        }

        if (include152mmMounts)
        {
            AddSecondaryMountSet(createdMounts, "152 mm PMK", 4, 0.73f, -0.30f, 0.30f, reloadSeconds152mm, muzzleVelocity152mmMS, 4.2f, burstRadius152mmMeters, shellDamage152mm, shellDamageType152mm, shellResistanceIgnore152mm, shellCaliber152mm, shellDirectImpactFuseThreshold152mm, shellFireChance152mm, 0.48f, 9.8f, 2.45f, CoreTacticalWeaponGroup.Secondary152mm, mountMaterial152mm, shellMaterial152mm);
        }
        mounts = createdMounts.ToArray();
    }

    private void AddSecondaryMountSet(
        List<SecondaryMount> targetMounts,
        string displayName,
        int mountsPerSide,
        float sideXScale,
        float minZScale,
        float maxZScale,
        float reloadSeconds,
        float muzzleVelocityMS,
        float shellVisualScale,
        float burstRadiusMeters,
        float shellDamage,
        CoreTacticalDamageType shellDamageType,
        float shellResistanceIgnorePercent,
        float shellCaliberMm,
        float shellDirectImpactFuseThresholdMeters,
        float shellFireChancePercent,
        float tracerTrailSeconds,
        float barrelLengthMeters,
        float gunSpacingMeters,
        CoreTacticalWeaponGroup weaponGroup,
        Material mountMaterial,
        Material shellMaterial)
    {
        Vector3 hullSize = owner != null ? owner.hullSizeMeters : new Vector3(62f, 20f, 350f);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < mountsPerSide; i++)
            {
                float t = mountsPerSide <= 1 ? 0.5f : i / (float)(mountsPerSide - 1);
                float localZ = Mathf.Lerp(hullSize.z * minZScale, hullSize.z * maxZScale, t);
                SecondaryMount mount = new SecondaryMount
                {
                    displayName = displayName + (side < 0 ? " Port " : " Starboard ") + (i + 1),
                    sideSign = side,
                    localX = side * hullSize.x * sideXScale,
                    localZ = localZ,
                    baseLocalYawDegrees = side > 0 ? 90f : -90f,
                    reloadSeconds = Mathf.Max(0.1f, reloadSeconds),
                    muzzleVelocityMS = Mathf.Max(1f, muzzleVelocityMS),
                    shellVisualScale = Mathf.Max(0.1f, shellVisualScale),
                    burstRadiusMeters = Mathf.Max(0.1f, burstRadiusMeters),
                    shellDamage = Mathf.Max(0f, shellDamage),
                    shellDamageType = shellDamageType,
                    shellResistanceIgnorePercent = Mathf.Max(0f, shellResistanceIgnorePercent),
                    shellCaliberMm = Mathf.Max(0f, shellCaliberMm),
                    shellDirectImpactFuseThresholdMeters = Mathf.Max(0f, shellDirectImpactFuseThresholdMeters),
                    shellFireChancePercent = Mathf.Max(0f, shellFireChancePercent),
                    detonationMode = detonationMode,
                    tracerTrailSeconds = Mathf.Max(0.1f, tracerTrailSeconds),
                    barrelLengthMeters = Mathf.Max(1f, barrelLengthMeters),
                    gunSpacingMeters = Mathf.Max(0.1f, gunSpacingMeters),
                    weaponGroup = weaponGroup,
                    shellMaterial = shellMaterial,
                    tracerMaterial = tracerMaterial
                };

                mount.currentLocalYawDegrees = mount.baseLocalYawDegrees;
                mount.desiredLocalYawDegrees = mount.baseLocalYawDegrees;
                mount.nextReadyTime = Time.time + Random.Range(0f, mount.reloadSeconds * 0.7f);
                CreateMountVisual(mount, mountMaterial);
                targetMounts.Add(mount);
            }
        }
    }

    private void CreateMountVisual(SecondaryMount mount, Material mountMaterial)
    {
        if (owner != null && owner.hideRuntimeWeaponVisuals)
        {
            return;
        }

        mount.rootObject = new GameObject("Core Tactical " + mount.displayName);
        mount.rootObject.transform.SetPositionAndRotation(GetMountWorldPosition(mount), GetMountWorldRotation(mount.currentLocalYawDegrees));

        GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObject.name = mount.displayName + " Base";
        baseObject.transform.SetParent(mount.rootObject.transform, false);
        float baseDiameter = mount.shellVisualScale * 2.6f;
        baseObject.transform.localScale = new Vector3(baseDiameter, 1.15f, baseDiameter);
        AssignRendererMaterial(baseObject, mountMaterial);
        DestroyPrimitiveCollider(baseObject);

        GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
        housing.name = mount.displayName + " Housing";
        housing.transform.SetParent(mount.rootObject.transform, false);
        housing.transform.localPosition = new Vector3(0f, mount.shellVisualScale * 0.85f, mount.shellVisualScale * 0.35f);
        housing.transform.localScale = new Vector3(mount.shellVisualScale * 2.3f, mount.shellVisualScale * 1.05f, mount.shellVisualScale * 1.75f);
        AssignRendererMaterial(housing, mountMaterial);
        DestroyPrimitiveCollider(housing);

        GameObject pivotObject = new GameObject(mount.displayName + " Barrel Pivot");
        pivotObject.transform.SetParent(mount.rootObject.transform, false);
        pivotObject.transform.localPosition = new Vector3(0f, mount.shellVisualScale * 1.05f, mount.shellVisualScale * 0.9f);
        mount.barrelPivot = pivotObject.transform;

        for (int gunIndex = 0; gunIndex < 2; gunIndex++)
        {
            float offset = (gunIndex == 0 ? -0.5f : 0.5f) * mount.gunSpacingMeters;
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = mount.displayName + " Barrel " + (gunIndex + 1);
            barrel.transform.SetParent(mount.barrelPivot, false);
            barrel.transform.localPosition = new Vector3(offset, 0f, mount.barrelLengthMeters * 0.5f);
            barrel.transform.localScale = new Vector3(mount.shellVisualScale * 0.42f, mount.shellVisualScale * 0.34f, mount.barrelLengthMeters);
            AssignRendererMaterial(barrel, barrelMaterial);
            DestroyPrimitiveCollider(barrel);
        }
    }

    private void RefreshTargetCandidates(float now)
    {
        if (now < nextTargetRefreshTime)
        {
            return;
        }

        targetCandidates = FindObjectsByType<CoreTacticalCombatant>(FindObjectsSortMode.None);
        nextTargetRefreshTime = now + Mathf.Max(0.03f, targetRefreshIntervalSeconds);
    }

    private void RefreshMountTargets()
    {
        if (mounts == null)
        {
            return;
        }

        float engagementRange = Mathf.Max(1f, maxRangeMeters);
        float awarenessRange = Mathf.Max(engagementRange, targetAwarenessRangeMeters);
        for (int i = 0; i < mounts.Length; i++)
        {
            SecondaryMount mount = mounts[i];
            if (mount == null)
            {
                continue;
            }

            CoreTacticalShipMotor bestTarget = FindBestTargetForMount(mount, true, engagementRange);
            if (bestTarget == null)
            {
                bestTarget = FindBestTargetForMount(mount, false, awarenessRange);
            }

            mount.target = bestTarget;
        }
    }

    private CoreTacticalShipMotor FindBestTargetForMount(SecondaryMount mount, bool requireInsideSector, float rangeMeters)
    {
        CoreTacticalShipMotor bestTarget = null;
        float bestScore = float.PositiveInfinity;
        if (requireInsideSector)
        {
            ConsiderTargetForMount(GetPriorityTarget(), mount, true, rangeMeters, ref bestTarget, ref bestScore);
            if (bestTarget != null)
            {
                return bestTarget;
            }
        }

        if (targetCandidates != null)
        {
            for (int i = 0; i < targetCandidates.Length; i++)
            {
                CoreTacticalCombatant combatant = targetCandidates[i];
                if (!CoreTacticalOreTargetingRules.IsAutomaticCombatTarget(combatant, targetTeam))
                {
                    continue;
                }

                ConsiderTargetForMount(combatant.ship, mount, requireInsideSector, rangeMeters, ref bestTarget, ref bestScore);
            }
        }

        ConsiderTargetForMount(target, mount, requireInsideSector, rangeMeters, ref bestTarget, ref bestScore);
        return bestTarget;
    }

    private CoreTacticalShipMotor GetPriorityTarget()
    {
        CoreTacticalPriorityTargetControl priorityControl = owner != null ? owner.GetComponent<CoreTacticalPriorityTargetControl>() : null;
        return priorityControl != null && priorityControl.TryGetPriorityTarget(targetTeam, out CoreTacticalShipMotor priorityTarget)
            ? priorityTarget
            : null;
    }

    private void ConsiderTargetForMount(
        CoreTacticalShipMotor candidate,
        SecondaryMount mount,
        bool requireInsideSector,
        float rangeMeters,
        ref CoreTacticalShipMotor bestTarget,
        ref float bestScore)
    {
        if (!IsValidTarget(candidate))
        {
            return;
        }

        Vector3 mountPosition = GetMountWorldPosition(mount);
        float flatDistance = GetFlatDistance(mountPosition, candidate.transform.position);
        if (flatDistance > Mathf.Max(1f, rangeMeters))
        {
            return;
        }

        float desiredYaw = GetDesiredLocalYawDegrees(mount, candidate);
        float sectorDelta = Mathf.Abs(Mathf.DeltaAngle(mount.baseLocalYawDegrees, desiredYaw));
        bool insideSector = sectorDelta <= SectorHalfAngleDegrees;
        if (requireInsideSector && !insideSector)
        {
            return;
        }

        float currentAimDelta = Mathf.Abs(Mathf.DeltaAngle(mount.currentLocalYawDegrees, desiredYaw));
        float score = flatDistance + currentAimDelta * 12f;
        if (!insideSector)
        {
            score += 25000f + sectorDelta * 100f;
        }

        if (score < bestScore)
        {
            bestScore = score;
            bestTarget = candidate;
        }
    }

    private bool IsValidTarget(CoreTacticalShipMotor candidate)
    {
        if (candidate == null || candidate == owner)
        {
            return false;
        }

        CoreTacticalCombatant combatant = candidate.GetComponent<CoreTacticalCombatant>();
        if (combatant != null && candidate != target && combatant.team != targetTeam)
        {
            return false;
        }

        CoreTacticalPrototypeHealth health = candidate.GetComponent<CoreTacticalPrototypeHealth>();
        return health == null || health.currentHealth > 0f;
    }

    private void UpdateMounts(float deltaSeconds)
    {
        if (mounts == null)
        {
            return;
        }

        for (int i = 0; i < mounts.Length; i++)
        {
            SecondaryMount mount = mounts[i];
            CoreTacticalShipMotor mountTarget = mount.target;
            if (IsValidTarget(mountTarget))
            {
                float desiredYaw = GetDesiredLocalYawDegrees(mount, mountTarget);
                mount.targetInsideSector = TryResolveSectorYaw(mount, desiredYaw, out float resolvedYaw);
                mount.desiredLocalYawDegrees = resolvedYaw;
                mount.aimed = mount.targetInsideSector
                    && Mathf.Abs(mount.currentLocalYawDegrees - mount.desiredLocalYawDegrees) <= Mathf.Max(0.1f, aimToleranceDegrees);
            }
            else
            {
                mount.target = null;
                mount.targetInsideSector = false;
                mount.aimed = false;
                mount.desiredLocalYawDegrees = mount.baseLocalYawDegrees;
            }

            mount.currentLocalYawDegrees = Mathf.MoveTowards(
                mount.currentLocalYawDegrees,
                mount.desiredLocalYawDegrees,
                Mathf.Max(0.01f, turretYawRateDegPerSecond) * deltaSeconds);

            ApplyMountTransform(mount);
            UpdateBarrelElevation(mount, mount.target);
        }
    }

    private void TryScheduleReadyMounts(float now)
    {
        for (int i = 0; i < mounts.Length; i++)
        {
            SecondaryMount mount = mounts[i];
            CoreTacticalShipMotor mountTarget = mount?.target;
            if (mount == null
                || !IsValidTarget(mountTarget)
                || now < mount.nextReadyTime
                || !mount.aimed
                || !CanFireWeapon(mount.weaponGroup)
                || !IsTargetInFireRange(mount, mountTarget)
                || !HasClearIdealShot(mount, mountTarget))
            {
                continue;
            }

            pendingShots.Add(new PendingShot { mountIndex = i, gunIndex = 0, fireTime = now, target = mountTarget });
            pendingShots.Add(new PendingShot { mountIndex = i, gunIndex = 1, fireTime = now + 0.08f, target = mountTarget });
            mount.nextReadyTime = now + mount.reloadSeconds * Random.Range(1f - ReloadRandomSpread, 1f + ReloadRandomSpread);
            mount.hasFired = true;
        }
    }

    private void HoldReadyMounts(float now)
    {
        if (mounts == null)
        {
            return;
        }

        for (int i = 0; i < mounts.Length; i++)
        {
            SecondaryMount mount = mounts[i];
            if (mount != null && mount.nextReadyTime <= now)
            {
                mount.nextReadyTime = now + Random.Range(0f, mount.reloadSeconds * 0.45f);
            }
        }
    }

    private void ProcessPendingShots(float now)
    {
        for (int i = pendingShots.Count - 1; i >= 0; i--)
        {
            PendingShot shot = pendingShots[i];
            if (now < shot.fireTime)
            {
                continue;
            }

            FireGun(shot.mountIndex, shot.gunIndex, shot.target);
            pendingShots.RemoveAt(i);
        }
    }

    private void FireGun(int mountIndex, int gunIndex, CoreTacticalShipMotor shotTarget)
    {
        if (!IsValidTarget(shotTarget) || mounts == null || mountIndex < 0 || mountIndex >= mounts.Length)
        {
            return;
        }

        SecondaryMount mount = mounts[mountIndex];
        Vector3 muzzlePosition = GetMuzzlePosition(mount, gunIndex);
        if (!TryGetAimingSolution(mount, shotTarget, muzzlePosition, out Vector3 predictedAimPoint, out Vector3 idealVelocity, out float idealFlightTime, out float predictedFlatDistance))
        {
            return;
        }

        float obstacleRadius = Mathf.Max(1.5f, mount.shellVisualScale * 0.45f);
        if (CoreTacticalWeaponOcclusion.BallisticPathHitsObstacle(
                muzzlePosition,
                idealVelocity,
                Mathf.Max(0.01f, gravityMS2),
                Mathf.Max(0f, projectileLinearDragK),
                idealFlightTime,
                obstacleRadius,
                out _))
        {
            return;
        }

        Vector3 aimedPosition = ApplyDispersion(predictedAimPoint, predictedFlatDistance);
        if (!TrySolveBallisticVelocity(
                muzzlePosition,
                aimedPosition,
                mount.muzzleVelocityMS,
                Mathf.Max(0.01f, gravityMS2),
                Mathf.Max(0f, projectileLinearDragK),
                out Vector3 initialVelocity,
                out float flightTime))
        {
            return;
        }

        if (!CanFireWeapon(mount.weaponGroup))
        {
            return;
        }

        CoreTacticalProjectileVisual.Create(
            mount.displayName + " Shell",
            muzzlePosition,
            initialVelocity,
            Mathf.Max(0.01f, gravityMS2),
            Mathf.Max(0f, projectileLinearDragK),
            Mathf.Max(100f, maxRangeMeters),
            mount.shellVisualScale,
            mount.tracerTrailSeconds,
            flightTime,
            mount.burstRadiusMeters,
            mount.shellMaterial,
            mount.tracerMaterial,
            shotTarget,
            Mathf.Max(0f, mount.shellDamage),
            mount.displayName,
            mount.detonationMode,
            mount.shellDamageType,
            mount.shellResistanceIgnorePercent,
            mount.shellCaliberMm,
            mount.shellDirectImpactFuseThresholdMeters,
            mount.shellFireChancePercent);
        CoreTacticalLeviathanController.NotifyArtilleryReport(muzzlePosition, maxRangeMeters, owner);
    }

    private bool IsTargetInFireRange(SecondaryMount mount, CoreTacticalShipMotor fireTarget)
    {
        if (!IsValidTarget(fireTarget))
        {
            return false;
        }

        float flatDistance = GetFlatDistance(GetMountWorldPosition(mount), fireTarget.transform.position);
        if (suppressFireWithinRangeMeters > 0f && flatDistance <= suppressFireWithinRangeMeters)
        {
            return false;
        }

        return flatDistance <= Mathf.Max(1f, maxRangeMeters);
    }

    private bool HasClearIdealShot(SecondaryMount mount, CoreTacticalShipMotor shotTarget)
    {
        Vector3 muzzlePosition = GetMuzzlePosition(mount, 0);
        if (!TryGetAimingSolution(mount, shotTarget, muzzlePosition, out _, out Vector3 initialVelocity, out float flightTime, out _))
        {
            return false;
        }

        return !CoreTacticalWeaponOcclusion.BallisticPathHitsObstacle(
            muzzlePosition,
            initialVelocity,
            Mathf.Max(0.01f, gravityMS2),
            Mathf.Max(0f, projectileLinearDragK),
            flightTime,
            Mathf.Max(1.5f, mount.shellVisualScale * 0.45f),
            out _);
    }

    private bool CanFireWeapon(CoreTacticalWeaponGroup group)
    {
        CoreTacticalWeaponControl control = ResolveWeaponControl();
        return control == null || control.CanFire(group);
    }

    private CoreTacticalWeaponControl ResolveWeaponControl()
    {
        if (weaponControl != null)
        {
            return weaponControl;
        }

        if (owner != null)
        {
            weaponControl = owner.GetComponent<CoreTacticalWeaponControl>();
        }

        if (weaponControl == null)
        {
            weaponControl = GetComponent<CoreTacticalWeaponControl>();
        }

        return weaponControl;
    }

    private bool TryGetAimingSolution(
        SecondaryMount mount,
        CoreTacticalShipMotor shotTarget,
        Vector3 muzzlePosition,
        out Vector3 predictedAimPoint,
        out Vector3 initialVelocity,
        out float flightTime,
        out float predictedFlatDistance)
    {
        predictedAimPoint = shotTarget != null ? shotTarget.transform.position : muzzlePosition + transform.forward * 100f;
        initialVelocity = Vector3.zero;
        flightTime = 0f;
        predictedFlatDistance = 0f;
        if (!IsValidTarget(shotTarget))
        {
            return false;
        }

        Vector3 targetPosition = shotTarget.transform.position;
        Vector3 targetVelocity = shotTarget.Body != null ? shotTarget.Body.linearVelocity : Vector3.zero;
        Vector3 aimPoint = targetPosition;
        for (int i = 0; i < AimPredictionIterations; i++)
        {
            if (!TrySolveBallisticVelocity(
                    muzzlePosition,
                    aimPoint,
                    mount.muzzleVelocityMS,
                    Mathf.Max(0.01f, gravityMS2),
                    Mathf.Max(0f, projectileLinearDragK),
                    out _,
                    out float iterationFlightTime))
            {
                return false;
            }

            aimPoint = targetPosition + targetVelocity * iterationFlightTime;
        }

        predictedFlatDistance = GetFlatDistance(muzzlePosition, aimPoint);
        if (predictedFlatDistance > Mathf.Max(1f, maxRangeMeters))
        {
            return false;
        }

        predictedAimPoint = aimPoint;
        return TrySolveBallisticVelocity(
            muzzlePosition,
            predictedAimPoint,
            mount.muzzleVelocityMS,
            Mathf.Max(0.01f, gravityMS2),
            Mathf.Max(0f, projectileLinearDragK),
            out initialVelocity,
            out flightTime);
    }

    private void UpdateBarrelElevation(SecondaryMount mount, CoreTacticalShipMotor mountTarget)
    {
        if (mount == null || mount.barrelPivot == null || !IsValidTarget(mountTarget))
        {
            return;
        }

        Vector3 muzzlePosition = GetMuzzlePosition(mount, 0);
        if (!TryGetAimingSolution(mount, mountTarget, muzzlePosition, out _, out Vector3 initialVelocity, out _, out _))
        {
            return;
        }

        Vector3 localVelocity = Quaternion.Inverse(mount.rootObject.transform.rotation) * initialVelocity.normalized;
        float elevationDegrees = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(localVelocity.y, -0.95f, 0.95f)) * Mathf.Rad2Deg, 0f, 42f);
        mount.barrelPivot.localRotation = Quaternion.Euler(-elevationDegrees, 0f, 0f);
    }

    private Vector3 GetMountWorldPosition(SecondaryMount mount)
    {
        Vector3 hullSize = owner != null ? owner.hullSizeMeters : new Vector3(62f, 20f, 350f);
        Transform ownerTransform = owner != null ? owner.transform : transform;
        return ownerTransform.position
            + ownerTransform.right * mount.localX
            + ownerTransform.up * (hullSize.y * 0.5f + mount.shellVisualScale * 0.45f)
            + ownerTransform.forward * mount.localZ;
    }

    private Quaternion GetMountWorldRotation(float localYawDegrees)
    {
        Transform ownerTransform = owner != null ? owner.transform : transform;
        return ownerTransform.rotation * Quaternion.Euler(0f, localYawDegrees, 0f);
    }

    private void ApplyMountTransform(SecondaryMount mount)
    {
        if (mount?.rootObject == null)
        {
            return;
        }

        mount.rootObject.transform.SetPositionAndRotation(
            GetMountWorldPosition(mount),
            GetMountWorldRotation(mount.currentLocalYawDegrees));
    }

    private Vector3 GetMuzzlePosition(SecondaryMount mount, int gunIndex)
    {
        if (mount?.barrelPivot == null)
        {
            return GetMountWorldPosition(mount);
        }

        float offset = (gunIndex == 0 ? -0.5f : 0.5f) * mount.gunSpacingMeters;
        return mount.barrelPivot.TransformPoint(new Vector3(offset, 0f, mount.barrelLengthMeters));
    }

    private float GetDesiredLocalYawDegrees(SecondaryMount mount, CoreTacticalShipMotor aimTarget)
    {
        Vector3 toTarget = aimTarget != null ? aimTarget.transform.position - GetMountWorldPosition(mount) : transform.forward;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return mount.currentLocalYawDegrees;
        }

        Vector3 ownerForward = owner != null ? owner.transform.forward : transform.forward;
        ownerForward.y = 0f;
        return Mathf.DeltaAngle(0f, Vector3.SignedAngle(ownerForward.normalized, toTarget.normalized, Vector3.up));
    }

    private static bool TryResolveSectorYaw(SecondaryMount mount, float desiredYaw, out float resolvedYaw)
    {
        float sectorDelta = Mathf.DeltaAngle(mount.baseLocalYawDegrees, desiredYaw);
        bool insideSector = Mathf.Abs(sectorDelta) <= SectorHalfAngleDegrees;
        float clampedDelta = Mathf.Clamp(sectorDelta, -SectorHalfAngleDegrees, SectorHalfAngleDegrees);
        resolvedYaw = mount.baseLocalYawDegrees + clampedDelta;
        return insideSector;
    }

    private Vector3 ApplyDispersion(Vector3 targetPosition, float flatDistance)
    {
        float dispersionRadius = dispersionAtMaxRangeMeters * Mathf.Clamp01(flatDistance / Mathf.Max(1f, maxRangeMeters));
        Vector2 randomOffset = Random.insideUnitCircle * dispersionRadius;
        return targetPosition + new Vector3(randomOffset.x, 0f, randomOffset.y);
    }

    private static float GetFlatDistance(Vector3 first, Vector3 second)
    {
        Vector3 flatDelta = second - first;
        flatDelta.y = 0f;
        return flatDelta.magnitude;
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

    private static Material CreateMaterial(Color color)
    {
        return CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(color, 1.7f);
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        return CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(color);
    }

    private static bool TrySolveBallisticVelocity(
        Vector3 start,
        Vector3 end,
        float speed,
        float gravity,
        float linearDragK,
        out Vector3 initialVelocity,
        out float flightTime)
    {
        initialVelocity = Vector3.zero;
        flightTime = 0f;

        Vector3 delta = end - start;
        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        float flatDistance = flatDelta.magnitude;
        if (flatDistance <= 0.01f || speed <= 0.01f)
        {
            return false;
        }

        if (linearDragK <= 0.0001f)
        {
            return TrySolveVacuumBallisticVelocity(start, end, speed, gravity, out initialVelocity, out flightTime);
        }

        float speedSquared = speed * speed;
        float previousTime = 0.02f;
        float previousError = GetLinearDragRequiredSpeedSqr(delta, previousTime, gravity, linearDragK) - speedSquared;
        bool hasBracket = false;
        float lowerTime = previousTime;
        float upperTime = previousTime;
        const float searchStepSeconds = 0.08f;
        const float maxSearchSeconds = 90f;

        for (float time = previousTime + searchStepSeconds; time <= maxSearchSeconds; time += searchStepSeconds)
        {
            float error = GetLinearDragRequiredSpeedSqr(delta, time, gravity, linearDragK) - speedSquared;
            if (previousError > 0f && error <= 0f)
            {
                lowerTime = previousTime;
                upperTime = time;
                hasBracket = true;
                break;
            }

            previousTime = time;
            previousError = error;
        }

        if (!hasBracket)
        {
            return false;
        }

        for (int i = 0; i < 36; i++)
        {
            float midTime = (lowerTime + upperTime) * 0.5f;
            float midError = GetLinearDragRequiredSpeedSqr(delta, midTime, gravity, linearDragK) - speedSquared;
            if (midError > 0f)
            {
                lowerTime = midTime;
            }
            else
            {
                upperTime = midTime;
            }
        }

        flightTime = (lowerTime + upperTime) * 0.5f;
        float dragTravel = GetLinearDragTravelFactor(flightTime, linearDragK);
        Vector3 flatVelocity = flatDelta / Mathf.Max(0.0001f, dragTravel);
        float verticalVelocity = (delta.y + gravity * flightTime / linearDragK) / Mathf.Max(0.0001f, dragTravel)
            - gravity / linearDragK;
        initialVelocity = flatVelocity + Vector3.up * verticalVelocity;
        return flightTime > 0.01f;
    }

    private static bool TrySolveVacuumBallisticVelocity(
        Vector3 start,
        Vector3 end,
        float speed,
        float gravity,
        out Vector3 initialVelocity,
        out float flightTime)
    {
        initialVelocity = Vector3.zero;
        flightTime = 0f;

        Vector3 delta = end - start;
        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        float flatDistance = flatDelta.magnitude;
        if (flatDistance <= 0.01f || speed <= 0.01f)
        {
            return false;
        }

        float speedSquared = speed * speed;
        float underRoot = speedSquared * speedSquared
            - gravity * (gravity * flatDistance * flatDistance + 2f * delta.y * speedSquared);
        if (underRoot < 0f)
        {
            return false;
        }

        float lowAngleTangent = (speedSquared - Mathf.Sqrt(underRoot)) / (gravity * flatDistance);
        float angle = Mathf.Atan(lowAngleTangent);
        Vector3 flatDirection = flatDelta / flatDistance;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        initialVelocity = flatDirection * (cos * speed) + Vector3.up * (sin * speed);
        flightTime = flatDistance / Mathf.Max(0.01f, cos * speed);
        return flightTime > 0.01f;
    }

    private static float GetLinearDragRequiredSpeedSqr(Vector3 delta, float time, float gravity, float linearDragK)
    {
        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        float dragTravel = GetLinearDragTravelFactor(time, linearDragK);
        if (dragTravel <= 0.0001f)
        {
            return float.PositiveInfinity;
        }

        Vector3 flatVelocity = flatDelta / dragTravel;
        float verticalVelocity = (delta.y + gravity * time / linearDragK) / dragTravel - gravity / linearDragK;
        return flatVelocity.sqrMagnitude + verticalVelocity * verticalVelocity;
    }

    private static float GetLinearDragTravelFactor(float time, float linearDragK)
    {
        return (1f - Mathf.Exp(-linearDragK * Mathf.Max(0f, time))) / linearDragK;
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalMachineGunMountBattery : MonoBehaviour
{
    private const int MaxActiveTracers = 768;
    private const float MachineGunBaseDiameterMeters = 2.7f;
    private const float MachineGunBaseHalfHeightMeters = 0.32f;
    private const float MachineGunPivotHeightMeters = 0.95f;
    private const float MachineGunPivotForwardOffsetMeters = 0.45f;
    private const float MachineGunBarrelLengthMeters = 3.8f;
    private const float MachineGunBarrelWidthMeters = 0.36f;
    private const float MachineGunBarrelHeightMeters = 0.26f;

    public CoreTacticalShipMotor owner;
    public CoreTacticalShipMotor target;
    public CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy;
    public float radiusMeters = 2000f;
    public float targetRefreshIntervalSeconds = 0.2f;
    public float damageTickIntervalSeconds = 0.5f;
    public float damagePerMountTick = 0.56f;
    public float machineGunResistanceIgnorePercent = 10f;
    public float tracerRatePerSecondPerMount = 22f;
    public float tracerSpeedMS = 2200f;
    public float tracerLengthMeters = 10f;
    public float tracerWidthMeters = 0.62f;
    public float tracerDistanceLengthScale = 0.006f;
    public float tracerDistanceWidthScale = 0.00016f;
    public float tracerTargetSpreadMeters = 260f;
    public float tracerTargetVerticalSpreadScale = 0.32f;
    public float tracerCorePassChance = 0.12f;
    public float mountYawRateDegPerSecond = 95f;
    public Color tracerColor = new Color(0.72f, 0.48f, 0.24f, 0.58f);

    private readonly MachineGunTracer[] activeTracers = new MachineGunTracer[MaxActiveTracers];
    private readonly List<Vector3> meshVertices = new List<Vector3>(MaxActiveTracers * 4);
    private readonly List<Color> meshColors = new List<Color>(MaxActiveTracers * 4);
    private readonly List<int> meshIndices = new List<int>(MaxActiveTracers * 6);
    private MachineGunMount[] mounts;
    private Camera tracerCamera;
    private GameObject tracerObject;
    private Mesh tracerMesh;
    private Material tracerMaterial;
    private Material mountMaterial;
    private Material barrelMaterial;
    private CoreTacticalWeaponControl weaponControl;
    private CoreTacticalCombatant[] targetCandidates = new CoreTacticalCombatant[0];
    private float nextDamageTickTime;
    private float nextTargetRefreshTime;
    private int nextTracerSlot;

    private struct MachineGunTracer
    {
        public bool active;
        public Vector3 startPosition;
        public Vector3 direction;
        public float distanceMeters;
        public float progress01;
        public Color color;
    }

    private sealed class MachineGunMount
    {
        public string displayName;
        public float localX;
        public float localZ;
        public float baseLocalYawDegrees;
        public float sectorHalfAngleDegrees;
        public float currentLocalYawDegrees;
        public float desiredLocalYawDegrees;
        public float spawnBudget;
        public bool canFire;
        public bool firingThisFrame;
        public CoreTacticalShipMotor target;
        public GameObject rootObject;
        public Transform barrelPivot;
    }

    private void Awake()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        weaponControl = ResolveWeaponControl();
        tracerCamera = Camera.main;
        nextDamageTickTime = Time.time + Mathf.Max(0.05f, damageTickIntervalSeconds);
        EnsureMounts();
        EnsureTracerRenderer();
    }

    private void OnDestroy()
    {
        if (mounts != null)
        {
            for (int i = 0; i < mounts.Length; i++)
            {
                if (mounts[i]?.rootObject != null)
                {
                    Destroy(mounts[i].rootObject);
                }
            }
        }

        if (tracerObject != null) Destroy(tracerObject);
        if (tracerMesh != null) Destroy(tracerMesh);
        if (tracerMaterial != null) Destroy(tracerMaterial);
        if (mountMaterial != null) Destroy(mountMaterial);
        if (barrelMaterial != null) Destroy(barrelMaterial);
    }

    private void Update()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        float deltaSeconds = Mathf.Max(0.001f, Time.deltaTime);
        EnsureMounts();
        float now = Time.time;
        RefreshTargetCandidates(now);
        RefreshMountTargets();
        UpdateMounts(deltaSeconds);
        ResetFiringFlags();

        if (owner != null && CanFireWeapon(CoreTacticalWeaponGroup.MachineGuns))
        {
            for (int i = 0; i < mounts.Length; i++)
            {
                MachineGunMount mount = mounts[i];
                if (!mount.canFire || !IsTargetInRange(mount.target))
                {
                    continue;
                }

                mount.firingThisFrame = true;
                SpawnTracerWave(mount, deltaSeconds);
            }
        }

        ProcessDamageTick();
        UpdateActiveTracerMesh(deltaSeconds);
    }

    private void ResetFiringFlags()
    {
        if (mounts == null)
        {
            return;
        }

        for (int i = 0; i < mounts.Length; i++)
        {
            if (mounts[i] != null)
            {
                mounts[i].firingThisFrame = false;
            }
        }
    }

    private void EnsureMounts()
    {
        if (mounts != null || owner == null)
        {
            return;
        }

        mountMaterial = CreateMaterial(new Color(0.18f, 0.19f, 0.20f, 1f));
        barrelMaterial = CreateMaterial(new Color(0.075f, 0.08f, 0.085f, 1f));
        Vector3 hullSize = owner.hullSizeMeters;
        List<MachineGunMount> createdMounts = new List<MachineGunMount>(12);
        float[] sideZ = { -0.36f, -0.12f, 0.12f, 0.36f };
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < sideZ.Length; i++)
            {
                createdMounts.Add(CreateMount(
                    "MG " + (side < 0 ? "Port " : "Starboard ") + (i + 1),
                    side * hullSize.x * 0.73f,
                    hullSize.z * sideZ[i],
                    side > 0 ? 90f : -90f,
                    90f));
            }
        }

        createdMounts.Add(CreateMount("MG Bow Port", -hullSize.x * 0.22f, hullSize.z * 0.47f, 0f, 95f));
        createdMounts.Add(CreateMount("MG Bow Starboard", hullSize.x * 0.22f, hullSize.z * 0.47f, 0f, 95f));
        createdMounts.Add(CreateMount("MG Stern Port", -hullSize.x * 0.22f, -hullSize.z * 0.47f, 180f, 95f));
        createdMounts.Add(CreateMount("MG Stern Starboard", hullSize.x * 0.22f, -hullSize.z * 0.47f, 180f, 95f));
        mounts = createdMounts.ToArray();
    }

    private MachineGunMount CreateMount(
        string displayName,
        float localX,
        float localZ,
        float baseLocalYawDegrees,
        float sectorHalfAngleDegrees)
    {
        MachineGunMount mount = new MachineGunMount
        {
            displayName = displayName,
            localX = localX,
            localZ = localZ,
            baseLocalYawDegrees = baseLocalYawDegrees,
            sectorHalfAngleDegrees = Mathf.Clamp(sectorHalfAngleDegrees, 1f, 180f),
            currentLocalYawDegrees = baseLocalYawDegrees,
            desiredLocalYawDegrees = baseLocalYawDegrees
        };

        if (owner != null && owner.hideRuntimeWeaponVisuals)
        {
            return mount;
        }

        mount.rootObject = new GameObject("Core Tactical " + displayName);
        mount.rootObject.transform.SetPositionAndRotation(GetMountWorldPosition(mount), GetMountWorldRotation(mount.currentLocalYawDegrees));

        GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseObject.name = displayName + " Pintle";
        baseObject.transform.SetParent(mount.rootObject.transform, false);
        baseObject.transform.localScale = new Vector3(
            MachineGunBaseDiameterMeters,
            MachineGunBaseHalfHeightMeters,
            MachineGunBaseDiameterMeters);
        AssignRendererMaterial(baseObject, mountMaterial);
        DestroyPrimitiveCollider(baseObject);

        GameObject pivotObject = new GameObject(displayName + " Barrel Pivot");
        pivotObject.transform.SetParent(mount.rootObject.transform, false);
        pivotObject.transform.localPosition = new Vector3(
            0f,
            MachineGunPivotHeightMeters,
            MachineGunPivotForwardOffsetMeters);
        mount.barrelPivot = pivotObject.transform;

        GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        barrel.name = displayName + " Barrel";
        barrel.transform.SetParent(mount.barrelPivot, false);
        barrel.transform.localPosition = new Vector3(0f, 0f, MachineGunBarrelLengthMeters * 0.5f);
        barrel.transform.localScale = new Vector3(
            MachineGunBarrelWidthMeters,
            MachineGunBarrelHeightMeters,
            MachineGunBarrelLengthMeters);
        AssignRendererMaterial(barrel, barrelMaterial);
        DestroyPrimitiveCollider(barrel);
        return mount;
    }

    private void UpdateMounts(float deltaSeconds)
    {
        if (mounts == null)
        {
            return;
        }

        for (int i = 0; i < mounts.Length; i++)
        {
            MachineGunMount mount = mounts[i];
            bool sectorAllowsFire = false;
            CoreTacticalShipMotor mountTarget = mount.target;
            if (IsValidTarget(mountTarget))
            {
                float desiredYaw = GetDesiredLocalYawDegrees(mount, mountTarget);
                sectorAllowsFire = TryResolveSectorYaw(mount, desiredYaw, out float resolvedYaw);
                mount.desiredLocalYawDegrees = resolvedYaw;
            }
            else
            {
                mount.target = null;
                mount.desiredLocalYawDegrees = mount.baseLocalYawDegrees;
            }

            mount.currentLocalYawDegrees = Mathf.MoveTowardsAngle(
                mount.currentLocalYawDegrees,
                mount.desiredLocalYawDegrees,
                Mathf.Max(0.01f, mountYawRateDegPerSecond) * deltaSeconds);

            ApplyMountTransform(mount);
            mount.canFire = sectorAllowsFire
                && Mathf.Abs(Mathf.DeltaAngle(mount.currentLocalYawDegrees, mount.desiredLocalYawDegrees)) <= 6f
                && IsTargetInRange(mount.target)
                && HasClearLineOfFire(mount, mount.target);
        }
    }

    private void RefreshTargetCandidates(float now)
    {
        if (now < nextTargetRefreshTime)
        {
            return;
        }

        targetCandidates = FindObjectsByType<CoreTacticalCombatant>(FindObjectsSortMode.None);
        nextTargetRefreshTime = now + Mathf.Max(0.03f, targetRefreshIntervalSeconds);
    }

    private void RefreshMountTargets()
    {
        if (mounts == null)
        {
            return;
        }

        float range = Mathf.Max(1f, radiusMeters);
        for (int i = 0; i < mounts.Length; i++)
        {
            MachineGunMount mount = mounts[i];
            if (mount == null)
            {
                continue;
            }

            CoreTacticalShipMotor bestTarget = FindBestTargetForMount(mount, true, range);
            if (bestTarget == null)
            {
                bestTarget = FindBestTargetForMount(mount, false, range);
            }

            mount.target = bestTarget;
        }
    }

    private CoreTacticalShipMotor FindBestTargetForMount(MachineGunMount mount, bool requireInsideSector, float rangeMeters)
    {
        CoreTacticalShipMotor bestTarget = null;
        float bestScore = float.PositiveInfinity;
        if (requireInsideSector)
        {
            ConsiderTargetForMount(GetPriorityTarget(), mount, true, rangeMeters, ref bestTarget, ref bestScore);
            if (bestTarget != null)
            {
                return bestTarget;
            }
        }

        if (targetCandidates != null)
        {
            for (int i = 0; i < targetCandidates.Length; i++)
            {
                CoreTacticalCombatant combatant = targetCandidates[i];
                if (!CoreTacticalOreTargetingRules.IsAutomaticCombatTarget(combatant, targetTeam))
                {
                    continue;
                }

                ConsiderTargetForMount(combatant.ship, mount, requireInsideSector, rangeMeters, ref bestTarget, ref bestScore);
            }
        }

        ConsiderTargetForMount(target, mount, requireInsideSector, rangeMeters, ref bestTarget, ref bestScore);
        return bestTarget;
    }

    private CoreTacticalShipMotor GetPriorityTarget()
    {
        CoreTacticalPriorityTargetControl priorityControl = owner != null ? owner.GetComponent<CoreTacticalPriorityTargetControl>() : null;
        return priorityControl != null && priorityControl.TryGetPriorityTarget(targetTeam, out CoreTacticalShipMotor priorityTarget)
            ? priorityTarget
            : null;
    }

    private void ConsiderTargetForMount(
        CoreTacticalShipMotor candidate,
        MachineGunMount mount,
        bool requireInsideSector,
        float rangeMeters,
        ref CoreTacticalShipMotor bestTarget,
        ref float bestScore)
    {
        if (!IsValidTarget(candidate))
        {
            return;
        }

        float flatDistance = GetFlatDistance(GetMountWorldPosition(mount), candidate.transform.position);
        if (flatDistance > Mathf.Max(1f, rangeMeters))
        {
            return;
        }

        float desiredYaw = GetDesiredLocalYawDegrees(mount, candidate);
        float sectorDelta = Mathf.Abs(Mathf.DeltaAngle(mount.baseLocalYawDegrees, desiredYaw));
        bool insideSector = sectorDelta <= mount.sectorHalfAngleDegrees;
        if (requireInsideSector && !insideSector)
        {
            return;
        }

        float score = flatDistance + Mathf.Abs(Mathf.DeltaAngle(mount.currentLocalYawDegrees, desiredYaw)) * 10f;
        if (!insideSector)
        {
            score += 12000f + sectorDelta * 60f;
        }

        if (score < bestScore)
        {
            bestScore = score;
            bestTarget = candidate;
        }
    }

    private bool IsValidTarget(CoreTacticalShipMotor candidate)
    {
        if (candidate == null || candidate == owner)
        {
            return false;
        }

        CoreTacticalCombatant combatant = candidate.GetComponent<CoreTacticalCombatant>();
        if (combatant != null && candidate != target && combatant.team != targetTeam)
        {
            return false;
        }

        CoreTacticalPrototypeHealth health = candidate.GetComponent<CoreTacticalPrototypeHealth>();
        return health == null || health.currentHealth > 0f;
    }

    private bool IsTargetInRange(CoreTacticalShipMotor candidate)
    {
        return owner != null
            && IsValidTarget(candidate)
            && GetFlatDistance(owner.transform.position, candidate.transform.position) <= Mathf.Max(1f, radiusMeters);
    }

    private void SpawnTracerWave(MachineGunMount mount, float deltaSeconds)
    {
        mount.spawnBudget += Mathf.Max(0f, tracerRatePerSecondPerMount) * deltaSeconds;
        int spawnCount = Mathf.Min(Mathf.FloorToInt(mount.spawnBudget), 4);
        if (spawnCount <= 0)
        {
            return;
        }

        mount.spawnBudget -= spawnCount;
        for (int i = 0; i < spawnCount; i++)
        {
            SpawnTracer(mount);
        }
    }

    private void SpawnTracer(MachineGunMount mount)
    {
        Vector3 start = GetMuzzlePosition(mount);
        Vector3 aimPoint = GetTargetPoint(start, mount.target);
        Vector3 direction = aimPoint - start;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        direction.Normalize();
        Color color = tracerColor;
        color.a *= Random.Range(0.42f, 0.9f);
        activeTracers[nextTracerSlot] = new MachineGunTracer
        {
            active = true,
            startPosition = start,
            direction = direction,
            distanceMeters = Mathf.Max(1f, radiusMeters),
            progress01 = 0f,
            color = color
        };
        nextTracerSlot = (nextTracerSlot + 1) % activeTracers.Length;
    }

    private void ProcessDamageTick()
    {
        float now = Time.time;
        if (now < nextDamageTickTime || mounts == null)
        {
            return;
        }

        Dictionary<CoreTacticalPrototypeHealth, int> firingMountsByTarget = new Dictionary<CoreTacticalPrototypeHealth, int>();
        for (int i = 0; i < mounts.Length; i++)
        {
            MachineGunMount mount = mounts[i];
            if (mount == null || !mount.firingThisFrame || !IsValidTarget(mount.target))
            {
                continue;
            }

            CoreTacticalPrototypeHealth health = mount.target.GetComponent<CoreTacticalPrototypeHealth>();
            if (health == null || health.currentHealth <= 0f)
            {
                continue;
            }

            firingMountsByTarget.TryGetValue(health, out int count);
            firingMountsByTarget[health] = count + 1;
        }

        foreach (KeyValuePair<CoreTacticalPrototypeHealth, int> entry in firingMountsByTarget)
        {
            if (entry.Key != null && entry.Key.currentHealth > 0f)
            {
                entry.Key.ApplyDamage(CoreTacticalDamageRequest.Kinetic(
                    Mathf.Max(0f, damagePerMountTick) * entry.Value,
                    "Machine gun mounts",
                    machineGunResistanceIgnorePercent));
            }
        }

        if (firingMountsByTarget.Count > 0)
        {
            nextDamageTickTime = now + Mathf.Max(0.05f, damageTickIntervalSeconds);
        }
    }

    private bool CanFireWeapon(CoreTacticalWeaponGroup group)
    {
        CoreTacticalWeaponControl control = ResolveWeaponControl();
        return control == null || control.CanFire(group);
    }

    private CoreTacticalWeaponControl ResolveWeaponControl()
    {
        if (weaponControl != null)
        {
            return weaponControl;
        }

        if (owner != null)
        {
            weaponControl = owner.GetComponent<CoreTacticalWeaponControl>();
        }

        if (weaponControl == null)
        {
            weaponControl = GetComponent<CoreTacticalWeaponControl>();
        }

        return weaponControl;
    }

    private bool HasClearLineOfFire(MachineGunMount mount, CoreTacticalShipMotor fireTarget)
    {
        if (owner == null || !IsValidTarget(fireTarget) || mount == null)
        {
            return false;
        }

        return !CoreTacticalWeaponOcclusion.HasObstacleBetween(
            GetMuzzlePosition(mount),
            fireTarget.transform.position,
            Mathf.Max(1.2f, tracerWidthMeters * 2.5f));
    }

    private void UpdateActiveTracerMesh(float deltaSeconds)
    {
        EnsureTracerRenderer();
        meshVertices.Clear();
        meshColors.Clear();
        meshIndices.Clear();

        Camera camera = GetTracerCamera();
        float travelStep = Mathf.Max(1f, tracerSpeedMS) * deltaSeconds;
        for (int i = 0; i < activeTracers.Length; i++)
        {
            MachineGunTracer tracer = activeTracers[i];
            if (!tracer.active)
            {
                continue;
            }

            tracer.progress01 += travelStep / Mathf.Max(1f, tracer.distanceMeters);
            if (tracer.progress01 >= 1f)
            {
                tracer.active = false;
                activeTracers[i] = tracer;
                continue;
            }

            Vector3 head = tracer.startPosition + tracer.direction * (tracer.distanceMeters * Mathf.Clamp01(tracer.progress01));
            Vector3 center = head;
            float cameraDistance = camera != null ? Vector3.Distance(camera.transform.position, center) : 0f;
            float segmentLength = Mathf.Max(
                Mathf.Max(1f, tracerLengthMeters),
                cameraDistance * Mathf.Max(0f, tracerDistanceLengthScale));
            Vector3 tail = tracer.startPosition + tracer.direction * Mathf.Max(0f, tracer.distanceMeters * tracer.progress01 - segmentLength);
            center = (tail + head) * 0.5f;
            Vector3 viewDirection = camera != null ? camera.transform.position - center : Vector3.up;
            Vector3 widthDirection = Vector3.Cross(tracer.direction, viewDirection);
            if (widthDirection.sqrMagnitude <= 0.0001f)
            {
                widthDirection = camera != null ? camera.transform.up : Vector3.up;
            }

            widthDirection.Normalize();
            cameraDistance = camera != null ? Vector3.Distance(camera.transform.position, center) : 0f;
            float tracerWidth = Mathf.Clamp(
                Mathf.Max(tracerWidthMeters, cameraDistance * Mathf.Max(0f, tracerDistanceWidthScale)),
                0.04f,
                2.4f);
            Color color = tracer.color;
            color.a *= Mathf.Clamp01(Mathf.InverseLerp(1f, 0.9f, tracer.progress01));
            AddTracerQuad(tail, head, widthDirection, tracerWidth, color);
            activeTracers[i] = tracer;
        }

        ApplyTracerMesh();
    }

    private void AddTracerQuad(Vector3 tail, Vector3 head, Vector3 widthDirection, float widthMeters, Color color)
    {
        Vector3 halfWidth = widthDirection * (widthMeters * 0.5f);
        int vertexIndex = meshVertices.Count;
        meshVertices.Add(tail - halfWidth);
        meshVertices.Add(tail + halfWidth);
        meshVertices.Add(head + halfWidth);
        meshVertices.Add(head - halfWidth);

        meshColors.Add(color);
        meshColors.Add(color);
        meshColors.Add(color);
        meshColors.Add(color);

        meshIndices.Add(vertexIndex);
        meshIndices.Add(vertexIndex + 1);
        meshIndices.Add(vertexIndex + 2);
        meshIndices.Add(vertexIndex);
        meshIndices.Add(vertexIndex + 2);
        meshIndices.Add(vertexIndex + 3);
    }

    private Vector3 GetTargetPoint(Vector3 emitterPosition, CoreTacticalShipMotor aimTarget)
    {
        Vector3 targetPosition = aimTarget != null ? aimTarget.transform.position : transform.position + transform.forward * radiusMeters;
        Vector3 hullSize = aimTarget != null ? aimTarget.hullSizeMeters : Vector3.one * 20f;
        Vector3 fireDirection = targetPosition - emitterPosition;
        if (fireDirection.sqrMagnitude <= 0.001f)
        {
            fireDirection = transform.forward;
        }

        fireDirection.Normalize();
        Vector3 tunnelRight = Vector3.Cross(Vector3.up, fireDirection);
        if (tunnelRight.sqrMagnitude <= 0.001f)
        {
            tunnelRight = transform.right;
        }

        tunnelRight.Normalize();
        Vector3 tunnelUp = Vector3.Cross(fireDirection, tunnelRight).normalized;
        float rangeRatio = Mathf.Clamp01(GetFlatDistance(emitterPosition, targetPosition) / Mathf.Max(1f, radiusMeters));
        float hullRadius = Mathf.Max(hullSize.x, hullSize.z) * 0.45f;
        float tunnelRadius = Mathf.Lerp(
            Mathf.Max(14f, hullRadius * 0.65f),
            Mathf.Max(hullRadius, tracerTargetSpreadMeters),
            rangeRatio);

        Vector2 tunnelOffset = Random.insideUnitCircle * tunnelRadius;
        if (Random.value < Mathf.Clamp01(tracerCorePassChance))
        {
            tunnelOffset *= 0.28f;
        }

        return targetPosition
            + tunnelRight * tunnelOffset.x
            + tunnelUp * (tunnelOffset.y * Mathf.Clamp(tracerTargetVerticalSpreadScale, 0.05f, 1f));
    }

    private Vector3 GetMountWorldPosition(MachineGunMount mount)
    {
        Vector3 hullSize = owner != null ? owner.hullSizeMeters : new Vector3(62f, 20f, 350f);
        Transform ownerTransform = owner != null ? owner.transform : transform;
        return ownerTransform.position
            + ownerTransform.right * mount.localX
            + ownerTransform.up * (hullSize.y * 0.5f + 2.0f)
            + ownerTransform.forward * mount.localZ;
    }

    private Quaternion GetMountWorldRotation(float localYawDegrees)
    {
        Transform ownerTransform = owner != null ? owner.transform : transform;
        return ownerTransform.rotation * Quaternion.Euler(0f, localYawDegrees, 0f);
    }

    private void ApplyMountTransform(MachineGunMount mount)
    {
        if (mount?.rootObject == null)
        {
            return;
        }

        mount.rootObject.transform.SetPositionAndRotation(GetMountWorldPosition(mount), GetMountWorldRotation(mount.currentLocalYawDegrees));
    }

    private Vector3 GetMuzzlePosition(MachineGunMount mount)
    {
        if (mount?.barrelPivot == null)
        {
            return GetMountWorldPosition(mount);
        }

        return mount.barrelPivot.TransformPoint(new Vector3(0f, 0f, MachineGunBarrelLengthMeters));
    }

    private float GetDesiredLocalYawDegrees(MachineGunMount mount, CoreTacticalShipMotor aimTarget)
    {
        Vector3 toTarget = aimTarget != null ? aimTarget.transform.position - GetMountWorldPosition(mount) : transform.forward;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return mount.currentLocalYawDegrees;
        }

        Vector3 ownerForward = owner != null ? owner.transform.forward : transform.forward;
        ownerForward.y = 0f;
        return Mathf.DeltaAngle(0f, Vector3.SignedAngle(ownerForward.normalized, toTarget.normalized, Vector3.up));
    }

    private static bool TryResolveSectorYaw(MachineGunMount mount, float desiredYaw, out float resolvedYaw)
    {
        float sectorDelta = Mathf.DeltaAngle(mount.baseLocalYawDegrees, desiredYaw);
        bool insideSector = Mathf.Abs(sectorDelta) <= mount.sectorHalfAngleDegrees;
        float clampedDelta = Mathf.Clamp(sectorDelta, -mount.sectorHalfAngleDegrees, mount.sectorHalfAngleDegrees);
        resolvedYaw = mount.baseLocalYawDegrees + clampedDelta;
        return insideSector;
    }

    private void ApplyTracerMesh()
    {
        tracerMesh.Clear(false);
        if (meshVertices.Count == 0)
        {
            return;
        }

        tracerMesh.SetVertices(meshVertices);
        tracerMesh.SetColors(meshColors);
        tracerMesh.SetIndices(meshIndices, MeshTopology.Triangles, 0, true);
        tracerMesh.RecalculateBounds();
    }

    private Camera GetTracerCamera()
    {
        if (tracerCamera == null)
        {
            tracerCamera = Camera.main;
        }

        return tracerCamera;
    }

    private void EnsureTracerRenderer()
    {
        if (tracerMesh != null && tracerObject != null)
        {
            return;
        }

        tracerObject = new GameObject("Core Tactical Machine Gun Mount Tracers");
        tracerObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        MeshFilter meshFilter = tracerObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = tracerObject.AddComponent<MeshRenderer>();
        tracerMesh = new Mesh
        {
            name = "Core Tactical Machine Gun Mount Tracer Mesh"
        };
        tracerMesh.MarkDynamic();
        meshFilter.sharedMesh = tracerMesh;
        tracerMaterial = CreateTransparentMaterial(tracerColor);
        meshRenderer.sharedMaterial = tracerMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
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

    private static Material CreateMaterial(Color color)
    {
        return CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(color, 1.15f);
    }

    private static Material CreateTransparentMaterial(Color color)
    {
        return CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(color);
    }

    private static float GetFlatDistance(Vector3 first, Vector3 second)
    {
        Vector3 delta = second - first;
        delta.y = 0f;
        return delta.magnitude;
    }
}

[DisallowMultipleComponent]
public sealed class CoreTacticalMachineGunAura : MonoBehaviour
{
    private const int MaxActiveTracers = 512;

    public CoreTacticalShipMotor owner;
    public CoreTacticalShipMotor target;
    public float radiusMeters = 2000f;
    public float damageTickIntervalSeconds = 0.5f;
    public float damagePerTick = 2.5f;
    public float machineGunResistanceIgnorePercent = 10f;
    public float tracerRatePerSecond = 220f;
    public float tracerSpeedMS = 2200f;
    public float tracerLengthMeters = 10f;
    public float tracerWidthMeters = 0.9f;
    public float tracerDistanceLengthScale = 0.006f;
    public float tracerDistanceWidthScale = 0.00018f;
    public float tracerTargetSpreadMeters = 205f;
    public float tracerTargetVerticalSpreadScale = 0.28f;
    public float tracerCorePassChance = 0.14f;
    public Color tracerColor = new Color(0.72f, 0.48f, 0.24f, 0.78f);

    private readonly MachineGunTracer[] activeTracers = new MachineGunTracer[MaxActiveTracers];
    private readonly List<Vector3> meshVertices = new List<Vector3>(MaxActiveTracers * 4);
    private readonly List<Color> meshColors = new List<Color>(MaxActiveTracers * 4);
    private readonly List<int> meshIndices = new List<int>(MaxActiveTracers * 6);
    private CoreTacticalPrototypeHealth targetHealth;
    private Camera tracerCamera;
    private GameObject tracerObject;
    private Mesh tracerMesh;
    private Material tracerMaterial;
    private float nextDamageTickTime;
    private float tracerSpawnBudget;
    private int nextTracerSlot;

    private struct MachineGunTracer
    {
        public bool active;
        public Vector3 startPosition;
        public Vector3 direction;
        public float distanceMeters;
        public float progress01;
        public Color color;
    }

    private void Awake()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        tracerCamera = Camera.main;
        targetHealth = target != null ? target.GetComponent<CoreTacticalPrototypeHealth>() : null;
        nextDamageTickTime = Time.time + Mathf.Max(0.05f, damageTickIntervalSeconds);
        EnsureTracerRenderer();
    }

    private void OnDestroy()
    {
        if (tracerObject != null)
        {
            Destroy(tracerObject);
        }

        if (tracerMesh != null)
        {
            Destroy(tracerMesh);
        }

        if (tracerMaterial != null)
        {
            Destroy(tracerMaterial);
        }
    }

    private void Update()
    {
        float deltaSeconds = Mathf.Max(0.001f, Time.deltaTime);
        bool targetInsideAura = owner != null
            && target != null
            && GetFlatDistance(owner.transform.position, target.transform.position) <= radiusMeters
            && HasClearLineOfFire();

        if (targetInsideAura)
        {
            ProcessDamageTick();
            SpawnTracerWave(deltaSeconds);
        }

        UpdateActiveTracerMesh(deltaSeconds);
    }

    private bool HasClearLineOfFire()
    {
        if (owner == null || target == null)
        {
            return false;
        }

        float occlusionRadius = Mathf.Max(1.5f, tracerWidthMeters * 2.5f);
        return !CoreTacticalWeaponOcclusion.HasObstacleBetween(
            owner.transform.position,
            target.transform.position,
            occlusionRadius);
    }

    private void ProcessDamageTick()
    {
        float now = Time.time;
        if (now < nextDamageTickTime)
        {
            return;
        }

        targetHealth ??= target != null ? target.GetComponent<CoreTacticalPrototypeHealth>() : null;
        if (targetHealth != null)
        {
            targetHealth.ApplyDamage(CoreTacticalDamageRequest.Kinetic(
                damagePerTick,
                "Machine gun aura",
                machineGunResistanceIgnorePercent));
        }

        nextDamageTickTime = now + Mathf.Max(0.05f, damageTickIntervalSeconds);
    }

    private void SpawnTracerWave(float deltaSeconds)
    {
        tracerSpawnBudget += Mathf.Max(0f, tracerRatePerSecond) * deltaSeconds;
        int spawnCount = Mathf.Min(Mathf.FloorToInt(tracerSpawnBudget), 24);
        if (spawnCount <= 0)
        {
            return;
        }

        tracerSpawnBudget = Mathf.Min(tracerSpawnBudget - spawnCount, MaxActiveTracers);
        for (int i = 0; i < spawnCount; i++)
        {
            SpawnTracer();
        }
    }

    private void SpawnTracer()
    {
        if (owner == null || target == null)
        {
            return;
        }

        Vector3 start = GetEmitterPoint();
        Vector3 aimPoint = GetTargetPoint(start);
        Vector3 direction = aimPoint - start;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        direction.Normalize();
        Color color = tracerColor;
        color.a *= Random.Range(0.45f, 1f);
        activeTracers[nextTracerSlot] = new MachineGunTracer
        {
            active = true,
            startPosition = start,
            direction = direction,
            distanceMeters = Mathf.Max(1f, radiusMeters),
            progress01 = 0f,
            color = color
        };
        nextTracerSlot = (nextTracerSlot + 1) % activeTracers.Length;
    }

    private void UpdateActiveTracerMesh(float deltaSeconds)
    {
        EnsureTracerRenderer();
        meshVertices.Clear();
        meshColors.Clear();
        meshIndices.Clear();

        Camera camera = GetTracerCamera();
        float travelStep = Mathf.Max(1f, tracerSpeedMS) * deltaSeconds;
        for (int i = 0; i < activeTracers.Length; i++)
        {
            MachineGunTracer tracer = activeTracers[i];
            if (!tracer.active)
            {
                continue;
            }

            tracer.progress01 += travelStep / Mathf.Max(1f, tracer.distanceMeters);
            if (tracer.progress01 >= 1f)
            {
                tracer.active = false;
                activeTracers[i] = tracer;
                continue;
            }

            Vector3 head = tracer.startPosition + tracer.direction * (tracer.distanceMeters * Mathf.Clamp01(tracer.progress01));
            Vector3 center = head;
            float cameraDistance = camera != null ? Vector3.Distance(camera.transform.position, center) : 0f;
            float segmentLength = Mathf.Max(
                Mathf.Max(1f, tracerLengthMeters),
                cameraDistance * Mathf.Max(0f, tracerDistanceLengthScale));
            Vector3 tail = tracer.startPosition + tracer.direction * Mathf.Max(0f, tracer.distanceMeters * tracer.progress01 - segmentLength);
            center = (tail + head) * 0.5f;
            Vector3 viewDirection = camera != null ? camera.transform.position - center : Vector3.up;
            Vector3 widthDirection = Vector3.Cross(tracer.direction, viewDirection);
            if (widthDirection.sqrMagnitude <= 0.0001f)
            {
                widthDirection = camera != null ? camera.transform.up : Vector3.up;
            }

            widthDirection.Normalize();
            cameraDistance = camera != null ? Vector3.Distance(camera.transform.position, center) : 0f;
            float tracerWidth = Mathf.Clamp(
                Mathf.Max(tracerWidthMeters, cameraDistance * Mathf.Max(0f, tracerDistanceWidthScale)),
                0.05f,
                3.0f);
            Color color = tracer.color;
            color.a *= Mathf.Clamp01(Mathf.InverseLerp(1f, 0.9f, tracer.progress01));
            AddTracerQuad(tail, head, widthDirection, tracerWidth, color);
            activeTracers[i] = tracer;
        }

        ApplyTracerMesh();
    }

    private void AddTracerQuad(Vector3 tail, Vector3 head, Vector3 widthDirection, float widthMeters, Color color)
    {
        Vector3 halfWidth = widthDirection * (widthMeters * 0.5f);
        int vertexIndex = meshVertices.Count;
        meshVertices.Add(tail - halfWidth);
        meshVertices.Add(tail + halfWidth);
        meshVertices.Add(head + halfWidth);
        meshVertices.Add(head - halfWidth);

        meshColors.Add(color);
        meshColors.Add(color);
        meshColors.Add(color);
        meshColors.Add(color);

        meshIndices.Add(vertexIndex);
        meshIndices.Add(vertexIndex + 1);
        meshIndices.Add(vertexIndex + 2);
        meshIndices.Add(vertexIndex);
        meshIndices.Add(vertexIndex + 2);
        meshIndices.Add(vertexIndex + 3);
    }

    private Vector3 GetEmitterPoint()
    {
        Vector3 hullSize = owner != null ? owner.hullSizeMeters : transform.localScale;
        Vector3 ownerPosition = owner != null ? owner.transform.position : transform.position;
        Quaternion ownerRotation = owner != null ? owner.transform.rotation : transform.rotation;
        float x = Random.Range(-hullSize.x * 0.52f, hullSize.x * 0.52f);
        float y = Random.Range(-hullSize.y * 0.10f, hullSize.y * 0.42f);
        float z = Random.Range(-hullSize.z * 0.48f, hullSize.z * 0.48f);
        return ownerPosition
            + (ownerRotation * Vector3.right) * x
            + (ownerRotation * Vector3.up) * y
            + (ownerRotation * Vector3.forward) * z;
    }

    private Vector3 GetTargetPoint(Vector3 emitterPosition)
    {
        Vector3 targetPosition = target != null ? target.transform.position : transform.position + transform.forward * radiusMeters;
        Vector3 hullSize = target != null ? target.hullSizeMeters : Vector3.one * 20f;
        Vector3 fireDirection = targetPosition - emitterPosition;
        if (fireDirection.sqrMagnitude <= 0.001f)
        {
            fireDirection = transform.forward;
        }

        fireDirection.Normalize();
        Vector3 tunnelRight = Vector3.Cross(Vector3.up, fireDirection);
        if (tunnelRight.sqrMagnitude <= 0.001f)
        {
            tunnelRight = transform.right;
        }

        tunnelRight.Normalize();
        Vector3 tunnelUp = Vector3.Cross(fireDirection, tunnelRight).normalized;
        float rangeRatio = Mathf.Clamp01(GetFlatDistance(emitterPosition, targetPosition) / Mathf.Max(1f, radiusMeters));
        float hullRadius = Mathf.Max(hullSize.x, hullSize.z) * 0.45f;
        float tunnelRadius = Mathf.Lerp(
            Mathf.Max(18f, hullRadius * 0.65f),
            Mathf.Max(hullRadius, tracerTargetSpreadMeters),
            rangeRatio);

        Vector2 tunnelOffset = Random.insideUnitCircle * tunnelRadius;
        if (Random.value < Mathf.Clamp01(tracerCorePassChance))
        {
            tunnelOffset *= 0.28f;
        }

        return targetPosition
            + tunnelRight * tunnelOffset.x
            + tunnelUp * (tunnelOffset.y * Mathf.Clamp(tracerTargetVerticalSpreadScale, 0.05f, 1f));
    }

    private void ClearTracerMesh()
    {
        EnsureTracerRenderer();
        tracerMesh.Clear(false);
    }

    private void ApplyTracerMesh()
    {
        tracerMesh.Clear(false);
        if (meshVertices.Count == 0)
        {
            return;
        }

        tracerMesh.SetVertices(meshVertices);
        tracerMesh.SetColors(meshColors);
        tracerMesh.SetIndices(meshIndices, MeshTopology.Triangles, 0, true);
        tracerMesh.RecalculateBounds();
    }

    private Camera GetTracerCamera()
    {
        if (tracerCamera == null)
        {
            tracerCamera = Camera.main;
        }

        return tracerCamera;
    }

    private void EnsureTracerRenderer()
    {
        if (tracerMesh != null && tracerObject != null)
        {
            return;
        }

        tracerObject = new GameObject("Core Tactical Machine Gun Tracers");
        tracerObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        MeshFilter meshFilter = tracerObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = tracerObject.AddComponent<MeshRenderer>();
        tracerMesh = new Mesh
        {
            name = "Core Tactical Machine Gun Tracer Mesh"
        };
        tracerMesh.MarkDynamic();
        meshFilter.sharedMesh = tracerMesh;
        tracerMaterial = CreateTracerMaterial();
        meshRenderer.sharedMaterial = tracerMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private Material CreateTracerMaterial()
    {
        return CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(tracerColor);
    }

    private static float GetFlatDistance(Vector3 first, Vector3 second)
    {
        Vector3 delta = second - first;
        delta.y = 0f;
        return delta.magnitude;
    }

}

[DisallowMultipleComponent]
public sealed class CoreTacticalSecondaryBattery : MonoBehaviour
{
    private const int PortSide = -1;
    private const int StarboardSide = 1;
    private const float FireRateMultiplier = 1.35f;
    private const float TracerTrailSeconds = 0.48f;
    private const float FlakBurstRadiusMeters = 10f;
    private const float ReloadRandomSpread = 0.1f;
    private const float ReadyFireDesyncFraction = 0.5f;
    private const int AimPredictionIterations = 3;

    public CoreTacticalShipMotor owner;
    public CoreTacticalShipMotor target;
    public float maxRangeMeters = 10000f;
    public float suppressFireWithinRangeMeters;
    public float dispersionAtMaxRangeMeters = 200f;
    public float gravityMS2 = 9.81f;
    // Intentional design choice: linear drag keeps the firing solution analytic and cheap,
    // while making long-range shells lose horizontal speed and fall on a steeper final arc.
    // Keep the solver and projectile visual on this same model; a vacuum parabola loses that feel.
    public float projectileLinearDragK = 0.025f;

    private BatteryGroup[] groups;
    private Material shellMaterial76mm;
    private Material shellMaterial125mm;
    private Material tracerMaterial;

    private void Awake()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        EnsureGroups();
    }

    private void Update()
    {
        if (owner == null || target == null)
        {
            return;
        }

        EnsureGroups();
        Vector3 targetPosition = target.transform.position;
        Vector3 ownerPosition = owner.transform.position;
        float flatDistance = Vector3.Distance(
            new Vector3(ownerPosition.x, 0f, ownerPosition.z),
            new Vector3(targetPosition.x, 0f, targetPosition.z));
        if (suppressFireWithinRangeMeters > 0f && flatDistance <= suppressFireWithinRangeMeters)
        {
            HoldFireSchedules(Time.time);
            return;
        }

        if (flatDistance > maxRangeMeters)
        {
            HoldFireSchedules(Time.time);
            return;
        }

        float now = Time.time;
        for (int i = 0; i < groups.Length; i++)
        {
            BatteryGroup group = groups[i];
            bool targetOnPort = IsTargetOnSide(targetPosition, PortSide);
            bool targetOnStarboard = IsTargetOnSide(targetPosition, StarboardSide);
            if (targetOnPort)
            {
                ProcessSide(group, PortSide, group.portNextFireTimes, targetPosition, flatDistance, now);
            }
            else
            {
                group.HoldFireTimes(group.portNextFireTimes, now, ReadyFireDesyncFraction);
            }

            if (targetOnStarboard)
            {
                ProcessSide(group, StarboardSide, group.starboardNextFireTimes, targetPosition, flatDistance, now);
            }
            else
            {
                group.HoldFireTimes(group.starboardNextFireTimes, now, ReadyFireDesyncFraction);
            }
        }
    }

    private void HoldFireSchedules(float now)
    {
        if (groups == null)
        {
            return;
        }

        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].HoldAllFireTimes(now, ReadyFireDesyncFraction);
        }
    }

    private void EnsureGroups()
    {
        if (groups != null)
        {
            return;
        }

        groups = new[]
        {
            new BatteryGroup(
                "76 mm PMK",
                gunsPerSide: 20,
                reloadSeconds: 3.6f / FireRateMultiplier,
                muzzleVelocityMS: 520f,
                shellVisualScale: 3.4f,
                burstRadiusMeters: FlakBurstRadiusMeters,
                tracerTrailSeconds: TracerTrailSeconds,
                CreateShellMaterial(new Color(1f, 0.72f, 0.24f, 1f), ref shellMaterial76mm),
                CreateTracerMaterial(ref tracerMaterial)),
            new BatteryGroup(
                "152 mm PMK",
                gunsPerSide: 10,
                reloadSeconds: 7f / FireRateMultiplier,
                muzzleVelocityMS: 650f,
                shellVisualScale: 5.4f,
                burstRadiusMeters: 20f,
                tracerTrailSeconds: TracerTrailSeconds,
                CreateShellMaterial(new Color(1f, 0.94f, 0.48f, 1f), ref shellMaterial125mm),
                CreateTracerMaterial(ref tracerMaterial))
        };

        float now = Time.time;
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].StaggerInitialFireTimes(now);
        }
    }

    private void ProcessSide(
        BatteryGroup group,
        int sideSign,
        float[] nextFireTimes,
        Vector3 targetPosition,
        float flatDistance,
        float now)
    {
        for (int gunIndex = 0; gunIndex < nextFireTimes.Length; gunIndex++)
        {
            if (now < nextFireTimes[gunIndex])
            {
                continue;
            }

            FireGun(group, sideSign, gunIndex, targetPosition, flatDistance);
            nextFireTimes[gunIndex] = now + group.GetRandomizedReloadSeconds(ReloadRandomSpread);
        }
    }

    private void FireGun(BatteryGroup group, int sideSign, int gunIndex, Vector3 targetPosition, float flatDistance)
    {
        Vector3 muzzlePosition = GetMuzzlePosition(sideSign, gunIndex, group.gunsPerSide);
        if (!TryPredictAimPoint(muzzlePosition, targetPosition, group, out Vector3 predictedAimPoint, out float predictedFlatDistance))
        {
            return;
        }

        float gravity = Mathf.Max(0.01f, gravityMS2);
        float linearDragK = Mathf.Max(0f, projectileLinearDragK);
        if (!TrySolveBallisticVelocity(
                muzzlePosition,
                predictedAimPoint,
                group.muzzleVelocityMS,
                gravity,
                linearDragK,
                out Vector3 idealInitialVelocity,
                out float idealFlightTime))
        {
            return;
        }

        float shellObstacleRadius = Mathf.Max(1.5f, group.shellVisualScale * 0.45f);
        if (CoreTacticalWeaponOcclusion.BallisticPathHitsObstacle(
                muzzlePosition,
                idealInitialVelocity,
                gravity,
                linearDragK,
                idealFlightTime,
                shellObstacleRadius,
                out _))
        {
            return;
        }

        Vector3 aimedPosition = ApplyDispersion(predictedAimPoint, predictedFlatDistance);
        if (!TrySolveBallisticVelocity(
                muzzlePosition,
                aimedPosition,
                group.muzzleVelocityMS,
                gravity,
                linearDragK,
                out Vector3 initialVelocity,
                out float flightTime))
        {
            return;
        }

        CoreTacticalProjectileVisual.Create(
            group.displayName + " Shell",
            muzzlePosition,
            initialVelocity,
            Mathf.Max(0.01f, gravityMS2),
            Mathf.Max(0f, projectileLinearDragK),
            maxRangeMeters,
            group.shellVisualScale,
            group.tracerTrailSeconds,
            flightTime,
            group.burstRadiusMeters,
            group.shellMaterial,
            group.tracerMaterial,
            target);
        CoreTacticalLeviathanController.NotifyArtilleryReport(muzzlePosition, maxRangeMeters, owner);
    }

    private bool TryPredictAimPoint(
        Vector3 muzzlePosition,
        Vector3 targetPosition,
        BatteryGroup group,
        out Vector3 predictedAimPoint,
        out float predictedFlatDistance)
    {
        predictedAimPoint = targetPosition;
        predictedFlatDistance = GetFlatDistance(muzzlePosition, predictedAimPoint);

        Vector3 targetVelocity = target != null && target.Body != null
            ? target.Body.linearVelocity
            : Vector3.zero;
        if (targetVelocity.sqrMagnitude <= 0.001f)
        {
            return true;
        }

        float gravity = Mathf.Max(0.01f, gravityMS2);
        float linearDragK = Mathf.Max(0f, projectileLinearDragK);
        for (int i = 0; i < AimPredictionIterations; i++)
        {
            if (!TrySolveBallisticVelocity(
                    muzzlePosition,
                    predictedAimPoint,
                    group.muzzleVelocityMS,
                    gravity,
                    linearDragK,
                    out _,
                    out float flightTime))
            {
                return false;
            }

            predictedAimPoint = targetPosition + targetVelocity * flightTime;
        }

        predictedFlatDistance = GetFlatDistance(muzzlePosition, predictedAimPoint);
        return predictedFlatDistance <= maxRangeMeters;
    }

    private Vector3 GetMuzzlePosition(int sideSign, int gunIndex, int gunsPerSide)
    {
        Vector3 hullSize = owner != null ? owner.hullSizeMeters : transform.localScale;
        float t = gunsPerSide <= 1 ? 0.5f : gunIndex / (float)(gunsPerSide - 1);
        float z = Mathf.Lerp(-hullSize.z * 0.42f, hullSize.z * 0.42f, t);
        float x = sideSign * (hullSize.x * 0.54f);
        float y = hullSize.y * 0.12f;
        return transform.position
            + transform.right * x
            + transform.up * y
            + transform.forward * z;
    }

    private Vector3 ApplyDispersion(Vector3 targetPosition, float flatDistance)
    {
        float dispersionRadius = dispersionAtMaxRangeMeters * Mathf.Clamp01(flatDistance / Mathf.Max(1f, maxRangeMeters));
        Vector2 randomOffset = Random.insideUnitCircle * dispersionRadius;
        return targetPosition + new Vector3(randomOffset.x, 0f, randomOffset.y);
    }

    private static float GetFlatDistance(Vector3 first, Vector3 second)
    {
        Vector3 flatDelta = second - first;
        flatDelta.y = 0f;
        return flatDelta.magnitude;
    }

    private bool IsTargetOnSide(Vector3 targetPosition, int sideSign)
    {
        Vector3 toTarget = targetPosition - transform.position;
        return Vector3.Dot(toTarget, transform.right) * sideSign > 0f;
    }

    private static bool TrySolveBallisticVelocity(
        Vector3 start,
        Vector3 end,
        float speed,
        float gravity,
        float linearDragK,
        out Vector3 initialVelocity,
        out float flightTime)
    {
        initialVelocity = Vector3.zero;
        flightTime = 0f;

        Vector3 delta = end - start;
        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        float flatDistance = flatDelta.magnitude;
        if (flatDistance <= 0.01f || speed <= 0.01f)
        {
            return false;
        }

        if (linearDragK <= 0.0001f)
        {
            return TrySolveVacuumBallisticVelocity(start, end, speed, gravity, out initialVelocity, out flightTime);
        }

        float speedSquared = speed * speed;
        float previousTime = 0.02f;
        float previousError = GetLinearDragRequiredSpeedSqr(delta, previousTime, gravity, linearDragK) - speedSquared;
        bool hasBracket = false;
        float lowerTime = previousTime;
        float upperTime = previousTime;
        const float searchStepSeconds = 0.08f;
        const float maxSearchSeconds = 90f;

        for (float time = previousTime + searchStepSeconds; time <= maxSearchSeconds; time += searchStepSeconds)
        {
            float error = GetLinearDragRequiredSpeedSqr(delta, time, gravity, linearDragK) - speedSquared;
            if (previousError > 0f && error <= 0f)
            {
                lowerTime = previousTime;
                upperTime = time;
                hasBracket = true;
                break;
            }

            previousTime = time;
            previousError = error;
        }

        if (!hasBracket)
        {
            return false;
        }

        for (int i = 0; i < 36; i++)
        {
            float midTime = (lowerTime + upperTime) * 0.5f;
            float midError = GetLinearDragRequiredSpeedSqr(delta, midTime, gravity, linearDragK) - speedSquared;
            if (midError > 0f)
            {
                lowerTime = midTime;
            }
            else
            {
                upperTime = midTime;
            }
        }

        flightTime = (lowerTime + upperTime) * 0.5f;
        float dragTravel = GetLinearDragTravelFactor(flightTime, linearDragK);
        Vector3 flatVelocity = flatDelta / Mathf.Max(0.0001f, dragTravel);
        float verticalVelocity = (delta.y + gravity * flightTime / linearDragK) / Mathf.Max(0.0001f, dragTravel)
            - gravity / linearDragK;
        initialVelocity = flatVelocity + Vector3.up * verticalVelocity;
        return flightTime > 0.01f;
    }

    private static bool TrySolveVacuumBallisticVelocity(
        Vector3 start,
        Vector3 end,
        float speed,
        float gravity,
        out Vector3 initialVelocity,
        out float flightTime)
    {
        initialVelocity = Vector3.zero;
        flightTime = 0f;

        Vector3 delta = end - start;
        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        float flatDistance = flatDelta.magnitude;
        if (flatDistance <= 0.01f || speed <= 0.01f)
        {
            return false;
        }

        float speedSquared = speed * speed;
        float underRoot = speedSquared * speedSquared
            - gravity * (gravity * flatDistance * flatDistance + 2f * delta.y * speedSquared);
        if (underRoot < 0f)
        {
            return false;
        }

        float lowAngleTangent = (speedSquared - Mathf.Sqrt(underRoot)) / (gravity * flatDistance);
        float angle = Mathf.Atan(lowAngleTangent);
        Vector3 flatDirection = flatDelta / flatDistance;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        initialVelocity = flatDirection * (cos * speed) + Vector3.up * (sin * speed);
        flightTime = flatDistance / Mathf.Max(0.01f, cos * speed);
        return flightTime > 0.01f;
    }

    private static float GetLinearDragRequiredSpeedSqr(Vector3 delta, float time, float gravity, float linearDragK)
    {
        Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
        float dragTravel = GetLinearDragTravelFactor(time, linearDragK);
        if (dragTravel <= 0.0001f)
        {
            return float.PositiveInfinity;
        }

        Vector3 flatVelocity = flatDelta / dragTravel;
        float verticalVelocity = (delta.y + gravity * time / linearDragK) / dragTravel - gravity / linearDragK;
        return flatVelocity.sqrMagnitude + verticalVelocity * verticalVelocity;
    }

    private static float GetLinearDragTravelFactor(float time, float linearDragK)
    {
        return (1f - Mathf.Exp(-linearDragK * Mathf.Max(0f, time))) / linearDragK;
    }

    private static Material CreateShellMaterial(Color color, ref Material cachedMaterial)
    {
        if (cachedMaterial != null)
        {
            return cachedMaterial;
        }

        cachedMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(color, 2.4f);
        return cachedMaterial;
    }

    private static Material CreateTracerMaterial(ref Material cachedMaterial)
    {
        if (cachedMaterial != null)
        {
            return cachedMaterial;
        }

        Color smokeColor = new Color(0.82f, 0.86f, 0.88f, 0.28f);
        cachedMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(smokeColor);
        return cachedMaterial;
    }

    private sealed class BatteryGroup
    {
        public readonly string displayName;
        public readonly int gunsPerSide;
        public readonly float reloadSeconds;
        public readonly float muzzleVelocityMS;
        public readonly float shellVisualScale;
        public readonly float burstRadiusMeters;
        public readonly float tracerTrailSeconds;
        public readonly Material shellMaterial;
        public readonly Material tracerMaterial;
        public readonly float[] portNextFireTimes;
        public readonly float[] starboardNextFireTimes;

        public BatteryGroup(
            string displayName,
            int gunsPerSide,
            float reloadSeconds,
            float muzzleVelocityMS,
            float shellVisualScale,
            float burstRadiusMeters,
            float tracerTrailSeconds,
            Material shellMaterial,
            Material tracerMaterial)
        {
            this.displayName = displayName;
            this.gunsPerSide = Mathf.Max(1, gunsPerSide);
            this.reloadSeconds = Mathf.Max(0.1f, reloadSeconds);
            this.muzzleVelocityMS = Mathf.Max(1f, muzzleVelocityMS);
            this.shellVisualScale = Mathf.Max(0.1f, shellVisualScale);
            this.burstRadiusMeters = Mathf.Max(0.1f, burstRadiusMeters);
            this.tracerTrailSeconds = Mathf.Max(0.1f, tracerTrailSeconds);
            this.shellMaterial = shellMaterial;
            this.tracerMaterial = tracerMaterial;
            portNextFireTimes = new float[this.gunsPerSide];
            starboardNextFireTimes = new float[this.gunsPerSide];
        }

        public void StaggerInitialFireTimes(float now)
        {
            HoldAllFireTimes(now, ReadyFireDesyncFraction);
        }

        public void HoldAllFireTimes(float now, float desyncFraction)
        {
            HoldFireTimes(portNextFireTimes, now, desyncFraction);
            HoldFireTimes(starboardNextFireTimes, now, desyncFraction);
        }

        public void HoldFireTimes(float[] nextFireTimes, float now, float desyncFraction)
        {
            if (nextFireTimes == null)
            {
                return;
            }

            float desyncWindowSeconds = reloadSeconds * Mathf.Clamp01(desyncFraction);
            float jitterSeconds = reloadSeconds * 0.04f;
            for (int i = 0; i < nextFireTimes.Length; i++)
            {
                if (nextFireTimes[i] > now)
                {
                    continue;
                }

                float phase = i / (float)Mathf.Max(1, nextFireTimes.Length);
                nextFireTimes[i] = now + phase * desyncWindowSeconds + Random.Range(0f, jitterSeconds);
            }
        }

        public float GetRandomizedReloadSeconds(float spread)
        {
            float safeSpread = Mathf.Clamp01(spread);
            return reloadSeconds * Random.Range(1f - safeSpread, 1f + safeSpread);
        }
    }
}

public enum CoreTacticalProjectileDetonationMode
{
    AirBurst,
    DirectImpact
}

public sealed class CoreTacticalProjectileVisual : MonoBehaviour
{
    private const int TrailSampleCount = 4;
    private const float MaxStableTrailMeters = 260f;
    private const float MaxFuseTimingErrorSeconds = 0.055f;
    private const float FuseTimingErrorFlightFraction = 0.035f;
    private const float DirectImpactMissRangeMultiplier = 1.5f;
    private const float CameraDistanceWidthScale = 0.00105f;
    private const float HeadCameraDistanceLengthScale = 0.0016f;
    private const float HeadCameraDistanceWidthScale = 0.00034f;
    private const float MinTracerCoreWidthMeters = 0.34f;
    private const float MinTracerGlowWidthMeters = 0.72f;
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];

    private readonly Vector3[] headVertices = new Vector3[4];
    private readonly int[] headIndices = { 0, 1, 2, 0, 2, 3 };
    private readonly List<Vector3> tracerVertices = new List<Vector3>(TrailSampleCount * 2);
    private readonly List<Color> tracerColors = new List<Color>(TrailSampleCount * 2);
    private readonly List<int> tracerIndices = new List<int>((TrailSampleCount - 1) * 6);
    private Vector3 startPosition;
    private Vector3 initialVelocity;
    private float gravityMS2;
    private float linearDragK;
    private float birthTime;
    private float deathTime;
    private float trailSeconds;
    private float fuseSeconds;
    private float burstRadiusMeters;
    private float hitRadius;
    private float damageAmount;
    private CoreTacticalDamageType damageType;
    private float damageResistanceIgnorePercent;
    private float damageCaliberMm;
    private float damageDirectImpactFuseThresholdMeters;
    private float damageFireChancePercent;
    private float visualScale;
    private string damageSource;
    private CoreTacticalProjectileDetonationMode detonationMode;
    private bool damageApplied;
    private CoreTacticalShipMotor target;
    private Mesh headMesh;
    private GameObject tracerObject;
    private Mesh tracerMesh;
    private Camera tracerCamera;
    private Vector3 previousPosition;

    public static void Create(
        string projectileName,
        Vector3 startPosition,
        Vector3 initialVelocity,
        float gravityMS2,
        float linearDragK,
        float maxRangeMeters,
        float visualScale,
        float trailSeconds,
        float fuseSeconds,
        float burstRadiusMeters,
        Material shellMaterial,
        Material tracerMaterial,
        CoreTacticalShipMotor target,
        float damageAmount = 0f,
        string damageSource = "",
        CoreTacticalProjectileDetonationMode detonationMode = CoreTacticalProjectileDetonationMode.AirBurst,
        CoreTacticalDamageType damageType = CoreTacticalDamageType.Kinetic,
        float resistanceIgnorePercent = 0f,
        float caliberMm = 0f,
        float directImpactFuseThresholdMeters = 0f,
        float fireChancePercent = 0f)
    {
        GameObject projectile = new GameObject(projectileName);
        projectile.name = projectileName;
        projectile.transform.position = startPosition;
        MeshFilter shellMeshFilter = projectile.AddComponent<MeshFilter>();
        MeshRenderer shellRenderer = projectile.AddComponent<MeshRenderer>();
        Mesh headMesh = new Mesh
        {
            name = projectileName + " Head Streak Mesh"
        };
        headMesh.MarkDynamic();
        shellMeshFilter.sharedMesh = headMesh;
        shellRenderer.sharedMaterial = shellMaterial;
        shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shellRenderer.receiveShadows = false;

        GameObject tracerObject = new GameObject(projectileName + " Tracer Ribbon");
        tracerObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        MeshFilter tracerMeshFilter = tracerObject.AddComponent<MeshFilter>();
        MeshRenderer tracerRenderer = tracerObject.AddComponent<MeshRenderer>();
        Mesh tracerMesh = new Mesh
        {
            name = projectileName + " Tracer Mesh"
        };
        tracerMesh.MarkDynamic();
        tracerMeshFilter.sharedMesh = tracerMesh;
        tracerRenderer.sharedMaterial = tracerMaterial != null ? tracerMaterial : shellMaterial;
        tracerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tracerRenderer.receiveShadows = false;

        CoreTacticalProjectileVisual visual = projectile.AddComponent<CoreTacticalProjectileVisual>();
        visual.Initialize(
            startPosition,
            initialVelocity,
            gravityMS2,
            linearDragK,
            maxRangeMeters,
            visualScale,
            trailSeconds,
            fuseSeconds,
            burstRadiusMeters,
            headMesh,
            tracerObject,
            tracerMesh,
            target,
            damageAmount,
            damageSource,
            detonationMode,
            damageType,
            resistanceIgnorePercent,
            caliberMm,
            directImpactFuseThresholdMeters,
            fireChancePercent);
    }

    private void Initialize(
        Vector3 newStartPosition,
        Vector3 newInitialVelocity,
        float newGravityMS2,
        float newLinearDragK,
        float maxRangeMeters,
        float visualScale,
        float newTrailSeconds,
        float newFuseSeconds,
        float newBurstRadiusMeters,
        Mesh newHeadMesh,
        GameObject newTracerObject,
        Mesh newTracerMesh,
        CoreTacticalShipMotor newTarget,
        float newDamageAmount,
        string newDamageSource,
        CoreTacticalProjectileDetonationMode newDetonationMode,
        CoreTacticalDamageType newDamageType,
        float newResistanceIgnorePercent,
        float newCaliberMm,
        float newDirectImpactFuseThresholdMeters,
        float newFireChancePercent)
    {
        startPosition = newStartPosition;
        initialVelocity = newInitialVelocity;
        gravityMS2 = newGravityMS2;
        linearDragK = Mathf.Max(0f, newLinearDragK);
        birthTime = Time.time;
        detonationMode = newDetonationMode;
        float lifetimeRangeMeters = detonationMode == CoreTacticalProjectileDetonationMode.DirectImpact
            ? maxRangeMeters * DirectImpactMissRangeMultiplier
            : maxRangeMeters;
        float maxRangeFlightSeconds = GetMaxRangeFlightSeconds(lifetimeRangeMeters, initialVelocity, linearDragK);
        // Time fuses should not be perfect range computers: air bursts get a tiny clock error
        // so repeated shots do not pop on the same invisible point. Direct hits/obstacle hits stay exact.
        float fuseTimingError = Mathf.Min(MaxFuseTimingErrorSeconds, Mathf.Max(0f, newFuseSeconds) * FuseTimingErrorFlightFraction);
        fuseSeconds = detonationMode == CoreTacticalProjectileDetonationMode.AirBurst
            ? Mathf.Clamp(
                newFuseSeconds + Random.Range(-fuseTimingError, fuseTimingError),
                0.05f,
                maxRangeFlightSeconds)
            : maxRangeFlightSeconds;
        deathTime = birthTime + maxRangeFlightSeconds;
        trailSeconds = Mathf.Max(0.1f, newTrailSeconds);
        burstRadiusMeters = Mathf.Max(0.1f, newBurstRadiusMeters);
        hitRadius = Mathf.Max(0.4f, visualScale * 0.5f);
        this.visualScale = Mathf.Max(0.1f, visualScale);
        headMesh = newHeadMesh;
        tracerObject = newTracerObject;
        tracerMesh = newTracerMesh;
        tracerCamera = Camera.main;
        target = newTarget;
        damageAmount = Mathf.Max(0f, newDamageAmount);
        damageType = newDamageType;
        damageResistanceIgnorePercent = Mathf.Max(0f, newResistanceIgnorePercent);
        damageCaliberMm = Mathf.Max(0f, newCaliberMm);
        damageDirectImpactFuseThresholdMeters = Mathf.Max(0f, newDirectImpactFuseThresholdMeters);
        damageFireChancePercent = Mathf.Max(0f, newFireChancePercent);
        damageSource = string.IsNullOrWhiteSpace(newDamageSource) ? gameObject.name : newDamageSource;
        previousPosition = newStartPosition;
    }

    private void OnDestroy()
    {
        if (tracerObject != null)
        {
            CoreTacticalMeshFadeDestroyer.Attach(
                tracerObject,
                tracerMesh,
                0.55f,
                new Color(0.82f, 0.86f, 0.88f, 0.28f));
            tracerObject = null;
            tracerMesh = null;
        }

        if (headMesh != null)
        {
            Destroy(headMesh);
        }
    }

    private void Update()
    {
        float elapsed = Time.time - birthTime;
        if (detonationMode == CoreTacticalProjectileDetonationMode.AirBurst && elapsed >= fuseSeconds)
        {
            ExplodeAt(EvaluatePosition(fuseSeconds));
            Destroy(gameObject);
            return;
        }

        if (Time.time >= deathTime)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 position = EvaluatePosition(elapsed);
        Vector3 frameDelta = position - previousPosition;
        if (CoreTacticalAutomatonWreck.TrySphereCastWreck(previousPosition, position, hitRadius, out CoreTacticalAutomatonWreck wreckHit, out Vector3 wreckHitPosition))
        {
            if (detonationMode == CoreTacticalProjectileDetonationMode.AirBurst)
            {
                ExplodeAt(wreckHitPosition);
            }
            else
            {
                ImpactAt(wreckHitPosition, false);
                wreckHit.ApplyProjectileDamage(damageAmount, damageSource, wreckHitPosition);
                damageApplied = damageAmount > 0f;
            }

            Destroy(gameObject);
            return;
        }

        if (CoreTacticalWeaponOcclusion.SegmentHitsObstacle(previousPosition, position, hitRadius, out Vector3 obstacleHitPosition))
        {
            if (detonationMode == CoreTacticalProjectileDetonationMode.AirBurst)
            {
                ExplodeAt(obstacleHitPosition);
            }
            else
            {
                ImpactAt(obstacleHitPosition, false);
            }

            Destroy(gameObject);
            return;
        }

        if (HitsTarget(previousPosition, position, out Vector3 targetHitPosition))
        {
            if (detonationMode == CoreTacticalProjectileDetonationMode.AirBurst)
            {
                ExplodeAt(targetHitPosition);
            }
            else
            {
                ImpactAt(targetHitPosition, true);
            }

            Destroy(gameObject);
            return;
        }

        transform.position = position;
        UpdateShellHead(frameDelta);
        previousPosition = position;
        UpdateTracer(elapsed);
    }

    private void ExplodeAt(Vector3 position)
    {
        CoreTacticalFlakBurstVisual.Create(position, burstRadiusMeters);
        CoreTacticalLeviathanController.NotifyProjectileImpact(position, burstRadiusMeters, 0.06f);
        TryApplyDamageAt(position);
    }

    private void ImpactAt(Vector3 position, bool applyDirectDamage)
    {
        CoreTacticalFlakBurstVisual.Create(position, Mathf.Max(0.75f, visualScale * 0.75f));
        CoreTacticalLeviathanController.NotifyProjectileImpact(position, Mathf.Max(8f, hitRadius * 3f), applyDirectDamage ? 0.105f : 0.045f);
        if (applyDirectDamage)
        {
            TryApplyDirectDamageAt(position);
        }
    }

    private void TryApplyDamageAt(Vector3 position)
    {
        if (damageApplied || damageAmount <= 0f)
        {
            return;
        }

        int wreckDamageCount = CoreTacticalAutomatonWreck.ApplyExplosionDamageToWrecks(
            position,
            burstRadiusMeters,
            damageAmount,
            damageSource);

        if (target == null)
        {
            damageApplied = wreckDamageCount > 0;
            return;
        }

        CoreTacticalPrototypeHealth health = target.GetComponent<CoreTacticalPrototypeHealth>();
        if (health == null || health.currentHealth <= 0f)
        {
            damageApplied = wreckDamageCount > 0;
            return;
        }

        float closestDistance = GetClosestTargetDistance(position);
        if (closestDistance > Mathf.Max(0.1f, burstRadiusMeters))
        {
            damageApplied = wreckDamageCount > 0;
            return;
        }

        float damageScale = Mathf.Lerp(
            0.35f,
            1f,
            1f - Mathf.Clamp01(closestDistance / Mathf.Max(0.1f, burstRadiusMeters)));
        health.ApplyDamage(BuildDamageRequest(damageAmount * damageScale, position, burstRadiusMeters));
        damageApplied = true;
    }

    private void TryApplyDirectDamageAt(Vector3 position)
    {
        if (damageApplied || damageAmount <= 0f || target == null)
        {
            return;
        }

        CoreTacticalPrototypeHealth health = target.GetComponent<CoreTacticalPrototypeHealth>();
        if (health == null || health.currentHealth <= 0f)
        {
            return;
        }

        float closestDistance = GetClosestTargetDistance(position);
        if (closestDistance > Mathf.Max(0.1f, hitRadius))
        {
            return;
        }

        health.ApplyDamage(BuildDamageRequest(damageAmount, position, 0f));
        damageApplied = true;
    }

    private CoreTacticalDamageRequest BuildDamageRequest(float damage, Vector3 position, float explosionRadiusMeters)
    {
        return CoreTacticalDamageRequest.Create(
            damageType,
            damage,
            damageSource,
            damageResistanceIgnorePercent,
            damageFireChancePercent,
            explosionRadiusMeters,
            position,
            initialVelocity);
    }

    private float GetClosestTargetDistance(Vector3 position)
    {
        Collider[] colliders = target != null ? target.GetComponentsInChildren<Collider>() : null;
        float closestDistance = float.PositiveInfinity;
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                closestDistance = Mathf.Min(closestDistance, Vector3.Distance(position, collider.ClosestPoint(position)));
            }
        }

        if (float.IsPositiveInfinity(closestDistance))
        {
            closestDistance = target != null
                ? Vector3.Distance(position, target.transform.position)
                : float.PositiveInfinity;
        }

        return closestDistance;
    }

    private void UpdateShellHead(Vector3 frameDelta)
    {
        if (headMesh == null)
        {
            return;
        }

        Vector3 tangent = frameDelta.sqrMagnitude > 0.0001f ? frameDelta.normalized : initialVelocity.normalized;
        Camera camera = GetTracerCamera();
        Vector3 worldPosition = transform.position;
        Vector3 viewDirection = camera != null ? camera.transform.position - worldPosition : Vector3.up;
        Vector3 widthDirection = Vector3.Cross(tangent, viewDirection);
        if (widthDirection.sqrMagnitude <= 0.0001f)
        {
            widthDirection = camera != null ? camera.transform.up : Vector3.up;
        }

        widthDirection.Normalize();
        float cameraDistance = camera != null ? Vector3.Distance(camera.transform.position, worldPosition) : 0f;
        float length = Mathf.Clamp(
            Mathf.Max(visualScale * 3.8f, cameraDistance * HeadCameraDistanceLengthScale),
            visualScale * 2.8f,
            visualScale * 10.0f);
        float width = Mathf.Clamp(
            Mathf.Max(MinTracerCoreWidthMeters, visualScale * 0.55f, cameraDistance * HeadCameraDistanceWidthScale),
            Mathf.Max(MinTracerCoreWidthMeters, visualScale * 0.34f),
            Mathf.Max(MinTracerGlowWidthMeters, visualScale * 1.55f));

        Vector3 tail = -tangent * (length * 0.74f);
        Vector3 head = tangent * (length * 0.26f);
        Vector3 halfWidth = widthDirection * (width * 0.5f);
        headVertices[0] = tail - halfWidth;
        headVertices[1] = tail + halfWidth;
        headVertices[2] = head + halfWidth;
        headVertices[3] = head - halfWidth;

        headMesh.Clear(false);
        headMesh.vertices = headVertices;
        headMesh.SetIndices(headIndices, MeshTopology.Triangles, 0, true);
        headMesh.RecalculateBounds();
    }

    private void UpdateTracer(float elapsed)
    {
        if (tracerMesh == null)
        {
            return;
        }

        tracerVertices.Clear();
        tracerColors.Clear();
        tracerIndices.Clear();

        Camera camera = GetTracerCamera();
        Vector3 head = EvaluatePosition(elapsed);
        Vector3 previous = EvaluatePosition(Mathf.Max(0f, elapsed - 0.035f));
        Vector3 tangent = head - previous;
        if (tangent.sqrMagnitude <= 0.0001f)
        {
            tangent = initialVelocity;
        }

        tangent.Normalize();
        float requestedTrailSeconds = Mathf.Min(trailSeconds, elapsed);
        Vector3 timedTail = EvaluatePosition(Mathf.Max(0f, elapsed - requestedTrailSeconds));
        float currentTrailLength = Vector3.Distance(head, timedTail);
        float stableTrailLength = Mathf.Clamp(currentTrailLength, visualScale * 12f, MaxStableTrailMeters);
        Vector3 tail = head - tangent * stableTrailLength;
        Vector3 center = (head + tail) * 0.5f;
        Vector3 viewDirection = camera != null ? camera.transform.position - center : Vector3.up;
        Vector3 widthDirection = Vector3.Cross(tangent, viewDirection);
        if (widthDirection.sqrMagnitude <= 0.0001f)
        {
            widthDirection = camera != null ? camera.transform.up : Vector3.up;
        }

        widthDirection.Normalize();
        float cameraDistance = camera != null ? Vector3.Distance(camera.transform.position, center) : 0f;
        float width = Mathf.Clamp(
            Mathf.Max(MinTracerCoreWidthMeters, visualScale * 0.42f, cameraDistance * CameraDistanceWidthScale),
            Mathf.Max(MinTracerCoreWidthMeters, visualScale * 0.24f),
            Mathf.Max(MinTracerGlowWidthMeters, visualScale * 1.45f));
        float glowWidth = Mathf.Max(MinTracerGlowWidthMeters, width * 2.2f);

        AddTracerTaperedQuad(
            tail,
            head,
            widthDirection,
            glowWidth * 0.30f,
            glowWidth,
            new Color(1.00f, 0.42f, 0.08f, 0.035f),
            new Color(1.00f, 0.78f, 0.18f, 0.18f));

        AddTracerTaperedQuad(
            tail,
            head,
            widthDirection,
            width * 0.18f,
            width,
            new Color(1.00f, 0.64f, 0.18f, 0.06f),
            new Color(1.00f, 0.90f, 0.42f, 0.62f));

        tracerMesh.Clear(false);
        tracerMesh.SetVertices(tracerVertices);
        tracerMesh.SetColors(tracerColors);
        tracerMesh.SetIndices(tracerIndices, MeshTopology.Triangles, 0, true);
        tracerMesh.RecalculateBounds();
    }

    private void AddTracerQuad(Vector3 tail, Vector3 head, Vector3 widthDirection, float widthMeters, Color color)
    {
        Vector3 halfWidth = widthDirection * (widthMeters * 0.5f);
        int vertexIndex = tracerVertices.Count;
        tracerVertices.Add(tail - halfWidth);
        tracerVertices.Add(tail + halfWidth);
        tracerVertices.Add(head + halfWidth);
        tracerVertices.Add(head - halfWidth);

        tracerColors.Add(color);
        tracerColors.Add(color);
        tracerColors.Add(color);
        tracerColors.Add(color);

        tracerIndices.Add(vertexIndex);
        tracerIndices.Add(vertexIndex + 1);
        tracerIndices.Add(vertexIndex + 2);
        tracerIndices.Add(vertexIndex);
        tracerIndices.Add(vertexIndex + 2);
        tracerIndices.Add(vertexIndex + 3);
    }

    private void AddTracerTaperedQuad(
        Vector3 tail,
        Vector3 head,
        Vector3 widthDirection,
        float tailWidthMeters,
        float headWidthMeters,
        Color tailColor,
        Color headColor)
    {
        Vector3 tailHalfWidth = widthDirection * (tailWidthMeters * 0.5f);
        Vector3 headHalfWidth = widthDirection * (headWidthMeters * 0.5f);
        int vertexIndex = tracerVertices.Count;
        tracerVertices.Add(tail - tailHalfWidth);
        tracerVertices.Add(tail + tailHalfWidth);
        tracerVertices.Add(head + headHalfWidth);
        tracerVertices.Add(head - headHalfWidth);

        tracerColors.Add(tailColor);
        tracerColors.Add(tailColor);
        tracerColors.Add(headColor);
        tracerColors.Add(headColor);

        tracerIndices.Add(vertexIndex);
        tracerIndices.Add(vertexIndex + 1);
        tracerIndices.Add(vertexIndex + 2);
        tracerIndices.Add(vertexIndex);
        tracerIndices.Add(vertexIndex + 2);
        tracerIndices.Add(vertexIndex + 3);
    }

    private Camera GetTracerCamera()
    {
        if (tracerCamera == null)
        {
            tracerCamera = Camera.main;
        }

        return tracerCamera;
    }

    private bool HitsTarget(Vector3 from, Vector3 to, out Vector3 hitPosition)
    {
        hitPosition = to;
        if (target == null)
        {
            return false;
        }

        Vector3 segment = to - from;
        float distance = segment.magnitude;
        if (distance <= 0.001f)
        {
            return false;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            from,
            hitRadius,
            segment / distance,
            HitBuffer,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = HitBuffer[i].collider;
            if (hitCollider != null && hitCollider.GetComponentInParent<CoreTacticalShipMotor>() == target)
            {
                hitPosition = HitBuffer[i].point;
                if (hitPosition == Vector3.zero)
                {
                    hitPosition = to;
                }

                return true;
            }
        }

        return false;
    }

    private Vector3 EvaluatePosition(float elapsed)
    {
        if (linearDragK <= 0.0001f)
        {
            Vector3 vacuumGravity = Vector3.down * gravityMS2;
            return startPosition + initialVelocity * elapsed + 0.5f * vacuumGravity * elapsed * elapsed;
        }

        Vector3 gravity = Vector3.down * gravityMS2;
        float dragTravel = GetLinearDragTravelFactor(elapsed, linearDragK);
        return startPosition
            + gravity * (elapsed / linearDragK)
            + (initialVelocity - gravity / linearDragK) * dragTravel;
    }

    private static float GetMaxRangeFlightSeconds(float maxRangeMeters, Vector3 initialVelocity, float linearDragK)
    {
        Vector3 flatVelocity = initialVelocity;
        flatVelocity.y = 0f;
        float flatSpeed = flatVelocity.magnitude;
        if (flatSpeed <= 0.01f)
        {
            return 0.05f;
        }

        if (linearDragK <= 0.0001f)
        {
            return Mathf.Max(0.05f, maxRangeMeters / flatSpeed);
        }

        float rangeRatio = maxRangeMeters * linearDragK / flatSpeed;
        if (rangeRatio >= 0.999f)
        {
            return 60f;
        }

        return Mathf.Max(0.05f, -Mathf.Log(1f - Mathf.Max(0f, rangeRatio)) / linearDragK);
    }

    private static float GetLinearDragTravelFactor(float time, float linearDragK)
    {
        return (1f - Mathf.Exp(-linearDragK * Mathf.Max(0f, time))) / linearDragK;
    }

}

public sealed class CoreTacticalMeshFadeDestroyer : MonoBehaviour
{
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock propertyBlock;
    private Renderer targetRenderer;
    private Mesh ownedMesh;
    private Vector3[] originalVertices;
    private Vector3[] evaporatingVertices;
    private Color[] originalColors;
    private Color[] evaporatingColors;
    private Color baseColor;
    private float birthTime;
    private float lifetimeSeconds;

    public static void Attach(GameObject visualObject, Mesh mesh, float lifetimeSeconds, Color fadeColor)
    {
        if (visualObject == null)
        {
            if (mesh != null)
            {
                Destroy(mesh);
            }

            return;
        }

        CoreTacticalMeshFadeDestroyer fade = visualObject.AddComponent<CoreTacticalMeshFadeDestroyer>();
        fade.Initialize(mesh, lifetimeSeconds, fadeColor);
    }

    private void Initialize(Mesh mesh, float newLifetimeSeconds, Color fadeColor)
    {
        targetRenderer = GetComponent<Renderer>();
        ownedMesh = mesh;
        lifetimeSeconds = Mathf.Max(0.05f, newLifetimeSeconds);
        birthTime = Time.time;
        baseColor = fadeColor;
        if (ownedMesh != null)
        {
            originalVertices = ownedMesh.vertices;
            evaporatingVertices = new Vector3[originalVertices.Length];
            originalColors = ownedMesh.colors;
            if (originalColors.Length == originalVertices.Length)
            {
                evaporatingColors = new Color[originalColors.Length];
            }
        }
    }

    private void Update()
    {
        float normalizedAge = Mathf.Clamp01((Time.time - birthTime) / lifetimeSeconds);
        if (normalizedAge >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        UpdateEvaporatingMesh(normalizedAge);
        Color color = baseColor;
        color.a *= Mathf.Pow(1f - normalizedAge, 1.35f);
        if (targetRenderer != null)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.SetColor(BaseColorPropertyId, color);
            propertyBlock.SetColor(ColorPropertyId, color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void UpdateEvaporatingMesh(float normalizedAge)
    {
        if (ownedMesh == null || originalVertices == null || evaporatingVertices == null)
        {
            return;
        }

        float widthScale = Mathf.Pow(1f - normalizedAge, 1.7f);
        for (int i = 0; i < originalVertices.Length; i += 4)
        {
            if (i + 3 >= originalVertices.Length)
            {
                for (int j = i; j < originalVertices.Length; j++)
                {
                    evaporatingVertices[j] = originalVertices[j];
                }

                break;
            }

            Vector3 tailCenter = (originalVertices[i] + originalVertices[i + 1]) * 0.5f;
            Vector3 headCenter = (originalVertices[i + 2] + originalVertices[i + 3]) * 0.5f;
            evaporatingVertices[i] = Vector3.Lerp(tailCenter, originalVertices[i], widthScale);
            evaporatingVertices[i + 1] = Vector3.Lerp(tailCenter, originalVertices[i + 1], widthScale);
            evaporatingVertices[i + 2] = Vector3.Lerp(headCenter, originalVertices[i + 2], widthScale);
            evaporatingVertices[i + 3] = Vector3.Lerp(headCenter, originalVertices[i + 3], widthScale);
        }

        ownedMesh.vertices = evaporatingVertices;
        if (originalColors != null && evaporatingColors != null && originalColors.Length == evaporatingColors.Length)
        {
            float colorFade = Mathf.Pow(1f - normalizedAge, 1.2f);
            for (int i = 0; i < originalColors.Length; i++)
            {
                Color color = originalColors[i];
                color.a *= colorFade;
                evaporatingColors[i] = color;
            }

            ownedMesh.colors = evaporatingColors;
        }

        ownedMesh.RecalculateBounds();
    }

    private void OnDestroy()
    {
        if (ownedMesh != null)
        {
            Destroy(ownedMesh);
        }
    }
}

public sealed class CoreTacticalFlakBurstVisual : MonoBehaviour
{
    private const float MinBurstVisualRadiusMeters = 7f;
    private const float BurstVisualRadiusMultiplier = 1.35f;
    private const float BurstVisualLifetimeSeconds = 0.48f;
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int TintColorPropertyId = Shader.PropertyToID("_TintColor");
    private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");
    private static Mesh sharedBurstMesh;
    private static Material sharedBurstMaterial;
    private static MaterialPropertyBlock propertyBlock;

    private Renderer burstRenderer;
    private Camera targetCamera;
    private float radiusMeters;
    private float birthTime;
    private float lifetimeSeconds;

    public static void Create(Vector3 position, float radiusMeters)
    {
        GameObject burstObject = new GameObject("Core Tactical Flak Burst");
        burstObject.transform.position = position;
        MeshFilter meshFilter = burstObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = burstObject.AddComponent<MeshRenderer>();
        meshFilter.sharedMesh = GetBurstMesh();
        meshRenderer.sharedMaterial = GetBurstMaterial();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        CoreTacticalFlakBurstVisual burst = burstObject.AddComponent<CoreTacticalFlakBurstVisual>();
        burst.Initialize(meshRenderer, radiusMeters);
    }

    private void Initialize(Renderer newRenderer, float newRadiusMeters)
    {
        burstRenderer = newRenderer;
        targetCamera = Camera.main;
        radiusMeters = Mathf.Max(MinBurstVisualRadiusMeters, newRadiusMeters * BurstVisualRadiusMultiplier);
        birthTime = Time.time;
        lifetimeSeconds = BurstVisualLifetimeSeconds;
        UpdateVisual(0f);
    }

    private void Update()
    {
        float age = Time.time - birthTime;
        if (age >= lifetimeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        UpdateVisual(Mathf.Clamp01(age / lifetimeSeconds));
    }

    private void UpdateVisual(float normalizedAge)
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            transform.rotation = targetCamera.transform.rotation;
        }

        float scale = radiusMeters * 2f * Mathf.Lerp(0.42f, 1f, Mathf.Sin(normalizedAge * Mathf.PI * 0.5f));
        transform.localScale = new Vector3(scale, scale, scale);

        propertyBlock ??= new MaterialPropertyBlock();
        Color color = new Color(1f, 0.78f, 0.18f, Mathf.Lerp(0.88f, 0f, normalizedAge));
        burstRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorPropertyId, color);
        propertyBlock.SetColor(ColorPropertyId, color);
        propertyBlock.SetColor(TintColorPropertyId, color);
        propertyBlock.SetColor(EmissionColorPropertyId, color * 2.8f);
        burstRenderer.SetPropertyBlock(propertyBlock);
    }

    private static Mesh GetBurstMesh()
    {
        if (sharedBurstMesh != null)
        {
            return sharedBurstMesh;
        }

        const int SegmentCount = 28;
        Vector3[] vertices = new Vector3[SegmentCount + 1];
        Color[] colors = new Color[SegmentCount + 1];
        int[] indices = new int[SegmentCount * 3];
        vertices[0] = Vector3.zero;
        colors[0] = new Color(1f, 0.92f, 0.36f, 1f);
        for (int i = 0; i < SegmentCount; i++)
        {
            float angle = i / (float)SegmentCount * Mathf.PI * 2f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            colors[i + 1] = new Color(1f, 0.48f, 0.06f, 0.74f);
        }

        for (int i = 0; i < SegmentCount; i++)
        {
            int index = i * 3;
            indices[index] = 0;
            indices[index + 1] = i + 1;
            indices[index + 2] = i == SegmentCount - 1 ? 1 : i + 2;
        }

        sharedBurstMesh = new Mesh
        {
            name = "Core Tactical Flak Burst Disk"
        };
        sharedBurstMesh.vertices = vertices;
        sharedBurstMesh.colors = colors;
        sharedBurstMesh.SetIndices(indices, MeshTopology.Triangles, 0, true);
        sharedBurstMesh.RecalculateBounds();
        return sharedBurstMesh;
    }

    private static Material GetBurstMaterial()
    {
        if (sharedBurstMaterial != null)
        {
            return sharedBurstMaterial;
        }

        Color color = new Color(1f, 0.78f, 0.18f, 0.88f);
        sharedBurstMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(color);
        if (sharedBurstMaterial.HasProperty(EmissionColorPropertyId))
        {
            sharedBurstMaterial.EnableKeyword("_EMISSION");
        }

        return sharedBurstMaterial;
    }
}

public enum CoreTacticalMissileGuidanceMode
{
    PredictedIntercept,
    DirectChase
}

public sealed class CoreTacticalMissileLauncher : MonoBehaviour
{
    public CoreTacticalShipMotor owner;
    public CoreTacticalShipMotor target;
    public CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy;
    public float targetRefreshIntervalSeconds = 0.2f;
    public string missileName = "Core Tactical Missile";
    public CoreTacticalMissileGuidanceMode guidanceMode = CoreTacticalMissileGuidanceMode.PredictedIntercept;
    public CoreTacticalWeaponGroup weaponGroup = CoreTacticalWeaponGroup.Missiles;
    public int sideSign = 1;
    public float localZScale = 0.2f;
    public Color missileColor = new Color(0.72f, 0.95f, 1f, 1f);
    public Color trailColor = new Color(0.48f, 0.66f, 0.72f, 1f);
    public float initialLaunchDelaySeconds = 0.75f;
    public float launchIntervalSeconds = 2f;
    public float missileSpeedMS = 300f;
    public float missileLifetimeSeconds = 20f;
    public float missileTurnRateDegPerSecond = 90f;
    public float guidanceIntervalSeconds = 0.12f;
    public float proximityRadiusMeters = 20f;
    public float explosionRadiusMeters = 32f;
    public float missileDamage = 95f;
    public CoreTacticalDamageType missileDamageType = CoreTacticalDamageType.Explosive;
    public float missileResistanceIgnorePercent = 90f;
    public float missileCaliberMm = 200f;
    public float missileFireChancePercent = 12f;
    public bool fullDamageInsideExplosionRadius;
    public bool manualLaunchOnly;
    public int projectilesPerManualSalvo = 1;
    public int automaticBurstProjectileCount = 1;
    public float automaticBurstShotIntervalSeconds = 0.08f;
    public float automaticBurstSpreadDegrees = 10f;
    public bool automaticBurstUsesChaoticCloud;
    public float automaticBurstCloudScatterDegrees = 7f;
    public float automaticBurstCloudForwardJitterDegrees = 3f;
    public float automaticBurstShotIntervalJitterSeconds = 0.04f;
    public float automaticBurstChaosAmplitudeDegrees = 12f;
    public float automaticBurstChaosFrequencyHz = 0.95f;
    public float manualFanAngleDegrees = 15f;
    public float manualAimSectorDegrees = 120f;
    public float manualCooldownSeconds = 8f;
    public string visualLauncherRole = "torpedo";

    private float nextLaunchTime;
    private CoreTacticalShipMotor automaticBurstTarget;
    private Vector3 automaticBurstCenterDirection = Vector3.forward;
    private int automaticBurstRemaining;
    private int automaticBurstLaunched;
    private int automaticBurstTotal;
    private float nextAutomaticBurstShotTime;
    private float automaticBurstSeed;
    private GameObject launcherObject;
    private Material launcherMaterial;
    private Material missileMaterial;
    private Material trailMaterial;
    private CoreTacticalWeaponControl weaponControl;
    private CoreTacticalCombatant[] targetCandidates = new CoreTacticalCombatant[0];
    private CoreTacticalVisualWeaponHandle visualLauncherHandle;
    private float nextTargetRefreshTime;
    private bool visualLauncherHandleResolved;
    private bool reloadCooldownStarted;

    public float ManualRangeMeters => Mathf.Max(1f, missileSpeedMS) * Mathf.Max(0.1f, missileLifetimeSeconds);
    public float ManualFanAngleDegrees => Mathf.Max(0f, manualFanAngleDegrees);
    public float ManualAimSectorDegrees => Mathf.Clamp(manualAimSectorDegrees, 1f, 180f);
    public int ProjectilesPerManualSalvo => Mathf.Max(1, projectilesPerManualSalvo);
    public int AutomaticBurstProjectileCount => Mathf.Max(1, automaticBurstProjectileCount);
    public float AutomaticBurstShotIntervalSeconds => Mathf.Max(0.01f, automaticBurstShotIntervalSeconds);
    public float AutomaticBurstSpreadDegrees => Mathf.Max(0f, automaticBurstSpreadDegrees);
    public bool AutomaticBurstUsesChaoticCloudForTests => automaticBurstUsesChaoticCloud;
    public float AutomaticBurstCloudScatterDegreesForTests => Mathf.Max(0f, automaticBurstCloudScatterDegrees);
    public float AutomaticBurstShotIntervalJitterSecondsForTests => Mathf.Max(0f, automaticBurstShotIntervalJitterSeconds);
    public float AutomaticBurstChaosAmplitudeDegreesForTests => Mathf.Max(0f, automaticBurstChaosAmplitudeDegrees);
    public float AutomaticBurstChaosFrequencyHzForTests => Mathf.Max(0f, automaticBurstChaosFrequencyHz);
    public float AutomaticBurstLauncherFanOffsetDegreesForTests => GetAutomaticBurstLauncherFanOffsetDegrees();
    public float ReloadCooldownRemainingSeconds => reloadCooldownStarted ? Mathf.Max(0f, nextLaunchTime - Time.time) : 0f;

    public void SetReloadCooldownRemainingSecondsForTests(float seconds)
    {
        nextLaunchTime = Time.time + Mathf.Max(0f, seconds);
        reloadCooldownStarted = seconds > 0.05f;
    }

    private void Awake()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        weaponControl = ResolveWeaponControl();
        nextLaunchTime = Time.time + Mathf.Max(0f, initialLaunchDelaySeconds);
        EnsureLauncherVisual();
    }

    private void OnDestroy()
    {
        if (launcherObject != null) Destroy(launcherObject);
        if (launcherMaterial != null) Destroy(launcherMaterial);
        if (missileMaterial != null) Destroy(missileMaterial);
        if (trailMaterial != null) Destroy(trailMaterial);
    }

    private void Update()
    {
        owner ??= GetComponent<CoreTacticalShipMotor>();
        if (owner == null)
        {
            return;
        }

        EnsureLauncherVisual();
        ApplyLauncherTransform();
        float now = Time.time;
        if (TryContinueAutomaticBurst(now))
        {
            return;
        }

        if (manualLaunchOnly)
        {
            return;
        }

        RefreshTargetCandidates(now);
        CoreTacticalShipMotor launchTarget = FindBestReachableTarget();
        if (launchTarget == null)
        {
            return;
        }

        if (Time.time < nextLaunchTime)
        {
            return;
        }

        if (!CanFireWeapon(weaponGroup))
        {
            return;
        }

        if (AutomaticBurstProjectileCount > 1)
        {
            if (StartAutomaticBurst(launchTarget, now))
            {
                nextLaunchTime = Time.time + Mathf.Max(0.05f, launchIntervalSeconds);
                reloadCooldownStarted = true;
            }

            return;
        }

        if (LaunchMissile(launchTarget))
        {
            nextLaunchTime = Time.time + Mathf.Max(0.05f, launchIntervalSeconds);
            reloadCooldownStarted = true;
        }
    }

    private void RefreshTargetCandidates(float now)
    {
        if (now < nextTargetRefreshTime)
        {
            return;
        }

        targetCandidates = FindObjectsByType<CoreTacticalCombatant>(FindObjectsSortMode.None);
        nextTargetRefreshTime = now + Mathf.Max(0.03f, targetRefreshIntervalSeconds);
    }

    private CoreTacticalShipMotor FindBestReachableTarget()
    {
        CoreTacticalShipMotor bestTarget = null;
        float bestScore = float.PositiveInfinity;
        ConsiderReachableTarget(GetPriorityTarget(), ref bestTarget, ref bestScore);
        if (bestTarget != null)
        {
            return bestTarget;
        }

        if (targetCandidates != null)
        {
            for (int i = 0; i < targetCandidates.Length; i++)
            {
                CoreTacticalCombatant combatant = targetCandidates[i];
                if (!CoreTacticalOreTargetingRules.IsAutomaticCombatTarget(combatant, targetTeam))
                {
                    continue;
                }

                ConsiderReachableTarget(combatant.ship, ref bestTarget, ref bestScore);
            }
        }

        ConsiderReachableTarget(target, ref bestTarget, ref bestScore);
        return bestTarget;
    }

    private CoreTacticalShipMotor GetPriorityTarget()
    {
        CoreTacticalPriorityTargetControl priorityControl = owner != null ? owner.GetComponent<CoreTacticalPriorityTargetControl>() : null;
        return priorityControl != null && priorityControl.TryGetPriorityTarget(targetTeam, out CoreTacticalShipMotor priorityTarget)
            ? priorityTarget
            : null;
    }

    private void ConsiderReachableTarget(CoreTacticalShipMotor candidate, ref CoreTacticalShipMotor bestTarget, ref float bestScore)
    {
        if (!IsValidTarget(candidate))
        {
            return;
        }

        Vector3 launchPosition = GetLaunchPosition();
        float directDistance = Vector3.Distance(launchPosition, candidate.transform.position);
        float maximumDirectTravel = Mathf.Max(1f, missileSpeedMS) * Mathf.Max(0.1f, missileLifetimeSeconds);
        if (directDistance > maximumDirectTravel)
        {
            return;
        }

        if (directDistance < bestScore)
        {
            bestScore = directDistance;
            bestTarget = candidate;
        }
    }

    private bool IsValidTarget(CoreTacticalShipMotor candidate)
    {
        if (candidate == null || candidate == owner)
        {
            return false;
        }

        CoreTacticalCombatant combatant = candidate.GetComponent<CoreTacticalCombatant>();
        if (combatant != null && candidate != target && combatant.team != targetTeam)
        {
            return false;
        }

        CoreTacticalPrototypeHealth health = candidate.GetComponent<CoreTacticalPrototypeHealth>();
        return health == null || health.currentHealth > 0f;
    }

    private bool LaunchMissile(CoreTacticalShipMotor launchTarget)
    {
        if (!IsValidTarget(launchTarget))
        {
            return false;
        }

        Vector3 launchPosition = GetLaunchPosition();
        Vector3 launchDirection = GetInitialLaunchDirection(launchPosition, launchTarget);
        return LaunchMissileInDirection(launchDirection, launchTarget, true);
    }

    private bool StartAutomaticBurst(CoreTacticalShipMotor launchTarget, float now)
    {
        if (!IsValidTarget(launchTarget) || !CanFireWeapon(weaponGroup))
        {
            return false;
        }

        Vector3 launchPosition = GetLaunchPosition();
        automaticBurstCenterDirection = FlattenDirection(GetInitialLaunchDirection(launchPosition, launchTarget), transform.forward);
        automaticBurstTarget = launchTarget;
        automaticBurstTotal = AutomaticBurstProjectileCount;
        automaticBurstRemaining = automaticBurstTotal;
        automaticBurstLaunched = 0;
        automaticBurstSeed = UnityEngine.Random.value * 1000f + Time.time * 13.37f + sideSign * 29.11f;
        nextAutomaticBurstShotTime = now;
        return TryContinueAutomaticBurst(now);
    }

    private bool TryContinueAutomaticBurst(float now)
    {
        if (automaticBurstRemaining <= 0)
        {
            return false;
        }

        bool launchedAny = false;
        float shotInterval = AutomaticBurstShotIntervalSeconds;
        int safety = 0;
        while (automaticBurstRemaining > 0
            && now + 0.0001f >= nextAutomaticBurstShotTime
            && safety < AutomaticBurstProjectileCount)
        {
            if (!CanFireWeapon(weaponGroup))
            {
                StopAutomaticBurst();
                return launchedAny;
            }

            Vector3 launchDirection = GetAutomaticBurstShotDirection(automaticBurstLaunched, automaticBurstTotal);
            if (!LaunchMissileInDirection(launchDirection, automaticBurstTarget, false))
            {
                StopAutomaticBurst();
                return launchedAny;
            }

            launchedAny = true;
            automaticBurstLaunched++;
            automaticBurstRemaining--;
            nextAutomaticBurstShotTime = now + shotInterval + GetAutomaticBurstShotIntervalJitter(automaticBurstLaunched);
            safety++;
            break;
        }

        if (automaticBurstRemaining <= 0)
        {
            StopAutomaticBurst();
        }

        return launchedAny || automaticBurstRemaining > 0;
    }

    private Vector3 GetAutomaticBurstShotDirection(int shotIndex, int shotCount)
    {
        Vector3 centerDirection = GetAutomaticBurstTrackedCenterDirection();
        if (shotCount <= 1)
        {
            return centerDirection;
        }

        if (automaticBurstUsesChaoticCloud)
        {
            return GetChaoticCloudShotDirection(centerDirection, shotIndex, shotCount);
        }

        float spread = AutomaticBurstSpreadDegrees;
        float normalized = shotIndex / Mathf.Max(1f, shotCount - 1f);
        float angle = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, normalized)
            + GetAutomaticBurstLauncherFanOffsetDegrees();
        return FlattenDirection(Quaternion.AngleAxis(angle, Vector3.up) * centerDirection, centerDirection);
    }

    private Vector3 GetChaoticCloudShotDirection(Vector3 centerDirection, int shotIndex, int shotCount)
    {
        float seed = automaticBurstSeed + shotIndex * 41.37f + sideSign * 7.91f;
        float scatter = Mathf.Max(0f, automaticBurstCloudScatterDegrees);
        float spread = Mathf.Max(scatter, AutomaticBurstSpreadDegrees * 0.55f);
        float normalized = shotCount <= 1 ? 0.5f : shotIndex / Mathf.Max(1f, shotCount - 1f);
        float wovenOffset = Mathf.Sin(seed * 1.21f) * spread * 0.42f
            + Mathf.Sin((normalized + 0.17f) * Mathf.PI * 4.5f + automaticBurstSeed) * spread * 0.32f;
        float randomOffset = (Hash01(seed) * 2f - 1f) * spread;
        float forwardJitter = (Hash01(seed + 19.43f) * 2f - 1f) * Mathf.Max(0f, automaticBurstCloudForwardJitterDegrees);
        float angle = Mathf.Clamp(randomOffset * 0.72f + wovenOffset + forwardJitter, -spread * 1.25f, spread * 1.25f);
        return FlattenDirection(Quaternion.AngleAxis(angle, Vector3.up) * centerDirection, centerDirection);
    }

    private Vector3 GetAutomaticBurstTrackedCenterDirection()
    {
        Vector3 fallbackDirection = FlattenDirection(automaticBurstCenterDirection, transform.forward);
        if (automaticBurstTarget == null)
        {
            return fallbackDirection;
        }

        automaticBurstCenterDirection = FlattenDirection(
            GetInitialLaunchDirection(GetLaunchPosition(), automaticBurstTarget),
            fallbackDirection);
        return automaticBurstCenterDirection;
    }

    private float GetAutomaticBurstLauncherFanOffsetDegrees()
    {
        if (weaponGroup != CoreTacticalWeaponGroup.Missiles
            || manualLaunchOnly
            || AutomaticBurstProjectileCount <= 1
            || automaticBurstUsesChaoticCloud)
        {
            return 0f;
        }

        float signedSide = sideSign < 0 ? -1f : 1f;
        return signedSide * Mathf.Clamp(AutomaticBurstSpreadDegrees * 0.32f, 2.5f, 5.5f);
    }

    private float GetAutomaticBurstShotIntervalJitter(int shotIndex)
    {
        if (!automaticBurstUsesChaoticCloud)
        {
            return 0f;
        }

        float jitter = Mathf.Max(0f, automaticBurstShotIntervalJitterSeconds);
        if (jitter <= 0.001f)
        {
            return 0f;
        }

        float seed = automaticBurstSeed + shotIndex * 17.73f;
        return (Hash01(seed) * 2f - 1f) * jitter;
    }

    private void StopAutomaticBurst()
    {
        automaticBurstTarget = null;
        automaticBurstRemaining = 0;
        automaticBurstLaunched = 0;
        automaticBurstTotal = 0;
        nextAutomaticBurstShotTime = 0f;
        automaticBurstSeed = 0f;
    }

    public bool TryLaunchManualFan(Vector3 worldDirection)
    {
        if (!manualLaunchOnly
            || Time.time < nextLaunchTime
            || !CanFireWeapon(weaponGroup)
            || !IsDirectionInsideManualSector(worldDirection))
        {
            return false;
        }

        Vector3 centerDirection = FlattenDirection(worldDirection, GetManualAimCenterDirection());
        int projectileCount = Mathf.Max(1, projectilesPerManualSalvo);
        float fanAngle = Mathf.Max(0f, manualFanAngleDegrees);
        int launched = 0;
        for (int i = 0; i < projectileCount; i++)
        {
            float normalized = projectileCount <= 1 ? 0.5f : i / (float)(projectileCount - 1);
            float localAngle = Mathf.Lerp(-fanAngle * 0.5f, fanAngle * 0.5f, normalized);
            Vector3 launchDirection = Quaternion.AngleAxis(localAngle, Vector3.up) * centerDirection;
            if (LaunchMissileInDirection(launchDirection, null, false))
            {
                launched++;
            }
        }

        if (launched <= 0)
        {
            return false;
        }

        nextLaunchTime = Time.time + Mathf.Max(0.2f, manualCooldownSeconds);
        reloadCooldownStarted = true;
        return true;
    }

    public Vector3 GetManualLaunchPosition()
    {
        return GetLaunchPosition();
    }

    public Vector3 GetManualAimCenterDirection()
    {
        return FlattenDirection(GetLauncherWorldRotation() * Vector3.forward, owner != null ? owner.transform.forward : transform.forward);
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

    public Vector3 GetInitialLaunchDirectionForTests(Vector3 launchPosition, CoreTacticalShipMotor launchTarget)
    {
        return GetInitialLaunchDirection(launchPosition, launchTarget);
    }

    public Vector3 GetAutomaticBurstTrackedCenterDirectionForTests(CoreTacticalShipMotor launchTarget)
    {
        automaticBurstTarget = launchTarget;
        if (automaticBurstCenterDirection.sqrMagnitude <= 0.0001f)
        {
            automaticBurstCenterDirection = FlattenDirection(transform.forward, Vector3.forward);
        }

        return GetAutomaticBurstTrackedCenterDirection();
    }

    private bool LaunchMissileInDirection(Vector3 launchDirection, CoreTacticalShipMotor launchTarget, bool allowTargetAcquisition)
    {
        if (!CanFireWeapon(weaponGroup))
        {
            return false;
        }

        Vector3 launchPosition = GetLaunchPosition();
        bool chaoticCloudShot = automaticBurstUsesChaoticCloud && automaticBurstTotal > 1;
        int cloudShotIndex = chaoticCloudShot ? automaticBurstLaunched : 0;
        float cloudSeed = automaticBurstSeed + cloudShotIndex * 23.71f + sideSign * 5.37f;
        float cloudDriftDegrees = chaoticCloudShot
            ? (Hash01(cloudSeed + 3.19f) * 2f - 1f) * Mathf.Max(0f, automaticBurstCloudScatterDegrees)
            : 0f;
        CoreTacticalGuidedMissile.Create(
            missileName,
            launchPosition,
            launchDirection,
            launchTarget,
            guidanceMode,
            Mathf.Max(1f, missileSpeedMS),
            Mathf.Max(0.1f, missileLifetimeSeconds),
            Mathf.Max(0f, missileTurnRateDegPerSecond),
            Mathf.Max(0.02f, guidanceIntervalSeconds),
            Mathf.Max(0.5f, proximityRadiusMeters),
            Mathf.Max(1f, explosionRadiusMeters),
            Mathf.Max(0f, missileDamage),
            missileDamageType,
            fullDamageInsideExplosionRadius,
            targetTeam,
            CreateMissileMaterial(ref missileMaterial, missileColor),
            CreateMissileTrailMaterial(ref trailMaterial, trailColor),
            trailColor,
            missileResistanceIgnorePercent,
            missileCaliberMm,
            0f,
            missileFireChancePercent,
            allowTargetAcquisition,
            chaoticCloudShot ? automaticBurstChaosAmplitudeDegrees : 0f,
            chaoticCloudShot ? automaticBurstChaosFrequencyHz : 0f,
            cloudSeed,
            cloudDriftDegrees);
        return true;
    }

    private bool CanFireWeapon(CoreTacticalWeaponGroup group)
    {
        CoreTacticalWeaponControl control = ResolveWeaponControl();
        return control == null || control.CanFire(group);
    }

    private CoreTacticalWeaponControl ResolveWeaponControl()
    {
        if (weaponControl != null)
        {
            return weaponControl;
        }

        if (owner != null)
        {
            weaponControl = owner.GetComponent<CoreTacticalWeaponControl>();
        }

        if (weaponControl == null)
        {
            weaponControl = GetComponent<CoreTacticalWeaponControl>();
        }

        return weaponControl;
    }

    private Vector3 GetLaunchPosition()
    {
        if (TryGetVisualLauncherMuzzle(out Vector3 visualMuzzle))
        {
            return visualMuzzle;
        }

        return GetLauncherWorldPosition() + GetLauncherWorldRotation() * Vector3.forward * 18f;
    }

    private Vector3 GetLauncherWorldPosition()
    {
        return GetFallbackLauncherWorldPosition();
    }

    private Vector3 GetFallbackLauncherWorldPosition()
    {
        Vector3 hullSize = owner != null ? owner.hullSizeMeters : transform.localScale;
        Transform ownerTransform = owner != null ? owner.transform : transform;
        if (IsMainRocketVisualRole())
        {
            float localZ = sideSign < 0 ? hullSize.z * 0.24f : -hullSize.z * 0.24f;
            return ownerTransform.position
                + ownerTransform.up * (hullSize.y * 0.58f + 3f)
                + ownerTransform.forward * localZ;
        }

        int safeSide = sideSign < 0 ? -1 : 1;
        return ownerTransform.position
            + ownerTransform.right * (safeSide * hullSize.x * 0.70f)
            + ownerTransform.up * (hullSize.y * 0.58f + 3f)
            + ownerTransform.forward * (hullSize.z * Mathf.Clamp(localZScale, -0.48f, 0.48f));
    }

    private Quaternion GetLauncherWorldRotation()
    {
        Transform ownerTransform = owner != null ? owner.transform : transform;
        if (IsMainRocketVisualRole())
        {
            return ownerTransform.rotation;
        }

        int safeSide = sideSign < 0 ? -1 : 1;
        return ownerTransform.rotation * Quaternion.Euler(0f, safeSide > 0 ? 90f : -90f, 0f);
    }

    private Vector3 GetInitialLaunchDirection(Vector3 launchPosition, CoreTacticalShipMotor launchTarget)
    {
        Vector3 aimPoint = GetInitialLaunchAimPoint(launchPosition, launchTarget);
        Vector3 toTarget = launchTarget != null ? aimPoint - launchPosition : transform.forward;
        if (toTarget.sqrMagnitude <= 0.001f)
        {
            return transform.forward;
        }

        return FlattenDirection(toTarget, transform.forward);
    }

    private Vector3 GetInitialLaunchAimPoint(Vector3 launchPosition, CoreTacticalShipMotor launchTarget)
    {
        if (launchTarget == null)
        {
            return launchPosition + transform.forward;
        }

        Vector3 targetPosition = launchTarget.transform.position;
        if (!ShouldUsePredictedLaunchAim())
        {
            return targetPosition;
        }

        Vector3 targetVelocity = launchTarget.Body != null ? launchTarget.Body.linearVelocity : Vector3.zero;
        if (targetVelocity.sqrMagnitude <= 0.001f)
        {
            return targetPosition;
        }

        return PredictInitialInterceptPoint(launchPosition, targetPosition, targetVelocity, missileSpeedMS);
    }

    private bool ShouldUsePredictedLaunchAim()
    {
        return guidanceMode == CoreTacticalMissileGuidanceMode.PredictedIntercept
            || (weaponGroup == CoreTacticalWeaponGroup.Missiles
                && !manualLaunchOnly
                && AutomaticBurstProjectileCount > 1
                && missileTurnRateDegPerSecond <= 0.001f);
    }

    private static Vector3 PredictInitialInterceptPoint(
        Vector3 missilePosition,
        Vector3 targetPosition,
        Vector3 targetVelocity,
        float missileSpeed)
    {
        Vector3 relativePosition = targetPosition - missilePosition;
        float safeMissileSpeed = Mathf.Max(1f, missileSpeed);
        float a = Vector3.Dot(targetVelocity, targetVelocity) - safeMissileSpeed * safeMissileSpeed;
        float b = 2f * Vector3.Dot(relativePosition, targetVelocity);
        float c = Vector3.Dot(relativePosition, relativePosition);

        float interceptTime = 0f;
        if (Mathf.Abs(a) <= 0.0001f)
        {
            interceptTime = Mathf.Abs(b) > 0.0001f ? -c / b : 0f;
        }
        else
        {
            float discriminant = b * b - 4f * a * c;
            if (discriminant >= 0f)
            {
                float sqrt = Mathf.Sqrt(discriminant);
                float first = (-b - sqrt) / (2f * a);
                float second = (-b + sqrt) / (2f * a);
                interceptTime = GetSmallestPositiveLaunchTime(first, second);
            }
        }

        if (interceptTime <= 0f)
        {
            interceptTime = relativePosition.magnitude / safeMissileSpeed;
        }

        return targetPosition + targetVelocity * interceptTime;
    }

    private static float GetSmallestPositiveLaunchTime(float first, float second)
    {
        bool firstPositive = first > 0f;
        bool secondPositive = second > 0f;
        if (firstPositive && secondPositive)
        {
            return Mathf.Min(first, second);
        }

        if (firstPositive)
        {
            return first;
        }

        return secondPositive ? second : 0f;
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

        launcherObject = new GameObject("Core Tactical Missile Launcher " + (sideSign < 0 ? "Port " : "Starboard ") + guidanceMode);
        launcherObject.transform.SetPositionAndRotation(GetLauncherWorldPosition(), GetLauncherWorldRotation());
        launcherMaterial = CreateMissileMaterial(ref launcherMaterial, guidanceMode == CoreTacticalMissileGuidanceMode.PredictedIntercept
            ? new Color(0.16f, 0.31f, 0.36f, 1f)
            : new Color(0.36f, 0.16f, 0.14f, 1f));

        GameObject rack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rack.name = "Rack";
        rack.transform.SetParent(launcherObject.transform, false);
        rack.transform.localScale = new Vector3(7.2f, 3.1f, 12.5f);
        AssignRendererMaterial(rack, launcherMaterial);
        DestroyPrimitiveCollider(rack);

        for (int i = 0; i < 2; i++)
        {
            GameObject tube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tube.name = "Launch Tube " + (i + 1);
            tube.transform.SetParent(launcherObject.transform, false);
            tube.transform.localPosition = new Vector3((i == 0 ? -1f : 1f) * 2.2f, 2.15f, 1.5f);
            tube.transform.localScale = new Vector3(1.7f, 1.45f, 13.5f);
            AssignRendererMaterial(tube, launcherMaterial);
            DestroyPrimitiveCollider(tube);
        }
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

    private void ApplyLauncherTransform()
    {
        if (launcherObject == null)
        {
            return;
        }

        launcherObject.transform.SetPositionAndRotation(GetLauncherWorldPosition(), GetLauncherWorldRotation());
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

    private static Material CreateMissileMaterial(ref Material cachedMaterial, Color color)
    {
        if (cachedMaterial != null)
        {
            return cachedMaterial;
        }

        cachedMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVisibleMaterial(color, 2.2f);
        return cachedMaterial;
    }

    private static Material CreateMissileTrailMaterial(ref Material cachedMaterial, Color color)
    {
        if (cachedMaterial != null)
        {
            return cachedMaterial;
        }

        cachedMaterial = CoreTacticalWeaponVisualMaterialUtility.CreateVertexColorTransparentMaterial(color);
        return cachedMaterial;
    }

    private static void ApplyMissileMaterialColor(Material material, Color color, float emissionMultiplier)
    {
        CoreTacticalWeaponVisualMaterialUtility.ApplyVisibleColor(material, color, emissionMultiplier);
    }

    private static void ConfigureMissileTrailMaterial(Material material)
    {
        CoreTacticalWeaponVisualMaterialUtility.ConfigureTransparentMaterial(material);
    }

    private bool IsMainRocketVisualRole()
    {
        string normalizedRole = string.IsNullOrWhiteSpace(visualLauncherRole)
            ? ""
            : visualLauncherRole.Trim().ToLowerInvariant();
        return normalizedRole.Contains("main")
            && (normalizedRole.Contains("rocket") || normalizedRole.Contains("nurs") || normalizedRole.Contains("missile"));
    }

    private static float Hash01(float value)
    {
        return Mathf.Repeat(Mathf.Sin(value * 12.9898f + 78.233f) * 43758.5453f, 1f);
    }
}

public sealed class CoreTacticalGuidedMissile : MonoBehaviour
{
    private const int TrailPointCount = 18;
    private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");

    private CoreTacticalShipMotor target;
    private CoreTacticalCombatTeam targetTeam = CoreTacticalCombatTeam.Enemy;
    private CoreTacticalMissileGuidanceMode guidanceMode;
    private float speedMS;
    private float turnRateRadPerSecond;
    private float guidanceIntervalSeconds;
    private float proximityRadiusMeters;
    private float explosionRadiusMeters;
    private float damageAmount;
    private CoreTacticalDamageType damageType;
    private float resistanceIgnorePercent;
    private float caliberMm;
    private float directImpactFuseThresholdMeters;
    private float fireChancePercent;
    private bool fullDamageInsideExplosionRadius;
    private bool damageApplied;
    private float deathTime;
    private float launchTime;
    private float nextGuidanceTime;
    private Vector3 currentForward;
    private Vector3 guidanceDirection;
    private Vector3 unguidedBaseDirection;
    private Vector3 previousPosition;
    private bool allowTargetAcquisition = true;
    private float unguidedChaosAmplitudeRad;
    private float unguidedChaosFrequencyHz;
    private float unguidedChaosPhase;
    private float unguidedChaosDriftRad;
    private LineRenderer trail;
    private readonly Vector3[] trailPoints = new Vector3[TrailPointCount];
    private Collider currentAvoidanceObstacle;
    private float currentAvoidanceSide = 1f;
    private int activeTrailPointCount;

    private float DetonationSensitivityRadiusMeters => GetDetonationSensitivityRadiusMeters(proximityRadiusMeters, explosionRadiusMeters);

    public static void Create(
        string missileName,
        Vector3 startPosition,
        Vector3 startDirection,
        CoreTacticalShipMotor target,
        CoreTacticalMissileGuidanceMode guidanceMode,
        float speedMS,
        float lifetimeSeconds,
        float turnRateDegPerSecond,
        float guidanceIntervalSeconds,
        float proximityRadiusMeters,
        float explosionRadiusMeters,
        float damageAmount,
        CoreTacticalDamageType damageType,
        bool fullDamageInsideExplosionRadius,
        CoreTacticalCombatTeam targetTeam,
        Material missileMaterial,
        Material trailMaterial,
        Color trailColor,
        float resistanceIgnorePercent,
        float caliberMm,
        float directImpactFuseThresholdMeters,
        float fireChancePercent,
        bool allowTargetAcquisition = true,
        float unguidedChaosAmplitudeDegrees = 0f,
        float unguidedChaosFrequencyHz = 0f,
        float unguidedChaosPhase = 0f,
        float unguidedChaosDriftDegrees = 0f)
    {
        GameObject missile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        missile.name = missileName;
        missile.transform.position = startPosition;
        missile.transform.localScale = Vector3.one * 9f;
        Collider collider = missile.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = missile.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = missileMaterial;
            ApplyMissileRendererColor(renderer, ResolveMaterialVisibleColor(missileMaterial, new Color(1f, 0.35f, 0.16f, 1f)));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        LineRenderer line = missile.AddComponent<LineRenderer>();
        line.sharedMaterial = trailMaterial != null ? trailMaterial : missileMaterial;
        Color visibleTrailColor = trailColor.maxColorComponent > 0.04f
            ? trailColor
            : ResolveMaterialVisibleColor(trailMaterial != null ? trailMaterial : missileMaterial, new Color(0.95f, 0.42f, 0.20f, 1f));
        line.startColor = visibleTrailColor;
        line.endColor = new Color(visibleTrailColor.r, visibleTrailColor.g, visibleTrailColor.b, 0.16f);
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = 3.6f;
        line.endWidth = 0.12f;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        CoreTacticalGuidedMissile guidedMissile = missile.AddComponent<CoreTacticalGuidedMissile>();
        guidedMissile.Initialize(
            startPosition,
            startDirection,
            target,
            guidanceMode,
            speedMS,
            lifetimeSeconds,
            turnRateDegPerSecond,
            guidanceIntervalSeconds,
            proximityRadiusMeters,
            explosionRadiusMeters,
            damageAmount,
            damageType,
            fullDamageInsideExplosionRadius,
            targetTeam,
            line,
            resistanceIgnorePercent,
            caliberMm,
            directImpactFuseThresholdMeters,
            fireChancePercent,
            allowTargetAcquisition,
            unguidedChaosAmplitudeDegrees,
            unguidedChaosFrequencyHz,
            unguidedChaosPhase,
            unguidedChaosDriftDegrees);
    }

    public static float ResolveDetonationSensitivityRadiusForTests(float proximityRadiusMeters, float explosionRadiusMeters)
    {
        return GetDetonationSensitivityRadiusMeters(proximityRadiusMeters, explosionRadiusMeters);
    }

    public static int ApplyExplosionDamageForTests(
        Vector3 position,
        CoreTacticalShipMotor preferredTarget,
        CoreTacticalCombatTeam targetTeam,
        float damageAmount,
        float explosionRadiusMeters,
        bool fullDamageInsideExplosionRadius)
    {
        return ApplyExplosionDamageAt(
            position,
            preferredTarget,
            targetTeam,
            Mathf.Max(0f, damageAmount),
            CoreTacticalDamageType.Explosive,
            0f,
            0f,
            0f,
            "Guided missile test",
            Vector3.forward,
            0f,
            Mathf.Max(1f, explosionRadiusMeters),
            fullDamageInsideExplosionRadius);
    }

    private static Color ResolveMaterialVisibleColor(Material material, Color fallback)
    {
        return CoreTacticalWeaponVisualMaterialUtility.ResolveVisibleColor(material, fallback);
    }

    private static void ApplyMissileRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorPropertyId, color);
        propertyBlock.SetColor(ColorPropertyId, color);
        propertyBlock.SetColor(EmissionColorPropertyId, color * 2.2f);
        renderer.SetPropertyBlock(propertyBlock);
    }

    private void Initialize(
        Vector3 startPosition,
        Vector3 startDirection,
        CoreTacticalShipMotor newTarget,
        CoreTacticalMissileGuidanceMode newGuidanceMode,
        float newSpeedMS,
        float lifetimeSeconds,
        float turnRateDegPerSecond,
        float newGuidanceIntervalSeconds,
        float newProximityRadiusMeters,
        float newExplosionRadiusMeters,
        float newDamageAmount,
        CoreTacticalDamageType newDamageType,
        bool newFullDamageInsideExplosionRadius,
        CoreTacticalCombatTeam newTargetTeam,
        LineRenderer newTrail,
        float newResistanceIgnorePercent,
        float newCaliberMm,
        float newDirectImpactFuseThresholdMeters,
        float newFireChancePercent,
        bool newAllowTargetAcquisition,
        float newUnguidedChaosAmplitudeDegrees,
        float newUnguidedChaosFrequencyHz,
        float newUnguidedChaosPhase,
        float newUnguidedChaosDriftDegrees)
    {
        target = newTarget;
        targetTeam = newTarget != null ? ResolveTargetTeam(newTarget) : newTargetTeam;
        guidanceMode = newGuidanceMode;
        speedMS = newSpeedMS;
        turnRateRadPerSecond = turnRateDegPerSecond * Mathf.Deg2Rad;
        guidanceIntervalSeconds = newGuidanceIntervalSeconds;
        proximityRadiusMeters = newProximityRadiusMeters;
        explosionRadiusMeters = Mathf.Max(1f, newExplosionRadiusMeters);
        damageAmount = Mathf.Max(0f, newDamageAmount);
        damageType = newDamageType;
        resistanceIgnorePercent = Mathf.Max(0f, newResistanceIgnorePercent);
        caliberMm = Mathf.Max(0f, newCaliberMm);
        directImpactFuseThresholdMeters = Mathf.Max(0f, newDirectImpactFuseThresholdMeters);
        fireChancePercent = Mathf.Max(0f, newFireChancePercent);
        fullDamageInsideExplosionRadius = newFullDamageInsideExplosionRadius;
        launchTime = Time.time;
        deathTime = Time.time + lifetimeSeconds;
        nextGuidanceTime = 0f;
        currentForward = FlattenDirection(startDirection, Vector3.forward);
        guidanceDirection = currentForward;
        unguidedBaseDirection = currentForward;
        allowTargetAcquisition = newAllowTargetAcquisition;
        unguidedChaosAmplitudeRad = Mathf.Max(0f, newUnguidedChaosAmplitudeDegrees) * Mathf.Deg2Rad;
        unguidedChaosFrequencyHz = Mathf.Max(0f, newUnguidedChaosFrequencyHz);
        unguidedChaosPhase = newUnguidedChaosPhase;
        unguidedChaosDriftRad = newUnguidedChaosDriftDegrees * Mathf.Deg2Rad;
        previousPosition = startPosition;
        trail = newTrail;
        transform.rotation = Quaternion.LookRotation(currentForward, Vector3.up);
        for (int i = 0; i < trailPoints.Length; i++)
        {
            trailPoints[i] = startPosition;
        }

        activeTrailPointCount = 1;
    }

    private void Update()
    {
        if (Time.time >= deathTime)
        {
            ExplodeAt(transform.position);
            DetachTrailForFade();
            Destroy(gameObject);
            return;
        }

        if (!IsLiveTarget(target))
        {
            if (allowTargetAcquisition)
            {
                target = FindNearestLiveTarget();
            }

            if (target == null)
            {
                guidanceDirection = currentForward;
            }
        }

        if (HasUnguidedChaos())
        {
            guidanceDirection = ResolveObstacleAvoidedDirection(GetUnguidedChaosDirection());
        }
        else if (Time.time >= nextGuidanceTime)
        {
            guidanceDirection = target != null ? CalculateGuidanceDirection() : ResolveObstacleAvoidedDirection(currentForward);
            nextGuidanceTime = Time.time + guidanceIntervalSeconds;
        }

        float deltaSeconds = Mathf.Max(0.001f, Time.deltaTime);
        if (HasUnguidedChaos())
        {
            currentForward = FlattenDirection(guidanceDirection, currentForward);
        }
        else
        {
            currentForward = Vector3.RotateTowards(
                currentForward,
                guidanceDirection,
                turnRateRadPerSecond * deltaSeconds,
                0f);
            currentForward = FlattenDirection(currentForward, guidanceDirection);
        }

        Vector3 nextPosition = transform.position + currentForward * (speedMS * deltaSeconds);
        if (CoreTacticalAutomatonWreck.TrySphereCastWreck(
                previousPosition,
                nextPosition,
                Mathf.Max(3f, proximityRadiusMeters * 0.35f),
                out _,
                out Vector3 wreckHitPosition))
        {
            ExplodeAt(wreckHitPosition);
            DetachTrailForFade();
            Destroy(gameObject);
            return;
        }

        if (CoreTacticalWeaponOcclusion.SegmentHitsObstacle(
                previousPosition,
                nextPosition,
                Mathf.Max(3f, proximityRadiusMeters * 0.35f),
                out Vector3 obstacleHitPosition))
        {
            ExplodeAt(obstacleHitPosition);
            DetachTrailForFade();
            Destroy(gameObject);
            return;
        }

        if (HitsTarget(previousPosition, nextPosition, out CoreTacticalShipMotor impactTarget))
        {
            target = impactTarget;
            ExplodeAt(nextPosition);
            DetachTrailForFade();
            Destroy(gameObject);
            return;
        }

        transform.SetPositionAndRotation(nextPosition, Quaternion.LookRotation(currentForward, Vector3.up));
        previousPosition = nextPosition;
        UpdateTrail(nextPosition);
    }

    private void ExplodeAt(Vector3 position)
    {
        CoreTacticalFlakBurstVisual.Create(position, explosionRadiusMeters);
        CoreTacticalLeviathanController.NotifyProjectileImpact(position, explosionRadiusMeters, 0.06f);
        TryApplyDamageAt(position);
    }

    private void TryApplyDamageAt(Vector3 position)
    {
        if (damageApplied || damageAmount <= 0f)
        {
            return;
        }

        string source = guidanceMode == CoreTacticalMissileGuidanceMode.PredictedIntercept
            ? "Smart missile"
            : "Dumb missile";
        int appliedCount = ApplyExplosionDamageAt(
            position,
            target,
            targetTeam,
            damageAmount,
            damageType,
            resistanceIgnorePercent,
            caliberMm,
            directImpactFuseThresholdMeters,
            source,
            currentForward,
            fireChancePercent,
            explosionRadiusMeters,
            fullDamageInsideExplosionRadius);
        damageApplied = appliedCount > 0;
    }

    private static int ApplyExplosionDamageAt(
        Vector3 position,
        CoreTacticalShipMotor preferredTarget,
        CoreTacticalCombatTeam targetTeam,
        float damageAmount,
        CoreTacticalDamageType damageType,
        float resistanceIgnorePercent,
        float caliberMm,
        float directImpactFuseThresholdMeters,
        string source,
        Vector3 damageDirection,
        float fireChancePercent,
        float explosionRadiusMeters,
        bool fullDamageInsideExplosionRadius)
    {
        if (damageAmount <= 0f)
        {
            return 0;
        }

        int appliedCount = 0;
        if (TryApplyExplosionDamageToTarget(
                position,
                preferredTarget,
                damageAmount,
                damageType,
                resistanceIgnorePercent,
                caliberMm,
                directImpactFuseThresholdMeters,
                source,
                damageDirection,
                fireChancePercent,
                explosionRadiusMeters,
                fullDamageInsideExplosionRadius))
        {
            appliedCount++;
        }

        CoreTacticalCombatant[] combatants = FindObjectsByType<CoreTacticalCombatant>(FindObjectsSortMode.None);
        for (int i = 0; i < combatants.Length; i++)
        {
            CoreTacticalCombatant combatant = combatants[i];
            CoreTacticalShipMotor candidate = combatant != null ? combatant.ship : null;
            if (candidate == null || candidate == preferredTarget || !IsValidExplosionTarget(candidate, preferredTarget, targetTeam))
            {
                continue;
            }

            if (TryApplyExplosionDamageToTarget(
                    position,
                    candidate,
                    damageAmount,
                    damageType,
                    resistanceIgnorePercent,
                    caliberMm,
                    directImpactFuseThresholdMeters,
                    source,
                    damageDirection,
                    fireChancePercent,
                    explosionRadiusMeters,
                    fullDamageInsideExplosionRadius))
            {
                appliedCount++;
            }
        }

        appliedCount += CoreTacticalAutomatonWreck.ApplyExplosionDamageToWrecks(
            position,
            explosionRadiusMeters,
            damageAmount,
            source);

        return appliedCount;
    }

    private static bool TryApplyExplosionDamageToTarget(
        Vector3 position,
        CoreTacticalShipMotor damageTarget,
        float damageAmount,
        CoreTacticalDamageType damageType,
        float resistanceIgnorePercent,
        float caliberMm,
        float directImpactFuseThresholdMeters,
        string source,
        Vector3 damageDirection,
        float fireChancePercent,
        float explosionRadiusMeters,
        bool fullDamageInsideExplosionRadius)
    {
        if (!IsLiveTarget(damageTarget))
        {
            return false;
        }

        CoreTacticalPrototypeHealth health = damageTarget.GetComponent<CoreTacticalPrototypeHealth>();
        if (health == null || health.currentHealth <= 0f)
        {
            return false;
        }

        float closestDistance = GetClosestTargetDistance(position, damageTarget);
        if (closestDistance > Mathf.Max(0.1f, explosionRadiusMeters))
        {
            return false;
        }

        float damageScale = fullDamageInsideExplosionRadius
            ? 1f
            : Mathf.Lerp(
                0.45f,
                1f,
                1f - Mathf.Clamp01(closestDistance / Mathf.Max(0.1f, explosionRadiusMeters)));
        health.ApplyDamage(CoreTacticalDamageRequest.Create(
            damageType,
            damageAmount * damageScale,
            source,
            resistanceIgnorePercent,
            fireChancePercent,
            explosionRadiusMeters,
            position,
            FlattenDirection(damageDirection, Vector3.forward)));
        return true;
    }

    private static float GetClosestTargetDistance(Vector3 position, CoreTacticalShipMotor distanceTarget)
    {
        Collider[] colliders = distanceTarget != null ? distanceTarget.GetComponentsInChildren<Collider>() : null;
        float closestDistance = float.PositiveInfinity;
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                closestDistance = Mathf.Min(closestDistance, Vector3.Distance(position, collider.ClosestPoint(position)));
            }
        }

        if (float.IsPositiveInfinity(closestDistance))
        {
            closestDistance = distanceTarget != null
                ? Vector3.Distance(position, distanceTarget.transform.position)
                : float.PositiveInfinity;
        }

        return closestDistance;
    }

    private void DetachTrailForFade()
    {
        if (trail == null)
        {
            return;
        }

        CoreTacticalLineTrailFadeDestroyer.CreateFrom(trail, activeTrailPointCount, 0.45f);
        trail = null;
    }

    private Vector3 CalculateGuidanceDirection()
    {
        if (!IsLiveTarget(target))
        {
            return ResolveObstacleAvoidedDirection(currentForward);
        }

        Vector3 missilePosition = transform.position;
        Vector3 targetPosition = target.transform.position;
        if (guidanceMode == CoreTacticalMissileGuidanceMode.DirectChase)
        {
            return ResolveObstacleAvoidedDirection(FlattenDirection(targetPosition - missilePosition, currentForward));
        }

        Vector3 targetVelocity = target.Body != null ? target.Body.linearVelocity : Vector3.zero;
        Vector3 interceptPoint = PredictInterceptPoint(missilePosition, targetPosition, targetVelocity, speedMS);
        return ResolveObstacleAvoidedDirection(FlattenDirection(interceptPoint - missilePosition, currentForward));
    }

    private bool HasUnguidedChaos()
    {
        return unguidedChaosAmplitudeRad > 0.001f && unguidedChaosFrequencyHz > 0.001f;
    }

    private Vector3 GetUnguidedChaosDirection()
    {
        float age = Mathf.Max(0f, Time.time - launchTime);
        float omega = unguidedChaosFrequencyHz * Mathf.PI * 2f;
        float yawRad =
            Mathf.Sin(age * omega + unguidedChaosPhase) * unguidedChaosAmplitudeRad +
            Mathf.Sin(age * omega * 1.73f + unguidedChaosPhase * 0.47f + 1.37f) * unguidedChaosAmplitudeRad * 0.48f +
            Mathf.Sin(age * omega * 0.41f + unguidedChaosPhase * 1.91f + 2.11f) * unguidedChaosAmplitudeRad * 0.31f;
        yawRad += unguidedChaosDriftRad * Mathf.Clamp01(age * 0.42f);
        return FlattenDirection(Quaternion.AngleAxis(yawRad * Mathf.Rad2Deg, Vector3.up) * unguidedBaseDirection, unguidedBaseDirection);
    }

    private Vector3 ResolveObstacleAvoidedDirection(Vector3 desiredDirection)
    {
        desiredDirection = FlattenDirection(desiredDirection, currentForward);
        float lookAhead = Mathf.Clamp(speedMS * 1.35f, 120f, 620f);
        float avoidanceRadius = Mathf.Max(10f, proximityRadiusMeters * 0.55f);
        if (!CoreTacticalWeaponOcclusion.TryGetNearestObstacleAhead(
                transform.position,
                desiredDirection,
                avoidanceRadius,
                lookAhead,
                out RaycastHit obstacleHit))
        {
            currentAvoidanceObstacle = null;
            return desiredDirection;
        }

        Collider obstacleCollider = obstacleHit.collider;
        Vector3 right = Vector3.Cross(Vector3.up, desiredDirection);
        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.Cross(Vector3.up, currentForward);
        }

        if (right.sqrMagnitude <= 0.0001f)
        {
            right = Vector3.right;
        }

        right.Normalize();
        float avoidanceSide = ResolveAvoidanceSide(obstacleCollider, right);
        currentAvoidanceObstacle = obstacleCollider;
        currentAvoidanceSide = avoidanceSide;

        float closeness = 1f - Mathf.Clamp01(obstacleHit.distance / Mathf.Max(1f, lookAhead));
        float sideWeight = Mathf.Lerp(0.85f, 2.35f, closeness);
        Vector3 normalPush = obstacleHit.normal;
        normalPush.y = 0f;
        Vector3 avoidedDirection = desiredDirection + right * avoidanceSide * sideWeight;
        if (normalPush.sqrMagnitude > 0.0001f)
        {
            avoidedDirection += normalPush.normalized * Mathf.Lerp(0.15f, 0.55f, closeness);
        }

        return FlattenDirection(avoidedDirection, desiredDirection);
    }

    private float ResolveAvoidanceSide(Collider obstacleCollider, Vector3 right)
    {
        if (currentAvoidanceObstacle == obstacleCollider && Mathf.Abs(currentAvoidanceSide) > 0.01f)
        {
            return currentAvoidanceSide;
        }

        if (obstacleCollider != null)
        {
            Vector3 obstacleOffset = obstacleCollider.bounds.center - transform.position;
            obstacleOffset.y = 0f;
            float signedSide = Vector3.Dot(obstacleOffset, right);
            if (Mathf.Abs(signedSide) > 1f)
            {
                return signedSide > 0f ? -1f : 1f;
            }
        }

        float currentSide = Vector3.Dot(currentForward, right);
        if (Mathf.Abs(currentSide) > 0.01f)
        {
            return currentSide >= 0f ? 1f : -1f;
        }

        return Mathf.Abs(currentAvoidanceSide) > 0.01f ? Mathf.Sign(currentAvoidanceSide) : 1f;
    }

    private static Vector3 PredictInterceptPoint(
        Vector3 missilePosition,
        Vector3 targetPosition,
        Vector3 targetVelocity,
        float missileSpeed)
    {
        Vector3 relativePosition = targetPosition - missilePosition;
        float a = Vector3.Dot(targetVelocity, targetVelocity) - missileSpeed * missileSpeed;
        float b = 2f * Vector3.Dot(relativePosition, targetVelocity);
        float c = Vector3.Dot(relativePosition, relativePosition);

        float interceptTime = 0f;
        if (Mathf.Abs(a) <= 0.0001f)
        {
            interceptTime = Mathf.Abs(b) > 0.0001f ? -c / b : 0f;
        }
        else
        {
            float discriminant = b * b - 4f * a * c;
            if (discriminant >= 0f)
            {
                float sqrt = Mathf.Sqrt(discriminant);
                float first = (-b - sqrt) / (2f * a);
                float second = (-b + sqrt) / (2f * a);
                interceptTime = GetSmallestPositive(first, second);
            }
        }

        if (interceptTime <= 0f)
        {
            interceptTime = relativePosition.magnitude / Mathf.Max(1f, missileSpeed);
        }

        return targetPosition + targetVelocity * interceptTime;
    }

    private bool HitsTarget(Vector3 from, Vector3 to, out CoreTacticalShipMotor hitTarget)
    {
        hitTarget = null;
        Vector3 segment = to - from;
        float distance = segment.magnitude;
        if (distance <= 0.001f)
        {
            return false;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            from,
            DetonationSensitivityRadiusMeters,
            segment / distance,
            HitBuffer,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = HitBuffer[i].collider;
            CoreTacticalShipMotor hitShip = hitCollider != null ? hitCollider.GetComponentInParent<CoreTacticalShipMotor>() : null;
            if (!IsLiveTarget(hitShip))
            {
                continue;
            }

            if (hitShip == target || IsValidExplosionTarget(hitShip, target, targetTeam))
            {
                hitTarget = hitShip;
                return true;
            }
        }

        return false;
    }

    private static bool IsValidExplosionTarget(
        CoreTacticalShipMotor candidate,
        CoreTacticalShipMotor preferredTarget,
        CoreTacticalCombatTeam targetTeam)
    {
        if (!IsLiveTarget(candidate))
        {
            return false;
        }

        if (candidate == preferredTarget)
        {
            return true;
        }

        CoreTacticalCombatant combatant = candidate.GetComponent<CoreTacticalCombatant>();
        return combatant != null && combatant.team == targetTeam;
    }

    private void UpdateTrail(Vector3 position)
    {
        for (int i = trailPoints.Length - 1; i > 0; i--)
        {
            trailPoints[i] = trailPoints[i - 1];
        }

        trailPoints[0] = position;
        activeTrailPointCount = Mathf.Min(activeTrailPointCount + 1, trailPoints.Length);
        if (trail == null)
        {
            return;
        }

        trail.positionCount = activeTrailPointCount;
        for (int i = 0; i < activeTrailPointCount; i++)
        {
            trail.SetPosition(i, trailPoints[i]);
        }
    }

    private static float GetSmallestPositive(float first, float second)
    {
        bool firstValid = first > 0f;
        bool secondValid = second > 0f;
        if (firstValid && secondValid)
        {
            return Mathf.Min(first, second);
        }

        if (firstValid)
        {
            return first;
        }

        return secondValid ? second : 0f;
    }

    private static Vector3 FlattenDirection(Vector3 direction, Vector3 fallback)
    {
        if (direction.sqrMagnitude > 0.0001f)
        {
            return direction.normalized;
        }

        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    private static CoreTacticalCombatTeam ResolveTargetTeam(CoreTacticalShipMotor target)
    {
        CoreTacticalCombatant combatant = target != null ? target.GetComponent<CoreTacticalCombatant>() : null;
        return combatant != null ? combatant.team : CoreTacticalCombatTeam.Enemy;
    }

    private static bool IsLiveTarget(CoreTacticalShipMotor candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        CoreTacticalCombatant combatant = candidate.GetComponent<CoreTacticalCombatant>();
        if (combatant != null && !combatant.IsAlive)
        {
            return false;
        }

        CoreTacticalPrototypeHealth health = candidate.GetComponent<CoreTacticalPrototypeHealth>();
        return health == null || health.currentHealth > 0f;
    }

    private CoreTacticalShipMotor FindNearestLiveTarget()
    {
        CoreTacticalCombatant[] combatants = FindObjectsByType<CoreTacticalCombatant>(FindObjectsSortMode.None);
        CoreTacticalShipMotor bestTarget = null;
        float bestDistanceSqr = float.PositiveInfinity;
        Vector3 missilePosition = transform.position;
        for (int i = 0; i < combatants.Length; i++)
        {
            CoreTacticalCombatant combatant = combatants[i];
            if (!CoreTacticalOreTargetingRules.IsAutomaticCombatTarget(combatant, targetTeam)
                || !IsLiveTarget(combatant.ship))
            {
                continue;
            }

            float distanceSqr = (combatant.ship.transform.position - missilePosition).sqrMagnitude;
            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestTarget = combatant.ship;
            }
        }

        return bestTarget;
    }

    private static float GetDetonationSensitivityRadiusMeters(float proximityRadiusMeters, float explosionRadiusMeters)
    {
        return Mathf.Max(0.5f, Mathf.Max(proximityRadiusMeters, explosionRadiusMeters));
    }
}

public sealed class CoreTacticalLineTrailFadeDestroyer : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private Color startColor;
    private Color endColor;
    private float birthTime;
    private float lifetimeSeconds;

    public static void CreateFrom(LineRenderer source, int pointCount, float lifetimeSeconds)
    {
        if (source == null || pointCount <= 1)
        {
            return;
        }

        GameObject trailObject = new GameObject(source.gameObject.name + " Lingering Trail");
        LineRenderer line = trailObject.AddComponent<LineRenderer>();
        line.sharedMaterial = source.sharedMaterial;
        line.positionCount = pointCount;
        line.useWorldSpace = true;
        line.startWidth = source.startWidth;
        line.endWidth = source.endWidth;
        line.numCapVertices = source.numCapVertices;
        line.numCornerVertices = source.numCornerVertices;
        line.textureMode = source.textureMode;
        line.alignment = source.alignment;
        line.startColor = source.startColor;
        line.endColor = source.endColor;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        for (int i = 0; i < pointCount; i++)
        {
            line.SetPosition(i, source.GetPosition(i));
        }

        CoreTacticalLineTrailFadeDestroyer fade = trailObject.AddComponent<CoreTacticalLineTrailFadeDestroyer>();
        fade.Initialize(line, lifetimeSeconds);
    }

    private void Initialize(LineRenderer line, float newLifetimeSeconds)
    {
        lineRenderer = line;
        lifetimeSeconds = Mathf.Max(0.05f, newLifetimeSeconds);
        birthTime = Time.time;
        startColor = lineRenderer.startColor;
        endColor = lineRenderer.endColor;
    }

    private void Update()
    {
        float normalizedAge = Mathf.Clamp01((Time.time - birthTime) / lifetimeSeconds);
        if (normalizedAge >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        if (lineRenderer == null)
        {
            return;
        }

        float fade = 1f - normalizedAge;
        Color fadedStart = startColor;
        fadedStart.a *= fade;
        Color fadedEnd = endColor;
        fadedEnd.a *= fade;
        lineRenderer.startColor = fadedStart;
        lineRenderer.endColor = fadedEnd;
    }
}

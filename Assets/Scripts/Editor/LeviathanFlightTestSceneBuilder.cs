using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LeviathanFlightTestSceneBuilder
{
    private const string GeneratedPrefix = "Leviathan Flight Test ";

    [MenuItem("Wild Wind/Leviathan/Собрать тест полета")]
    public static void BuildFlightTestScene()
    {
        ClearGeneratedObjects();

        Leviathan leviathan = CreateLeviathan();
        CreateReferenceMarkers();
        EnsureCameraAndLight(leviathan.transform);

        EditorSceneManager.MarkSceneDirty(leviathan.gameObject.scene);
        Selection.activeGameObject = leviathan.gameObject;
        Debug.Log("[ЛевиафанТест] Сцена собрана. Левиафану создан случайный маршрут в радиусе 1 км. В Play Mode он сам пойдёт по путевой машинке; маршрут можно пересоздать кнопкой в инспекторе левиафана.");
    }

    private static void ClearGeneratedObjects()
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = objects.Length - 1; i >= 0; i--)
        {
            GameObject obj = objects[i];
            if (obj != null && obj.scene.IsValid() && obj.name.StartsWith(GeneratedPrefix))
            {
                Undo.DestroyObjectImmediate(obj);
            }
        }
    }

    private static Leviathan CreateLeviathan()
    {
        GameObject root = new GameObject(GeneratedPrefix + "Route Leviathan");
        Undo.RegisterCreatedObjectUndo(root, "Create leviathan flight test");
        root.transform.position = new Vector3(0f, 120f, 0f);

        Rigidbody body = Undo.AddComponent<Rigidbody>(root);
        body.mass = 3200f;
        body.useGravity = false;
        body.linearDamping = 0f;
        body.angularDamping = 0.45f;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        CapsuleCollider collider = Undo.AddComponent<CapsuleCollider>(root);
        collider.direction = 2;
        collider.radius = 4.5f;
        collider.height = 26f;

        Leviathan leviathan = Undo.AddComponent<Leviathan>(root);
        leviathan.Initialize("test_leviathan_route", null, null, root.transform.position);
        leviathan.displayName = "Тестовый левиафан";
        leviathan.name = "Тестовый левиафан";
        leviathan.bodyLengthMeters = 26f;
        leviathan.bodyRadiusMeters = 4.5f;
        leviathan.massKg = 3200f;
        leviathan.maxHealth = 240f;
        leviathan.health = 240f;
        leviathan.claudiumLiftKg = 3200f;
        leviathan.forwardThrustKgf = 1425f;
        leviathan.omniThrustKgf = 920f;
        leviathan.turnTorqueNm = 4200f;
        leviathan.turnDamping = 0.9f;
        leviathan.stationaryTurnEffectiveness = 0.25f;
        leviathan.fullTurnEffectSpeedMS = 8f;
        leviathan.autoCalculateDragArea = true;
        leviathan.dragCoefficient = 0.55f;
        leviathan.bellyDownStabilization = true;
        leviathan.bellyDownTorqueNm = 2200f;
        leviathan.bellyDownDamping = 0.75f;
        leviathan.organicSwimMotion = true;
        leviathan.swimSwayFrequency = 0.24f;
        leviathan.swimSwayAngleDeg = 5.5f;
        leviathan.swimSwaySideForceKgf = 24f;
        leviathan.swimNoiseRadiusMeters = 16f;
        leviathan.swimNoiseFrequency = 0.07f;
        leviathan.swimVerticalNoiseMeters = 5f;
        leviathan.visualBodySwayDeg = 5f;
        leviathan.visualTailSwayMeters = 1.8f;
        leviathan.cruiseSpeedMS = 18f;
        leviathan.failureGravityRampSeconds = 20f;
        leviathan.failureStartGravity01 = 0.12f;
        leviathan.debugLogging = true;
        leviathan.routeCruiseSpeedMS = 12f;
        leviathan.routeWaypointRadiusMeters = 22f;
        leviathan.routeLoop = true;
        leviathan.GenerateRandomRoute(1000f, 10);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(leviathan);
        return leviathan;
    }

    private static void CreateReferenceMarkers()
    {
        CreateMarker("Цель вперед", new Vector3(0f, 120f, 160f), Color.green);
        CreateMarker("Цель вверх", new Vector3(-90f, 180f, 80f), Color.cyan);
        CreateMarker("Цель вниз", new Vector3(90f, 70f, 80f), Color.yellow);
    }

    private static void CreateMarker(string name, Vector3 position, Color color)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Undo.RegisterCreatedObjectUndo(marker, "Create leviathan marker");
        marker.name = GeneratedPrefix + name;
        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * 8f;
        Renderer renderer = marker.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = new Material(Shader.Find("Standard")) { color = color };
        }
    }

    private static void EnsureCameraAndLight(Transform target)
    {
        Camera camera = Object.FindFirstObjectByType<Camera>();
        if (camera == null)
        {
            GameObject cameraObject = new GameObject(GeneratedPrefix + "Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create leviathan camera");
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        camera.transform.position = target.position + new Vector3(0f, 55f, -120f);
        camera.transform.LookAt(target.position + Vector3.up * 8f);
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 2000f;

        Light light = Object.FindFirstObjectByType<Light>();
        if (light == null)
        {
            GameObject lightObject = new GameObject(GeneratedPrefix + "Sun");
            Undo.RegisterCreatedObjectUndo(lightObject, "Create leviathan light");
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
        }

        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        light.intensity = 1.2f;
    }
}

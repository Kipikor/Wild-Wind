using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SortieLocationIsolationController : MonoBehaviour
{
    public const string ControllerObjectName = "Sortie Location Isolation Controller";
    public const string SessionSceneObjectsRootName = "Session Scene Objects";

    [InspectorName("Meta State")]
    public MetaGameState metaGameState;

    private readonly List<IsolationTarget> targets = new List<IsolationTarget>();
    private bool isolationApplied;

    public bool IsIsolationApplied => isolationApplied;
    public int TargetCount => targets.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallSortieLocationIsolationBootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureForLoadedGameplayScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForLoadedGameplayScene();
    }

    public static SortieLocationIsolationController EnsureForLoadedGameplayScene()
    {
        if (!WildWindSessionFlow.IsGameplaySceneLoaded())
        {
            return null;
        }

        SortieLocationIsolationController existing = FindFirstObjectByType<SortieLocationIsolationController>();
        if (existing != null)
        {
            return existing;
        }

        GameObject controllerObject = new GameObject(ControllerObjectName);
        return controllerObject.AddComponent<SortieLocationIsolationController>();
    }

    private void Awake()
    {
        ResolveReferences();
        RefreshTargets();
        RefreshNow();
    }

    private void Update()
    {
        RefreshNow();
    }

    private void OnDisable()
    {
        ApplyIsolation(false);
    }

    private void OnDestroy()
    {
        ApplyIsolation(false);
    }

    public void RefreshNow()
    {
        ResolveReferences();
        RefreshTargets();
        ApplyIsolation(ShouldIsolate());
    }

    private void ResolveReferences()
    {
        if (metaGameState == null)
        {
            metaGameState = FindFirstObjectByType<MetaGameState>();
        }
    }

    private bool ShouldIsolate()
    {
        return metaGameState != null
            && metaGameState.CurrentMode == GameSessionMode.Flight
            && metaGameState.HasActiveSortie;
    }

    private void RefreshTargets()
    {
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            if (targets[i].Root == null)
            {
                targets.RemoveAt(i);
            }
        }

        TryRegisterTarget(GameObject.Find(SessionSceneObjectsRootName));
    }

    private void TryRegisterTarget(GameObject root)
    {
        if (root == null || root == gameObject)
        {
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].Root == root)
            {
                return;
            }
        }

        targets.Add(new IsolationTarget(root));
    }

    private void ApplyIsolation(bool isolate)
    {
        if (isolationApplied == isolate && TargetsAlreadyMatch(isolate))
        {
            return;
        }

        isolationApplied = isolate;
        for (int i = 0; i < targets.Count; i++)
        {
            IsolationTarget target = targets[i];
            if (target.Root == null)
            {
                continue;
            }

            bool visible = !isolate && target.WasActiveAtRegistration;
            if (target.Root.activeSelf != visible)
            {
                target.Root.SetActive(visible);
            }
        }
    }

    private bool TargetsAlreadyMatch(bool isolate)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            IsolationTarget target = targets[i];
            if (target.Root == null)
            {
                continue;
            }

            bool expectedVisible = !isolate && target.WasActiveAtRegistration;
            if (target.Root.activeSelf != expectedVisible)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class IsolationTarget
    {
        public readonly GameObject Root;
        public readonly bool WasActiveAtRegistration;

        public IsolationTarget(GameObject root)
        {
            Root = root;
            WasActiveAtRegistration = root != null && root.activeSelf;
        }
    }
}

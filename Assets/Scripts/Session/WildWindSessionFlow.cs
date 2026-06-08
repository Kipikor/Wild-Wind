using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WildWindSessionFlow
{
    public const string DefaultGameplaySceneName = "WildWindSessionScene";

    public static bool IsGameplaySceneLoaded()
    {
        return SceneManager.GetActiveScene().name == DefaultGameplaySceneName;
    }

    public static bool TryEnterPort(out string error)
    {
        return TryEnterPort(DefaultGameplaySceneName, out error);
    }

    public static bool TryEnterPort(string gameplaySceneName, out string error)
    {
        return TryLoadScene(ResolveSceneName(gameplaySceneName, DefaultGameplaySceneName), out error);
    }

    private static bool TryLoadScene(string sceneName, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            error = "Scene name is empty.";
            return false;
        }

        try
        {
            SceneManager.LoadScene(sceneName);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static string ResolveSceneName(string sceneName, string fallback)
    {
        return string.IsNullOrWhiteSpace(sceneName) ? fallback : sceneName.Trim();
    }
}

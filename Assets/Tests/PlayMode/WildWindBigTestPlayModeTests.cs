using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class WildWindBigTestPlayModeTests
{
    private const string RunnerTypeName = "WildWindBigTestRunner, Assembly-CSharp";
    private const string ResultTypeName = "WildWindBigTestResult, Assembly-CSharp";

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator BigTestRunner_CompletesRequiredContract()
    {
        Type runnerType = RequireType(RunnerTypeName);
        Type resultType = RequireType(ResultTypeName);
        object result = null;

        SetStaticProperty(runnerType, "SuppressRunOnStartForAutomation", true);
        try
        {
            SceneManager.LoadScene(GetConstString(runnerType, "DefaultWorldSceneName"));
            yield return null;
            yield return null;

            MonoBehaviour runner = UnityEngine.Object.FindFirstObjectByType(runnerType) as MonoBehaviour;
            if (runner == null)
            {
                GameObject runnerObject = new GameObject("Wild Wind Big Test Runner (Automation)");
                runner = runnerObject.AddComponent(runnerType) as MonoBehaviour;
                yield return null;
            }

            Assert.NotNull(runner, "WildWindBigTestRunner must exist in the world scene.");

            Invoke(runnerType, runner, "ResetRunStateForEditor");
            SetMember(runnerType, runner, "runOnStart", false);
            SetMember(runnerType, runner, "logFullReportToConsole", false);
            SetMember(runnerType, runner, "writeReportFile", true);

            object callback = CreateResultCallback(resultType, value => result = value);
            IEnumerator routine = Invoke(runnerType, runner, "RunBigTestForAutomation", callback) as IEnumerator;
            Assert.NotNull(routine, "RunBigTestForAutomation must return IEnumerator.");

            yield return routine;
        }
        finally
        {
            SetStaticProperty(runnerType, "SuppressRunOnStartForAutomation", false);
            Time.timeScale = 1f;
        }

        Assert.NotNull(result, "Big test did not return a machine-readable result.");
        Assert.IsTrue(GetBool(result, "Completed"), GetString(result, "ReportText"));
        Assert.Zero(GetInt(result, "FailureCount"), GetString(result, "ReportText"));
        Assert.IsTrue(GetBool(result, "RequiredSectionsSatisfied"), GetString(result, "ReportText"));
        Assert.GreaterOrEqual(GetInt(result, "CheckCount"), GetInt(result, "MinimumExpectedCheckCount"), GetString(result, "ReportText"));
        Assert.IsTrue(GetBool(result, "Succeeded"), GetString(result, "ReportText"));
    }

    [Test]
    public void BigTestRunner_CanaryFailureTurnsResultRed()
    {
        Type runnerType = RequireType(RunnerTypeName);
        object result = Invoke(runnerType, null, "RunCanarySelfTest");

        Assert.NotNull(result);
        Assert.IsTrue(GetBool(result, "Completed"));
        Assert.IsFalse(GetBool(result, "Succeeded"));
        Assert.AreEqual(1, GetInt(result, "FailureCount"));
        Assert.AreEqual(1, GetInt(result, "CheckCount"));
    }

    [Test]
    public void BigTestRunner_ConsoleCanaryFailureTurnsResultRed()
    {
        Type runnerType = RequireType(RunnerTypeName);
        object result = Invoke(runnerType, null, "RunConsoleCanarySelfTest");

        Assert.NotNull(result);
        Assert.IsTrue(GetBool(result, "Completed"));
        Assert.IsFalse(GetBool(result, "Succeeded"));
        Assert.AreEqual(1, GetInt(result, "FailureCount"));
        Assert.AreEqual(1, GetInt(result, "CheckCount"));
    }

    private static Type RequireType(string assemblyQualifiedName)
    {
        Type type = Type.GetType(assemblyQualifiedName);
        Assert.NotNull(type, "Missing type: " + assemblyQualifiedName);
        return type;
    }

    private static object Invoke(Type type, object target, string methodName, params object[] args)
    {
        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        Assert.NotNull(method, "Missing method: " + type.Name + "." + methodName);
        return method.Invoke(target, args);
    }

    private static string GetConstString(Type type, string fieldName)
    {
        FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(field, "Missing const field: " + fieldName);
        return field.GetRawConstantValue() as string;
    }

    private static void SetStaticProperty(Type type, string propertyName, object value)
    {
        PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(property, "Missing static property: " + propertyName);
        property.SetValue(null, value);
    }

    private static void SetMember(Type type, object target, string memberName, object value)
    {
        FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(target, value);
            return;
        }

        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(property, "Missing member: " + memberName);
        property.SetValue(target, value);
    }

    private static object CreateResultCallback(Type resultType, Action<object> capture)
    {
        MethodInfo method = typeof(WildWindBigTestPlayModeTests)
            .GetMethod(nameof(CreateTypedResultCallback), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(resultType);
        return method.Invoke(null, new object[] { capture });
    }

    private static Action<T> CreateTypedResultCallback<T>(Action<object> capture)
    {
        return value => capture(value);
    }

    private static bool GetBool(object target, string propertyName)
    {
        return (bool)GetProperty(target, propertyName);
    }

    private static int GetInt(object target, string propertyName)
    {
        return (int)GetProperty(target, propertyName);
    }

    private static string GetString(object target, string propertyName)
    {
        return (string)GetProperty(target, propertyName);
    }

    private static object GetProperty(object target, string propertyName)
    {
        PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property, "Missing result property: " + propertyName);
        return property.GetValue(target);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class WildWindUsageAudit
{
    private const string LogPrefix = "[WildWindUsageAudit] ";
    private const string ReportFolderRelative = "TestReports/UsageAudit";
    public const string GenerateAfterBigTestArmedSessionKey = "WildWind.UsageAudit.GenerateAfterBigTest.Armed";
    public const string GenerateAfterBigTestRequestedSessionKey = "WildWind.UsageAudit.GenerateAfterBigTest.Requested";
    private const int MaxReferenceSamples = 6;
    private const int MaxReportRowsPerSection = 80;

    private static readonly string[] ScriptRoots =
    {
        "Assets/Scripts",
        "Assets/Tests"
    };

    private static readonly string[] CoverageSearchRoots =
    {
        "CodeCoverage",
        "TestReports",
        "Library/CodeCoverage"
    };

    private static readonly string[] ThirdPartyPathFragments =
    {
        "Assets/AllSkyFree/",
        "Assets/Fog Particles/",
        "Assets/Mirza/",
        "Assets/ShadowVision/",
        "Assets/TextMesh Pro/",
        "Assets/TrueClouds/"
    };

    private static readonly HashSet<string> ReferenceExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".asmdef",
        ".asset",
        ".controller",
        ".csv",
        ".json",
        ".mat",
        ".overridecontroller",
        ".playable",
        ".prefab",
        ".shader",
        ".shadergraph",
        ".txt",
        ".unity",
        ".uss",
        ".uxml"
    };

    private static readonly HashSet<string> CoverageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".coverage",
        ".info",
        ".json",
        ".lcov",
        ".txt",
        ".xml"
    };

    private static readonly Regex TypeRegex = new Regex(
        @"\b(?:public|internal|private|protected|static|sealed|abstract|partial|new|\s)*\b(?:class|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Compiled);

    private static readonly Regex IdentifierRegex = new Regex(@"\b[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled);
    private static readonly Regex GuidReferenceRegex = new Regex(@"\bguid:\s*([0-9a-fA-F]{32})\b", RegexOptions.Compiled);
    private static readonly Regex LcovSourceRegex = new Regex(@"^SF:(.+)$", RegexOptions.Compiled);
    private static readonly Regex LcovLineRegex = new Regex(@"^DA:\d+,(\d+)", RegexOptions.Compiled);

    static WildWindUsageAudit()
    {
        EditorApplication.update -= GenerateRequestedReportWhenReady;
        EditorApplication.update += GenerateRequestedReportWhenReady;
    }

    public static void ArmForBigTest()
    {
        SessionState.SetBool(GenerateAfterBigTestArmedSessionKey, true);
        SessionState.SetBool(GenerateAfterBigTestRequestedSessionKey, false);
    }

    public static void GenerateReadOnlyReport()
    {
        string projectRoot = GetProjectRoot();
        List<ScriptAuditRecord> scripts = CollectScripts(projectRoot);
        CoverageIndex coverage = CoverageIndex.Load(projectRoot);

        ApplyGuidReferences(projectRoot, scripts);
        ApplyTextReferences(scripts);
        ApplyCoverage(scripts, coverage);
        ApplyRiskAssessment(scripts, coverage.HasCoverageInput);

        UsageAuditResult result = BuildResult(projectRoot, scripts, coverage);
        WriteReports(projectRoot, result);

        Debug.Log(LogPrefix + "Read-only report generated: " + result.latestMarkdownPath);
    }

    public static void OpenLatestReport()
    {
        string path = Path.Combine(GetProjectRoot(), ReportFolderRelative, "WildWindUsageAudit_latest.md");
        if (!File.Exists(path))
        {
            Debug.LogWarning(LogPrefix + "No latest report found. Generate the read-only report first.");
            return;
        }

        EditorUtility.RevealInFinder(path);
    }

    private static void GenerateRequestedReportWhenReady()
    {
        if (!SessionState.GetBool(GenerateAfterBigTestRequestedSessionKey, false))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        if (EditorApplication.isPlaying && WildWindBigTestRunner.IsMainWorldCheckInProgress)
        {
            return;
        }

        SessionState.SetBool(GenerateAfterBigTestRequestedSessionKey, false);
        SessionState.SetBool(GenerateAfterBigTestArmedSessionKey, false);
        GenerateReadOnlyReport();
    }

    private static List<ScriptAuditRecord> CollectScripts(string projectRoot)
    {
        string[] guids = AssetDatabase.FindAssets("t:MonoScript", ScriptRoots);
        List<ScriptAuditRecord> scripts = new List<ScriptAuditRecord>();
        HashSet<string> seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < guids.Length; i++)
        {
            string guid = guids[i];
            string path = NormalizeProjectPath(AssetDatabase.GUIDToAssetPath(guid), projectRoot);
            if (string.IsNullOrWhiteSpace(path) ||
                !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                !seenPaths.Add(path))
            {
                continue;
            }

            string absolutePath = Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar));
            string text = TryReadAllText(absolutePath);
            string codeText = StripCSharpTrivia(text);
            ScriptAuditRecord record = new ScriptAuditRecord
            {
                guid = guid,
                path = path,
                typeNames = ExtractTypeNames(codeText),
                isEditorScript = IsEditorPath(path) || ContainsAny(text, "UnityEditor", "[MenuItem("),
                isTestScript = path.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("[Test]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("[UnityTest]", StringComparison.OrdinalIgnoreCase) >= 0,
                isThirdParty = IsThirdPartyPath(path),
                hasUnityEntryPoint = ContainsAny(
                    text,
                    "[MenuItem(",
                    "[RuntimeInitializeOnLoadMethod",
                    "[InitializeOnLoad",
                    "[InitializeOnLoadMethod",
                    "[CustomEditor",
                    "[CreateAssetMenu",
                    "IPostprocess",
                    "AssetPostprocessor"),
                hasReflectionOrDynamicLoad = ContainsAny(
                    text,
                    "Type.GetType",
                    "GetMethod(",
                    "GetField(",
                    "Invoke(",
                    "SendMessage(",
                    "BroadcastMessage(",
                    "Resources.Load",
                    "AssetDatabase.LoadAssetAtPath",
                    "SceneManager.LoadScene")
            };

            scripts.Add(record);
        }

        scripts.Sort((left, right) => string.Compare(left.path, right.path, StringComparison.OrdinalIgnoreCase));
        return scripts;
    }

    private static void ApplyGuidReferences(string projectRoot, List<ScriptAuditRecord> scripts)
    {
        Dictionary<string, ScriptAuditRecord> byGuid = scripts
            .Where(script => !string.IsNullOrWhiteSpace(script.guid))
            .ToDictionary(script => script.guid, script => script, StringComparer.OrdinalIgnoreCase);

        foreach (string relativePath in GetReferenceTextFilePaths(projectRoot))
        {
            string absolutePath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string text = StripCSharpTrivia(TryReadAllText(absolutePath));
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            MatchCollection matches = GuidReferenceRegex.Matches(text);
            for (int i = 0; i < matches.Count; i++)
            {
                string guid = matches[i].Groups[1].Value;
                if (!byGuid.TryGetValue(guid, out ScriptAuditRecord script) ||
                    string.Equals(script.path, relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                script.guidReferenceCount++;
                AddSample(script.guidReferenceSamples, relativePath);
            }
        }
    }

    private static void ApplyTextReferences(List<ScriptAuditRecord> scripts)
    {
        Dictionary<string, List<ScriptAuditRecord>> byType = new Dictionary<string, List<ScriptAuditRecord>>(StringComparer.Ordinal);
        for (int i = 0; i < scripts.Count; i++)
        {
            ScriptAuditRecord script = scripts[i];
            for (int t = 0; t < script.typeNames.Count; t++)
            {
                string typeName = script.typeNames[t];
                if (!byType.TryGetValue(typeName, out List<ScriptAuditRecord> owners))
                {
                    owners = new List<ScriptAuditRecord>();
                    byType[typeName] = owners;
                }

                owners.Add(script);
            }
        }

        for (int i = 0; i < scripts.Count; i++)
        {
            ScriptAuditRecord reader = scripts[i];
            string absolutePath = Path.Combine(GetProjectRoot(), reader.path.Replace('/', Path.DirectorySeparatorChar));
            string text = TryReadAllText(absolutePath);
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            HashSet<string> identifiers = new HashSet<string>(StringComparer.Ordinal);
            MatchCollection matches = IdentifierRegex.Matches(text);
            for (int m = 0; m < matches.Count; m++)
            {
                identifiers.Add(matches[m].Value);
            }

            foreach (string identifier in identifiers)
            {
                if (!byType.TryGetValue(identifier, out List<ScriptAuditRecord> owners))
                {
                    continue;
                }

                for (int ownerIndex = 0; ownerIndex < owners.Count; ownerIndex++)
                {
                    ScriptAuditRecord owner = owners[ownerIndex];
                    if (string.Equals(owner.path, reader.path, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    owner.textReferenceCount++;
                    AddSample(owner.textReferenceSamples, reader.path);
                }
            }
        }
    }

    private static void ApplyCoverage(List<ScriptAuditRecord> scripts, CoverageIndex coverage)
    {
        for (int i = 0; i < scripts.Count; i++)
        {
            ScriptAuditRecord script = scripts[i];
            if (coverage.coveredPaths.Contains(script.path))
            {
                script.coverageStatus = "covered";
            }
            else if (coverage.mentionedPaths.Contains(script.path))
            {
                script.coverageStatus = "uncovered";
            }
            else
            {
                script.coverageStatus = coverage.HasCoverageInput ? "not-in-coverage" : "coverage-missing";
            }
        }
    }

    private static void ApplyRiskAssessment(List<ScriptAuditRecord> scripts, bool hasCoverageInput)
    {
        for (int i = 0; i < scripts.Count; i++)
        {
            ScriptAuditRecord script = scripts[i];
            bool hasStaticReferences = script.guidReferenceCount > 0 || script.textReferenceCount > 0;
            bool covered = string.Equals(script.coverageStatus, "covered", StringComparison.OrdinalIgnoreCase);
            bool uncoveredByCoverage = string.Equals(script.coverageStatus, "uncovered", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(script.coverageStatus, "not-in-coverage", StringComparison.OrdinalIgnoreCase);

            if (script.isThirdParty)
            {
                script.riskBucket = "vendor-or-package";
                script.recommendation = "Ignore for cleanup unless the whole package is intentionally removed.";
            }
            else if (covered)
            {
                script.riskBucket = "active-in-big-test";
                script.recommendation = "Keep. Big test exercised this script.";
            }
            else if (hasStaticReferences && !hasCoverageInput)
            {
                script.riskBucket = "referenced-static-needs-coverage";
                script.recommendation = "Static references exist; run code coverage to learn whether the big test executes this script.";
            }
            else if (hasStaticReferences)
            {
                script.riskBucket = "referenced-but-not-covered";
                script.recommendation = "Keep for now; either expand the big test or inspect the referenced feature.";
            }
            else if (script.hasUnityEntryPoint || script.hasReflectionOrDynamicLoad)
            {
                script.riskBucket = "dynamic-entrypoint";
                script.recommendation = "Manual review only. Unity attributes, reflection, scene loading, or resource loading can hide references.";
            }
            else if (script.isEditorScript || script.isTestScript)
            {
                script.riskBucket = "tooling-or-test";
                script.recommendation = "Manual review. Tooling and tests may be intentionally outside gameplay coverage.";
            }
            else if (!hasCoverageInput)
            {
                script.riskBucket = "static-orphan-needs-coverage";
                script.recommendation = "Looks unreferenced statically, but run code coverage before deleting.";
            }
            else if (uncoveredByCoverage)
            {
                script.riskBucket = "cleanup-candidate";
                script.recommendation = "Candidate only. Delete in a tiny batch, then compile and rerun the big test.";
            }
            else
            {
                script.riskBucket = "manual-review";
                script.recommendation = "Manual review.";
            }
        }
    }

    private static UsageAuditResult BuildResult(string projectRoot, List<ScriptAuditRecord> scripts, CoverageIndex coverage)
    {
        UsageAuditResult result = new UsageAuditResult
        {
            generatedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            projectRoot = projectRoot,
            scripts = scripts,
            coverageFiles = coverage.sourceFiles,
            hasCoverageInput = coverage.HasCoverageInput,
            codeCoveragePackageInManifest = IsCodeCoveragePackageInManifest(projectRoot)
        };

        return result;
    }

    private static void WriteReports(string projectRoot, UsageAuditResult result)
    {
        string folder = Path.Combine(projectRoot, ReportFolderRelative);
        Directory.CreateDirectory(folder);

        string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string markdownPath = Path.Combine(folder, "WildWindUsageAudit_" + timestamp + ".md");
        string jsonPath = Path.Combine(folder, "WildWindUsageAudit_" + timestamp + ".json");
        string latestMarkdownPath = Path.Combine(folder, "WildWindUsageAudit_latest.md");
        string latestJsonPath = Path.Combine(folder, "WildWindUsageAudit_latest.json");

        result.markdownPath = markdownPath;
        result.jsonPath = jsonPath;
        result.latestMarkdownPath = latestMarkdownPath;
        result.latestJsonPath = latestJsonPath;

        string markdown = BuildMarkdown(result);
        string json = JsonUtility.ToJson(UsageAuditJson.FromResult(result), true);

        File.WriteAllText(markdownPath, markdown, Encoding.UTF8);
        File.WriteAllText(jsonPath, json, Encoding.UTF8);
        File.WriteAllText(latestMarkdownPath, markdown, Encoding.UTF8);
        File.WriteAllText(latestJsonPath, json, Encoding.UTF8);
        AssetDatabase.Refresh();
    }

    private static string BuildMarkdown(UsageAuditResult result)
    {
        StringBuilder builder = new StringBuilder(32768);
        List<ScriptAuditRecord> scripts = result.scripts;
        List<ScriptAuditRecord> cleanupCandidates = FilterByBucket(scripts, "cleanup-candidate");
        List<ScriptAuditRecord> staticOrphans = FilterByBucket(scripts, "static-orphan-needs-coverage");
        List<ScriptAuditRecord> referencedButNotCovered = FilterByBucket(scripts, "referenced-but-not-covered");
        List<ScriptAuditRecord> referencedStaticNeedsCoverage = FilterByBucket(scripts, "referenced-static-needs-coverage");
        List<ScriptAuditRecord> dynamicEntrypoints = FilterByBucket(scripts, "dynamic-entrypoint");
        List<ScriptAuditRecord> active = FilterByBucket(scripts, "active-in-big-test");

        builder.AppendLine("# Wild Wind Usage Audit");
        builder.AppendLine();
        builder.AppendLine("- Generated UTC: " + result.generatedAtUtc);
        builder.AppendLine("- Safety mode: read-only. No files were moved or deleted.");
        builder.AppendLine("- Scripts scanned: " + scripts.Count);
        builder.AppendLine("- Coverage files found: " + result.coverageFiles.Count);
        builder.AppendLine("- Code Coverage package in manifest: " + (result.codeCoveragePackageInManifest ? "yes" : "no"));
        builder.AppendLine();

        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine("| Bucket | Count | Meaning |");
        builder.AppendLine("| --- | ---: | --- |");
        AppendBucketRow(builder, scripts, "active-in-big-test", "Executed according to available coverage.");
        AppendBucketRow(builder, scripts, "referenced-but-not-covered", "Static Unity/C# references exist, but coverage did not touch it.");
        AppendBucketRow(builder, scripts, "referenced-static-needs-coverage", "Static Unity/C# references exist, but dynamic coverage input is missing.");
        AppendBucketRow(builder, scripts, "cleanup-candidate", "No static references and no dynamic coverage evidence.");
        AppendBucketRow(builder, scripts, "static-orphan-needs-coverage", "No static references, but coverage input is missing.");
        AppendBucketRow(builder, scripts, "dynamic-entrypoint", "Reflection, Resources, scene loading, or Unity entry attributes require manual review.");
        AppendBucketRow(builder, scripts, "tooling-or-test", "Editor tooling or tests.");
        AppendBucketRow(builder, scripts, "vendor-or-package", "Third-party or imported package code.");
        AppendBucketRow(builder, scripts, "manual-review", "Fallback bucket.");
        builder.AppendLine();

        builder.AppendLine("## Coverage Input");
        builder.AppendLine();
        if (result.coverageFiles.Count == 0)
        {
            builder.AppendLine("No coverage files were found. The report still checks static C# and Unity YAML references, but it cannot prove what the big test executed.");
            builder.AppendLine();
            builder.AppendLine("Expected places: `CodeCoverage`, `Library/CodeCoverage`, or `TestReports`. XML, LCOV, JSON, and text coverage files are scanned.");
        }
        else
        {
            for (int i = 0; i < result.coverageFiles.Count; i++)
            {
                builder.AppendLine("- `" + result.coverageFiles[i] + "`");
            }
        }

        if (!result.codeCoveragePackageInManifest)
        {
            builder.AppendLine();
            builder.AppendLine("Note: `com.unity.testtools.codecoverage` is not listed in `Packages/manifest.json`. Add Unity Code Coverage when you want the dynamic big-test part of this audit.");
        }
        builder.AppendLine();

        builder.AppendLine("## Cleanup Candidates");
        builder.AppendLine();
        if (cleanupCandidates.Count == 0 && staticOrphans.Count == 0)
        {
            builder.AppendLine("No low-reference cleanup candidates were found in this pass.");
            builder.AppendLine();
        }
        else
        {
            if (cleanupCandidates.Count > 0)
            {
                builder.AppendLine("These are the strongest candidates. Still inspect before deleting, then remove in small batches and rerun the big test.");
                builder.AppendLine();
                AppendScriptTable(builder, cleanupCandidates, MaxReportRowsPerSection);
                builder.AppendLine();
            }

            if (staticOrphans.Count > 0)
            {
                builder.AppendLine("These look statically orphaned, but coverage was missing. Treat them as review targets, not delete targets.");
                builder.AppendLine();
                AppendScriptTable(builder, staticOrphans, MaxReportRowsPerSection);
                builder.AppendLine();
            }
        }

        builder.AppendLine("## Referenced But Not Covered");
        builder.AppendLine();
        if (referencedButNotCovered.Count == 0 && referencedStaticNeedsCoverage.Count == 0)
        {
            builder.AppendLine("No referenced scripts were left uncovered by the available coverage data.");
        }
        else
        {
            builder.AppendLine(result.hasCoverageInput
                ? "These are usually test gaps or features the big test does not touch yet."
                : "These have static references, but coverage input is missing, so runtime usage is still unknown.");
            builder.AppendLine();
            AppendScriptTable(builder, referencedButNotCovered.Count > 0 ? referencedButNotCovered : referencedStaticNeedsCoverage, MaxReportRowsPerSection);
        }
        builder.AppendLine();

        builder.AppendLine("## Dynamic Entrypoints");
        builder.AppendLine();
        if (dynamicEntrypoints.Count == 0)
        {
            builder.AppendLine("No dynamic-entrypoint scripts need special review.");
        }
        else
        {
            builder.AppendLine("Do not delete these from coverage alone. Unity attributes and dynamic loads can bypass static reference checks.");
            builder.AppendLine();
            AppendScriptTable(builder, dynamicEntrypoints, MaxReportRowsPerSection);
        }
        builder.AppendLine();

        builder.AppendLine("## Active In Big Test");
        builder.AppendLine();
        if (active.Count == 0)
        {
            builder.AppendLine(result.hasCoverageInput
                ? "Coverage input was found, but no project scripts were marked covered by the supported parsers."
                : "Coverage input is missing, so this section is empty.");
        }
        else
        {
            AppendScriptTable(builder, active, MaxReportRowsPerSection);
        }
        builder.AppendLine();

        builder.AppendLine("## Safe Cleanup Loop");
        builder.AppendLine();
        builder.AppendLine("1. Generate coverage by running the PlayMode big test with Unity Code Coverage enabled.");
        builder.AppendLine("2. Generate this audit report.");
        builder.AppendLine("3. Inspect only `cleanup-candidate` rows first.");
        builder.AppendLine("4. Delete a tiny batch manually.");
        builder.AppendLine("5. Compile, run the big test, and regenerate the audit.");
        builder.AppendLine("6. Commit each clean batch separately.");

        return builder.ToString();
    }

    private static void AppendBucketRow(StringBuilder builder, List<ScriptAuditRecord> scripts, string bucket, string meaning)
    {
        builder.AppendLine("| `" + bucket + "` | " + scripts.Count(script => script.riskBucket == bucket) + " | " + meaning + " |");
    }

    private static void AppendScriptTable(StringBuilder builder, List<ScriptAuditRecord> scripts, int limit)
    {
        builder.AppendLine("| Script | Coverage | GUID refs | C# refs | Types | Recommendation | Samples |");
        builder.AppendLine("| --- | --- | ---: | ---: | --- | --- | --- |");

        int rowCount = Mathf.Min(limit, scripts.Count);
        for (int i = 0; i < rowCount; i++)
        {
            ScriptAuditRecord script = scripts[i];
            builder.AppendLine("| `" + EscapeMarkdown(script.path) + "` | `" +
                EscapeMarkdown(script.coverageStatus) + "` | " +
                script.guidReferenceCount + " | " +
                script.textReferenceCount + " | " +
                EscapeMarkdown(JoinLimited(script.typeNames, 4)) + " | " +
                EscapeMarkdown(script.recommendation) + " | " +
                EscapeMarkdown(BuildSampleText(script)) + " |");
        }

        if (scripts.Count > limit)
        {
            builder.AppendLine();
            builder.AppendLine("_Showing " + limit + " of " + scripts.Count + " rows. Full data is in the JSON report._");
        }
    }

    private static string BuildSampleText(ScriptAuditRecord script)
    {
        List<string> samples = new List<string>();
        samples.AddRange(script.guidReferenceSamples);
        samples.AddRange(script.textReferenceSamples);
        return JoinLimited(samples, MaxReferenceSamples);
    }

    private static List<ScriptAuditRecord> FilterByBucket(List<ScriptAuditRecord> scripts, string bucket)
    {
        return scripts
            .Where(script => script.riskBucket == bucket)
            .OrderBy(script => script.isEditorScript)
            .ThenBy(script => script.path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ExtractTypeNames(string text)
    {
        List<string> names = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return names;
        }

        MatchCollection matches = TypeRegex.Matches(text);
        for (int i = 0; i < matches.Count; i++)
        {
            string name = matches[i].Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static string StripCSharpTrivia(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        StringBuilder builder = new StringBuilder(text.Length);
        bool inLineComment = false;
        bool inBlockComment = false;
        bool inString = false;
        bool inVerbatimString = false;
        bool inChar = false;

        for (int i = 0; i < text.Length; i++)
        {
            char current = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (inLineComment)
            {
                if (current == '\n')
                {
                    inLineComment = false;
                    builder.Append('\n');
                }
                else
                {
                    builder.Append(' ');
                }

                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    builder.Append(' ');
                    builder.Append(' ');
                    i++;
                }
                else
                {
                    builder.Append(current == '\n' ? '\n' : ' ');
                }

                continue;
            }

            if (inString)
            {
                if (inVerbatimString && current == '"' && next == '"')
                {
                    builder.Append(' ');
                    builder.Append(' ');
                    i++;
                    continue;
                }

                if (current == '"' && (inVerbatimString || !IsEscaped(text, i)))
                {
                    inString = false;
                    inVerbatimString = false;
                }

                builder.Append(current == '\n' ? '\n' : ' ');
                continue;
            }

            if (inChar)
            {
                if (current == '\'' && !IsEscaped(text, i))
                {
                    inChar = false;
                }

                builder.Append(current == '\n' ? '\n' : ' ');
                continue;
            }

            if (current == '/' && next == '/')
            {
                inLineComment = true;
                builder.Append(' ');
                builder.Append(' ');
                i++;
                continue;
            }

            if (current == '/' && next == '*')
            {
                inBlockComment = true;
                builder.Append(' ');
                builder.Append(' ');
                i++;
                continue;
            }

            if (current == '"' ||
                (current == '@' && next == '"') ||
                (current == '$' && next == '"') ||
                (current == '$' && next == '@' && i + 2 < text.Length && text[i + 2] == '"') ||
                (current == '@' && next == '$' && i + 2 < text.Length && text[i + 2] == '"'))
            {
                inString = true;
                inVerbatimString = current == '@' ||
                    (current == '$' && next == '@') ||
                    (current == '@' && next == '$');
                builder.Append(' ');
                continue;
            }

            if (current == '\'')
            {
                inChar = true;
                builder.Append(' ');
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static bool IsEscaped(string text, int index)
    {
        int slashCount = 0;
        for (int i = index - 1; i >= 0 && text[i] == '\\'; i--)
        {
            slashCount++;
        }

        return slashCount % 2 == 1;
    }

    private static IEnumerable<string> GetReferenceTextFilePaths(string projectRoot)
    {
        HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] assetPaths = AssetDatabase.GetAllAssetPaths();
        for (int i = 0; i < assetPaths.Length; i++)
        {
            string path = NormalizeProjectPath(assetPaths[i], projectRoot);
            if (IsReferenceTextFile(path))
            {
                paths.Add(path);
            }
        }

        string projectSettings = Path.Combine(projectRoot, "ProjectSettings");
        if (Directory.Exists(projectSettings))
        {
            foreach (string absolutePath in Directory.EnumerateFiles(projectSettings, "*.*", SearchOption.AllDirectories))
            {
                string relative = NormalizeProjectPath(absolutePath, projectRoot);
                if (IsReferenceTextFile(relative))
                {
                    paths.Add(relative);
                }
            }
        }

        return paths;
    }

    private static bool IsReferenceTextFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ReferenceExtensions.Contains(Path.GetExtension(path));
    }

    private static bool IsEditorPath(string path)
    {
        return path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            path.EndsWith("Editor.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsThirdPartyPath(string path)
    {
        for (int i = 0; i < ThirdPartyPathFragments.Length; i++)
        {
            if (path.IndexOf(ThirdPartyPathFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (int i = 0; i < needles.Length; i++)
        {
            if (text.IndexOf(needles[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void AddSample(List<string> samples, string value)
    {
        if (samples.Count >= MaxReferenceSamples ||
            string.IsNullOrWhiteSpace(value) ||
            samples.Contains(value))
        {
            return;
        }

        samples.Add(value);
    }

    private static string JoinLimited(List<string> values, int limit)
    {
        if (values == null || values.Count == 0)
        {
            return "";
        }

        int count = Mathf.Min(limit, values.Count);
        string text = string.Join(", ", values.Take(count).ToArray());
        if (values.Count > limit)
        {
            text += ", +" + (values.Count - limit);
        }

        return text;
    }

    private static string EscapeMarkdown(string value)
    {
        return string.IsNullOrEmpty(value)
            ? ""
            : value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }

    private static bool IsCodeCoveragePackageInManifest(string projectRoot)
    {
        string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
        string manifest = TryReadAllText(manifestPath);
        return manifest.IndexOf("com.unity.testtools.codecoverage", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string TryReadAllText(string absolutePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                return "";
            }

            FileInfo info = new FileInfo(absolutePath);
            if (info.Length > 16L * 1024L * 1024L)
            {
                return "";
            }

            return File.ReadAllText(absolutePath, Encoding.UTF8);
        }
        catch
        {
            return "";
        }
    }

    private static string GetProjectRoot()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }

    private static string NormalizeProjectPath(string path, string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        string normalized = path.Replace('\\', '/').Trim();
        if (normalized.StartsWith("file:///", StringComparison.OrdinalIgnoreCase) &&
            Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri))
        {
            normalized = uri.LocalPath.Replace('\\', '/');
        }

        string root = projectRoot.Replace('\\', '/').TrimEnd('/');
        if (Path.IsPathRooted(normalized))
        {
            string full = Path.GetFullPath(normalized).Replace('\\', '/');
            if (full.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = full.Substring(root.Length + 1);
            }
        }

        int assetsIndex = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
        if (assetsIndex >= 0)
        {
            normalized = normalized.Substring(assetsIndex + 1);
        }

        int projectSettingsIndex = normalized.IndexOf("/ProjectSettings/", StringComparison.OrdinalIgnoreCase);
        if (projectSettingsIndex >= 0)
        {
            normalized = normalized.Substring(projectSettingsIndex + 1);
        }

        return normalized.TrimStart('/');
    }

    private sealed class CoverageIndex
    {
        public readonly HashSet<string> mentionedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> coveredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<string> sourceFiles = new List<string>();

        public bool HasCoverageInput => sourceFiles.Count > 0;

        public static CoverageIndex Load(string projectRoot)
        {
            CoverageIndex index = new CoverageIndex();
            foreach (string file in FindCoverageFiles(projectRoot))
            {
                string relative = NormalizeProjectPath(file, projectRoot);
                index.sourceFiles.Add(relative);
                string extension = Path.GetExtension(file);
                if (string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".coverage", StringComparison.OrdinalIgnoreCase))
                {
                    index.ReadXml(file, projectRoot);
                }
                else if (string.Equals(extension, ".info", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".lcov", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase))
                {
                    index.ReadLcovLike(file, projectRoot);
                }
                else
                {
                    index.ReadLooseText(file, projectRoot);
                }
            }

            index.sourceFiles.Sort(StringComparer.OrdinalIgnoreCase);
            return index;
        }

        private static IEnumerable<string> FindCoverageFiles(string projectRoot)
        {
            HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < CoverageSearchRoots.Length; i++)
            {
                string rootName = CoverageSearchRoots[i];
                string root = Path.Combine(projectRoot, rootName.Replace('/', Path.DirectorySeparatorChar));
                bool requireCoverageName = rootName.Equals("TestReports", StringComparison.OrdinalIgnoreCase);
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (string file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
                {
                    if (requireCoverageName && !LooksLikeCoverageFileName(file))
                    {
                        continue;
                    }

                    string extension = Path.GetExtension(file);
                    if (!CoverageExtensions.Contains(extension))
                    {
                        continue;
                    }

                    FileInfo info = new FileInfo(file);
                    if (info.Length <= 0L || info.Length > 32L * 1024L * 1024L)
                    {
                        continue;
                    }

                    files.Add(file);
                }
            }

            return files;
        }

        private static bool LooksLikeCoverageFileName(string file)
        {
            string name = Path.GetFileName(file);
            return name.IndexOf("coverage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("opencover", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("cobertura", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("lcov", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ReadXml(string file, string projectRoot)
        {
            try
            {
                XDocument document = XDocument.Load(file);
                Dictionary<string, string> openCoverFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (XElement element in document.Descendants())
                {
                    if (element.Name.LocalName == "File")
                    {
                        string id = AttributeValue(element, "uid", "id", "fileid");
                        string path = AttributeValue(element, "fullPath", "path", "filename", "url");
                        string normalized = NormalizeCoveragePath(path, projectRoot);
                        if (!string.IsNullOrWhiteSpace(normalized))
                        {
                            mentionedPaths.Add(normalized);
                            if (!string.IsNullOrWhiteSpace(id))
                            {
                                openCoverFiles[id] = normalized;
                            }
                        }
                    }
                }

                foreach (XElement element in document.Descendants())
                {
                    if (element.Name.LocalName == "SequencePoint" || element.Name.LocalName == "BranchPoint")
                    {
                        string id = AttributeValue(element, "fileid", "fileId", "fid");
                        if (string.IsNullOrWhiteSpace(id) || !openCoverFiles.TryGetValue(id, out string path))
                        {
                            continue;
                        }

                        if (ReadPositiveHitCount(element, "vc", "visitcount", "visits", "hits"))
                        {
                            coveredPaths.Add(path);
                        }
                    }

                    if (element.Name.LocalName == "class" || element.Name.LocalName == "file" || element.Name.LocalName == "sourcefile")
                    {
                        string path = AttributeValue(element, "filename", "path", "fullPath", "url", "name");
                        string normalized = NormalizeCoveragePath(path, projectRoot);
                        if (string.IsNullOrWhiteSpace(normalized))
                        {
                            continue;
                        }

                        mentionedPaths.Add(normalized);
                        if (ElementTreeHasHit(element))
                        {
                            coveredPaths.Add(normalized);
                        }
                    }
                }
            }
            catch
            {
                ReadLooseText(file, projectRoot);
            }
        }

        private void ReadLcovLike(string file, string projectRoot)
        {
            try
            {
                string currentPath = "";
                bool currentCovered = false;
                foreach (string line in File.ReadLines(file))
                {
                    Match sourceMatch = LcovSourceRegex.Match(line);
                    if (sourceMatch.Success)
                    {
                        FlushLcovRecord(currentPath, currentCovered, projectRoot);
                        currentPath = sourceMatch.Groups[1].Value.Trim();
                        currentCovered = false;
                        continue;
                    }

                    Match lineMatch = LcovLineRegex.Match(line);
                    if (lineMatch.Success &&
                        int.TryParse(lineMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hits) &&
                        hits > 0)
                    {
                        currentCovered = true;
                    }

                    if (line.StartsWith("end_of_record", StringComparison.OrdinalIgnoreCase))
                    {
                        FlushLcovRecord(currentPath, currentCovered, projectRoot);
                        currentPath = "";
                        currentCovered = false;
                    }
                }

                FlushLcovRecord(currentPath, currentCovered, projectRoot);
            }
            catch
            {
                ReadLooseText(file, projectRoot);
            }
        }

        private void ReadLooseText(string file, string projectRoot)
        {
            string text = TryReadAllText(file);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (Match match in Regex.Matches(text, @"(?:[A-Za-z]:)?[^""'<>\r\n]*Assets[/\\][^""'<>\r\n]+?\.cs"))
            {
                string normalized = NormalizeCoveragePath(match.Value, projectRoot);
                if (!string.IsNullOrWhiteSpace(normalized))
                {
                    mentionedPaths.Add(normalized);
                }
            }
        }

        private void FlushLcovRecord(string path, bool covered, string projectRoot)
        {
            string normalized = NormalizeCoveragePath(path, projectRoot);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            mentionedPaths.Add(normalized);
            if (covered)
            {
                coveredPaths.Add(normalized);
            }
        }

        private static bool ElementTreeHasHit(XElement element)
        {
            foreach (XElement descendant in element.DescendantsAndSelf())
            {
                if (ReadPositiveHitCount(descendant, "hits", "visitcount", "visits", "vc") ||
                    string.Equals(AttributeValue(descendant, "visited"), "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ReadPositiveHitCount(XElement element, params string[] attributeNames)
        {
            for (int i = 0; i < attributeNames.Length; i++)
            {
                string value = AttributeValue(element, attributeNames[i]);
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hits) && hits > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string AttributeValue(XElement element, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                XAttribute attribute = element.Attribute(names[i]);
                if (attribute != null)
                {
                    return attribute.Value;
                }
            }

            return "";
        }

        private static string NormalizeCoveragePath(string path, string projectRoot)
        {
            string normalized = NormalizeProjectPath(path, projectRoot);
            return normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? normalized : "";
        }
    }

    [Serializable]
    private sealed class UsageAuditJson
    {
        public string generatedAtUtc;
        public string projectRoot;
        public bool hasCoverageInput;
        public bool codeCoveragePackageInManifest;
        public string[] coverageFiles;
        public ScriptAuditJson[] scripts;

        public static UsageAuditJson FromResult(UsageAuditResult result)
        {
            return new UsageAuditJson
            {
                generatedAtUtc = result.generatedAtUtc,
                projectRoot = result.projectRoot,
                hasCoverageInput = result.hasCoverageInput,
                codeCoveragePackageInManifest = result.codeCoveragePackageInManifest,
                coverageFiles = result.coverageFiles.ToArray(),
                scripts = result.scripts.Select(ScriptAuditJson.FromRecord).ToArray()
            };
        }
    }

    [Serializable]
    private sealed class ScriptAuditJson
    {
        public string path;
        public string guid;
        public string[] typeNames;
        public string coverageStatus;
        public int guidReferenceCount;
        public int textReferenceCount;
        public string[] guidReferenceSamples;
        public string[] textReferenceSamples;
        public bool isEditorScript;
        public bool isTestScript;
        public bool isThirdParty;
        public bool hasUnityEntryPoint;
        public bool hasReflectionOrDynamicLoad;
        public string riskBucket;
        public string recommendation;

        public static ScriptAuditJson FromRecord(ScriptAuditRecord record)
        {
            return new ScriptAuditJson
            {
                path = record.path,
                guid = record.guid,
                typeNames = record.typeNames.ToArray(),
                coverageStatus = record.coverageStatus,
                guidReferenceCount = record.guidReferenceCount,
                textReferenceCount = record.textReferenceCount,
                guidReferenceSamples = record.guidReferenceSamples.ToArray(),
                textReferenceSamples = record.textReferenceSamples.ToArray(),
                isEditorScript = record.isEditorScript,
                isTestScript = record.isTestScript,
                isThirdParty = record.isThirdParty,
                hasUnityEntryPoint = record.hasUnityEntryPoint,
                hasReflectionOrDynamicLoad = record.hasReflectionOrDynamicLoad,
                riskBucket = record.riskBucket,
                recommendation = record.recommendation
            };
        }
    }

    private sealed class UsageAuditResult
    {
        public string generatedAtUtc;
        public string projectRoot;
        public bool hasCoverageInput;
        public bool codeCoveragePackageInManifest;
        public List<string> coverageFiles = new List<string>();
        public List<ScriptAuditRecord> scripts = new List<ScriptAuditRecord>();
        public string markdownPath;
        public string jsonPath;
        public string latestMarkdownPath;
        public string latestJsonPath;
    }

    private sealed class ScriptAuditRecord
    {
        public string path;
        public string guid;
        public List<string> typeNames = new List<string>();
        public string coverageStatus = "coverage-missing";
        public int guidReferenceCount;
        public int textReferenceCount;
        public List<string> guidReferenceSamples = new List<string>();
        public List<string> textReferenceSamples = new List<string>();
        public bool isEditorScript;
        public bool isTestScript;
        public bool isThirdParty;
        public bool hasUnityEntryPoint;
        public bool hasReflectionOrDynamicLoad;
        public string riskBucket = "manual-review";
        public string recommendation = "Manual review.";
    }
}

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SafetyTraining;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class TelemetryDataValidator : IPreprocessBuildWithReport
{
    private static readonly Regex CanonicalLevelPattern =
        new Regex("^level-[0-9]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public int callbackOrder => -1000;

    [MenuItem("Tools/Safety Training/Validate Telemetry Data")]
    public static void ValidateFromMenu()
    {
        List<string> warnings;
        List<string> errors = Validate(out warnings);
        WriteResults(warnings, errors);
        if (errors.Count == 0)
            Debug.Log("<color=lime>[TelemetryDataValidator] Telemetri veri sözleşmesi geçerli.</color>");
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        List<string> warnings;
        List<string> errors = Validate(out warnings);
        WriteResults(warnings, errors);
        if (errors.Count > 0)
            throw new BuildFailedException(
                "Telemetri veri sözleşmesi geçersiz. Tools > Safety Training > Validate Telemetry Data çıktısını kontrol edin.");
    }

    private static List<string> Validate(out List<string> warnings)
    {
        warnings = new List<string>();
        var errors = new List<string>();
        var levelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var eventKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string[] levelGuids = AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/_Project" });
        foreach (string guid in levelGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null) continue;

            string levelId = (level.levelID ?? string.Empty).Trim();
            if (!CanonicalLevelPattern.IsMatch(levelId))
                errors.Add($"{path}: levelID kanonik değil ('{levelId}'). Beklenen biçim: level-<numara>.");
            if (!levelIds.Add(levelId))
                errors.Add($"{path}: yinelenen levelID '{levelId}'.");

            var sequenceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (level.sequences == null)
            {
                errors.Add($"{path}: sequences dizisi null.");
                continue;
            }

            foreach (SequenceData sequence in level.sequences)
            {
                if (sequence == null)
                {
                    errors.Add($"{path}: null sequence referansı.");
                    continue;
                }

                string sequenceId = (sequence.sequenceID ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(sequenceId))
                    errors.Add($"{AssetDatabase.GetAssetPath(sequence)}: sequenceID boş.");
                else if (!sequenceIds.Add(sequenceId))
                    errors.Add($"{path}: yinelenen sequenceID '{sequenceId}'.");

                var rawActionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (sequence.actions == null)
                {
                    errors.Add($"{AssetDatabase.GetAssetPath(sequence)}: actions dizisi null.");
                    continue;
                }

                for (int actionIndex = 0; actionIndex < sequence.actions.Length; actionIndex++)
                {
                    ActionData action = sequence.actions[actionIndex];
                    if (action == null)
                    {
                        errors.Add($"{AssetDatabase.GetAssetPath(sequence)}: actions[{actionIndex}] null.");
                        continue;
                    }

                    string actionId = (action.actionID ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(actionId))
                    {
                        errors.Add($"{AssetDatabase.GetAssetPath(action)}: actionID boş.");
                        continue;
                    }

                    if (!rawActionIds.Add(actionId))
                        warnings.Add($"{AssetDatabase.GetAssetPath(sequence)}: '{actionId}' birden çok kez kullanılıyor; " +
                                     "event actionKey değeri sıra numarasıyla benzersizleştirilecek.");

                    string eventKey = string.Join("/", levelId, sequenceId, (actionIndex + 1).ToString("D3"), actionId);
                    if (!eventKeys.Add(eventKey))
                        errors.Add($"Yinelenen telemetri actionKey: {eventKey}");

                    if (action.actionType == ActionType.Quiz && action.quizData == null)
                        errors.Add($"{AssetDatabase.GetAssetPath(action)}: Quiz action için quizData eksik.");
                }
            }
        }

        if (levelGuids.Length == 0)
            errors.Add("Assets/_Project altında LevelData bulunamadı.");
        return errors;
    }

    private static void WriteResults(List<string> warnings, List<string> errors)
    {
        foreach (string warning in warnings)
            Debug.LogWarning("[TelemetryDataValidator] " + warning);
        foreach (string error in errors)
            Debug.LogError("[TelemetryDataValidator] " + error);
    }
}

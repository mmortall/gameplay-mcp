// Copyright (c) 2026 Koji Hasegawa.
// This software is released under the MIT License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using GameplayMcp.Agent;
using ModelContextProtocol.Server;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace GameplayMcp.Tools
{
    /// <summary>
    /// Semantic gameplay-agent contract layered over existing Gameplay MCP tools.
    /// </summary>
    [McpServerToolType]
    public static class GameplayAgentTools
    {
        [McpServerTool(Name = "game_session", ReadOnly = false, Destructive = false)]
        [Description("Starts or reports a semantic gameplay-agent session.")]
        [Preserve]
        public static async Task<string> GameSessionAsync(
            [Description("Operation: start or status.")] string operation = "status",
            [Description("Session mode. v1 supports semantic.")] string mode = "semantic",
            CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            if (string.Equals(operation, "start", StringComparison.OrdinalIgnoreCase))
            {
                GameplayAgentRuntime.StartSession(mode);
            }
            else if (!string.Equals(operation, "status", StringComparison.OrdinalIgnoreCase))
            {
                return Error("invalid_operation", "operation must be 'start' or 'status'");
            }

            return JsonSerializer.Serialize(GameplayAgentRuntime.SessionSnapshot());
        }

        [McpServerTool(Name = "game_observe", ReadOnly = true, Destructive = false)]
        [Description("Returns current scenes and stable semantic targets.")]
        [Preserve]
        public static async Task<string> GameObserveAsync(
            [Description("Include inactive semantic targets.")] bool includeInactive = false,
            CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            GameplayAgentRuntime.EnsureSession();
            var targets = new List<Dictionary<string, object>>();
            foreach (var id in GameplayAgentRuntime.FindAutomationIds())
            {
                if (!includeInactive && !id.gameObject.activeInHierarchy) continue;
                if (string.IsNullOrWhiteSpace(id.Value)) continue;
                targets.Add(new Dictionary<string, object>
                {
                    ["automationId"] = id.Value,
                    ["name"] = id.gameObject.name,
                    ["active"] = id.gameObject.activeInHierarchy,
                    ["interactable"] = IsInteractable(id.gameObject),
                });
            }

            var scenes = new List<Dictionary<string, object>>();
            var activeScene = SceneManager.GetActiveScene();
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                scenes.Add(new Dictionary<string, object>
                {
                    ["name"] = scene.name,
                    ["active"] = scene == activeScene,
                });
            }

            return JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["session"] = GameplayAgentRuntime.SessionSnapshot(),
                ["scenes"] = scenes,
                ["targets"] = targets.OrderBy(item => item["automationId"]).ToList(),
                ["duplicateAutomationIds"] = GameplayAgentRuntime.DuplicateAutomationIds(),
            });
        }

        [McpServerTool(Name = "game_act", ReadOnly = false, Destructive = false)]
        [Description("Performs a semantic action on a stable automationId. v1 supports click.")]
        [Preserve]
        public static async Task<string> GameActAsync(
            [Description("Stable target identifier returned by game_observe.")] string automationId,
            [Description("Semantic action. v1 supports click.")] string action = "click",
            McpConfig config = null,
            CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            GameplayAgentRuntime.EnsureSession();
            if (!string.Equals(action, "click", StringComparison.OrdinalIgnoreCase))
            {
                return Error("unsupported_action", "v1 supports only click");
            }

            var target = GameplayAgentRuntime.FindAutomationId(automationId);
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return Error("target_not_found", $"No active target found for automationId '{automationId}'.");
            }

            try
            {
                var path = BuildPath(target.transform);
                var result = await InvokeActionTool.InvokeActionAsync(
                    "UguiClickOperator", path: path, config: config, cancellationToken: cancellationToken);
                var succeeded = result.StartsWith("Invoked '", StringComparison.Ordinal);
                GameplayAgentRuntime.Record(succeeded ? "action.click" : "action.error",
                    $"{automationId}: {result}");
                if (!succeeded)
                    return Error("action_failed", result);
                return JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["ok"] = true,
                    ["automationId"] = automationId,
                    ["action"] = "click",
                    ["result"] = result,
                });
            }
            catch (Exception exception)
            {
                GameplayAgentRuntime.Record("action.error", $"{automationId}: {exception.Message}");
                return Error("action_failed", exception.ToString());
            }
        }

        [McpServerTool(Name = "game_reset", ReadOnly = false, Destructive = true)]
        [Description("Resets game state through configured provider or reloads active scene.")]
        [Preserve]
        public static async Task<string> GameResetAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            try
            {
                var result = await GameplayAgentRuntime.ResetAsync(cancellationToken);
                return JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["ok"] = true,
                    ["result"] = result,
                    ["session"] = GameplayAgentRuntime.SessionSnapshot(),
                });
            }
            catch (Exception exception)
            {
                return Error("reset_failed", exception.ToString());
            }
        }

        [McpServerTool(Name = "game_diagnostics", ReadOnly = true, Destructive = false)]
        [Description("Returns session, semantic-target, scene, and trace diagnostics.")]
        [Preserve]
        public static async Task<string> GameDiagnosticsAsync(CancellationToken cancellationToken = default)
        {
            await UniTask.SwitchToMainThread(cancellationToken);
            GameplayAgentRuntime.EnsureSession();
            return JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["session"] = GameplayAgentRuntime.SessionSnapshot(),
                ["sceneCount"] = SceneManager.sceneCount,
                ["automationTargetCount"] = GameplayAgentRuntime.FindAutomationIds().Count,
                ["duplicateAutomationIds"] = GameplayAgentRuntime.DuplicateAutomationIds(),
                ["trace"] = GameplayAgentRuntime.GetTrace(),
                ["isEditor"] = Application.isEditor,
                ["isDevelopmentBuild"] = Debug.isDebugBuild,
            });
        }

        private static bool IsInteractable(GameObject target)
        {
            var button = target.GetComponent<UnityEngine.UI.Button>();
            return button == null || (button.isActiveAndEnabled && button.interactable);
        }

        private static string BuildPath(Transform target)
        {
            var names = new Stack<string>();
            for (var current = target; current != null; current = current.parent)
                names.Push(current.name);
            return "/" + string.Join("/", names);
        }

        private static string Error(string code, string message)
        {
            return JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["ok"] = false,
                ["error"] = new Dictionary<string, object>
                {
                    ["code"] = code,
                    ["message"] = message,
                },
            });
        }
    }
}

// Copyright (c) 2026 Koji Hasegawa.
// This software is released under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameplayMcp.Agent
{
    /// <summary>
    /// Shared runtime state for semantic gameplay-agent tools.
    /// </summary>
    public static class GameplayAgentRuntime
    {
        public const string DefaultMode = "semantic";

        private static readonly object Sync = new object();
        private static readonly TraceRecorder Trace = new TraceRecorder(128);
        private static string _sessionId;
        private static string _mode = DefaultMode;
        private static int _resetCount;
        private static DateTime _startedUtc;

        /// <summary>
        /// Optional game-specific reset implementation. Default reloads active scene.
        /// </summary>
        public static Func<CancellationToken, UniTask<string>> ResetHandler { get; set; }

        public static string SessionId
        {
            get
            {
                lock (Sync)
                {
                    return _sessionId;
                }
            }
        }

        public static string Mode
        {
            get
            {
                lock (Sync)
                {
                    return _mode;
                }
            }
        }

        public static string StartSession(string mode)
        {
            lock (Sync)
            {
                _sessionId = Guid.NewGuid().ToString("N");
                _mode = string.IsNullOrWhiteSpace(mode) ? DefaultMode : mode.Trim().ToLowerInvariant();
                _startedUtc = DateTime.UtcNow;
                _resetCount = 0;
                Trace.Clear();
                Trace.Record("session.start", _mode);
                return _sessionId;
            }
        }

        public static void EnsureSession()
        {
            lock (Sync)
            {
                if (string.IsNullOrEmpty(_sessionId))
                {
                    StartSession(DefaultMode);
                }
            }
        }

        public static void Record(string kind, string detail)
        {
            EnsureSession();
            Trace.Record(kind, detail);
        }

        public static IReadOnlyList<TraceEntry> GetTrace() => Trace.Snapshot();

        public static Dictionary<string, object> SessionSnapshot()
        {
            EnsureSession();
            lock (Sync)
            {
                return new Dictionary<string, object>
                {
                    ["id"] = _sessionId,
                    ["mode"] = _mode,
                    ["startedUtc"] = _startedUtc.ToString("O"),
                    ["resetCount"] = _resetCount,
                    ["scene"] = SceneManager.GetActiveScene().name,
                };
            }
        }

        public static async UniTask<string> ResetAsync(CancellationToken cancellationToken)
        {
            EnsureSession();
            var handler = ResetHandler;
            string result;
            if (handler != null)
            {
                result = await handler(cancellationToken);
            }
            else
            {
                var scene = SceneManager.GetActiveScene();
                if (!scene.IsValid() || scene.buildIndex < 0)
                {
                    throw new InvalidOperationException("Active scene is not reloadable.");
                }

                var sceneName = scene.name;
                var operation = SceneManager.LoadSceneAsync(scene.buildIndex, LoadSceneMode.Single);
                if (operation == null)
                {
                    throw new InvalidOperationException($"Could not reload scene '{scene.name}'.");
                }

                await UniTask.WaitUntil(() => operation.isDone, cancellationToken: cancellationToken);
                result = $"Reloaded scene '{sceneName}'.";
            }

            lock (Sync)
            {
                _resetCount++;
            }

            Record("session.reset", result);
            return result;
        }

        public static IReadOnlyList<AutomationId> FindAutomationIds()
        {
            return UnityEngine.Object.FindObjectsByType<AutomationId>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        public static AutomationId FindAutomationId(string automationId)
        {
            if (string.IsNullOrWhiteSpace(automationId)) return null;
            return FindAutomationIds().FirstOrDefault(item =>
                string.Equals(item.Value, automationId.Trim(), StringComparison.Ordinal));
        }

        public static List<string> DuplicateAutomationIds()
        {
            return FindAutomationIds()
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .GroupBy(item => item.Value, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }
    }

    /// <summary>
    /// One bounded diagnostic event retained for the current session.
    /// </summary>
    public sealed class TraceEntry
    {
        public string Kind { get; }
        public string Detail { get; }
        public string TimestampUtc { get; }

        public TraceEntry(string kind, string detail)
        {
            Kind = kind;
            Detail = detail;
            TimestampUtc = DateTime.UtcNow.ToString("O");
        }
    }

    internal sealed class TraceRecorder
    {
        private readonly int _capacity;
        private readonly Queue<TraceEntry> _entries = new Queue<TraceEntry>();

        public TraceRecorder(int capacity)
        {
            _capacity = Math.Max(1, capacity);
        }

        public void Record(string kind, string detail)
        {
            lock (_entries)
            {
                while (_entries.Count >= _capacity) _entries.Dequeue();
                _entries.Enqueue(new TraceEntry(kind, detail));
            }
        }

        public List<TraceEntry> Snapshot()
        {
            lock (_entries)
            {
                return _entries.ToList();
            }
        }

        public void Clear()
        {
            lock (_entries)
            {
                _entries.Clear();
            }
        }
    }
}

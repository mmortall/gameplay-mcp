// Copyright (c) 2026 Koji Hasegawa.
// This software is released under the MIT License.

using UnityEngine;

namespace GameplayMcp.Agent
{
    /// <summary>
    /// Stable semantic identifier for a GameObject exposed to gameplay agents.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutomationId : MonoBehaviour
    {
        [SerializeField] private string value;

        /// <summary>
        /// Stable identifier used by game_observe and game_act.
        /// </summary>
        public string Value => value;

        /// <summary>
        /// Assigns identifier at runtime for adapters that bind generated UI.
        /// </summary>
        public void SetValue(string automationId)
        {
            value = automationId?.Trim();
        }
    }
}

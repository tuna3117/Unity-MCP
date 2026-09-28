using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Entry point for the project-specific AI tool layer. Every tool is a static method
    /// exposed under the "AI Tools/" menu so it can be triggered via MCP execute_menu_item
    /// or batch mode -executeMethod.
    /// </summary>
    public static class AIToolsMenu
    {
        [MenuItem("AI Tools/Ping")]
        public static void Ping()
        {
            Debug.Log("[AI Tools] Ping OK - Project.Editor assembly compiled and menu items are live.");
        }
    }
}

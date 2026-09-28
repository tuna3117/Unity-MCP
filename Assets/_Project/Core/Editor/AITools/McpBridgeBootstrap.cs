using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Makes sure the MCP for Unity local HTTP server and the editor-side bridge are running
    /// after every editor launch / domain reload, so an MCP client (Claude Code) can always
    /// reach the editor at http://localhost:8080/mcp without anyone clicking in the MCP window.
    /// </summary>
    [InitializeOnLoad]
    public static class McpBridgeBootstrap
    {
        private const string Prefix = "[AI Tools/MCP] ";
        private const string AutoStartKey = "MCPForUnity.AutoStartOnLoad";
        private const string UseHttpKey = "MCPForUnity.UseHttpTransport";
        private const double ServerWaitSeconds = 180;
        private const double IdleWaitSeconds = 120;

        static McpBridgeBootstrap()
        {
            if (Application.isBatchMode) return;

            // Keep the package's own auto-start enabled for future editor launches.
            if (!EditorPrefs.GetBool(AutoStartKey, false)) EditorPrefs.SetBool(AutoStartKey, true);
            if (!EditorPrefs.GetBool(UseHttpKey, true)) EditorPrefs.SetBool(UseHttpKey, true);

            Debug.Log(Prefix + "bootstrap loaded; scheduling bridge check.");
            EditorApplication.delayCall += () => _ = EnsureAsync(verbose: true);
        }

        [MenuItem("AI Tools/MCP/Ensure Bridge Running")]
        public static void EnsureMenu() => _ = EnsureAsync(verbose: true);

        [MenuItem("AI Tools/MCP/Log Status")]
        public static void LogStatus()
        {
            try
            {
                Debug.Log(Prefix + Status());
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Prefix + "status failed: " + ex);
            }
        }

        private static string Status()
        {
            var server = MCPServiceLocator.Server;
            var tm = MCPServiceLocator.TransportManager;
            return $"AutoStartOnLoad={EditorPrefs.GetBool(AutoStartKey, false)} " +
                   $"UseHttpTransport={EditorPrefs.GetBool(UseHttpKey, true)} " +
                   $"serverRunning={server.IsLocalHttpServerRunning()} " +
                   $"serverReachable={server.IsLocalHttpServerReachable()} " +
                   $"httpBridgeConnected={tm.IsRunning(TransportMode.Http)} " +
                   $"serverLog={server.GetLocalHttpServerLaunchLogPath()}";
        }

        private static async Task EnsureAsync(bool verbose)
        {
            try
            {
                double idleStart = EditorApplication.timeSinceStartup;
                while (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    if (EditorApplication.timeSinceStartup - idleStart > IdleWaitSeconds)
                    {
                        Debug.LogWarning(Prefix + "editor still busy after idle wait; giving up for this reload.");
                        return;
                    }
                    await Task.Delay(500);
                }

                Debug.Log(Prefix + "checking: " + Status());

                var server = MCPServiceLocator.Server;
                var tm = MCPServiceLocator.TransportManager;

                if (!server.IsLocalHttpServerReachable())
                {
                    Debug.Log(Prefix + "starting local MCP HTTP server...");
                    if (!server.StartLocalHttpServer(quiet: true))
                    {
                        Debug.LogWarning(Prefix + "StartLocalHttpServer returned false.");
                        server.LogLocalHttpServerLaunchFailure();
                        return;
                    }
                }

                double start = EditorApplication.timeSinceStartup;
                while (!server.IsLocalHttpServerReachable())
                {
                    if (EditorApplication.timeSinceStartup - start > ServerWaitSeconds)
                    {
                        Debug.LogWarning(Prefix + $"server not reachable after {ServerWaitSeconds}s. Log: {server.GetLocalHttpServerLaunchLogPath()}");
                        return;
                    }
                    await Task.Delay(1000);
                }
                Debug.Log(Prefix + "server reachable.");

                if (tm.IsRunning(TransportMode.Http))
                {
                    if (verbose) Debug.Log(Prefix + "bridge already connected.");
                    return;
                }

                bool ok = await MCPServiceLocator.Bridge.StartAsync();
                if (ok) Debug.Log(Prefix + "bridge connected. " + Status());
                else Debug.LogWarning(Prefix + "bridge failed to connect. " + Status());
            }
            catch (Exception ex)
            {
                Debug.LogWarning(Prefix + "ensure failed: " + ex);
            }
        }
    }
}

using System;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>
    /// Keeps the editor working while it sits behind other apps, so MCP work (refresh, compile, tests, recordings)
    /// never needs Unity brought to the front (that `open -a` kept pulling Tuna off YouTube, 2026-10-02).
    /// Play Mode froze completely in the background (0 frames in 4 s, Player Settings "Run In Background" off);
    /// every Play Mode session now runs in the background, editor only, Player Settings untouched. In the background
    /// the editor ticks only 5-7 times a second (an internal 0.2 s timer: waking the main run loop or posting NSEvents
    /// from another thread at 60 Hz changed nothing), so frame-by-frame recordings are slower there, but they finish.
    /// Under macOS App Nap it also stalled (1.3 s without a tick; with App Nap off the longest gap in 11 s was
    /// 0.2 s). App Nap is turned off with a user-initiated
    /// NSProcessInfo activity that still lets the Mac sleep: one per editor session (SessionState survives domain
    /// reloads), it ends when the editor quits.
    /// </summary>
    [InitializeOnLoad]
    public static class KeepEditorAwake
    {
        private const string Prefix = "[AI Tools/Awake] ";
        private const string TokenKey = "Project.KeepEditorAwake.Token";
        // NSActivityUserInitiatedAllowingIdleSystemSleep = NSActivityUserInitiated & ~NSActivityIdleSystemSleepDisabled
        private const ulong UserInitiatedAllowingIdleSleep = 0x00FFFFFFUL & ~(1UL << 20);

        static KeepEditorAwake()
        {
            if (Application.isBatchMode) return;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlaying) Application.runInBackground = true;
            Begin();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode) Application.runInBackground = true;
        }

        public static bool IsActive => SessionState.GetString(TokenKey, "") != "";

        [MenuItem("AI Tools/MCP/Log Awake Status")]
        public static void LogStatus() =>
            Debug.Log(Prefix + (IsActive ? "App Nap is off for this editor session." : "no activity; App Nap may throttle the editor in the background.") +
                      " Play Mode runs in the background: " + (!EditorApplication.isPlaying || Application.runInBackground));

        private static void Begin()
        {
#if UNITY_EDITOR_OSX
            if (IsActive) return;
            try
            {
                IntPtr info = Send(objc_getClass("NSProcessInfo"), sel_registerName("processInfo"));
                IntPtr reason = SendString(Send(objc_getClass("NSString"), sel_registerName("alloc")),
                    sel_registerName("initWithUTF8String:"), "Unity MCP: keep working in the background");
                IntPtr token = SendActivity(info, sel_registerName("beginActivityWithOptions:reason:"), UserInitiatedAllowingIdleSleep, reason);
                if (token == IntPtr.Zero)
                {
                    Debug.LogWarning(Prefix + "beginActivity returned nil.");
                    return;
                }
                Send(token, sel_registerName("retain"));   // the token is autoreleased; the activity lives as long as it does
                SessionState.SetString(TokenKey, token.ToInt64().ToString());
                Debug.Log(Prefix + "App Nap off for this editor session; Unity keeps working behind other apps.");
            }
            catch (Exception e)
            {
                Debug.LogWarning(Prefix + "could not turn App Nap off: " + e.Message);
            }
#endif
        }

#if UNITY_EDITOR_OSX
        private const string ObjC = "/usr/lib/libobjc.dylib";
        [DllImport(ObjC)] private static extern IntPtr objc_getClass(string name);
        [DllImport(ObjC)] private static extern IntPtr sel_registerName(string name);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr Send(IntPtr receiver, IntPtr selector);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendString(IntPtr receiver, IntPtr selector, string utf8);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] private static extern IntPtr SendActivity(IntPtr receiver, IntPtr selector, ulong options, IntPtr reason);
#endif
    }
}

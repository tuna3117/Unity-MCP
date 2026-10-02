using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Project.Editor.AITools;
using UnityEngine;

namespace Project.Tests
{
    /// <summary>
    /// Unity must never be brought to the front: Tuna works on the same Mac and every activation pulled them off what they
    /// were doing (2026-10-02). These guard the three ways it happened: our scripts' `open -a`, the MCP server's focus nudge
    /// (it fronted Unity Hub), and Play Mode freezing in the background, which is why focus was wanted in the first place.
    /// </summary>
    public class BackgroundWorkTests
    {
        private static string Root => Path.GetDirectoryName(Application.dataPath);

        [Test]
        public void TestJobs_ReportTheEditorFocused_SoTheServerNeverNudgesFocus()
        {
            string src = File.ReadAllText(Path.Combine(Root, "Packages/com.coplaydev.unity-mcp/Editor/Services/TestJobManager.cs"));
            StringAssert.Contains("editor_is_focused = true,", src, "the focus-nudge patch was lost (package update?)");
            StringAssert.DoesNotContain("editor_is_focused = InternalEditorUtility.isApplicationActive", src);
        }

        [Test]
        public void ToolScripts_NeverBringUnityToTheFront_ExceptTheAnnouncedCostStage()
        {
            var activate = new Regex(@"\bopen\s+-(?![a-z]*g)[a-z]*a\b|osascript|tell application");
            var hits = Directory.GetFiles(Path.Combine(Root, "Tools"), "*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".sh") || f.EndsWith(".py") || f.EndsWith(".mjs"))
                .Where(f => !f.Contains("node_modules") && !f.Contains("/out/"))
                .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
                .Where(x => activate.IsMatch(x.line) && !x.line.TrimStart().StartsWith("#"))
                .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}: {x.line.Trim()}")
                .ToList();
            Assert.AreEqual(1, hits.Count, string.Join("\n", hits));
            StringAssert.StartsWith("record_specials.sh:", hits[0]);
        }

        [Test]
        public void EditorSession_HasAppNapTurnedOff()
        {
            if (Application.platform != RuntimePlatform.OSXEditor || Application.isBatchMode) Assert.Ignore("macOS editor only");
            Assert.IsTrue(KeepEditorAwake.IsActive);
        }
    }
}

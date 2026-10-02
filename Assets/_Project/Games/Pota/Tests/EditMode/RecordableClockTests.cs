using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Project.Tests
{
    /// <summary>
    /// ai_colorhunt_record steps the paused editor: game time and unscaled time follow the steps, the wall clock does not. Color Hunt's
    /// code — the special looks included — must time itself with deltaTime / unscaledDeltaTime / unscaledTime only (Task 12).
    /// </summary>
    public class RecordableClockTests
    {
        private static readonly string[] WallClock = { "realtimeSinceStartup", "DateTime.Now", "DateTime.UtcNow", "Stopwatch", "Environment.TickCount" };

        [Test]
        public void ColorHuntCode_NeverReadsTheWallClock()
        {
            var hits = new List<string>();
            foreach (var path in Directory.GetFiles("Assets/_Project/Games/Pota/Scripts/ColorHunt", "*.cs", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                    foreach (var token in WallClock)
                        if (lines[i].Contains(token)) hits.Add($"{path}:{i + 1}: {token}");
            }
            Assert.IsEmpty(hits, "the frame recorder cannot step these:\n" + string.Join("\n", hits));
        }
    }
}

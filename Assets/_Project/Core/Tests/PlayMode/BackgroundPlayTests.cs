using NUnit.Framework;
using UnityEngine;

namespace Project.Tests
{
    public class BackgroundPlayTests
    {
        /// <summary>
        /// Without this, Play Mode froze while Unity sat behind other apps (0 frames in 4 s) and tools brought Unity to the front
        /// to unstick it. KeepEditorAwake turns it on for every Play Mode session in the editor.
        /// </summary>
        [Test]
        public void PlayMode_RunsInTheBackground()
        {
            if (!Application.isEditor) Assert.Ignore("editor only");
            Assert.IsTrue(Application.runInBackground);
        }
    }
}

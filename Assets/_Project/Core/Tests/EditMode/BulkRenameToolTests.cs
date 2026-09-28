using NUnit.Framework;
using Project.Editor.AITools;

namespace Project.Tests
{
    public class BulkRenameToolTests
    {
        [Test]
        public void Expand_ReplacesNameToken()
        {
            Assert.AreEqual("Cube_Old", BulkRenameTool.Expand("{name}_Old", "Cube", 1));
        }

        [Test]
        public void Expand_ReplacesIndexToken()
        {
            Assert.AreEqual("Enemy_7", BulkRenameTool.Expand("Enemy_{i}", "x", 7));
        }

        [Test]
        public void Expand_PadsIndexToken()
        {
            Assert.AreEqual("Enemy_007", BulkRenameTool.Expand("Enemy_{i:000}", "x", 7));
        }

        [Test]
        public void Expand_LeavesPlainPatternUntouched()
        {
            Assert.AreEqual("Plain", BulkRenameTool.Expand("Plain", "x", 3));
        }
    }
}

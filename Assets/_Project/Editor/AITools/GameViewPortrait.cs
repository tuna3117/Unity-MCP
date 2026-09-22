using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Project.Editor.AITools
{
    /// <summary>Switches the Game view to a fixed phone-portrait resolution (reflection over UnityEditor.GameViewSizes).</summary>
    public static class GameViewPortrait
    {
        [MenuItem("AI Tools/Color Hunt/Game View 1080x1920")]
        public static void Menu() => AIToolsCommon.Log(Apply(1080, 1920));

        public static string Apply(int width, int height)
        {
            string step = "start";
            try
            {
                const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
                var asm = typeof(UnityEditor.Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                step = "instance";
                var instance = sizesType.BaseType.GetProperty("instance", Any).GetValue(null);
                step = "currentGroup";
                var group = sizesType.GetProperty("currentGroup", Any).GetValue(instance);
                var gType = group.GetType();
                step = "count";
                int total = (int)gType.GetMethod("GetTotalCount", Any).Invoke(group, null);
                string label = $"Phone {width}x{height}";
                int index = -1;
                for (int i = 0; i < total; i++)
                {
                    var size = gType.GetMethod("GetGameViewSize", Any).Invoke(group, new object[] { i });
                    var text = (string)size.GetType().GetProperty("baseText", Any).GetValue(size);
                    if (text == label) { index = i; break; }
                }
                if (index < 0)
                {
                    step = "add";
                    var sizeType = asm.GetType("UnityEditor.GameViewSize");
                    var kind = asm.GetType("UnityEditor.GameViewSizeType");
                    var ctor = sizeType.GetConstructor(new[] { kind, typeof(int), typeof(int), typeof(string) });
                    var newSize = ctor.Invoke(new object[] { Enum.Parse(kind, "FixedResolution"), width, height, label });
                    gType.GetMethod("AddCustomSize", Any).Invoke(group, new[] { newSize });
                    index = (int)gType.GetMethod("GetTotalCount", Any).Invoke(group, null) - 1;
                }
                step = "window";
                var gameViewType = asm.GetType("UnityEditor.GameView");
                var window = EditorWindow.GetWindow(gameViewType);
                step = "select";
                var prop = gameViewType.GetProperty("selectedSizeIndex", Any);
                if (prop != null && prop.CanWrite) prop.SetValue(window, index);
                else gameViewType.GetMethod("SizeSelectionCallback", Any).Invoke(window, new object[] { index, null });
                window.Repaint();
                return $"game view set to {label} (index {index})";
            }
            catch (Exception e)
            {
                return $"game view size not changed at '{step}': {e.GetType().Name} {e.Message}";
            }
        }
    }
}

using System;
using System.Collections;
using NUnit.Framework;
using Project.ColorHunt;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Project.Tests
{
    /// <summary>
    /// PotaFlow switches the board input off and on per phase. A finger that was down when the input went off and lifted
    /// while it was off must not turn into a tap when the input comes back (it broke a block on the next board).
    /// </summary>
    public class ColorHuntInputPlayTests
    {
        private static IEnumerator WaitUntil(Func<bool> done, float timeout)
        {
            float t = 0f;
            while (!done() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
        }

        [UnityTest]
        public IEnumerator InputSwitchedBackOn_DoesNotTapWithAPressItNeverSawEnd()
        {
            SceneManager.LoadScene("ColorHunt");
            yield return null;
            var game = Object.FindAnyObjectByType<ColorHuntGame>();
            var input = Object.FindAnyObjectByType<ColorHuntInput>();
            yield return WaitUntil(() => game.Current == ColorHuntGame.State.Playing && !game.Busy, 5f);
            var mouse = Mouse.current ?? InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            int cell = 0;
            while (cell < game.Board.Cells.Length && game.Board.Cells[cell].Hp > game.Board.Ammo) cell++;
            Vector2 screen = input.Cam.WorldToScreenPoint(game.CellWorld(cell));

            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left, true));
            yield return null;                                   // input sees the press over an affordable block
            input.enabled = false;                               // phase change while the finger is down
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
            yield return null;                                   // the finger lifts while the input is off
            int ammo = game.Board.Ammo;
            input.enabled = true;
            yield return null;
            yield return null;
            Assert.AreEqual(ammo, game.Board.Ammo, "a block broke from a press the input never saw released");
            Assert.IsFalse(game.Busy);
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.ColorHunt
{
    /// <summary>Touch / mouse: press selects the block under the pointer, drag changes the selection, release taps it.</summary>
    public class ColorHuntInput : MonoBehaviour
    {
        public ColorHuntGame Game;
        public Camera Cam;

        private bool _down;

        // PotaFlow switches this input off between phases: forget a press that began before, or its release (seen once the
        // input is back) would tap whatever block is under the last pointer position.
        private void OnDisable()
        {
            _down = false;
            if (Game != null) Game.Select(-1);
        }

        private void Update()
        {
            if (Game == null || Cam == null) return;
            var pointer = Pointer.current;
            if (pointer == null) return;
            bool pressed = pointer.press.isPressed;
            Vector2 pos = pointer.position.ReadValue();
            if (pressed && !_down)
            {
                _down = true;
                Game.Select(CellUnder(pos));
            }
            else if (pressed && _down)
            {
                Game.Select(CellUnder(pos));
            }
            else if (!pressed && _down)
            {
                _down = false;
                int cell = CellUnder(pos);
                Game.Select(-1);
                if (cell >= 0) Game.Tap(cell);
            }
        }

        private int CellUnder(Vector2 screen)
        {
            if (Game.Current != ColorHuntGame.State.Playing || Game.Busy) return -1;
            var ray = Cam.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out var hit, 100f)) return -1;
            var view = hit.collider.GetComponent<BlockView>();
            return view != null ? view.Cell : -1;
        }
    }
}

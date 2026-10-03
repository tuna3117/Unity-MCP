using Project.ColorHunt;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Basket
{
    /// <summary>Pointer (mouse / touch) swipe → <see cref="AimMapper"/> → <see cref="BasketRound.Shoot"/>.</summary>
    public class ShotInput : MonoBehaviour
    {
        public BasketRound Round;
        public AimPreview Preview;
        public bool InputEnabled = true;

        /// <summary>F10: every drag frame — pull 0..1 (FeelCurves.PullFraction), the aim, and whether the drag maps to a shot yet.</summary>
        public event System.Action<float, AimPoint, bool> Pulled;
        /// <summary>F10: the finger left (a shot may follow at once).</summary>
        public event System.Action Released;

        private Vector2 _press;
        private bool _dragging;
        private bool _hasAim;
        private AimPoint _aim;

        private void Update()
        {
            if (Round == null || !InputEnabled) return;
            var pointer = Pointer.current;
            if (pointer == null) return;
            bool pressed = pointer.press.isPressed;
            var pos = pointer.position.ReadValue();

            if (pressed && !_dragging)
            {
                if (Round.Current != BasketRound.State.Aim) return;
                _dragging = true;
                _press = pos;
                _hasAim = false;
            }
            else if (pressed && _dragging)
            {
                var d = pos - _press; // screen y grows upward; AimMapper wants dy negative for "up"
                _hasAim = AimMapper.TryMap(d.x, -d.y, Screen.width, Screen.height, out _aim);
                if (_hasAim) Preview?.Show(_aim, Round.Settings); else Preview?.Hide();
                Pulled?.Invoke(_hasAim ? FeelCurves.PullFraction(_aim.Power) : 0f, _aim, _hasAim);
            }
            else if (!pressed && _dragging)
            {
                _dragging = false;
                Preview?.Hide();
                Released?.Invoke();
                if (_hasAim && Round.Current == BasketRound.State.Aim) Round.Shoot(_aim);
                _hasAim = false;
            }
        }

        /// <summary>Drives a pull without a pointer (recorder, tests): shows the preview and raises Pulled.</summary>
        public void SimulatePull(AimPoint aim)
        {
            if (Round == null) return;
            Preview?.Show(aim, Round.Settings);
            Pulled?.Invoke(FeelCurves.PullFraction(aim.Power), aim, true);
        }

        /// <summary>Ends a simulated pull without shooting.</summary>
        public void SimulateRelease()
        {
            Preview?.Hide();
            Released?.Invoke();
        }
    }
}

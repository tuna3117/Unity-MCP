using System.Linq;
using UnityEngine;

namespace Project.Dev
{
    /// <summary>
    /// Plays every clip of a character's Animator in turn (a looping clip twice) and names it on screen: a quick look at a
    /// studio delivery's animations together. Dev/showcase scenes only.
    /// </summary>
    public class AnimShowcase : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField, Min(0f)] private float crossFade = 0.1f;
        [Tooltip("Pause between clips (s).")]
        [SerializeField, Min(0f)] private float gap = 0.35f;

        private AnimationClip[] _clips;
        private int _index = -1;
        private float _next;

        public Animator Animator => animator;
        public string Current => _index >= 0 && _clips != null && _clips.Length > 0 ? _clips[_index].name : "";

        private void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (_clips == null) _clips = animator.runtimeAnimatorController.animationClips.Distinct().ToArray();
            if (_clips.Length == 0 || Time.time < _next) return;
            _index = (_index + 1) % _clips.Length;
            var clip = _clips[_index];
            animator.CrossFadeInFixedTime(clip.name, _index == 0 && _next == 0f ? 0f : crossFade);
            _next = Time.time + clip.length * (clip.isLooping ? 2f : 1f) + gap;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(Current)) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * 0.035f), fontStyle = FontStyle.Bold };
            style.normal.textColor = new Color(1f, 0.8f, 0.25f);
            GUI.Label(new Rect(Screen.width * 0.05f, Screen.height * 0.04f, Screen.width * 0.9f, Screen.height * 0.08f), Current, style);
        }
    }
}

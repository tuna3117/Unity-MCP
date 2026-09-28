using UnityEngine;

namespace Project.ColorHunt
{
    /// <summary>Connects the HUD to the game's events once both exist (events are not serializable).</summary>
    public class ColorHuntHudBinder : MonoBehaviour
    {
        public ColorHuntHud Hud;

        private void Awake()
        {
            var game = Hud != null ? Hud.Game : null;
            if (game == null) game = FindAnyObjectByType<ColorHuntGame>();
            if (Hud != null && game != null) Hud.Bind(game);
        }
    }
}

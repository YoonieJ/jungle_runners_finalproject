using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class GameManager
{
    public ScreenManager Screens { get; } = new();
    public AssetManager Assets { get; } = new();
    public InputManager Input { get; } = new();
    public AudioManager Audio { get; } = new();

    // Updates shared input first, then lets the active screen update.
    public void Update(GameTime gameTime)
    {
        Input.Update();
        Screens.Update(gameTime);
    }

    // Draws the current screen through the screen manager.
    public void Draw(SpriteBatch spriteBatch)
    {
        Screens.Draw(spriteBatch);
    }
}

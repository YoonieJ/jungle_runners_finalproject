using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public interface IScreen
{
    // Updates the screen's simulation and input for the current frame.
    void Update(GameTime gameTime);

    // Draws the screen through the shared sprite batch.
    void Draw(SpriteBatch spriteBatch);
}

public enum GameScreen
{
    MainMenu,
    Login,
    HowToPlay,
    StageSelect,
    Playing,
    Pause,
    GameOver
}

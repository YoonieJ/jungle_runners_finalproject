using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public interface IScreen
{
    void Update(GameTime gameTime);
    void Draw(SpriteBatch spriteBatch);
}

public enum GameScreen
{
    MainMenu,
    Login,
    StageSelect,
    Playing,
    Pause,
    GameOver
}

public enum MenuFocus
{
    UserId,
    Password,
    Options
}

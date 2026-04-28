using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace jungle_runners_finalproject;

public sealed class GameOverScreen : IScreen
{
    public string Title { get; set; } = "Game Over";
    public string Detail { get; set; } = string.Empty;
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public int StarRating { get; private set; }
    public bool NextStageUnlocked { get; private set; }

    // Updates game-over input when it owns input through ScreenManager.
    public void Update(GameTime gameTime)
    {
    }

    public void UpdateInput(KeyboardState keyboard, Func<KeyboardState, Keys, bool> isNewKeyPress, Action stageSelect, Action mainMenu)
    {
        if (isNewKeyPress(keyboard, Keys.Enter))
        {
            stageSelect();
        }

        if (isNewKeyPress(keyboard, Keys.Escape))
        {
            mainMenu();
        }
    }

    public void SetResult(string title, string detail, int score, int bestScore, int starRating, bool nextStageUnlocked)
    {
        Title = title;
        Detail = detail;
        Score = score;
        BestScore = bestScore;
        StarRating = starRating;
        NextStageUnlocked = nextStageUnlocked;
    }

    // Draws the standalone game-over screen when rendering ownership moves fully here.
    public void Draw(SpriteBatch spriteBatch)
    {
    }
}

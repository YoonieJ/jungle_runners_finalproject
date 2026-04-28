using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace jungle_runners_finalproject;

public sealed class StageSelectScreen : IScreen
{
    public int SelectedStageIndex { get; set; }
    public string IntroDialogue { get; private set; } = string.Empty;

    // Updates stage-select navigation when it owns input through ScreenManager.
    public void Update(GameTime gameTime)
    {
    }

    public void UpdateInput(
        KeyboardState keyboard,
        int stageCount,
        Func<KeyboardState, Keys, bool> isNewKeyPress,
        Action backToMenu,
        Action<int> cycleDifficulty,
        Action startRun)
    {
        if (isNewKeyPress(keyboard, Keys.Escape))
        {
            backToMenu();
            return;
        }

        if (isNewKeyPress(keyboard, Keys.Right))
        {
            SelectedStageIndex = (SelectedStageIndex + 1) % stageCount;
        }

        if (isNewKeyPress(keyboard, Keys.Left))
        {
            SelectedStageIndex = (SelectedStageIndex + stageCount - 1) % stageCount;
        }

        if (isNewKeyPress(keyboard, Keys.Up))
        {
            cycleDifficulty(1);
        }

        if (isNewKeyPress(keyboard, Keys.Down))
        {
            cycleDifficulty(-1);
        }

        if (isNewKeyPress(keyboard, Keys.Enter))
        {
            startRun();
        }
    }

    public StageProgress? GetProgress(UserProfile? user, int stageNumber)
    {
        if (user is null)
        {
            return null;
        }

        return user.StageProgress.TryGetValue(stageNumber, out StageProgress? progress) ? progress : null;
    }

    public void SetIntroDialogue(StageDefinition stage)
    {
        IntroDialogue = $"Stage {stage.Number}: {stage.Name}";
    }

    // Draws the standalone stage-select screen when rendering ownership moves fully here.
    public void Draw(SpriteBatch spriteBatch)
    {
    }
}

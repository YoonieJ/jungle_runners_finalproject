using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class StageSelectScreen : IScreen
{
    public int SelectedStageIndex { get; set; }

    // Updates future stage-select navigation and unlock display state.
    public void Update(GameTime gameTime)
    {
        // TODO: Read saved StageProgress so locked/completed stages, best scores, and route choices are visible.
        // TODO: Trigger stage intro dialogue before gameplay starts.
    }

    // Draws the future standalone stage-select screen.
    public void Draw(SpriteBatch spriteBatch)
    {
    }
}

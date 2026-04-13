using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class StageSelectScreen : IScreen
{
    public int SelectedStageIndex { get; set; }

    public void Update(GameTime gameTime)
    {
        // TODO NEXT: Read saved StageProgress here so locked/completed stages and best scores are visible.
    }

    public void Draw(SpriteBatch spriteBatch)
    {
    }
}

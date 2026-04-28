using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace jungle_runners_finalproject;

public sealed class TopViewRenderer : IWorldRenderer
{
    private const int OriginX = 100;
    private const int OriginY = 190;
    private const int Cell = 104;
    private const int TileSize = 100;
    private const int ContentSize = 52;
    private const int ContentInset = (TileSize - ContentSize) / 2;

    private readonly Texture2D _pixel;
    private readonly Texture2D _coinTexture;
    private readonly Texture2D _extraLifeTexture;
    private readonly Texture2D _shieldTexture;
    private readonly Texture2D _ropeTexture;
    private readonly Texture2D _mysteryBoxTexture;
    private readonly Texture2D _obstacleTexture;
    private readonly Texture2D _stageArrowTexture;
    private readonly Texture2D _bossArrowTexture;

    public TopViewRenderer(
        Texture2D pixel,
        Texture2D coinTexture,
        Texture2D extraLifeTexture,
        Texture2D shieldTexture,
        Texture2D ropeTexture,
        Texture2D mysteryBoxTexture,
        Texture2D obstacleTexture,
        Texture2D stageArrowTexture,
        Texture2D bossArrowTexture)
    {
        _pixel = pixel;
        _coinTexture = coinTexture;
        _extraLifeTexture = extraLifeTexture;
        _shieldTexture = shieldTexture;
        _ropeTexture = ropeTexture;
        _mysteryBoxTexture = mysteryBoxTexture;
        _obstacleTexture = obstacleTexture;
        _stageArrowTexture = stageArrowTexture;
        _bossArrowTexture = bossArrowTexture;
    }

    // Draws the world from the overhead grid perspective.
    // scrollOffset is WorldScroller.OffsetX; playerRow and playerRunFrame are supplied by Game1
    // until player state moves into its own type.
    public void Draw(SpriteBatch spriteBatch, GridWorld world, Rectangle viewport)
    {
        // Top-view does not use the IWorldRenderer viewport — it uses fixed grid constants.
        // The overload below carries the extra state needed until player is extracted.
    }

    // Full top-view draw used by Game1 while player state still lives there.
    public void Draw(
        SpriteBatch spriteBatch,
        GridWorld world,
        float scrollOffset,
        int playerRow,
        Texture2D playerRunFrame,
        SpriteFont? debugFont)
    {
        float scroll = scrollOffset * (Cell / Constants.GameplayTileSpacing);

        for (int row = 0; row < world.Rows; row++)
        {
            for (int column = 0; column < world.Columns; column++)
            {
                Tile tile = world.GetTile(column, row);
                int x = (int)(OriginX + column * Cell - scroll);
                if (x < -Cell || x > Constants.WindowWidth + Cell)
                    continue;

                int displayRow = world.Rows - 1 - row;
                int y = OriginY + displayRow * (Cell + 18);

                Color baseColor = tile.Type switch
                {
                    TileType.Branch => new Color(72, 135, 102),
                    TileType.Merge  => new Color(85, 148, 105),
                    TileType.Hazard => new Color(75, 55, 50),
                    _               => new Color(30, 94, 64)
                };

                spriteBatch.Draw(_pixel, new Rectangle(x, y, TileSize, TileSize), baseColor * 0.5f);

                if (tile.HasContent)
                {
                    Rectangle contentDestination = new(x + ContentInset, y + ContentInset, ContentSize, ContentSize);
                    Texture2D? texture = GetTileContentTexture(tile.Content);
                    if (texture is not null)
                        DrawTextureInBounds(spriteBatch, texture, contentDestination, Color.White);
                    else
                        spriteBatch.Draw(_pixel, contentDestination, GetTileColor(tile));
                }
            }
        }

        // Draw player marker.
        int playerX = (int)(OriginX + Constants.RunnerX * (Cell / Constants.GameplayTileSpacing));
        int playerDisplayRow = world.Rows - 1 - playerRow;
        int playerY = OriginY + playerDisplayRow * (Cell + 18);
        DrawTextureInBounds(spriteBatch, playerRunFrame, new Rectangle(playerX, playerY, TileSize, TileSize), Color.White);
    }

    // Looks up real sprite art for tile content that has an added asset.
    private Texture2D? GetTileContentTexture(TileContent content)
    {
        return content switch
        {
            TileContent.Coin             => _coinTexture,
            TileContent.LifeItem         => _extraLifeTexture,
            TileContent.ScoreBooster     => _mysteryBoxTexture,
            TileContent.StageItem        => _shieldTexture,
            TileContent.RopeItem         => _ropeTexture,
            TileContent.OutOfStageItem   => _mysteryBoxTexture,
            TileContent.Collectible
            or TileContent.Item          => _mysteryBoxTexture,
            TileContent.Projectile       => _stageArrowTexture,
            TileContent.HomingProjectile => _bossArrowTexture,
            TileContent.Obstacle         => _obstacleTexture,
            _                            => null
        };
    }

    // Chooses the placeholder draw color for a tile based on content first, then tile type.
    private static Color GetTileColor(Tile tile)
    {
        return tile.Content switch
        {
            TileContent.Coin             => Color.Gold,
            TileContent.LifeItem         => Color.LightPink,
            TileContent.ScoreBooster     => Color.Cyan,
            TileContent.StageItem        => Color.DeepSkyBlue,
            TileContent.RopeItem         => Color.SandyBrown,
            TileContent.OutOfStageItem   => Color.LightGreen,
            TileContent.Projectile       => Color.OrangeRed,
            TileContent.HomingProjectile => Color.Magenta,
            TileContent.Obstacle         => Color.DarkRed,
            TileContent.Boss             => Color.Purple,
            TileContent.Collectible
            or TileContent.Item          => Color.LightGreen,
            _ when tile.Type == TileType.Branch => Color.LightGreen,
            _ when tile.Type == TileType.Merge  => Color.SeaGreen,
            _                            => Color.White
        };
    }

    // Draws an asset centered inside a rectangle without squashing its pixels.
    private static void DrawTextureInBounds(SpriteBatch spriteBatch, Texture2D texture, Rectangle bounds, Color color)
    {
        float scale = MathF.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height);
        int width = Math.Max(1, (int)(texture.Width * scale));
        int height = Math.Max(1, (int)(texture.Height * scale));
        Rectangle destination = new(
            bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2,
            width,
            height);
        spriteBatch.Draw(texture, destination, color);
    }
}

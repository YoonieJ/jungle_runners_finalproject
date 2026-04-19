using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class FrontViewRenderer : IWorldRenderer
{
    private readonly RowDepthMapper _rowDepthMapper;
    private readonly Texture2D _pixel;
    private readonly Texture2D _coinTexture;
    private readonly Texture2D _extraLifeTexture;
    private readonly Texture2D _shieldTexture;
    private readonly Texture2D _mysteryBoxTexture;
    private readonly Texture2D _obstacleTexture;
    private readonly Texture2D _stageArrowTexture;
    private readonly Texture2D _bossArrowTexture;

    public FrontViewRenderer(
        RowDepthMapper rowDepthMapper,
        Texture2D pixel,
        Texture2D coinTexture,
        Texture2D extraLifeTexture,
        Texture2D shieldTexture,
        Texture2D mysteryBoxTexture,
        Texture2D obstacleTexture,
        Texture2D stageArrowTexture,
        Texture2D bossArrowTexture)
    {
        _rowDepthMapper = rowDepthMapper;
        _pixel = pixel;
        _coinTexture = coinTexture;
        _extraLifeTexture = extraLifeTexture;
        _shieldTexture = shieldTexture;
        _mysteryBoxTexture = mysteryBoxTexture;
        _obstacleTexture = obstacleTexture;
        _stageArrowTexture = stageArrowTexture;
        _bossArrowTexture = bossArrowTexture;
    }

    // Draws the world from the front runner perspective.
    public void Draw(SpriteBatch spriteBatch, GridWorld world, Rectangle viewport)
    {
        foreach (Tile tile in world.AllTiles.OrderByDescending(t => t.Row))
        {
            float x = GetTileScreenX(tile.Column, viewport);
            if (x < -100f || x > viewport.Width + 100f)
                continue;

            if (tile.HasContent || tile.Type is TileType.Branch or TileType.Merge)
                DrawFrontTile(spriteBatch, tile, x);
        }
    }

    // Draws one visible tile or tile content marker in the front-view lane space.
    private void DrawFrontTile(SpriteBatch spriteBatch, Tile tile, float x)
    {
        float scale = _rowDepthMapper.GetScale(tile.Row);
        float groundY = _rowDepthMapper.GetGroundY(tile.Row);
        Color color = GetTileColor(tile);
        int width = (int)(TileVisualWidth(tile) * scale);
        int height = (int)(TileVisualHeight(tile) * scale);
        int y = (int)(groundY - height);
        Rectangle destination = new((int)x, y, width, height);

        Texture2D? texture = GetTileContentTexture(tile.Content);
        if (texture is not null)
        {
            DrawTextureInBounds(spriteBatch, texture, destination, Color.White);
            return;
        }

        spriteBatch.Draw(_pixel, destination, color);
    }

    // Converts a stage grid column into the current screen-space x position.
    // The viewport.X encodes the SpawnScreenOffset minus WorldScroller.OffsetX supplied by Game1.
    private static float GetTileScreenX(int column, Rectangle viewport)
    {
        return viewport.X + column * Constants.GameplayTileSpacing;
    }

    // Looks up real sprite art for tile content that has an added asset.
    private Texture2D? GetTileContentTexture(TileContent content)
    {
        return content switch
        {
            TileContent.Coin            => _coinTexture,
            TileContent.LifeItem        => _extraLifeTexture,
            TileContent.ScoreBooster    => _mysteryBoxTexture,
            TileContent.StageItem       => _shieldTexture,
            TileContent.RopeItem        => _mysteryBoxTexture,
            TileContent.OutOfStageItem  => _mysteryBoxTexture,
            TileContent.Collectible
            or TileContent.Item         => _mysteryBoxTexture,
            TileContent.Projectile      => _stageArrowTexture,
            TileContent.HomingProjectile => _bossArrowTexture,
            TileContent.Obstacle        => _obstacleTexture,
            _                           => null
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

    // Returns a content-specific placeholder width for front-view tile rendering.
    private static float TileVisualWidth(Tile tile)
    {
        return tile.Content switch
        {
            TileContent.Projectile       => 144f,
            TileContent.HomingProjectile => 88f,
            TileContent.Coin             => 68f,
            TileContent.Boss             => 236f,
            _                            => 124f
        };
    }

    // Returns a content-specific placeholder height for front-view tile rendering.
    private static float TileVisualHeight(Tile tile)
    {
        return tile.Content switch
        {
            TileContent.Projectile       => 36f,
            TileContent.HomingProjectile => 88f,
            TileContent.Coin             => 68f,
            TileContent.Boss             => 240f,
            TileContent.ScoreBooster     => 88f,
            TileContent.LifeItem         => 84f,
            TileContent.StageItem        => 84f,
            TileContent.RopeItem         => 84f,
            TileContent.OutOfStageItem   => 84f,
            _                            => 144f
        };
    }

    // Draws an asset centered inside a gameplay rectangle without squashing its pixels.
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

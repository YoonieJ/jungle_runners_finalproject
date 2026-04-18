using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

public sealed class RowDepthMapper
{
    public int MinRow { get; set; }
    public int MaxRow { get; set; } = Constants.GameplayRows - 1;
    public float ClosestGroundY { get; set; } = 610f;
    public float FarthestGroundY { get; set; } = 430f;
    public float ClosestScale { get; set; } = 1.1f;
    public float FarthestScale { get; set; } = 0.90f;

    // Converts a row index into a normalized depth value from closest to farthest.
    public float GetDepth(int row)
    {
        if (MaxRow <= MinRow)
        {
            return 0f;
        }

        return (row - MinRow) / (float)(MaxRow - MinRow);
    }

    // Returns the screen-space ground y position for a row.
    public float GetGroundY(int row)
    {
        return MathHelper.Lerp(ClosestGroundY, FarthestGroundY, GetDepth(row));
    }

    // Returns the sprite scale that matches a row's depth.
    public float GetScale(int row)
    {
        return MathHelper.Lerp(ClosestScale, FarthestScale, GetDepth(row));
    }

    // Returns a layer depth value that draws closer rows on top.
    public float GetLayerDepth(int row)
    {
        return 1f - GetDepth(row);
    }
}

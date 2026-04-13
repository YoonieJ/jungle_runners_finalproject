using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

public sealed class RowDepthMapper
{
    public int MinRow { get; set; }
    public int MaxRow { get; set; } = Constants.GameplayRows - 1;
    public float ClosestGroundY { get; set; } = 610f;
    public float FarthestGroundY { get; set; } = 430f;
    public float ClosestScale { get; set; } = 1.1f;
    public float FarthestScale { get; set; } = 0.62f;

    public float GetDepth(int row)
    {
        if (MaxRow <= MinRow)
        {
            return 0f;
        }

        return (row - MinRow) / (float)(MaxRow - MinRow);
    }

    public float GetGroundY(int row)
    {
        return MathHelper.Lerp(ClosestGroundY, FarthestGroundY, GetDepth(row));
    }

    public float GetScale(int row)
    {
        return MathHelper.Lerp(ClosestScale, FarthestScale, GetDepth(row));
    }

    public float GetLayerDepth(int row)
    {
        return 1f - GetDepth(row);
    }
}

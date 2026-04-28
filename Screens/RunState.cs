using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace jungle_runners_finalproject;

public sealed class RunState
{
    public StageDefinition ActiveStage { get; set; } = null!;
    public Stage ActiveStageData { get; set; } = new();
    public List<MapSegment> Segments { get; set; } = [];
    public MapSegment ActiveSegment { get; set; } = null!;
    public float SegmentProgress { get; set; }
    public float Distance { get; set; }
    public float DistanceScore { get; set; }
    public float PlayerJumpOffset { get; set; }
    public float PlayerVelocityY { get; set; }
    public bool CanDoubleJump { get; set; }
    public bool IsDoubleJumping { get; set; }
    public float SlideTimer { get; set; }
    public float RopeTimer { get; set; }
    public Vector2 RopeSwingPivot { get; set; }
    public float RunAnimationTimer { get; set; }
    public float ScoreBoostTimer { get; set; }
    public float InvulnerableTimer { get; set; }
    public float DamageFlashTimer { get; set; }
    public float ScreenShakeTimer { get; set; }
    public int Lives { get; set; }
    public int Coins { get; set; }
    public int Boosters { get; set; }
    public int CoinScore { get; set; }
    public int StageItemShieldCharges { get; set; }
    public int RopeItemCharges { get; set; }
    public int Score { get; set; }
    public bool RunWon { get; set; }
    public int PlayerRow { get; set; } = Constants.MiddleLayer;
    public Random Random { get; set; } = new(1);
    public HashSet<string> CollectedItemsThisRun { get; } = [];
}

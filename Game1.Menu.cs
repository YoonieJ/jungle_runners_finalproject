using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace jungle_runners_finalproject;

public partial class Game1
{
    private const float GameplayTileSpacing = 150f;
    private const float SpawnScreenOffset = 620f;
    private const float FrontViewLookaheadX = 900f;
    private const float PlayerCollisionWidth = 64f;
    private const int MaxUserIdLength = 16;
    private const int BossStageNumber = 3;
    private const int BossSurvivalScoreBonus = 2200;
    private const float BossIntroDuration = 2.4f;
    private const float BossFightDuration = 34f;
    private const float BossAttackInitialDelay = 0.60f;
    private const float BossAttackMinInterval = 0.30f;
    private const float BossAttackMaxInterval = 0.72f;
    private const float BossAttackBurstSpacing = 96f;
    private const string BossName = "THE SUNKEN IDOL";
    private const float MapProjectileActivationPadding = 220f;
    private const float MapProjectileStandardSpeed = 450f;
    private const float MapProjectileHomingSpeed = 340f;
    private const float MapProjectileHomingInterval = 0.50f;
    private const float MeteorFrontRevealDistance = 340f;
    private const float MeteorDiagonalStartOffsetX = 190f;
    private const float MeteorDiagonalStartOffsetY = 280f;
    private const float LaneGroundInset = 54f;
    private const float LaneBandHeight = 96f;
    private const float PlayerHitboxInsetXRatio = 0.22f;
    private const float PlayerHitboxTopInsetRatio = 0.12f;
    private const float PlayerHitboxBottomInsetRatio = 0.08f;

    private readonly List<BossAttack> _bossAttacks = [];
    private readonly List<MapProjectile> _mapProjectiles = [];
    private readonly HashSet<string> _activatedProjectileTiles = [];
    private bool _bossEncounterStarted;
    private bool _bossIntroActive;
    private bool _bossFightActive;
    private bool _bossDefeated;
    private float _bossIntroTimer;
    private float _bossFightTimer;
    private float _bossAttackTimer;
    private int _bossSurvivalBonus;
    private Boss? _boss;
    private readonly List<BossWeakPoint> _bossWeakPoints = [];
    private float _weakPointTimer;
    private const float WeakPointMinInterval = 5.4f;
    private const float WeakPointMaxInterval = 8.0f;
    private const float WeakPointLifetime = 2.6f;
    private const float WeakPointSpeed = 470f;
    private const int WeakPointDamage = 1;

    // Loads local save data and guarantees the user dictionary is ready to use.
    private void LoadSaveFile()
    {
        _saveFile = SaveManager.LoadData<SaveFile>(_savePath, _jsonOptions);
        _saveFile.Users ??= [];
        _saveFile.LastUserId ??= string.Empty;
        NormalizeSaveFile();
    }

    // Writes the current local save data to disk.
    private void SaveSaveFile()
    {
        SaveManager.SaveData(_savePath, _saveFile, _jsonOptions);
    }

    // Guarantees loaded save data has all dictionaries/lists needed by newer save fields.
    private void NormalizeSaveFile()
    {
        foreach ((string userId, UserProfile user) in _saveFile.Users)
        {
            NormalizeUserProfile(userId, user);
        }
    }

    // Fills missing collection properties for profiles loaded from older JSON.
    private static void NormalizeUserProfile(string userId, UserProfile user)
    {
        user.UserId = string.IsNullOrWhiteSpace(user.UserId) ? userId : user.UserId.Trim().ToUpperInvariant();
        user.Settings ??= new SettingsData();
        user.StageProgress ??= [];
        user.TopScores ??= [];
        user.OwnedOutOfStageItems ??= [];
        user.Scores ??= [];

        foreach ((int stageNumber, StageProgress progress) in user.StageProgress)
        {
            progress.StageNumber = progress.StageNumber == 0 ? stageNumber : progress.StageNumber;

            if (!user.TopScores.ContainsKey(stageNumber))
            {
                user.TopScores[stageNumber] = progress.BestScore;
            }
        }
    }

    // Handles main menu navigation, user profile creation, sound toggling, and logout.
    private void UpdateMainMenu(KeyboardState keyboard, float deltaSeconds)
    {
        if (_currentUser is null)
        {
            UpdateUserIdEntry(keyboard);
            return;
        }

        // TODO: Add a full settings menu for difficulty, view mode, sound, controls, and display options.
        _mainMenuScreen.UpdateInput(keyboard, _previousKeyboard, deltaSeconds, SelectMainMenuOption);
    }

    // Executes the highlighted main-menu command.
    private void SelectMainMenuOption(int selectedIndex)
    {
        EnsureCurrentUser();

        if (selectedIndex == 0)
        {
            _screen = GameScreen.StageSelect;
        }
        else if (selectedIndex == 1)
        {
            _screen = GameScreen.HowToPlay;
        }
        else if (selectedIndex == 2)
        {
            ToggleSound();
        }
        else
        {
            _currentUser = null;
            _saveFile.LastUserId = string.Empty;
            _loginScreen.Reset();
            _mainMenuScreen.ResetSelection();
            SaveSaveFile();
        }
    }

    // Returns from the controls/help page to the main menu.
    private void UpdateHowToPlay(KeyboardState keyboard)
    {
        if (IsNewKeyPress(keyboard, Keys.Enter) || IsNewKeyPress(keyboard, Keys.Escape))
        {
            _screen = GameScreen.MainMenu;
        }
    }

    // Captures a local profile id before the player enters the main menu.
    private void UpdateUserIdEntry(KeyboardState keyboard)
    {
        _loginScreen.UpdateInput(keyboard, _previousKeyboard, MaxUserIdLength, _ => LoadCurrentUserFromTypedId());
    }

    // Handles stage-select navigation and starts the selected stage.
    private void UpdateStageSelect(KeyboardState keyboard)
    {
        _stageSelectScreen.UpdateInput(
            keyboard,
            _stages.Length,
            IsNewKeyPress,
            () =>
            {
                _screen = GameScreen.MainMenu;
                _audioManager.PlaySongForLevel(0);
            },
            CycleDifficulty,
            StartRun);
        _stageSelectScreen.SetIntroDialogue(_stages[_selectedStage]);
    }

    // Runs the active stage simulation, including movement, actions, scoring, and win checks.
    private void UpdatePlaying(KeyboardState keyboard, float deltaSeconds)
    {
        if (IsNewKeyPress(keyboard, Keys.Escape))
        {
            _screen = GameScreen.StageSelect;
            return;
        }

        _viewMode = keyboard.IsKeyDown(Keys.V) ? ViewMode.Top : ViewMode.Front;

        if (_awaitingRouteChoice)
        {
            UpdateRouteChoice(keyboard);
            return;
        }

        if (IsNewKeyPress(keyboard, Keys.M))
        {
            ToggleSound();
        }

        if (IsNewKeyPress(keyboard, Keys.Left))
        {
            _playerRow = System.Math.Max(Constants.FrontLayer, _playerRow - 1);
        }

        if (IsNewKeyPress(keyboard, Keys.Right))
        {
            _playerRow = System.Math.Min(Constants.BackLayer, _playerRow + 1);
        }

        if (IsNewKeyPress(keyboard, Keys.Space))
        {
            if (_playerJumpOffset <= 0f)
            {
                _playerVelocityY = JumpVelocity;
                _canDoubleJump = true;
                _isDoubleJumping = false;
                _slideTimer = 0f;
            }
            else if (_canDoubleJump)
            {
                _playerVelocityY = DoubleJumpVelocity;
                _canDoubleJump = false;
                _isDoubleJumping = true;
                _slideTimer = 0f;
            }
        }

        if (IsNewKeyPress(keyboard, Keys.Down) && _playerJumpOffset <= 0f)
        {
            _slideTimer = SlideDuration;
        }

        if (IsNewKeyPress(keyboard, Keys.R) && _ropeTimer <= 0f && _ropeItemCharges > 0 && !IsBossEncounterActive())
        {
            _ropeItemCharges--;
            _ropeTimer = RopeDuration;
            _ropeSwingPivot = GetCurrentRopeSwingPivot();
        }

        UpdatePlayerActionTimers(deltaSeconds);

        _worldScroller.Speed = IsBossEncounterActive() ? 0f : Constants.ScrollSpeed * (_ropeTimer > 0f ? 1.6f : 1f);
        _runAnimationTimer += deltaSeconds * (_worldScroller.Speed / Constants.ScrollSpeed);
        float previousDistance = _distance;
        _worldScroller.Update(deltaSeconds);
        _distance = _worldScroller.OffsetX;
        _distanceScore += System.Math.Max(0f, _distance - previousDistance) * GetScoreMultiplier();
        _segmentProgress += _worldScroller.Speed * deltaSeconds;
        TryStartBossEncounterFromGrid();
        UpdateBossEncounter(deltaSeconds);
        if (_screen != GameScreen.Playing)
        {
            return;
        }

        ActivateVisibleMapProjectiles();
        UpdateMapProjectiles(deltaSeconds);
        if (_screen != GameScreen.Playing)
        {
            return;
        }

        ResolveGridInteractions();
        if (_screen != GameScreen.Playing)
        {
            return;
        }

        UpdateRouteProgress();

        _score = CalculateRunScore();

        float stageEnd = SpawnScreenOffset + _activeStageData.World.Columns * GameplayTileSpacing - RunnerX;
        if (_worldScroller.OffsetX >= stageEnd && (_activeStage.Number != BossStageNumber || _bossDefeated))
        {
            _runWon = true;
            SaveStageProgress();
            ShowGameOver("Stage Clear");
        }
    }

    // Handles the game-over screen shortcuts back to stage select or the main menu.
    private void UpdateGameOver(KeyboardState keyboard)
    {
        _gameOverScreen.UpdateInput(
            keyboard,
            IsNewKeyPress,
            () => _screen = GameScreen.StageSelect,
            () => _screen = GameScreen.MainMenu);
    }

    // Draws the prototype main menu and current local user id.
    private void DrawMainMenu()
    {
        _spriteBatch.Draw(_mainMenuBackground, new Rectangle(0, 0, WindowWidth, WindowHeight), Color.White);
        _spriteBatch.DrawString(_minecraftFont, "JUNGLE RUNNERS", new Vector2(92, 82), Color.DarkOliveGreen, 0f, Vector2.Zero, 5f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_minecraftFont, "JUNGLE RUNNERS", new Vector2(90, 80), Color.Gold, 0f, Vector2.Zero, 5f, SpriteEffects.None, 0f);
        PixelFont.Draw(_spriteBatch, _pixel, _menuMessage, 100, 180, 3, Color.White);
        string userText = string.IsNullOrWhiteSpace(_typedUserId) ? "USER : " : $"USER : {_typedUserId}";
        PixelFont.Draw(_spriteBatch, _pixel, userText, 100, 220, 3, _loginScreen.IsProfileLoaded ? Color.White : Color.Gold);

        if (_currentUser is null)
        {
            return;
        }

        int completedStages = _currentUser.StageProgress.Values.Count(progress => progress.IsCompleted);
        PixelFont.Draw(_spriteBatch, _pixel, $"BEST SCORE {_currentUser.BestScore}", 100, 260, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, $"STAGES CLEARED {completedStages}", 100, 300, 3, Color.White);

        for (int i = 0; i < _mainMenuScreen.Options.Count; i++)
        {
            Color color = i == _mainMenuSelection ? Color.LimeGreen : Color.White;
            string prefix = i == _mainMenuSelection ? "> " : "  ";
            string optionText = _mainMenuScreen.GetDisplayText(i, _soundEnabled);
            PixelFont.Draw(_spriteBatch, _pixel, prefix + optionText, 120, 360 + i * 54, 4, color);
        }
    }

    // Draws the current stage card and stage-select instructions.
    private void DrawStageSelect()
    {
        StageDefinition stage = _stages[_selectedStage];
        StageProgress? progress = _stageSelectScreen.GetProgress(_currentUser, stage.Number);
        bool isUnlocked = IsStageUnlocked(stage);
        Texture2D background = isUnlocked
            ? _stageSelectBackgrounds[_selectedStage]
            : _lockedStageSelectBackgrounds[_selectedStage];
        DrawFullScreenTexture(background);
        if (!isUnlocked)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, WindowWidth, WindowHeight), Color.Black * 0.46f);
        }

        _spriteBatch.DrawString(_minecraftFont, "SELECT STAGE", new Vector2(92, 82), Color.DarkOliveGreen, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_minecraftFont, "SELECT STAGE", new Vector2(90, 80), Color.Gold, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_minecraftFont, $"STAGE {stage.Number}: {stage.Name}", new Vector2(102, 212), Color.DarkOliveGreen, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_minecraftFont, $"STAGE {stage.Number}: {stage.Name}", new Vector2(100, 210), Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
        PixelFont.Draw(_spriteBatch, _pixel, stage.Description, 100, 270, 3, Color.LightGreen);
        if (progress is null)
        {
            PixelFont.Draw(_spriteBatch, _pixel, "NOT CLEARED YET", 100, 345, 3, Color.White);
            PixelFont.Draw(_spriteBatch, _pixel, "BEST 0  STARS 0", 100, 385, 3, Color.White);
        }
        else
        {
            string clearedText = progress.IsCompleted ? "CLEARED" : "NOT CLEARED YET";
            PixelFont.Draw(_spriteBatch, _pixel, clearedText, 100, 345, 3, progress.IsCompleted ? Color.Gold : Color.White);
            PixelFont.Draw(_spriteBatch, _pixel, $"BEST {progress.BestScore}  STARS {progress.StarRating}", 100, 385, 3, Color.White);
        }
        PixelFont.Draw(_spriteBatch, _pixel, isUnlocked ? "STATUS UNLOCKED" : "STATUS LOCKED", 100, 430, 3, isUnlocked ? Color.LightGreen : Color.OrangeRed);
        if (!string.IsNullOrWhiteSpace(_stageSelectScreen.IntroDialogue))
        {
            PixelFont.Draw(_spriteBatch, _pixel, _stageSelectScreen.IntroDialogue, 100, 470, 2, Color.White);
        }

        if (!isUnlocked)
        {
            PixelFont.Draw(_spriteBatch, _pixel, "STAGE LOCKED", 455, 505, 5, Color.OrangeRed);
            PixelFont.Draw(_spriteBatch, _pixel, $"CLEAR STAGE {stage.Number - 1} TO UNLOCK", 456, 565, 2, Color.White);
        }

        string startText = isUnlocked ? "LEFT/RIGHT CHOOSE  ENTER START  ESC MENU" : "LEFT/RIGHT CHOOSE  ESC MENU";
        PixelFont.Draw(_spriteBatch, _pixel, startText, 100, 610, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, $"UP/DOWN DIFFICULTY  {_selectedDifficulty.ToString().ToUpperInvariant()}", 100, 650, 3, Color.Gold);
    }

    // Draws controls, item rules, and survival tips from the main menu.
    private void DrawHowToPlay()
    {
        _spriteBatch.Draw(_mainMenuBackground, new Rectangle(0, 0, WindowWidth, WindowHeight), Color.White);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, WindowWidth, WindowHeight), Color.Black * 0.45f);

        PixelFont.Draw(_spriteBatch, _pixel, "HOW TO PLAY", 95, 70, 7, Color.Gold);
        PixelFont.Draw(_spriteBatch, _pixel, "RUN THROUGH THE JUNGLE AND CLEAR EACH STAGE", 100, 155, 3, Color.White);

        PixelFont.Draw(_spriteBatch, _pixel, "CONTROLS", 100, 225, 4, Color.LightGreen);
        PixelFont.Draw(_spriteBatch, _pixel, "LEFT/RIGHT MOVE ROWS", 130, 285, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "SPACE JUMP AND DOUBLE JUMP", 130, 325, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "DOWN SLIDE UNDER ARROWS", 130, 365, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "HOLD V TOP VIEW", 130, 405, 3, Color.White);

        PixelFont.Draw(_spriteBatch, _pixel, "ITEMS", 610, 225, 4, Color.LightGreen);
        PixelFont.Draw(_spriteBatch, _pixel, "COINS ADD SCORE", 640, 285, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "SHIELDS BLOCK ONE HIT", 640, 325, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ROPE ITEM LETS YOU USE R ONCE", 640, 365, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ROPE IS USED UP AFTER ACTIVATING", 640, 405, 3, Color.White);

        PixelFont.Draw(_spriteBatch, _pixel, "WATCH FOR METEOR TARGETS IN TOP VIEW", 100, 500, 3, Color.Orange);
        PixelFont.Draw(_spriteBatch, _pixel, "STAGE 3 HAS A BOSS: SLIDE SPEARS AND JUMP BOULDERS", 100, 545, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ENTER OR ESC BACK", 100, 630, 3, Color.Gold);
    }

    // Draws the side-view run by walking rows from back to front so closer lanes layer on top.
    private void DrawFrontView()
    {
        // TODO: Add barricade sprites, polish rope feedback, and add more run frames.
        DrawGameplayBackground();
        DrawFrontLanes();

        for (int row = Constants.BackLayer; row >= Constants.FrontLayer; row--)
        {
            foreach (Tile tile in _activeStageData.World.AllTiles.Where(tile => tile.Row == row))
            {
                float x = GetTileScreenX(tile.Column);
                if (x < -100f || x > FrontViewLookaheadX)
                {
                    continue;
                }

                if (tile.HasContent)
                {
                    DrawFrontTile(tile, x);
                }
            }

            DrawMapProjectilesFront(row);

            if (_playerRow == row)
            {
                DrawPlayerFront();
            }
        }
    }

    // Draws the runner in its current lane layer.
    private void DrawPlayerFront()
    {
        Color playerColor = GetPlayerDamageBlinkColor();
        PlayerAnimationFrame frame = GetPlayerAnimationFrame();
        if (frame.IsRope)
        {
            DrawPlayerSwingFront(frame, playerColor);
            return;
        }

        Rectangle bounds = GetPlayerFrontBounds(frame);
        if (frame.PreserveDesignedSize)
        {
            _spriteBatch.Draw(frame.Texture, bounds, frame.Source, playerColor);
            return;
        }

        DrawTextureInBounds(frame.Texture, frame.Source, bounds, playerColor);
    }

    // Draws the overhead grid view of the same stage data.
    private void DrawTopView()
    {
        DrawGameplayBackground();
        DrawTopGrid();
        DrawMapProjectilesTop();
        PixelFont.Draw(_spriteBatch, _pixel, "TOP VIEW SAME GRID", 60, 620, 4, Color.White);
    }

    // Draws the score, lives, counters, input hints, and route-choice prompt.
    private void DrawHud()
    {
        PixelFont.Draw(_spriteBatch, _pixel, $"SCORE {_score}", 865, 28, 4, Color.White);
        DrawHudLifeMeter(970, 72);
        PixelFont.Draw(_spriteBatch, _pixel, $"COINS {_coins}", 970, 116, 3, Color.White);
        DrawHudRopeMeter(970, 148);
        PixelFont.Draw(_spriteBatch, _pixel, $"SHIELDS {_stageItemShieldCharges}", 970, 190, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, !_audioManager.IsMuted ? "SOUND ON" : "SOUND OFF", 970, 224, 3, Color.White);
        DrawHudBoostIndicator(970, 258);

        PixelFont.Draw(_spriteBatch, _pixel, "SPACE JUMP  DOWN SLIDE  R ROPE", 36, 28, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "HOLD V TOP VIEW  M SOUND  LEFT/RIGHT ROW", 36, 62, 3, Color.White);

        if (_awaitingRouteChoice && _activeStageData.CurrentNode is not null)
        {
            StageNode route = _activeStageData.CurrentNode.Next[_routeChoiceIndex];
            PixelFont.Draw(_spriteBatch, _pixel, "ROUTE CHOICE", 420, 330, 5, Color.Gold);
            PixelFont.Draw(_spriteBatch, _pixel, $"LEFT/RIGHT PICK  ENTER {route.Label}", 270, 405, 3, Color.White);
            DrawRouteChoiceLabels(270, 455, 3);
        }
    }

    // Draws one heart slot per life available at the selected difficulty.
    private void DrawHudLifeMeter(int x, int y)
    {
        DrawHudIconRow(
            _extraLifeTexture,
            GetMaxLivesForCurrentRun(),
            _lives,
            x,
            y,
            34,
            8,
            Color.White,
            new Color(85, 85, 85));
    }

    // Draws two rope slots and tints inactive ones gray.
    private void DrawHudRopeMeter(int x, int y)
    {
        DrawHudIconRow(
            _ropeTexture,
            MaxRopeCharges,
            _ropeItemCharges,
            x,
            y,
            34,
            8,
            new Color(214, 171, 111),
            new Color(85, 85, 85),
            "R");
    }

    // Draws the score boost as an icon that brightens while the effect is active.
    private void DrawHudBoostIndicator(int x, int y)
    {
        bool boostActive = _scoreBoostTimer > 0f;
        Rectangle iconBounds = new(x, y, 34, 34);
        Color panelColor = boostActive ? new Color(80, 57, 12) : new Color(32, 32, 32);
        Color borderColor = boostActive ? Color.Gold : new Color(96, 96, 96);
        Color iconColor = boostActive ? Color.White : new Color(95, 95, 95);

        _spriteBatch.Draw(_pixel, iconBounds.InflateBy(2), Color.Black * 0.42f);
        _spriteBatch.Draw(_pixel, iconBounds, panelColor * 0.95f);
        DrawRectangleOutline(iconBounds, 2, borderColor);
        DrawTextureInBounds(_scoreBoosterTexture, iconBounds.InflateBy(-4), iconColor);
        PixelFont.Draw(_spriteBatch, _pixel, "X10", x + 48, y + 4, 2, boostActive ? Color.Gold : new Color(148, 148, 148));

        if (boostActive)
        {
            PixelFont.Draw(_spriteBatch, _pixel, $"{MathF.Ceiling(_scoreBoostTimer)}", x + 58, y + 18, 2, Color.White);
        }
    }

    // Draws a row of HUD icons with active and inactive states.
    private void DrawHudIconRow(
        Texture2D texture,
        int slotCount,
        int filledCount,
        int x,
        int y,
        int size,
        int spacing,
        Color filledColor,
        Color emptyColor,
        string? overlayText = null)
    {
        int clampedFilledCount = Math.Clamp(filledCount, 0, slotCount);

        for (int i = 0; i < slotCount; i++)
        {
            Rectangle bounds = new(x + i * (size + spacing), y, size, size);
            bool isFilled = i < clampedFilledCount;
            Color frameColor = isFilled ? filledColor : new Color(62, 62, 62);
            Color backgroundColor = isFilled ? Color.Black * 0.26f : Color.Black * 0.16f;
            Color iconColor = isFilled ? filledColor : emptyColor;

            _spriteBatch.Draw(_pixel, bounds.InflateBy(2), backgroundColor);
            DrawRectangleOutline(bounds, 2, frameColor * 0.7f);
            DrawTextureInBounds(texture, bounds.InflateBy(-4), iconColor);

            if (!string.IsNullOrEmpty(overlayText))
            {
                PixelFont.Draw(
                    _spriteBatch,
                    _pixel,
                    overlayText,
                    bounds.X + 10,
                    bounds.Y + 8,
                    2,
                    isFilled ? new Color(61, 31, 0) : new Color(170, 170, 170));
            }
        }
    }

    // Draws the stage 3 boss overlay, then redraws the runner so the overlay does not dim the player.
    private void DrawBossEncounter()
    {
        if (!IsBossEncounterActive())
        {
            return;
        }

        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, WindowWidth, WindowHeight), new Color(4, 10, 12) * 0.58f);

        Rectangle bossImage = _boss?.Bounds ?? new Rectangle(WindowWidth - 450, 220, 360, 390);
        DrawTextureInBounds(_bossBodyTexture, bossImage, Color.White);
        if (_viewMode == ViewMode.Front)
        {
            DrawPlayerFront();
        }

        if (_bossIntroActive)
        {
            DrawBossIntroPanel();
            return;
        }

        foreach (BossAttack attack in _bossAttacks)
        {
            Texture2D attackTexture = attack.Kind == BossAttackKind.Spear ? _bossArrowTexture : _bossBoulderTexture;
            DrawTextureInBounds(attackTexture, GetBossAttackBounds(attack), Color.White);
        }

        foreach (BossWeakPoint weakPoint in _bossWeakPoints)
        {
            DrawTextureInBounds(_coinTexture, GetWeakPointBounds(weakPoint), Color.White);
        }

        DrawBossStatusPanel();
        DrawBossLegendPanel();
    }

    // Draws the boss arrival banner with the encounter rules.
    private void DrawBossIntroPanel()
    {
        Rectangle panel = new(150, 148, 760, 214);
        DrawBossPanel(panel, new Color(216, 178, 73), new Color(19, 35, 39) * 0.96f);
        DrawTextureInBounds(_bossArrowTexture, new Rectangle(panel.X + 26, panel.Y + 34, 86, 28), Color.Gold);
        DrawTextureInBounds(_bossArrowTexture, new Rectangle(panel.Right - 112, panel.Y + 34, 86, 28), Color.Gold);
        PixelFont.Draw(_spriteBatch, _pixel, BossName, panel.X + 70, panel.Y + 38, 5, Color.Gold);
        PixelFont.Draw(_spriteBatch, _pixel, "THE IDOL AWAKENS", panel.X + 186, panel.Y + 98, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "SURVIVE THE ASSAULT OR BREAK THE WEAK POINTS", panel.X + 84, panel.Y + 144, 2, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ROPE DISABLED", panel.X + 280, panel.Y + 178, 2, Color.OrangeRed);
    }

    // Draws the active fight HUD with health and survival bars.
    private void DrawBossStatusPanel()
    {
        Rectangle panel = new(72, 50, 690, 114);
        DrawBossPanel(panel, new Color(216, 178, 73), new Color(19, 35, 39) * 0.94f);
        PixelFont.Draw(_spriteBatch, _pixel, BossName, panel.X + 22, panel.Y + 14, 3, Color.Gold);

        float bossHealthProgress = _boss is null || _boss.Health.MaxHealth <= 0
            ? 0f
            : _boss.Health.CurrentHealth / (float)_boss.Health.MaxHealth;
        string bossHealthText = _boss is null ? "0/0" : $"{_boss.Health.CurrentHealth}/{_boss.Health.MaxHealth}";
        DrawBossMeter(panel.X + 22, panel.Y + 56, 318, 24, "IDOL HP", bossHealthProgress, bossHealthText, new Color(209, 66, 55));

        float timerProgress = MathHelper.Clamp(_bossFightTimer / BossFightDuration, 0f, 1f);
        DrawBossMeter(panel.X + 364, panel.Y + 56, 296, 24, "SURVIVE", timerProgress, $"{MathF.Ceiling(_bossFightTimer)}", new Color(223, 175, 59));
    }

    // Draws the boss-side legend so attacks and weak points read faster.
    private void DrawBossLegendPanel()
    {
        Rectangle panel = new(908, 58, 308, 228);
        DrawBossPanel(panel, new Color(216, 178, 73), new Color(19, 35, 39) * 0.94f);
        PixelFont.Draw(_spriteBatch, _pixel, "COMBAT READ", panel.X + 24, panel.Y + 16, 2, Color.Gold);
        DrawBossLegendRow(_bossArrowTexture, panel.X + 22, panel.Y + 54, "SLIDE THE SPEAR", new Color(225, 132, 63));
        DrawBossLegendRow(_bossBoulderTexture, panel.X + 22, panel.Y + 108, "JUMP THE BOULDER", new Color(212, 196, 166));
        DrawBossLegendRow(_coinTexture, panel.X + 22, panel.Y + 162, "STRIKE WEAK POINTS", new Color(231, 193, 65));
        PixelFont.Draw(_spriteBatch, _pixel, "ROPE DISABLED", panel.X + 70, panel.Y + 198, 2, Color.OrangeRed);
    }

    // Draws a compact boss legend row with an icon and label.
    private void DrawBossLegendRow(Texture2D texture, int x, int y, string label, Color accent)
    {
        Rectangle iconBounds = new(x, y, 36, 36);
        _spriteBatch.Draw(_pixel, iconBounds.InflateBy(3), Color.Black * 0.32f);
        _spriteBatch.Draw(_pixel, iconBounds, new Color(31, 31, 31));
        DrawRectangleOutline(iconBounds, 2, accent);
        DrawTextureInBounds(texture, iconBounds.InflateBy(-4), Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, label, x + 56, y + 9, 2, Color.White);
    }

    // Draws a shared panel style for boss HUD sections.
    private void DrawBossPanel(Rectangle bounds, Color borderColor, Color fillColor)
    {
        _spriteBatch.Draw(_pixel, bounds.InflateBy(6), Color.Black * 0.24f);
        _spriteBatch.Draw(_pixel, bounds, fillColor);
        DrawRectangleOutline(bounds, 3, borderColor);
        DrawRectangleOutline(bounds.InflateBy(6), 1, borderColor * 0.42f);
    }

    // Draws one labelled progress bar used by the boss HUD.
    private void DrawBossMeter(int x, int y, int width, int height, string label, float progress, string valueText, Color fillColor)
    {
        PixelFont.Draw(_spriteBatch, _pixel, label, x, y - 18, 2, Color.White);
        Rectangle bounds = new(x, y, width, height);
        Rectangle innerBounds = bounds.InflateBy(-4);
        int fillWidth = (int)MathF.Round(innerBounds.Width * MathHelper.Clamp(progress, 0f, 1f));

        _spriteBatch.Draw(_pixel, bounds, new Color(16, 16, 16) * 0.95f);
        if (fillWidth > 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(innerBounds.X, innerBounds.Y, fillWidth, innerBounds.Height), fillColor);
        }

        DrawRectangleOutline(bounds, 2, Color.White * 0.72f);
        PixelFont.Draw(_spriteBatch, _pixel, valueText, x + width + 18, y - 3, 2, Color.White);
    }

    // Draws the stage-clear or game-over result screen.
    private void DrawGameOver()
    {
        Color titleColor = _runWon ? Color.Gold : Color.OrangeRed;
        PixelFont.Draw(_spriteBatch, _pixel, _gameOverTitle, 120, 160, 8, titleColor);
        PixelFont.Draw(_spriteBatch, _pixel, _gameOverDetail, 130, 285, 4, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, $"BEST {_gameOverScreen.BestScore}  STARS {_gameOverScreen.StarRating}", 130, 340, 3, Color.Gold);
        if (_gameOverScreen.NextStageUnlocked)
        {
            PixelFont.Draw(_spriteBatch, _pixel, "NEXT STAGE UNLOCKED", 130, 380, 3, Color.LightGreen);
        }

        PixelFont.Draw(_spriteBatch, _pixel, "ENTER STAGE SELECT  ESC MENU", 130, 440, 3, Color.White);
    }

    // Draws the active stage art behind gameplay elements.
    private void DrawGameplayBackground()
    {
        int stageIndex = Math.Clamp(_activeStage.Number - 1, 0, _gameplayBackgrounds.Length - 1);
        DrawScrollingBackground(_gameplayBackgrounds[stageIndex], _worldScroller.OffsetX);
    }

    // Scales a background texture to the fixed game back buffer.
    private void DrawFullScreenTexture(Texture2D texture)
    {
        DrawFullScreenTexture(texture, Color.White);
    }

    // Scales a background texture to the fixed game back buffer with a tint.
    private void DrawFullScreenTexture(Texture2D texture, Color tint)
    {
        _spriteBatch.Draw(texture, new Rectangle(0, 0, WindowWidth, WindowHeight), tint);
    }

    // Creates grayscale versions of loaded stage art for locked stage cards.
    private Texture2D[] BuildGrayscaleCopies(Texture2D[] textures)
    {
        Texture2D[] copies = new Texture2D[textures.Length];
        for (int i = 0; i < textures.Length; i++)
        {
            copies[i] = BuildGrayscaleCopy(textures[i]);
        }

        return copies;
    }

    private Texture2D BuildGrayscaleCopy(Texture2D source)
    {
        Color[] pixels = new Color[source.Width * source.Height];
        source.GetData(pixels);

        for (int i = 0; i < pixels.Length; i++)
        {
            Color color = pixels[i];
            byte gray = (byte)MathHelper.Clamp(
                color.R * 0.30f + color.G * 0.59f + color.B * 0.11f,
                0f,
                255f);
            pixels[i] = new Color(gray, gray, gray, color.A);
        }

        Texture2D grayscale = new(GraphicsDevice, source.Width, source.Height);
        grayscale.SetData(pixels);
        return grayscale;
    }

    // Tiles a wide background horizontally so it can scroll forever while gameplay advances.
    private void DrawScrollingBackground(Texture2D texture, float scrollOffset)
    {
        float scale = WindowHeight / (float)texture.Height;
        int scaledWidth = (int)MathF.Ceiling(texture.Width * scale);
        int scrollX = (int)(scrollOffset % scaledWidth);

        for (int x = -scrollX; x < WindowWidth; x += scaledWidth)
        {
            _spriteBatch.Draw(texture, new Rectangle(x, 0, scaledWidth, WindowHeight), Color.White);
        }
    }

    // Resets run state and builds the selected stage for a fresh attempt.
    private void StartRun()
    {
        EnsureCurrentUser();

        // Apply selected difficulty to stage generation, starting lives, scoring targets, and hazard speed.
        _activeStage = _stages[_selectedStage];
        if (!IsStageUnlocked(_activeStage))
        {
            _stageSelectScreen.SetIntroDialogue(_activeStage);
            return;
        }

        _activeStageData = _stageFactory.Create(_activeStage, _selectedDifficulty);
        _segments = _activeStageData.Segments;
        _activeSegment = _segments[0];
        _activeStageData.CurrentNode = _activeStageData.Graph.Start;
        _worldScroller.Reset();
        _segmentProgress = 0f;
        _distance = 0f;
        _distanceScore = 0f;
        _playerJumpOffset = 0f;
        _playerVelocityY = 0f;
        _canDoubleJump = true;
        _isDoubleJumping = false;
        _playerRow = Constants.MiddleLayer;
        _slideTimer = 0f;
        _ropeTimer = 0f;
        _ropeSwingPivot = Vector2.Zero;
        _scoreBoostTimer = 0f;
        _invulnerableTimer = 0f;
        _damageFlashTimer = 0f;
        _screenShakeTimer = 0f;
        _runAnimationTimer = 0f;
        _lives = GetStartingLivesForDifficulty(_selectedDifficulty);
        _coins = 0;
        _boosters = 0;
        _coinScore = 0;
        _stageItemShieldCharges = 0;
        _ropeItemCharges = 0;
        _score = 0;
        _runWon = false;
        _nextStageUnlockedThisRun = false;
        _collectedItemsThisRun.Clear();
        _runRandom = new Random(_activeStage.Number);
        _screen = GameScreen.Playing;
        _audioManager.PlaySongForLevel(_selectedStage + 1);
        _score = 0;
        _runWon = false;
        _collectedItemsThisRun.Clear();
        _runRandom = new Random(_activeStage.Number);
        _screen = GameScreen.Playing;
        _audioManager.PlaySongForLevel(_selectedStage + 1);
        _score = 0;
        _collectedItemsThisRun.Clear();
        ResetMapProjectiles();
        _runWon = false;
        _awaitingRouteChoice = false;
        _nextStageUnlockedThisRun = false;
        _routeChoiceIndex = 0;
        ResetBossEncounter();
        _runRandom = new Random(_activeStage.Number * 1000 + (int)_selectedDifficulty);
        _screen = GameScreen.Playing;
    }

    // Lets the player choose between available route branches.
    private void UpdateRouteChoice(KeyboardState keyboard)
    {
        if (_activeStageData.CurrentNode is null || _activeStageData.CurrentNode.Next.Count == 0)
        {
            _awaitingRouteChoice = false;
            return;
        }

        int routeCount = _activeStageData.CurrentNode.Next.Count;
        if (IsNewKeyPress(keyboard, Keys.Right))
        {
            _routeChoiceIndex = (_routeChoiceIndex + 1) % routeCount;
        }

        if (IsNewKeyPress(keyboard, Keys.Left))
        {
            _routeChoiceIndex = (_routeChoiceIndex + routeCount - 1) % routeCount;
        }

        if (IsNewKeyPress(keyboard, Keys.Enter))
        {
            AdvanceToRoute(_activeStageData.CurrentNode.Next[_routeChoiceIndex]);
        }
    }

    // Advances automatic routes or pauses for player choice at branch nodes.
    private void UpdateRouteProgress()
    {
        if (_activeStageData.CurrentNode is null || _segmentProgress < _activeSegment.Length)
        {
            return;
        }

        if (_activeStageData.CurrentNode.Next.Count == 0)
        {
            return;
        }

        if (_activeStageData.CurrentNode.Next.Count == 1)
        {
            AdvanceToRoute(_activeStageData.CurrentNode.Next[0]);
            return;
        }

        _awaitingRouteChoice = true;
        _routeChoiceIndex = 0;
    }

    // Moves the run into the selected graph node without overriding manual row control.
    private void AdvanceToRoute(StageNode nextNode)
    {
        _activeStageData.CurrentNode = nextNode;
        _activeSegment = nextNode.Segment;
        _segmentProgress = 0f;
        _awaitingRouteChoice = false;
        _routeChoiceIndex = 0;
    }

    // Creates or restores the current local profile using only the typed user id.
    private void EnsureCurrentUser()
    {
        if (_currentUser is not null)
        {
            return;
        }

        LoadCurrentUserFromTypedId();
    }

    // Loads the requested local profile, or creates it when this id has not played before.
    private void LoadCurrentUserFromTypedId()
    {
        UserProfile? user = _loginScreen.LoadOrCreateProfile(_saveFile, NormalizeUserProfile, out bool isNewUser);
        if (user is null)
        {
            return;
        }

        UnlockFirstStage(user);
        _currentUser = user;
        _saveFile.LastUserId = _currentUser.UserId;
        _soundEnabled = _currentUser.Settings.SoundEnabled;
        _audioManager.SetMute(!_soundEnabled);
        _selectedDifficulty = _currentUser.Settings.Difficulty;
        _viewMode = ViewMode.Front;
        _loginScreen.MarkLoaded(
            _currentUser.UserId,
            isNewUser ? $"Created profile {_currentUser.UserId}." : $"Loaded profile {_currentUser.UserId}.");
        SaveSaveFile();
    }

    // Guarantees new and older profiles can always start the first stage.
    private static void UnlockFirstStage(UserProfile user)
    {
        if (!user.StageProgress.TryGetValue(1, out StageProgress? firstStageProgress))
        {
            firstStageProgress = new StageProgress
            {
                StageNumber = 1
            };
            user.StageProgress[1] = firstStageProgress;
        }

        firstStageProgress.IsUnlocked = true;
    }

    // Persists score, stars, lives, and completion data for the current local user.
    private void SaveStageProgress()
    {
        // TODO: Cap or summarize score history so saves do not grow forever.
        EnsureCurrentUser();

        if (!_currentUser!.StageProgress.TryGetValue(_activeStage.Number, out StageProgress? progress))
        {
            progress = new StageProgress
            {
                StageNumber = _activeStage.Number,
                IsUnlocked = true
            };
            _currentUser.StageProgress[_activeStage.Number] = progress;
        }

        progress.IsCompleted = progress.IsCompleted || _runWon;
        int attemptStarRating = CalculateStarRating(_score);
        if (_score > progress.BestScore)
        {
            progress.BestScore = _score;
            progress.BestDifficulty = (int)_selectedDifficulty;
        }
        progress.StarRating = System.Math.Max(progress.StarRating, attemptStarRating);
        if (_runWon)
        {
            _nextStageUnlockedThisRun = UnlockNextStage() || _nextStageUnlockedThisRun;
        }

        foreach (string itemId in _collectedItemsThisRun)
        {
            _currentUser.OwnedOutOfStageItems.Add(itemId);
        }
        _currentUser.TopScores[_activeStage.Number] = progress.BestScore;
        _currentUser.BestScore = System.Math.Max(_currentUser.BestScore, _score);
        _currentUser.Lives = _lives;
        _currentUser.Scores.Add(new ScoreEntry
        {
            StageNumber = _activeStage.Number,
            Score = _score
        });
        SaveSaveFile();
    }

    // Unlocks the next stage after a clear while preserving any existing progress.
    private bool UnlockNextStage()
    {
        if (_currentUser is null)
        {
            return false;
        }

        StageDefinition? nextStage = _stages.FirstOrDefault(stage => stage.Number == _activeStage.Number + 1);
        if (nextStage is null)
        {
            return false;
        }

        if (!_currentUser.StageProgress.TryGetValue(nextStage.Number, out StageProgress? nextProgress))
        {
            nextProgress = new StageProgress
            {
                StageNumber = nextStage.Number
            };
            _currentUser.StageProgress[nextStage.Number] = nextProgress;
        }

        bool wasLocked = !nextProgress.IsUnlocked;
        nextProgress.IsUnlocked = true;
        return wasLocked;
    }

    // Moves the latest round result into the game-over screen state.
    private void ShowGameOver(string title)
    {
        int starRating = CalculateStarRating(_score);
        int bestScore = _currentUser is not null && _currentUser.TopScores.TryGetValue(_activeStage.Number, out int topScore)
            ? topScore
            : _score;

        _gameOverScreen.SetResult(title, $"Score {_score}", _score, bestScore, starRating, _runWon && _nextStageUnlockedThisRun);
        _screen = GameScreen.GameOver;
    }

    // Returns whether the selected stage is playable for the current profile.
    private bool IsStageUnlocked(StageDefinition stage)
    {
        if (stage.Number == 1)
        {
            return true;
        }

        if (_currentUser is null)
        {
            return false;
        }

        bool explicitlyUnlocked = _currentUser.StageProgress.TryGetValue(stage.Number, out StageProgress? progress)
            && progress.IsUnlocked;
        bool previousStageCleared = _currentUser.StageProgress.TryGetValue(stage.Number - 1, out StageProgress? previousProgress)
            && previousProgress.IsCompleted;

        return explicitlyUnlocked || previousStageCleared;
    }

    // Toggles sound preference and saves it when a profile is active.
    private void ToggleSound()
    {
        _soundEnabled = !_soundEnabled;
        _audioManager.SetMute(!_soundEnabled);
        if (_currentUser is not null)
        {
            _currentUser.Settings.SoundEnabled = _soundEnabled;
            SaveSaveFile();
        }

        _menuMessage = _soundEnabled ? "Sound enabled." : "Sound muted.";
    }

    // Changes the selected difficulty and stores it on the current profile.
    private void CycleDifficulty(int direction)
    {
        Difficulty[] difficulties = Enum.GetValues<Difficulty>();
        int index = Array.IndexOf(difficulties, _selectedDifficulty);
        if (index < 0)
        {
            index = Array.IndexOf(difficulties, Difficulty.Medium);
        }

        _selectedDifficulty = difficulties[(index + direction + difficulties.Length) % difficulties.Length];

        if (_currentUser is not null)
        {
            _currentUser.Settings.Difficulty = _selectedDifficulty;
            SaveSaveFile();
        }
    }

    // Starting lives depend only on difficulty, not on the selected stage.
    private static int GetStartingLivesForDifficulty(Difficulty difficulty)
    {
        return difficulty switch
        {
            Difficulty.Easy => 3,
            Difficulty.Hard => 1,
            _ => 2
        };
    }

    // The HUD heart row and life refills share the same difficulty-based cap.
    private int GetMaxLivesForCurrentRun()
    {
        return GetStartingLivesForDifficulty(_selectedDifficulty);
    }

    // Updates short-lived jump, slide, rope, score boost, and invulnerability timers.
    private void UpdatePlayerActionTimers(float deltaSeconds)
    {
        if (_playerJumpOffset > 0f || _playerVelocityY < 0f)
        {
            _playerVelocityY += Gravity * deltaSeconds;
            _playerJumpOffset -= _playerVelocityY * deltaSeconds;

            if (_playerJumpOffset <= 0f)
            {
                _playerJumpOffset = 0f;
                _playerVelocityY = 0f;
                _canDoubleJump = true;
                _isDoubleJumping = false;
            }
        }

        _slideTimer = System.Math.Max(0f, _slideTimer - deltaSeconds);
        float previousRopeTimer = _ropeTimer;
        _ropeTimer = System.Math.Max(0f, _ropeTimer - deltaSeconds);
        if (previousRopeTimer > 0f && _ropeTimer <= 0f)
        {
            _ropeSwingPivot = Vector2.Zero;
        }

        _scoreBoostTimer = System.Math.Max(0f, _scoreBoostTimer - deltaSeconds);
        _invulnerableTimer = System.Math.Max(0f, _invulnerableTimer - deltaSeconds);
        _damageFlashTimer = System.Math.Max(0f, _damageFlashTimer - deltaSeconds);
        _screenShakeTimer = System.Math.Max(0f, _screenShakeTimer - deltaSeconds);
    }

    // Returns the current score including the stage 3 boss survival bonus when earned.
    private int CalculateRunScore()
    {
        return (int)_distanceScore + _coinScore + _bossSurvivalBonus;
    }

    // Returns the score multiplier for points earned during an active score boost.
    private float GetScoreMultiplier()
    {
        return _scoreBoostTimer > 0f ? ScoreBoostMultiplier : 1f;
    }

    // Applies the active score multiplier to one-time score awards like coins.
    private int GetBoostedScoreValue(int baseValue)
    {
        return (int)MathF.Round(baseValue * GetScoreMultiplier());
    }

    // True while the stage 3 boss has control of the run.
    private bool IsBossEncounterActive()
    {
        return _bossIntroActive || _bossFightActive;
    }

    // Resets the stage 3 boss state for a fresh run.
    private void ResetBossEncounter()
    {
        _bossAttacks.Clear();
        _bossWeakPoints.Clear();
        _bossEncounterStarted = false;
        _bossIntroActive = false;
        _bossFightActive = false;
        _bossDefeated = false;
        _bossIntroTimer = 0f;
        _bossFightTimer = 0f;
        _bossAttackTimer = 0f;
        _weakPointTimer = 0f;
        _bossSurvivalBonus = 0;
        _boss = null;
    }

    // Starts the stage 3 boss arrival screen when the runner enters the boss tile area.
    private void StartBossEncounter()
    {
        // TODO: Add pre-boss RPG dialogue before freezing the runner and starting the fight.
        if (_bossEncounterStarted)
        {
            return;
        }

        _bossEncounterStarted = true;
        _bossIntroActive = true;
        _bossIntroTimer = BossIntroDuration;
        _bossAttacks.Clear();
        _bossWeakPoints.Clear();
        _mapProjectiles.Clear();
        _ropeTimer = 0f;
        _scoreBoostTimer = 0f;
        _boss = new Boss
        {
            Position = new Vector2(WindowWidth - 450, 220f),
            Size = new Vector2(360f, 390f),
            IsActive = true
        };

        foreach (Tile tile in _activeStageData.World.AllTiles.Where(tile => tile.Content == TileContent.Boss))
        {
            tile.Content = TileContent.Empty;
        }
    }

    // Starts the boss as soon as the runner reaches boss tiles, before normal stage hazards resolve.
    private void TryStartBossEncounterFromGrid()
    {
        if (_bossEncounterStarted || _activeStage.Number != BossStageNumber)
        {
            return;
        }

        Rectangle playerBounds = GetPlayerCollisionBounds();
        foreach (Tile tile in _activeStageData.World.AllTiles.Where(tile => tile.Content == TileContent.Boss && tile.Row == _playerRow))
        {
            float x = GetTileScreenX(tile.Column);
            if (playerBounds.Intersects(GetFrontTileCollisionBounds(tile, x)))
            {
                StartBossEncounter();
                return;
            }
        }
    }

    // Advances the boss intro, timed fight, and projectile/object attacks.
    private void UpdateBossEncounter(float deltaSeconds)
    {
        if (_bossIntroActive)
        {
            _bossIntroTimer -= deltaSeconds;
            if (_bossIntroTimer <= 0f)
            {
                StartBossFight();
            }
        }

        if (!_bossFightActive)
        {
            return;
        }

        _bossFightTimer -= deltaSeconds;
        _bossAttackTimer -= deltaSeconds;
        _weakPointTimer -= deltaSeconds;
        UpdateBossMotion();

        if (_bossAttackTimer <= 0f)
        {
            SpawnBossAttack();
            _bossAttackTimer = GetNextBossAttackInterval();
        }

        if (_weakPointTimer <= 0f)
        {
            SpawnWeakPoint();
            _weakPointTimer = WeakPointMinInterval + (float)_runRandom.NextDouble() * (WeakPointMaxInterval - WeakPointMinInterval);
        }

        UpdateBossAttacks(deltaSeconds);
        UpdateWeakPoints(deltaSeconds);
        if (_screen != GameScreen.Playing)
        {
            return;
        }

        if (_boss is not null && (!_boss.IsActive || _boss.Health.IsDead))
        {
            CompleteBossFight();
            return;
        }

        if (_bossFightTimer <= 0f)
        {
            CompleteBossFight();
        }
    }

    // Freezes scrolling and starts the timed survival battle.
    private void StartBossFight()
    {
        _bossIntroActive = false;
        _bossFightActive = true;
        _bossFightTimer = BossFightDuration;
        _bossAttackTimer = BossAttackInitialDelay;
        _weakPointTimer = WeakPointMinInterval + 0.6f;
    }

    // Marks the boss as beaten and clears stage 3 immediately.
    private void CompleteBossFight()
    {
        _bossFightActive = false;
        _bossDefeated = true;
        _bossSurvivalBonus = BossSurvivalScoreBonus;
        _bossAttacks.Clear();
        _bossWeakPoints.Clear();
        if (_boss is not null)
        {
            _boss.IsActive = false;
        }
        _score = CalculateRunScore();
        _runWon = true;
        SaveStageProgress();
        ShowGameOver("Stage Clear");
    }

    // Creates one boss attack in a random lane.
    private void SpawnBossAttack()
    {
        int burstCount = GetBossAttackBurstCount();
        HashSet<int> usedRows = [];

        for (int i = 0; i < burstCount; i++)
        {
            int row = GetBossAttackRow(usedRows);
            usedRows.Add(row);
            BossAttackKind kind = ChooseBossAttackKind(i);
            float scale = _rowDepthMapper.GetScale(row);
            float groundY = _rowDepthMapper.GetGroundY(row);
            float y = kind == BossAttackKind.Spear ? groundY - 150f * scale : groundY - 72f * scale;
            float speed = kind == BossAttackKind.Spear
                ? 660f + 150f * GetBossPhaseProgress()
                : 500f + 120f * GetBossPhaseProgress();
            float spawnX = WindowWidth - 330f + i * BossAttackBurstSpacing;

            _bossAttacks.Add(new BossAttack(kind, row, new Vector2(spawnX, y), speed));
        }
    }

    // Keeps the idol visually alive and makes the attack source less static during the fight.
    private void UpdateBossMotion()
    {
        if (_boss is null || !_bossFightActive)
        {
            return;
        }

        float elapsed = BossFightDuration - _bossFightTimer;
        float phase = GetBossPhaseProgress();
        float x = MathHelper.Lerp(WindowWidth - 450f, WindowWidth - 500f, phase);
        float y = 220f + MathF.Sin(elapsed * MathHelper.Lerp(1.7f, 3.2f, phase)) * MathHelper.Lerp(12f, 32f, phase);
        _boss.Position = new Vector2(x, y);
    }

    // Ramps the boss pressure up over the duration of the fight.
    private float GetBossPhaseProgress()
    {
        return 1f - MathHelper.Clamp(_bossFightTimer / BossFightDuration, 0f, 1f);
    }

    // Returns the next delay between attack bursts, with phase and difficulty pressure applied.
    private float GetNextBossAttackInterval()
    {
        float phase = GetBossPhaseProgress();
        float minimum = MathHelper.Lerp(BossAttackMaxInterval * 0.82f, BossAttackMinInterval, phase);
        float maximum = MathHelper.Lerp(BossAttackMaxInterval * 1.08f, BossAttackMaxInterval * 0.70f, phase);
        float difficultyScale = _selectedDifficulty switch
        {
            Difficulty.Easy => 1.10f,
            Difficulty.Hard => 0.82f,
            _ => 1f
        };

        return (minimum + (float)_runRandom.NextDouble() * (maximum - minimum)) * difficultyScale;
    }

    // Adds layered lane attacks as the fight escalates.
    private int GetBossAttackBurstCount()
    {
        float phase = GetBossPhaseProgress();
        if (phase > 0.82f || (_selectedDifficulty == Difficulty.Hard && phase > 0.62f))
        {
            return 3;
        }

        return phase > 0.42f || (_selectedDifficulty == Difficulty.Hard && phase > 0.24f) ? 2 : 1;
    }

    // Avoids duplicate rows inside one burst while making the opening hit often pressure the current lane.
    private int GetBossAttackRow(HashSet<int> usedRows)
    {
        int[] rows =
        [
            Constants.FrontLayer,
            Constants.MiddleLayer,
            Constants.BackLayer
        ];

        int[] availableRows = rows.Where(row => !usedRows.Contains(row)).ToArray();
        if (availableRows.Length == 0)
        {
            return Constants.MiddleLayer;
        }

        float phase = GetBossPhaseProgress();
        double playerPressureChance = MathHelper.Lerp(0.38f, 0.68f, phase);
        if (_selectedDifficulty == Difficulty.Hard)
        {
            playerPressureChance += 0.12;
        }

        if (!usedRows.Contains(_playerRow) && _runRandom.NextDouble() < playerPressureChance)
        {
            return _playerRow;
        }

        return availableRows[_runRandom.Next(availableRows.Length)];
    }

    // Mixes slide and jump checks so multi-lane bursts do not collapse into one repeated dodge.
    private BossAttackKind ChooseBossAttackKind(int burstIndex)
    {
        float spearBias = MathHelper.Lerp(0.52f, 0.68f, GetBossPhaseProgress());
        if (burstIndex > 0)
        {
            spearBias = 1f - spearBias;
        }

        return _runRandom.NextDouble() < spearBias ? BossAttackKind.Spear : BossAttackKind.Boulder;
    }

    // Creates a collectible weak point that lets the runner damage the boss directly.
    private void SpawnWeakPoint()
    {
        if (_boss is null || !_boss.IsActive || _boss.Health.IsDead)
        {
            return;
        }

        int row = _runRandom.Next(Constants.FrontLayer, Constants.BackLayer + 1);
        float scale = _rowDepthMapper.GetScale(row);
        float groundY = _rowDepthMapper.GetGroundY(row);
        Vector2 size = GetWeakPointSize(row);
        float y = groundY - 92f * scale;

        float phase = GetBossPhaseProgress();
        float speed = WeakPointSpeed + 90f * phase;
        BossWeakPoint weakPoint = new(row, new Vector2(WindowWidth - 330f, y), speed, WeakPointLifetime)
        {
            Size = size
        };
        _bossWeakPoints.Add(weakPoint);
    }

    // Moves boss attacks and applies damage when their visual bounds overlap the runner hitbox.
    private void UpdateBossAttacks(float deltaSeconds)
    {
        for (int i = _bossAttacks.Count - 1; i >= 0; i--)
        {
            BossAttack attack = _bossAttacks[i];
            attack.Position = new Vector2(attack.Position.X - attack.Speed * deltaSeconds, attack.Position.Y);
            Rectangle attackBounds = GetBossAttackBounds(attack);
            Rectangle playerBounds = GetPlayerCollisionBounds();
            if (!attack.HasCheckedCollision && attack.Row == _playerRow && playerBounds.Intersects(attackBounds))
            {
                bool avoided = attack.Kind == BossAttackKind.Spear ? _slideTimer > 0f : _playerJumpOffset >= 56f;
                if (!avoided)
                {
                    DamagePlayer();
                    if (_screen != GameScreen.Playing)
                    {
                        return;
                    }
                }

                attack.HasCheckedCollision = true;
            }

            if (attackBounds.Right < -40)
            {
                _bossAttacks.RemoveAt(i);
            }
        }
    }

    // Moves weak points across the lane and damages the boss when the runner collects one.
    private void UpdateWeakPoints(float deltaSeconds)
    {
        if (_boss is null)
        {
            _bossWeakPoints.Clear();
            return;
        }

        Rectangle playerBounds = GetPlayerPickupBounds();
        for (int i = _bossWeakPoints.Count - 1; i >= 0; i--)
        {
            BossWeakPoint weakPoint = _bossWeakPoints[i];
            weakPoint.Position = new Vector2(weakPoint.Position.X - weakPoint.Speed * deltaSeconds, weakPoint.Position.Y);
            weakPoint.Lifetime -= deltaSeconds;

            Rectangle weakPointBounds = GetWeakPointBounds(weakPoint);
            if (weakPoint.Row == _playerRow && playerBounds.Intersects(weakPointBounds))
            {
                _boss.Health.Damage(WeakPointDamage);
                _bossWeakPoints.RemoveAt(i);
                continue;
            }

            if (weakPoint.Lifetime <= 0f || weakPointBounds.Right < -40)
            {
                _bossWeakPoints.RemoveAt(i);
            }
        }
    }

    // Returns screen-space attack dimensions with lane-depth scaling.
    private Vector2 GetBossAttackSize(BossAttack attack)
    {
        float scale = _rowDepthMapper.GetScale(attack.Row);
        return attack.Kind == BossAttackKind.Spear
            ? new Vector2(186f * scale, 40f * scale)
            : new Vector2(72f * scale, 72f * scale);
    }

    // Returns the visual and collision bounds for an active boss attack.
    private Rectangle GetBossAttackBounds(BossAttack attack)
    {
        Vector2 size = GetBossAttackSize(attack);
        return new Rectangle((int)attack.Position.X, (int)attack.Position.Y, (int)size.X, (int)size.Y);
    }

    // Returns screen-space weak point dimensions with lane-depth scaling.
    private Vector2 GetWeakPointSize(int row)
    {
        float scale = _rowDepthMapper.GetScale(row);
        return new Vector2(52f * scale, 52f * scale);
    }

    // Returns the visual and pickup bounds for an active boss weak point.
    private static Rectangle GetWeakPointBounds(BossWeakPoint weakPoint)
    {
        return new Rectangle(
            (int)weakPoint.Position.X,
            (int)weakPoint.Position.Y,
            (int)weakPoint.Size.X,
            (int)weakPoint.Size.Y);
    }

    // Clears normal map projectile state between stage attempts.
    private void ResetMapProjectiles()
    {
        _mapProjectiles.Clear();
        _activatedProjectileTiles.Clear();
    }

    // Converts projectile tile markers into active flying hazards as they enter the camera.
    private void ActivateVisibleMapProjectiles()
    {
        if (IsBossEncounterActive())
        {
            return;
        }

        foreach (Tile tile in _activeStageData.World.AllTiles)
        {
            if (tile.Content is not (TileContent.Projectile or TileContent.HomingProjectile))
            {
                continue;
            }

            float x = GetTileScreenX(tile.Column);
            if (x > FrontViewLookaheadX + MapProjectileActivationPadding || x < RunnerX - PlayerCollisionWidth)
            {
                continue;
            }

            string tileKey = $"{tile.Column}:{tile.Row}";
            if (!_activatedProjectileTiles.Add(tileKey))
            {
                continue;
            }

            float speed = tile.Content == TileContent.HomingProjectile ? MapProjectileHomingSpeed : MapProjectileStandardSpeed;
            _mapProjectiles.Add(new MapProjectile(tile.Content, tile.Row, new Vector2(x, GetMapProjectileY(tile.Row, tile.Content)), speed, MapProjectileHomingInterval));
            tile.Content = TileContent.Empty;
        }
    }

    // Moves active map projectiles, updates homing rows, and checks visual-bounds collisions.
    private void UpdateMapProjectiles(float deltaSeconds)
    {
        if (IsBossEncounterActive())
        {
            _mapProjectiles.Clear();
            return;
        }

        for (int i = _mapProjectiles.Count - 1; i >= 0; i--)
        {
            MapProjectile projectile = _mapProjectiles[i];
            projectile.Position = new Vector2(projectile.Position.X - projectile.Speed * deltaSeconds, GetMapProjectileY(projectile.Row, projectile.Kind));

            if (projectile.Kind == TileContent.HomingProjectile)
            {
                projectile.RowShiftTimer -= deltaSeconds;
                if (projectile.RowShiftTimer <= 0f)
                {
                    projectile.RowShiftTimer = MapProjectileHomingInterval;
                    projectile.Row += System.Math.Sign(_playerRow - projectile.Row);
                    projectile.Row = System.Math.Clamp(projectile.Row, Constants.FrontLayer, Constants.BackLayer);
                }
            }

            projectile.Position = new Vector2(projectile.Position.X, GetMapProjectileY(projectile.Row, projectile.Kind));
            Rectangle projectileBounds = GetMapProjectileBounds(projectile);
            Rectangle playerBounds = GetPlayerCollisionBounds();
            if (!projectile.HasCheckedCollision && projectile.Row == _playerRow && playerBounds.Intersects(projectileBounds))
            {
                bool avoided = _slideTimer > 0f;
                if (!avoided)
                {
                    DamagePlayer();
                    if (_screen != GameScreen.Playing)
                    {
                        return;
                    }
                }

                projectile.HasCheckedCollision = true;
            }

            if (projectileBounds.Right < -40)
            {
                _mapProjectiles.RemoveAt(i);
            }
        }
    }

    // Draws active flying map projectiles in front-view lane space.
    private void DrawMapProjectilesFront(int row)
    {
        foreach (MapProjectile projectile in _mapProjectiles.Where(projectile => projectile.Row == row))
        {
            Rectangle bounds = GetMapProjectileBounds(projectile);
            Texture2D? texture = GetTileContentTexture(projectile.Kind);
            if (texture is null)
            {
                _spriteBatch.Draw(_pixel, bounds, GetMapProjectileColor(projectile));
            }
            else
            {
                DrawTextureInBounds(texture, bounds, Color.White);
            }
        }
    }

    // Draws active flying map projectiles in top-view grid space.
    private void DrawMapProjectilesTop()
    {
        const int originY = 190;
        const int cell = 104;
        const int projectileSize = 44;

        foreach (MapProjectile projectile in _mapProjectiles)
        {
            int displayRow = _activeStageData.World.Rows - 1 - projectile.Row;
            int y = originY + displayRow * (cell + 18) + (100 - projectileSize) / 2;
            Rectangle bounds = new((int)projectile.Position.X, y, projectileSize, projectileSize);
            Texture2D? texture = GetTileContentTexture(projectile.Kind);
            if (texture is null)
            {
                _spriteBatch.Draw(_pixel, bounds, GetMapProjectileColor(projectile));
            }
            else
            {
                DrawTextureInBounds(texture, bounds, Color.White);
            }
        }
    }

    // Returns the front-view y position: arrows fly just high enough to slide under, homing rocks stay grounded.
    private float GetMapProjectileY(int row, TileContent kind)
    {
        float scale = _rowDepthMapper.GetScale(row);
        float groundY = _rowDepthMapper.GetGroundY(row);
        return kind == TileContent.HomingProjectile ? groundY - 122f * scale : groundY - 150f * scale;
    }

    // Returns screen-space projectile size with row depth applied.
    private Vector2 GetMapProjectileSize(MapProjectile projectile)
    {
        float scale = _rowDepthMapper.GetScale(projectile.Row);
        return projectile.Kind == TileContent.HomingProjectile
            ? new Vector2(74f * scale, 74f * scale)
            : new Vector2(138f * scale, 30f * scale);
    }

    // Returns the visual and collision bounds for an active map projectile.
    private Rectangle GetMapProjectileBounds(MapProjectile projectile)
    {
        Vector2 size = GetMapProjectileSize(projectile);
        return new Rectangle((int)projectile.Position.X, (int)projectile.Position.Y, (int)size.X, (int)size.Y);
    }

    // Uses different placeholder colors for straight and homing map projectiles.
    private static Color GetMapProjectileColor(MapProjectile projectile)
    {
        return projectile.Kind == TileContent.HomingProjectile ? Color.Magenta : Color.OrangeRed;
    }

    // Checks pickup and hazard bounds against the runner instead of using loose x-distance checks.
    private void ResolveGridInteractions()
    {
        if (IsBossEncounterActive())
        {
            return;
        }

        Rectangle playerHitBounds = GetPlayerCollisionBounds();
        Rectangle playerPickupBounds = GetPlayerPickupBounds();

        foreach (Tile tile in _activeStageData.World.AllTiles)
        {
            if (!tile.HasContent || tile.Row != _playerRow)
            {
                continue;
            }

            float x = GetTileScreenX(tile.Column);
            Rectangle tileBounds = GetFrontTileCollisionBounds(tile, x);
            bool touchesPlayer = IsPickupContent(tile.Content)
                ? playerPickupBounds.Intersects(tileBounds)
                : playerHitBounds.Intersects(tileBounds);
            if (!touchesPlayer)
            {
                continue;
            }

            switch (tile.Content)
            {
                case TileContent.Coin:
                    _coins++;
                    _coinScore += GetBoostedScoreValue(CoinsScoreWeight);
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.LifeItem:
                    _lives = System.Math.Min(GetMaxLivesForCurrentRun(), _lives + 1);
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.ScoreBooster:
                    _boosters++;
                    _scoreBoostTimer = ScoreBoostDuration;
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.StageItem:
                    // Stage items are temporary advantages; they are intentionally not saved.
                    _stageItemShieldCharges++;
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.RopeItem:
                    _ropeItemCharges = System.Math.Min(MaxRopeCharges, _ropeItemCharges + 1);
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.Collectible:
                case TileContent.Item:
                case TileContent.OutOfStageItem:
                    TrackCollectedItem(tile);
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.Projectile:
                case TileContent.HomingProjectile:
                    if (_slideTimer <= 0f)
                    {
                        DamagePlayer();
                    }
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.Meteor:
                    if (playerHitBounds.Intersects(tileBounds))
                    {
                        DamagePlayer();
                    }
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.Boss:
                    StartBossEncounter();
                    return;
                case TileContent.Obstacle:
                    DamagePlayer();
                    tile.Content = TileContent.Empty;
                    break;
            }
        }
    }

    // Records only non-consumable, out-of-stage placeholder items for the user JSON inventory.
    private void TrackCollectedItem(Tile tile)
    {
        // TODO: Let users equip out-of-stage items and use them for character customization or run bonuses.
        string itemId = $"{tile.Content}-S{_activeStage.Number}-C{tile.Column}-R{tile.Row}";
        _collectedItemsThisRun.Add(itemId);
    }

    // Returns true for tile contents that should feel forgiving to collect.
    private static bool IsPickupContent(TileContent content)
    {
        return content is TileContent.Coin
            or TileContent.LifeItem
            or TileContent.ScoreBooster
            or TileContent.StageItem
            or TileContent.RopeItem
            or TileContent.OutOfStageItem
            or TileContent.Collectible
            or TileContent.Item;
    }

    // Applies damage, temporary invulnerability, and the transition to game over.
    private void DamagePlayer()
    {
        if (_invulnerableTimer > 0f || (_ropeTimer > 0f && !IsBossEncounterActive()))
        {
            return;
        }

        if (_stageItemShieldCharges > 0)
        {
            _stageItemShieldCharges--;
            _invulnerableTimer = 0.6f;
            StartDamageFeedback(false);
            return;
        }

        _lives--;
        _invulnerableTimer = 1.2f;
        StartDamageFeedback(true);

        if (_lives <= 0)
        {
            _runWon = false;
            SaveStageProgress();
            ShowGameOver("Game Over");
        }
    }

    // Draws lane bands around each row's ground line so characters and hazards feel planted.
    private void DrawFrontLanes()
    {
        for (int row = Constants.BackLayer; row >= Constants.FrontLayer; row--)
        {
            float y = _rowDepthMapper.GetGroundY(row);
            float scale = _rowDepthMapper.GetScale(row);
            Color laneColor = row switch
            {
                Constants.FrontLayer => new Color(45, 126, 57),
                Constants.MiddleLayer => new Color(37, 105, 60),
                _ => new Color(30, 88, 67)
            };

            int laneTop = (int)(y - LaneGroundInset * scale);
            int laneHeight = (int)(LaneBandHeight * scale);
            _spriteBatch.Draw(_pixel, new Rectangle(0, laneTop, WindowWidth, laneHeight), laneColor * 0.5f);
        }
    }

    // Draws visible tile content in front-view lane space; route-only markers stay in top view.
    private void DrawFrontTile(Tile tile, float x)
    {
        float scale = _rowDepthMapper.GetScale(tile.Row);
        float groundY = _rowDepthMapper.GetGroundY(tile.Row);
        Color color = GetTileColor(tile);

        if (tile.Content == TileContent.Meteor)
        {
            DrawMeteorFrontTile(tile, x, scale, groundY);
            return;
        }

        Rectangle destination = GetFrontTileBounds(tile, x);

        Texture2D? texture = GetTileContentTexture(tile.Content);
        if (texture is not null)
        {
            DrawTextureInBounds(texture, destination, Color.White);
            return;
        }

        _spriteBatch.Draw(_pixel, destination, color);
    }

    // Returns the runner's front-view visual bounds using the same lane ground as hazards.
    private Rectangle GetPlayerFrontBounds()
    {
        return GetPlayerFrontBounds(GetPlayerAnimationFrame());
    }

    // Sizes the visible frame art directly and plants its bottom edge on the lane ground.
    private Rectangle GetPlayerFrontBounds(PlayerAnimationFrame frame)
    {
        float playerScale = _rowDepthMapper.GetScale(_playerRow);
        int playerHeight = frame.PreserveDesignedSize
            ? Math.Max(1, (int)(frame.Source.Height * PlayerDesignedFrameScale * playerScale * frame.VisualScale))
            : Math.Max(1, (int)(frame.VisualHeight * playerScale * frame.VisualScale));
        int playerWidth = frame.PreserveDesignedSize
            ? Math.Max(1, (int)(frame.Source.Width * PlayerDesignedFrameScale * playerScale * frame.VisualScale))
            : Math.Max(1, (int)(playerHeight * (frame.Source.Width / (float)frame.Source.Height)));
        int playerGroundY = (int)(_rowDepthMapper.GetGroundY(_playerRow) - _playerJumpOffset);
        int playerX = frame.IsRope
            ? (int)(RunnerX - playerWidth * 0.28f)
            : (int)RunnerX;
        return new Rectangle(playerX, playerGroundY - playerHeight, playerWidth, playerHeight);
    }

    // Draws the swing art around a fixed top pivot so the rope feels anchored as the player arcs right.
    private void DrawPlayerSwingFront(PlayerAnimationFrame frame, Color color)
    {
        float playerScale = _rowDepthMapper.GetScale(_playerRow);
        float playerWidth = PlayerStandingVisualWidth * playerScale;
        float scale = playerWidth / frame.Source.Width * frame.VisualScale;
        float elapsed = RopeDuration - _ropeTimer;
        float progress = MathHelper.Clamp(elapsed / RopeDuration, 0f, 1f);
        float swingEase = MathF.Sin(progress * MathHelper.PiOver2);
        float rotation = -0.55f * swingEase;
        Vector2 origin = new(frame.Source.Width * 0.5f, 0f);
        Vector2 topPivot = _ropeSwingPivot == Vector2.Zero
            ? GetCurrentRopeSwingPivot()
            : _ropeSwingPivot;

        _spriteBatch.Draw(frame.Texture, topPivot, frame.Source, color, rotation, origin, scale, SpriteEffects.None, 0f);
    }

    // Captures the rope top point when the rope starts, keeping the swing anchored during the effect.
    private Vector2 GetCurrentRopeSwingPivot()
    {
        return new Vector2(
            RunnerX,
            0f);
    }

    // Shrinks transparent sprite space so damage matches the visible runner body.
    private Rectangle GetPlayerCollisionBounds()
    {
        Rectangle bounds = GetPlayerFrontBounds();
        int insetX = (int)(bounds.Width * PlayerHitboxInsetXRatio);
        int topInset = (int)(bounds.Height * PlayerHitboxTopInsetRatio);
        int bottomInset = (int)(bounds.Height * PlayerHitboxBottomInsetRatio);
        return new Rectangle(
            bounds.X + insetX,
            bounds.Y + topInset,
            Math.Max(1, bounds.Width - insetX * 2),
            Math.Max(1, bounds.Height - topInset - bottomInset));
    }

    // Gives pickups a little grace without affecting hazard damage.
    private Rectangle GetPlayerPickupBounds()
    {
        return GetPlayerCollisionBounds().InflateBy(18);
    }

    // Returns the front-view visual bounds for a tile marker or tile content.
    private Rectangle GetFrontTileBounds(Tile tile, float x)
    {
        float scale = _rowDepthMapper.GetScale(tile.Row);
        float groundY = _rowDepthMapper.GetGroundY(tile.Row);
        int width = (int)(TileVisualWidth(tile) * scale);
        int height = (int)(TileVisualHeight(tile) * scale);
        return new Rectangle((int)x, (int)(groundY - height), width, height);
    }

    // Returns collision bounds that match the visual marker used in front view.
    private Rectangle GetFrontTileCollisionBounds(Tile tile, float x)
    {
        if (tile.Content == TileContent.Meteor)
        {
            float scale = _rowDepthMapper.GetScale(tile.Row);
            float groundY = _rowDepthMapper.GetGroundY(tile.Row);
            return GetMeteorFrontTargetBounds(tile, x, scale, groundY);
        }

        Rectangle bounds = GetFrontTileBounds(tile, x);
        return tile.Content switch
        {
            TileContent.Coin or TileContent.LifeItem or TileContent.ScoreBooster or TileContent.StageItem
                or TileContent.RopeItem or TileContent.OutOfStageItem or TileContent.Collectible or TileContent.Item
                => bounds.InflateBy(8),
            TileContent.Projectile or TileContent.HomingProjectile
                => bounds.InflateBy(-Math.Max(2, bounds.Height / 8)),
            TileContent.Obstacle
                => GetObstacleCollisionBounds(bounds),
            _ => bounds
        };
    }

    // Uses the lower part of obstacles for damage so jumps clear them a little sooner.
    private static Rectangle GetObstacleCollisionBounds(Rectangle bounds)
    {
        int insetX = Math.Max(2, bounds.Width / 12);
        int topInset = Math.Max(10, bounds.Height / 3);
        return new Rectangle(
            bounds.X + insetX,
            bounds.Y + topInset,
            Math.Max(1, bounds.Width - insetX * 2),
            Math.Max(1, bounds.Height - topInset));
    }

    // Draws the scrollable overhead grid and the player's current row.
    private void DrawTopGrid()
    {
        const int originX = 100;
        const int originY = 190;
        const int cell = 104;
        const int tileSize = 100;
        const int contentSize = 52;
        const int contentInset = (tileSize - contentSize) / 2;
        float scroll = _worldScroller.OffsetX * (cell / GameplayTileSpacing);

        for (int row = 0; row < _activeStageData.World.Rows; row++)
        {
            for (int column = 0; column < _activeStageData.World.Columns; column++)
            {
                Tile tile = _activeStageData.World.GetTile(column, row);
                int x = (int)(originX + column * cell - scroll);
                if (x < -cell || x > WindowWidth + cell)
                {
                    continue;
                }

                int displayRow = _activeStageData.World.Rows - 1 - row;
                int y = originY + displayRow * (cell + 18);
                Color baseColor = tile.Type switch
                {
                    TileType.Branch => new Color(72, 135, 102),
                    TileType.Merge => new Color(85, 148, 105),
                    TileType.Hazard => new Color(75, 55, 50),
                    _ => new Color(30, 94, 64)
                };

                _spriteBatch.Draw(_pixel, new Rectangle(x, y, tileSize, tileSize), baseColor * 0.5f);
                if (tile.HasContent)
                {
                    Rectangle contentDestination = new(x + contentInset, y + contentInset, contentSize, contentSize);
                    if (tile.Content == TileContent.Meteor)
                    {
                        DrawMeteorTarget(contentDestination);
                    }
                    else
                    {
                        Texture2D? texture = GetTileContentTexture(tile.Content);
                        if (texture is not null)
                        {
                            DrawTextureInBounds(texture, contentDestination, Color.White);
                        }
                        else
                        {
                            _spriteBatch.Draw(_pixel, contentDestination, GetTileColor(tile));
                        }
                    }
                }
            }
        }

        DrawRoutePreviewsTop(originX, originY, cell, tileSize, scroll);

        int playerX = (int)(originX + RunnerX * (cell / GameplayTileSpacing));
        int playerDisplayRow = _activeStageData.World.Rows - 1 - _playerRow;
        int playerY = originY + playerDisplayRow * (cell + 18);
        PlayerAnimationFrame playerFrame = GetPlayerAnimationFrame();
        DrawTextureInBounds(playerFrame.Texture, playerFrame.Source, new Rectangle(playerX, playerY, tileSize, tileSize), Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ROW 3", 28, originY + 8, 2, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ROW 2", 28, originY + cell + 26, 2, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ROW 1", 28, originY + (cell + 18) * 2 + 8, 2, Color.White);
    }

    // Highlights graph route choices in top view so each branch's lane and merge point are visible.
    private void DrawRoutePreviewsTop(int originX, int originY, int cell, int tileSize, float scroll)
    {
        if (!_awaitingRouteChoice || _activeStageData.CurrentNode is null || _activeStageData.CurrentNode.Next.Count <= 1)
        {
            return;
        }

        IReadOnlyList<StageNode> candidates = _activeStageData.CurrentNode.Next;
        for (int index = 0; index < candidates.Count; index++)
        {
            StageNode candidate = candidates[index];
            bool isSelected = index == _routeChoiceIndex;
            Color color = index == 0 ? Color.Gold : Color.Cyan;
            bool dashed = index % 2 == 1;

            DrawRouteSegmentTop(candidate.Segment, originX, originY, cell, tileSize, scroll, color, dashed, isSelected);

            if (candidate.Next.Count > 0)
            {
                StageNode mergeNode = candidate.Next[0];
                DrawRouteSegmentTop(mergeNode.Segment, originX, originY, cell, tileSize, scroll, color * 0.55f, dashed, false);
                DrawRouteTransitionTop(candidate.Segment, mergeNode.Segment, originX, originY, cell, tileSize, scroll, color, dashed);
            }
        }

        PixelFont.Draw(_spriteBatch, _pixel, "ROUTE PREVIEW", 430, 612, 3, Color.Gold);
        DrawRouteChoiceLabels(430, 646, 2);
    }

    // Lists route names beside their preview styles so the branch colors are meaningful.
    private void DrawRouteChoiceLabels(int x, int y, int scale)
    {
        if (_activeStageData.CurrentNode is null || _activeStageData.CurrentNode.Next.Count == 0)
        {
            return;
        }

        IReadOnlyList<StageNode> candidates = _activeStageData.CurrentNode.Next;
        for (int index = 0; index < candidates.Count; index++)
        {
            StageNode candidate = candidates[index];
            string inputLabel = index == 0 ? "LEFT" : index == 1 ? "RIGHT" : $"ROUTE {index + 1}";
            string styleLabel = index == 0 ? "GOLD GLOW" : "CYAN DASH";
            Color color = index == _routeChoiceIndex ? Color.White : index == 0 ? Color.Gold : Color.Cyan;
            PixelFont.Draw(_spriteBatch, _pixel, $"{inputLabel} {styleLabel}: {candidate.Label}", x, y + index * 28, scale, color);
        }
    }

    // Draws one graph segment as repeated top-view tile outlines.
    private void DrawRouteSegmentTop(MapSegment segment, int originX, int originY, int cell, int tileSize, float scroll, Color color, bool dashed, bool isSelected)
    {
        int startColumn = Math.Min(segment.PreviewStartColumn, segment.PreviewEndColumn);
        int endColumn = Math.Max(segment.PreviewStartColumn, segment.PreviewEndColumn);

        for (int column = startColumn; column <= endColumn; column++)
        {
            Rectangle bounds = GetTopGridCellBounds(column, segment.PreferredRow, originX, originY, cell, tileSize, scroll);
            if (bounds.Right < -cell || bounds.X > WindowWidth + cell)
            {
                continue;
            }

            if (dashed && column % 2 != 0)
            {
                DrawRouteDash(bounds, color);
            }
            else
            {
                if (!dashed)
                {
                    DrawRectangleOutline(bounds.InflateBy(10), 2, color * 0.28f);
                    DrawRectangleOutline(bounds.InflateBy(5), 2, color * 0.45f);
                }

                DrawRectangleOutline(bounds.InflateBy(2), isSelected ? 5 : 3, color);
            }

            if (isSelected)
            {
                DrawRectangleOutline(bounds.InflateBy(8), 2, Color.White * 0.72f);
            }
        }
    }

    // Draws a vertical hint where a branch returns to the merge route.
    private void DrawRouteTransitionTop(MapSegment from, MapSegment to, int originX, int originY, int cell, int tileSize, float scroll, Color color, bool dashed)
    {
        int column = from.PreviewEndColumn;
        int minRow = Math.Min(from.PreferredRow, to.PreferredRow);
        int maxRow = Math.Max(from.PreferredRow, to.PreferredRow);

        for (int row = minRow; row <= maxRow; row++)
        {
            Rectangle bounds = GetTopGridCellBounds(column, row, originX, originY, cell, tileSize, scroll);
            if (bounds.Right < -cell || bounds.X > WindowWidth + cell)
            {
                continue;
            }

            if (dashed && row % 2 == 0)
            {
                DrawRouteDash(bounds, color * 0.7f);
            }
            else
            {
                DrawRectangleOutline(bounds.InflateBy(2), 3, color * 0.7f);
            }
        }
    }

    // Converts one world tile coordinate into its top-view screen rectangle.
    private Rectangle GetTopGridCellBounds(int column, int row, int originX, int originY, int cell, int tileSize, float scroll)
    {
        int x = (int)(originX + column * cell - scroll);
        int displayRow = _activeStageData.World.Rows - 1 - row;
        int y = originY + displayRow * (cell + 18);
        return new Rectangle(x, y, tileSize, tileSize);
    }

    // Draws four short corner marks to make a route outline read as dashed.
    private void DrawRouteDash(Rectangle bounds, Color color)
    {
        int length = Math.Max(14, bounds.Width / 3);
        int thickness = 4;
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, length, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, thickness, length), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - length, bounds.Y, length, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, length), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, length, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - length, thickness, length), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - length, bounds.Bottom - thickness, length, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - thickness, bounds.Bottom - length, thickness, length), color);
    }

    // Converts a stage grid column into the current screen-space x position.
    private float GetTileScreenX(int column)
    {
        return SpawnScreenOffset + column * GameplayTileSpacing - _worldScroller.OffsetX;
    }

    // Chooses the active movement strip and frame, prioritizing special actions over the run loop.
    private PlayerAnimationFrame GetPlayerAnimationFrame()
    {
        if (_ropeTimer > 0f && !IsBossEncounterActive())
        {
            return new PlayerAnimationFrame(_playerSwingTexture, _playerSwingSource, PlayerRopeVisualHeight, PlayerRopeVisualScale, true, false);
        }

        if (_slideTimer > 0f)
        {
            int frameIndex = GetWeightedTimedActionFrame(SlideDuration - _slideTimer, SlideDuration, PlayerSlideFrameWeights);
            return new PlayerAnimationFrame(_playerSlideTextures[frameIndex], _playerSlideFrames[frameIndex], PlayerStandingVisualHeight, 1f, false, true);
        }

        if (_playerJumpOffset > 0f || _playerVelocityY != 0f)
        {
            int frameIndex = GetJumpFrameIndex();
            return new PlayerAnimationFrame(_playerJumpTextures[frameIndex], _playerJumpFrames[frameIndex], PlayerStandingVisualHeight, PlayerJumpVisualScale, false, true);
        }

        int runFrameIndex = (int)(_runAnimationTimer / RunnerFrameTime) % PlayerRunFrameCount;
        return new PlayerAnimationFrame(_playerRunSheet, _playerRunFrames[runFrameIndex], PlayerStandingVisualHeight, 1f, false, false);
    }

    private readonly record struct PlayerAnimationFrame(
        Texture2D Texture,
        Rectangle Source,
        float VisualHeight,
        float VisualScale,
        bool IsRope,
        bool PreserveDesignedSize);

    private static Rectangle[] BuildFullTextureSources(Texture2D[] textures)
    {
        Rectangle[] frames = new Rectangle[textures.Length];
        for (int frameIndex = 0; frameIndex < textures.Length; frameIndex++)
        {
            Texture2D texture = textures[frameIndex];
            frames[frameIndex] = new Rectangle(0, 0, texture.Width, texture.Height);
        }

        return frames;
    }

    private static Rectangle BuildTrimmedTextureSource(Texture2D texture)
    {
        Color[] pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);

        int minX = texture.Width;
        int minY = texture.Height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < texture.Height; y++)
        {
            for (int x = 0; x < texture.Width; x++)
            {
                if (pixels[y * texture.Width + x].A <= SpriteTrimAlphaThreshold)
                {
                    continue;
                }

                minX = System.Math.Min(minX, x);
                minY = System.Math.Min(minY, y);
                maxX = System.Math.Max(maxX, x);
                maxY = System.Math.Max(maxY, y);
            }
        }

        return maxX < minX || maxY < minY
            ? new Rectangle(0, 0, texture.Width, texture.Height)
            : new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static int GetWeightedTimedActionFrame(float elapsed, float duration, float[] frameWeights)
    {
        float totalWeight = 0f;
        foreach (float weight in frameWeights)
        {
            totalWeight += System.Math.Max(0.001f, weight);
        }

        float targetWeight = MathHelper.Clamp(elapsed / duration, 0f, 0.999f) * totalWeight;
        float accumulatedWeight = 0f;
        for (int frameIndex = 0; frameIndex < frameWeights.Length; frameIndex++)
        {
            accumulatedWeight += System.Math.Max(0.001f, frameWeights[frameIndex]);
            if (targetWeight < accumulatedWeight)
            {
                return frameIndex;
            }
        }

        return frameWeights.Length - 1;
    }

    // Maps the jump physics to the ordered player_jump1..8 images.
    private int GetJumpFrameIndex()
    {
        if (_playerVelocityY < 0f)
        {
            if (!_isDoubleJumping && _playerJumpOffset < 16f)
            {
                return 0;
            }

            if (!_isDoubleJumping && _playerJumpOffset < 34f)
            {
                return 1;
            }

            if (_playerJumpOffset < 78f)
            {
                return 2;
            }

            return _playerJumpOffset < 116f ? 3 : 4;
        }

        if (_playerVelocityY < 180f)
        {
            return 4;
        }

        if (_playerVelocityY < 520f)
        {
            return 5;
        }

        if (_playerJumpOffset < 24f)
        {
            return 7;
        }

        return 6;
    }

    // Blinks the runner while damage invulnerability is active.
    private Color GetPlayerDamageBlinkColor()
    {
        if (_invulnerableTimer <= 0f)
        {
            return Color.White;
        }

        int blinkFrame = (int)(_invulnerableTimer * 18f);
        return blinkFrame % 2 == 0 ? Color.White * 0.35f : Color.LightPink;
    }

    // Starts a short visual impact cue for either shield blocks or direct damage.
    private void StartDamageFeedback(bool directHit)
    {
        _damageFlashTimer = directHit ? DamageFlashDuration : DamageFlashDuration * 0.55f;
        _screenShakeTimer = directHit ? DamageShakeDuration : DamageShakeDuration * 0.5f;
    }

    // Returns a tiny deterministic shake offset while the damage timer is active.
    private Vector2 GetScreenShakeOffset()
    {
        if (_screenShakeTimer <= 0f)
        {
            return Vector2.Zero;
        }

        float strength = _screenShakeTimer / DamageShakeDuration;
        return new Vector2(
            MathF.Sin(_screenShakeTimer * 84f) * DamageShakeMagnitude * strength,
            MathF.Cos(_screenShakeTimer * 67f) * DamageShakeMagnitude * strength);
    }

    // Draws a fast red wash over the screen after damage.
    private void DrawDamageFlash()
    {
        float alpha = 0.32f * (_damageFlashTimer / DamageFlashDuration);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, WindowWidth, WindowHeight), Color.Red * alpha);
    }

    // Looks up sprite art for tile content that has an added asset.
    private Texture2D? GetTileContentTexture(TileContent content)
    {
        // TODO: Add a barricade-specific sprite for obstacle tiles.
        return content switch
        {
            TileContent.Coin => _coinTexture,
            TileContent.LifeItem => _extraLifeTexture,
            TileContent.ScoreBooster => _scoreBoosterTexture,
            TileContent.StageItem => _shieldTexture,
            TileContent.RopeItem => _ropeTexture,
            TileContent.OutOfStageItem => _mysteryBoxTexture,
            TileContent.Collectible or TileContent.Item => _mysteryBoxTexture,
            TileContent.Projectile => _activeStage.Number == BossStageNumber ? _bossArrowTexture : _stageArrowTexture,
            TileContent.HomingProjectile => _bossArrowTexture,
            TileContent.Meteor => _meteorTexture,
            TileContent.Obstacle => _obstacleTexture,
            _ => null
        };
    }

    // Draws a readable side-view meteor warning and a diagonal incoming meteor.
    private void DrawMeteorFrontTile(Tile tile, float x, float scale, float groundY)
    {
        float distanceToRunner = x - RunnerX;
        if (distanceToRunner > MeteorFrontRevealDistance)
        {
            return;
        }

        float revealProgress = MathHelper.Clamp(1f - MathF.Max(0f, distanceToRunner) / MeteorFrontRevealDistance, 0f, 1f);
        float fallProgress = MathHelper.Clamp(MathF.Pow(revealProgress, 0.62f), 0f, 1f);
        Rectangle target = GetMeteorFrontTargetBounds(tile, x, scale, groundY);
        DrawMeteorTarget(target, revealProgress, true);

        int meteorSize = Math.Max(24, (int)(78f * scale));
        float impactX = target.Center.X - meteorSize / 2f;
        float impactY = groundY - meteorSize;
        float startX = impactX + MeteorDiagonalStartOffsetX * scale;
        float startY = impactY - MeteorDiagonalStartOffsetY * scale;
        Vector2 meteorPosition = new(
            MathHelper.Lerp(startX, impactX, fallProgress),
            MathHelper.Lerp(startY, impactY, fallProgress));
        Rectangle meteorBounds = new((int)meteorPosition.X, (int)meteorPosition.Y, meteorSize, meteorSize);

        Vector2 meteorCenter = new(meteorBounds.Center.X, meteorBounds.Center.Y);
        Vector2 trailEnd = meteorCenter + new Vector2(32f * scale, -36f * scale);
        Vector2 trailStart = trailEnd + new Vector2(92f * scale, -112f * scale);
        DrawLine(trailStart, trailEnd, Math.Max(6, (int)(12f * scale)), Color.OrangeRed * 0.34f);
        DrawLine(trailStart + new Vector2(18f * scale, -16f * scale), meteorCenter, Math.Max(3, (int)(5f * scale)), Color.Yellow * 0.42f);

        DrawTextureInBounds(_meteorTexture, meteorBounds, Color.White);
    }

    // Returns the ground target bounds used by both meteor warning art and collision.
    private static Rectangle GetMeteorFrontTargetBounds(Tile tile, float x, float scale, float groundY)
    {
        int targetWidth = Math.Max(44, (int)(116f * scale));
        int targetHeight = Math.Max(14, (int)(34f * scale));
        int targetX = (int)(x + (TileVisualWidth(tile) * scale - targetWidth) / 2f);
        int targetY = (int)(groundY - targetHeight * 0.75f);
        return new Rectangle(targetX, targetY, targetWidth, targetHeight);
    }

    // Draws a full-strength meteor warning for top-view scouting.
    private void DrawMeteorTarget(Rectangle bounds)
    {
        DrawMeteorTarget(bounds, 1f);
    }

    // Draws a round high-contrast bullseye warning by default.
    private void DrawMeteorTarget(Rectangle bounds, float intensity)
    {
        DrawMeteorTarget(bounds, intensity, false);
    }

    // Draws a high-contrast bullseye, optionally using flattened lane-shaped bounds.
    private void DrawMeteorTarget(Rectangle bounds, float intensity, bool useBoundsShape)
    {
        float alpha = MathHelper.Clamp(intensity, 0.35f, 1f);
        Rectangle circleBounds = useBoundsShape ? bounds : SquareInside(bounds);
        DrawFilledEllipse(circleBounds.InflateBy(4), Color.Black * 0.26f);
        DrawEllipseRing(circleBounds.InflateBy(3), Math.Max(2, circleBounds.Width / 16), Color.White * (0.40f + 0.22f * alpha));
        DrawFilledEllipse(circleBounds, Color.Red * (0.16f + 0.16f * alpha));
        DrawEllipseRing(circleBounds, Math.Max(2, circleBounds.Width / 14), Color.Red * (0.72f + 0.2f * alpha));
        DrawEllipseRing(circleBounds.InflateBy(-circleBounds.Width / 4), Math.Max(2, circleBounds.Width / 18), Color.OrangeRed * (0.82f + 0.12f * alpha));

        int dotSize = Math.Max(4, circleBounds.Width / 7);
        Rectangle centerDot = new(
            circleBounds.Center.X - dotSize / 2,
            circleBounds.Center.Y - dotSize / 2,
            dotSize,
            dotSize);
        DrawFilledEllipse(centerDot, Color.Red);

        int alertHeight = Math.Max(8, circleBounds.Height / 3);
        int alertWidth = Math.Max(2, circleBounds.Width / 12);
        Rectangle alertStem = new(
            circleBounds.Center.X - alertWidth / 2,
            circleBounds.Center.Y - alertHeight / 2,
            alertWidth,
            alertHeight);
        Rectangle alertDot = new(
            circleBounds.Center.X - alertWidth / 2,
            circleBounds.Center.Y + alertHeight / 3,
            alertWidth,
            alertWidth);
        _spriteBatch.Draw(_pixel, alertStem, Color.White * (0.55f + 0.25f * alpha));
        _spriteBatch.Draw(_pixel, alertDot, Color.White * (0.55f + 0.25f * alpha));
    }

    // Draws an asset centered inside a gameplay rectangle without squashing its pixels.
    private void DrawTextureInBounds(Texture2D texture, Rectangle bounds, Color color)
    {
        float scale = System.Math.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height);
        int width = System.Math.Max(1, (int)(texture.Width * scale));
        int height = System.Math.Max(1, (int)(texture.Height * scale));
        Rectangle destination = new(
            bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2,
            width,
            height);

        _spriteBatch.Draw(texture, destination, color);
    }

    // Draws one frame from a sprite sheet centered inside a gameplay rectangle.
    private void DrawTextureInBounds(Texture2D texture, Rectangle source, Rectangle bounds, Color color)
    {
        float scale = System.Math.Min(bounds.Width / (float)source.Width, bounds.Height / (float)source.Height);
        int width = System.Math.Max(1, (int)(source.Width * scale));
        int height = System.Math.Max(1, (int)(source.Height * scale));
        Rectangle destination = new(
            bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2,
            width,
            height);

        _spriteBatch.Draw(texture, destination, source, color);
    }

    // Draws a thick line using the shared one-pixel texture.
    private void DrawLine(Vector2 start, Vector2 end, int thickness, Color color)
    {
        Vector2 delta = end - start;
        float length = delta.Length();
        if (length <= 0f || thickness <= 0)
        {
            return;
        }

        float rotation = MathF.Atan2(delta.Y, delta.X);
        _spriteBatch.Draw(
            _pixel,
            start,
            null,
            color,
            rotation,
            new Vector2(0f, 0.5f),
            new Vector2(length, thickness),
            SpriteEffects.None,
            0f);
    }

    // Returns the largest centered square that fits inside a rectangle.
    private static Rectangle SquareInside(Rectangle bounds)
    {
        int size = Math.Min(bounds.Width, bounds.Height);
        return new Rectangle(
            bounds.X + (bounds.Width - size) / 2,
            bounds.Y + (bounds.Height - size) / 2,
            size,
            size);
    }

    // Fills an ellipse using horizontal pixel spans.
    private void DrawFilledEllipse(Rectangle bounds, Color color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        float radiusX = bounds.Width / 2f;
        float radiusY = bounds.Height / 2f;
        float centerX = bounds.X + radiusX;
        float centerY = bounds.Y + radiusY;

        for (int y = bounds.Top; y < bounds.Bottom; y++)
        {
            float normalizedY = (y + 0.5f - centerY) / radiusY;
            float halfWidth = radiusX * MathF.Sqrt(MathF.Max(0f, 1f - normalizedY * normalizedY));
            int left = (int)MathF.Ceiling(centerX - halfWidth);
            int right = (int)MathF.Floor(centerX + halfWidth);

            if (right >= left)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(left, y, right - left + 1, 1), color);
            }
        }
    }

    // Draws an ellipse outline by painting only the outer band of an ellipse.
    private void DrawEllipseRing(Rectangle bounds, int thickness, Color color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || thickness <= 0)
        {
            return;
        }

        float outerRadiusX = bounds.Width / 2f;
        float outerRadiusY = bounds.Height / 2f;
        float innerRadiusX = MathF.Max(0f, outerRadiusX - thickness);
        float innerRadiusY = MathF.Max(0f, outerRadiusY - thickness);
        float centerX = bounds.X + outerRadiusX;
        float centerY = bounds.Y + outerRadiusY;

        for (int y = bounds.Top; y < bounds.Bottom; y++)
        {
            float outerNormalizedY = (y + 0.5f - centerY) / outerRadiusY;
            if (MathF.Abs(outerNormalizedY) > 1f)
            {
                continue;
            }

            float outerHalfWidth = outerRadiusX * MathF.Sqrt(MathF.Max(0f, 1f - outerNormalizedY * outerNormalizedY));
            int outerLeft = (int)MathF.Ceiling(centerX - outerHalfWidth);
            int outerRight = (int)MathF.Floor(centerX + outerHalfWidth);

            if (innerRadiusX <= 0f || innerRadiusY <= 0f)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(outerLeft, y, outerRight - outerLeft + 1, 1), color);
                continue;
            }

            float innerNormalizedY = (y + 0.5f - centerY) / innerRadiusY;
            if (MathF.Abs(innerNormalizedY) >= 1f)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(outerLeft, y, outerRight - outerLeft + 1, 1), color);
                continue;
            }

            float innerHalfWidth = innerRadiusX * MathF.Sqrt(MathF.Max(0f, 1f - innerNormalizedY * innerNormalizedY));
            int innerLeft = (int)MathF.Ceiling(centerX - innerHalfWidth);
            int innerRight = (int)MathF.Floor(centerX + innerHalfWidth);

            if (innerLeft > outerLeft)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(outerLeft, y, innerLeft - outerLeft, 1), color);
            }

            if (outerRight > innerRight)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(innerRight + 1, y, outerRight - innerRight, 1), color);
            }
        }
    }

    // Draws a simple rectangle outline using the shared one-pixel texture.
    private void DrawRectangleOutline(Rectangle bounds, int thickness, Color color)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }

    // Chooses the placeholder draw color for a tile based on content first, then tile type.
    private Color GetTileColor(Tile tile)
    {
        return tile.Content switch
        {
            TileContent.Coin => Color.Gold,
            TileContent.LifeItem => Color.LightPink,
            TileContent.ScoreBooster => Color.Cyan,
            TileContent.StageItem => Color.DeepSkyBlue,
            TileContent.RopeItem => Color.SandyBrown,
            TileContent.OutOfStageItem => Color.LightGreen,
            TileContent.Projectile => Color.OrangeRed,
            TileContent.HomingProjectile => Color.Magenta,
            TileContent.Meteor => Color.Red,
            TileContent.Obstacle => Color.DarkRed,
            TileContent.Boss => Color.Purple,
            TileContent.Collectible or TileContent.Item => Color.LightGreen,
            _ when tile.Type == TileType.Branch => Color.LightGreen,
            _ when tile.Type == TileType.Merge => Color.SeaGreen,
            _ => Color.White
        };
    }

    // Returns a content-specific placeholder width for front-view tile rendering.
    private static float TileVisualWidth(Tile tile)
    {
        return tile.Content switch
        {
            TileContent.Projectile => 144f,
            TileContent.HomingProjectile => 88f,
            TileContent.Coin => 68f,
            TileContent.Meteor => 108f,
            TileContent.Boss => 236f,
            _ => 124f
        };
    }

    // Returns a content-specific placeholder height for front-view tile rendering.
    private static float TileVisualHeight(Tile tile)
    {
        return tile.Content switch
        {
            TileContent.Projectile => 36f,
            TileContent.HomingProjectile => 88f,
            TileContent.Coin => 68f,
            TileContent.Meteor => 108f,
            TileContent.Boss => 240f,
            TileContent.Obstacle => 108f,
            TileContent.ScoreBooster => 88f,
            TileContent.LifeItem => 84f,
            TileContent.StageItem => 84f,
            TileContent.RopeItem => 84f,
            TileContent.OutOfStageItem => 84f,
            _ => 144f
        };
    }

    // Converts the final score into a bronze, silver, or gold star count.
    private int CalculateStarRating(int score)
    {
        if (_activeStage.Number == BossStageNumber && !_bossDefeated)
        {
            return 0;
        }

        if (score >= _activeStage.GoldScore)
        {
            return 3;
        }

        if (score >= _activeStage.SilverScore)
        {
            return 2;
        }

        return score >= _activeStage.BronzeScore ? 1 : 0;
    }

    // Detects a key press on the frame it transitions from up to down.
    private bool IsNewKeyPress(KeyboardState keyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
    }

}

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
    private const float PlayerCollisionWidth = 64f;
    private const int MaxUserIdLength = 16;
    private const int BossStageNumber = 3;
    private const int BossSurvivalScoreBonus = 1600;
    private const float BossIntroDuration = 2.4f;
    private const float BossFightDuration = 18f;
    private const float BossAttackInitialDelay = 0.75f;
    private const float BossAttackMinInterval = 0.55f;
    private const float BossAttackMaxInterval = 1.05f;
    private const string BossName = "THE SUNKEN IDOL";
    private const float MapProjectileActivationPadding = 220f;
    private const float MapProjectileCollisionWidth = 56f;
    private const float MapProjectileStandardSpeed = 520f;
    private const float MapProjectileHomingSpeed = 430f;
    private const float MapProjectileHomingInterval = 0.42f;
    private const float MeteorFrontRevealDistance = 270f;
    private const float MeteorJumpClearHeight = 120f;

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
        if (IsRepeatingKeyPress(keyboard, Keys.Down, ref _menuDownRepeatTimer, deltaSeconds))
        {
            _mainMenuSelection = (_mainMenuSelection + 1) % _mainMenuOptions.Length;
        }

        if (IsRepeatingKeyPress(keyboard, Keys.Up, ref _menuUpRepeatTimer, deltaSeconds))
        {
            _mainMenuSelection = (_mainMenuSelection + _mainMenuOptions.Length - 1) % _mainMenuOptions.Length;
        }

        if (IsNewKeyPress(keyboard, Keys.Enter))
        {
            EnsureCurrentUser();

            if (_mainMenuSelection == 0)
            {
                _screen = GameScreen.StageSelect;
            }
            else if (_mainMenuSelection == 1)
            {
                _screen = GameScreen.HowToPlay;
            }
            else if (_mainMenuSelection == 2)
            {
                ToggleSound();
            }
            else
            {
                _currentUser = null;
                _saveFile.LastUserId = string.Empty;
                _typedUserId = string.Empty;
                _mainMenuSelection = 0;
                _menuFocus = MenuFocus.UserId;
                _menuMessage = "Enter user id, then press Enter.";
                SaveSaveFile();
            }
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
        foreach (Keys key in keyboard.GetPressedKeys())
        {
            if (!_previousKeyboard.IsKeyUp(key))
            {
                continue;
            }

            if (key == Keys.Back && _typedUserId.Length > 0)
            {
                _typedUserId = _typedUserId[..^1];
                continue;
            }

            if (key == Keys.Enter)
            {
                LoadCurrentUserFromTypedId();
                continue;
            }

            if (TryGetUserIdCharacter(key, keyboard, out char character) && _typedUserId.Length < MaxUserIdLength)
            {
                _typedUserId += character;
            }
        }
    }

    // Handles stage-select navigation and starts the selected stage.
    private void UpdateStageSelect(KeyboardState keyboard)
    {
        if (IsNewKeyPress(keyboard, Keys.Escape))
        {
            _screen = GameScreen.MainMenu;
            _audioManager.PlaySongForLevel(0);
        }

        if (IsNewKeyPress(keyboard, Keys.Right))
        {
            _selectedStage = (_selectedStage + 1) % _stages.Length;
        }

        if (IsNewKeyPress(keyboard, Keys.Left))
        {
            _selectedStage = (_selectedStage + _stages.Length - 1) % _stages.Length;
        }

        if (IsNewKeyPress(keyboard, Keys.Up))
        {
            CycleDifficulty(1);
        }

        if (IsNewKeyPress(keyboard, Keys.Down))
        {
            CycleDifficulty(-1);
        }

        if (IsNewKeyPress(keyboard, Keys.Enter))
        {
            StartRun();
        }
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
            }
            else if (_canDoubleJump)
            {
                _playerVelocityY = DoubleJumpVelocity;
                _canDoubleJump = false;
            }
        }

        if (IsNewKeyPress(keyboard, Keys.Down))
        {
            _slideTimer = 0.55f;
        }

        if (IsNewKeyPress(keyboard, Keys.R) && _ropeTimer <= 0f && _ropeItemCharges > 0 && !IsBossEncounterActive())
        {
            _ropeItemCharges--;
            _ropeTimer = 1.8f;
        }

        UpdatePlayerActionTimers(deltaSeconds);

        _worldScroller.Speed = IsBossEncounterActive() ? 0f : Constants.ScrollSpeed * (_ropeTimer > 0f ? 1.6f : 1f);
        _runAnimationTimer += deltaSeconds * (_worldScroller.Speed / Constants.ScrollSpeed);
        float previousDistance = _distance;
        _worldScroller.Update(deltaSeconds);
        _distance = _worldScroller.OffsetX;
        _distanceScore += System.Math.Max(0f, _distance - previousDistance) * GetScoreMultiplier();
        _segmentProgress += _worldScroller.Speed * deltaSeconds;
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
            _gameOverTitle = "Stage Clear";
            _gameOverDetail = $"Score {_score}";
            SaveStageProgress();
            _screen = GameScreen.GameOver;
        }
    }

    // Handles the game-over screen shortcuts back to stage select or the main menu.
    private void UpdateGameOver(KeyboardState keyboard)
    {
        if (IsNewKeyPress(keyboard, Keys.Enter))
        {
            _screen = GameScreen.StageSelect;
        }

        if (IsNewKeyPress(keyboard, Keys.Escape))
        {
            _screen = GameScreen.MainMenu;
        }
    }

    // Draws the prototype main menu and current local user id.
    private void DrawMainMenu()
    {
        _spriteBatch.Draw(_mainMenuBackground, new Rectangle(0, 0, WindowWidth, WindowHeight), Color.White);
        _spriteBatch.DrawString(_minecraftFont, "JUNGLE RUNNERS", new Vector2(92, 82), Color.DarkOliveGreen, 0f, Vector2.Zero, 5f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_minecraftFont, "JUNGLE RUNNERS", new Vector2(90, 80), Color.Gold, 0f, Vector2.Zero, 5f, SpriteEffects.None, 0f);
        PixelFont.Draw(_spriteBatch, _pixel, _menuMessage, 100, 180, 3, Color.White);
        string userText = string.IsNullOrWhiteSpace(_typedUserId) ? "USER : " : $"USER : {_typedUserId}";
        PixelFont.Draw(_spriteBatch, _pixel, userText, 100, 220, 3, _menuFocus == MenuFocus.UserId ? Color.Gold : Color.White);

        if (_currentUser is null)
        {
            return;
        }

        int completedStages = _currentUser.StageProgress.Values.Count(progress => progress.IsCompleted);
        PixelFont.Draw(_spriteBatch, _pixel, $"BEST SCORE {_currentUser.BestScore}", 100, 260, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, $"STAGES CLEARED {completedStages}", 100, 300, 3, Color.White);

        for (int i = 0; i < _mainMenuOptions.Length; i++)
        {
            Color color = i == _mainMenuSelection ? Color.LimeGreen : Color.White;
            string prefix = i == _mainMenuSelection ? "> " : "  ";
            PixelFont.Draw(_spriteBatch, _pixel, prefix + _mainMenuOptions[i], 120, 360 + i * 54, 4, color);
        }
    }

    // Draws the current stage card and stage-select instructions.
    private void DrawStageSelect()
    {
        StageDefinition stage = _stages[_selectedStage];
        StageProgress? progress = GetCurrentStageProgress(stage.Number);
        DrawFullScreenTexture(_stageSelectBackgrounds[_selectedStage]);
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
        PixelFont.Draw(_spriteBatch, _pixel, "LEFT/RIGHT CHOOSE  ENTER START  ESC MENU", 100, 610, 3, Color.White);
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

    // Draws the side-view prototype using lane bands, sprites, active hazards, and the runner.
    private void DrawFrontView()
    {
        // TODO: Move this rendering into FrontViewRenderer and pass assets through AssetManager.
        // TODO: Add barricade sprites, rope feedback, jump/slide sprites, and more run frames.
        // TODO: Tune lane grounding so player, obstacles, and projectiles sit on the lane surface.
        DrawGameplayBackground();
        DrawFrontLanes();

        foreach (Tile tile in _activeStageData.World.AllTiles.OrderByDescending(tile => tile.Row))
        {
            float x = GetTileScreenX(tile.Column);
            if (x < -100f || x > WindowWidth + 100f)
            {
                continue;
            }

            if (tile.HasContent || tile.Type is TileType.Branch or TileType.Merge)
            {
                DrawFrontTile(tile, x);
            }
        }

        DrawMapProjectilesFront();

        float playerScale = _rowDepthMapper.GetScale(_playerRow);
        float playerHeight = _slideTimer > 0f ? 92f * playerScale : 176f * playerScale;
        float playerWidth = 92f * playerScale;
        float playerY = _rowDepthMapper.GetGroundY(_playerRow) - playerHeight - _playerJumpOffset;
        Color playerColor = GetPlayerDamageBlinkColor();
        Texture2D playerTexture = GetPlayerRunFrame();
        _spriteBatch.Draw(playerTexture, new Rectangle((int)RunnerX, (int)playerY, (int)playerWidth, (int)playerHeight), playerColor);
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
        PixelFont.Draw(_spriteBatch, _pixel, $"LIVES {_lives}", 970, 76, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, $"COINS {_coins}", 970, 110, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, $"ROPES {_ropeItemCharges}", 970, 144, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, $"SHIELDS {_stageItemShieldCharges}", 970, 178, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, !_audioManager.IsMuted ? "SOUND ON" : "SOUND OFF", 970, 212, 3, Color.White);
        if (_scoreBoostTimer > 0f)
        {
            PixelFont.Draw(_spriteBatch, _pixel, $"BOOST X10 {MathF.Ceiling(_scoreBoostTimer)}", 970, 246, 3, Color.Gold);
        }

        PixelFont.Draw(_spriteBatch, _pixel, "SPACE JUMP  DOWN SLIDE  R ROPE", 36, 28, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "HOLD V TOP VIEW  M SOUND  LEFT/RIGHT ROW", 36, 62, 3, Color.White);

        if (_awaitingRouteChoice && _activeStageData.CurrentNode is not null)
        {
            StageNode route = _activeStageData.CurrentNode.Next[_routeChoiceIndex];
            PixelFont.Draw(_spriteBatch, _pixel, "ROUTE CHOICE", 420, 330, 5, Color.Gold);
            PixelFont.Draw(_spriteBatch, _pixel, $"LEFT/RIGHT PICK  ENTER {route.Label}", 270, 405, 3, Color.White);
        }
    }

    // Draws the stage 3 boss arrival overlay, boss image, and attacks.
    private void DrawBossEncounter()
    {
        if (!IsBossEncounterActive())
        {
            return;
        }

        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, WindowWidth, WindowHeight), Color.Black * 0.45f);

        Rectangle bossImage = new(WindowWidth - 450, 220, 360, 390);
        DrawTextureInBounds(_bossBodyTexture, bossImage, Color.White);

        if (_bossIntroActive)
        {
            PixelFont.Draw(_spriteBatch, _pixel, $"{BossName} APPEARED", 210, 160, 5, Color.Gold);
            PixelFont.Draw(_spriteBatch, _pixel, "SURVIVE THE BATTLE TO CLEAR STAGE 3", 210, 245, 3, Color.White);
            PixelFont.Draw(_spriteBatch, _pixel, "ROPE DISABLED", 210, 295, 3, Color.OrangeRed);
            return;
        }

        foreach (BossAttack attack in _bossAttacks)
        {
            Vector2 size = GetBossAttackSize(attack);
            Rectangle attackBounds = new((int)attack.Position.X, (int)attack.Position.Y, (int)size.X, (int)size.Y);
            Texture2D attackTexture = attack.Kind == BossAttackKind.Spear ? _bossArrowTexture : _bossBoulderTexture;
            DrawTextureInBounds(attackTexture, attackBounds, Color.White);
        }

        PixelFont.Draw(_spriteBatch, _pixel, $"{BossName} {MathF.Ceiling(_bossFightTimer)}", 220, 120, 4, Color.Gold);
        PixelFont.Draw(_spriteBatch, _pixel, "SLIDE SPEARS  JUMP BOULDERS", 220, 180, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ROPE DISABLED", 220, 220, 3, Color.OrangeRed);
    }

    // Draws the stage-clear or game-over result screen.
    private void DrawGameOver()
    {
        Color titleColor = _runWon ? Color.Gold : Color.OrangeRed;
        PixelFont.Draw(_spriteBatch, _pixel, _gameOverTitle, 120, 160, 8, titleColor);
        PixelFont.Draw(_spriteBatch, _pixel, _gameOverDetail, 130, 285, 4, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ENTER STAGE SELECT  ESC MENU", 130, 420, 3, Color.White);
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
        _spriteBatch.Draw(texture, new Rectangle(0, 0, WindowWidth, WindowHeight), Color.White);
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

        // Apply selected difficulty to stage generation, lives, scoring targets, and hazard speed.
        _activeStage = _stages[_selectedStage];
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
        _playerRow = Constants.MiddleLayer;
        _slideTimer = 0f;
        _ropeTimer = 0f;
        _scoreBoostTimer = 0f;
        _invulnerableTimer = 0f;
        _damageFlashTimer = 0f;
        _screenShakeTimer = 0f;
        _runAnimationTimer = 0f;
        _lives = 4 - (_selectedStage + 1);
        _coins = 0;
        _boosters = 0;
        _coinScore = 0;
        _stageItemShieldCharges = 0;
        _ropeItemCharges = 0;
        _score = 0;
        _runWon = false;
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

    // Moves the run into the selected graph node and aligns the player with that route.
    private void AdvanceToRoute(StageNode nextNode)
    {
        _activeStageData.CurrentNode = nextNode;
        _activeSegment = nextNode.Segment;
        _segmentProgress = 0f;
        _playerRow = nextNode.Segment.PreferredRow;
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
        if (string.IsNullOrWhiteSpace(_typedUserId))
        {
            _menuMessage = "Type an id first.";
            return;
        }

        string userId = _typedUserId.Trim().ToUpperInvariant();
        _typedUserId = userId;

        bool isNewUser = false;
        if (!TryGetSavedUser(userId, out UserProfile? user) || user is null)
        {
            user = new UserProfile
            {
                UserId = userId
            };
            _saveFile.Users[userId] = user;
            isNewUser = true;
        }

        NormalizeUserProfile(userId, user);
        _currentUser = user;
        _saveFile.LastUserId = _currentUser.UserId;
        _soundEnabled = _currentUser.Settings.SoundEnabled;
        if (!_soundEnabled) _audioManager.ToggleMute();
        _selectedDifficulty = _currentUser.Settings.Difficulty;
        _viewMode = ViewMode.Front;
        _menuFocus = MenuFocus.Options;
        _menuMessage = isNewUser ? $"Created profile {_currentUser.UserId}." : $"Loaded profile {_currentUser.UserId}.";
        SaveSaveFile();
    }

    // Looks up profiles without making user ids case-sensitive.
    private bool TryGetSavedUser(string userId, out UserProfile? user)
    {
        if (_saveFile.Users.TryGetValue(userId, out user))
        {
            return true;
        }

        string? matchingKey = _saveFile.Users.Keys.FirstOrDefault(key => string.Equals(key, userId, StringComparison.OrdinalIgnoreCase));
        if (matchingKey is null)
        {
            return false;
        }

        user = _saveFile.Users[matchingKey];
        if (matchingKey != userId)
        {
            _saveFile.Users.Remove(matchingKey);
            _saveFile.Users[userId] = user;
        }

        return true;
    }

    // Reads saved stage progress for the active user.
    private StageProgress? GetCurrentStageProgress(int stageNumber)
    {
        if (_currentUser is null)
        {
            return null;
        }

        return _currentUser.StageProgress.TryGetValue(stageNumber, out StageProgress? progress) ? progress : null;
    }

    // Persists score, stars, lives, and completion data for the current local user.
    private void SaveStageProgress()
    {
        // TODO: Unlock the next stage on clear, then show a per-user scoreboard at round end.
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

    // Toggles sound preference and saves it when a profile is active.
    private void ToggleSound()
    {
        _soundEnabled = !_soundEnabled;
        _audioManager.ToggleMute();
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
            }
        }

        _slideTimer = System.Math.Max(0f, _slideTimer - deltaSeconds);
        _ropeTimer = System.Math.Max(0f, _ropeTimer - deltaSeconds);
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
        _bossEncounterStarted = false;
        _bossIntroActive = false;
        _bossFightActive = false;
        _bossDefeated = false;
        _bossIntroTimer = 0f;
        _bossFightTimer = 0f;
        _bossAttackTimer = 0f;
        _bossSurvivalBonus = 0;
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
        _ropeTimer = 0f;
        _scoreBoostTimer = 0f;

        foreach (Tile tile in _activeStageData.World.AllTiles.Where(tile => tile.Content == TileContent.Boss))
        {
            tile.Content = TileContent.Empty;
        }
    }

    // Advances the boss intro, timed fight, and projectile/object attacks.
    private void UpdateBossEncounter(float deltaSeconds)
    {
        // TODO: Add boss movement patterns instead of keeping the boss sprite fixed on screen.
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

        if (_bossAttackTimer <= 0f)
        {
            SpawnBossAttack();
            _bossAttackTimer = BossAttackMinInterval + (float)_runRandom.NextDouble() * (BossAttackMaxInterval - BossAttackMinInterval);
        }

        UpdateBossAttacks(deltaSeconds);
        if (_screen != GameScreen.Playing)
        {
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
    }

    // Marks the boss as beaten and clears stage 3 immediately.
    private void CompleteBossFight()
    {
        _bossFightActive = false;
        _bossDefeated = true;
        _bossSurvivalBonus = BossSurvivalScoreBonus;
        _bossAttacks.Clear();
        _score = CalculateRunScore();
        _runWon = true;
        _gameOverTitle = "Stage Clear";
        _gameOverDetail = $"Score {_score}";
        SaveStageProgress();
        _screen = GameScreen.GameOver;
    }

    // Creates one boss attack in a random lane.
    private void SpawnBossAttack()
    {
        BossAttackKind kind = _runRandom.NextDouble() < 0.52 ? BossAttackKind.Spear : BossAttackKind.Boulder;
        int row = _runRandom.Next(Constants.FrontLayer, Constants.BackLayer + 1);
        float scale = _rowDepthMapper.GetScale(row);
        float groundY = _rowDepthMapper.GetGroundY(row);
        float y = kind == BossAttackKind.Spear ? groundY - 118f * scale : groundY - 72f * scale;
        float speed = kind == BossAttackKind.Spear ? 620f : 470f;

        _bossAttacks.Add(new BossAttack(kind, row, new Vector2(WindowWidth - 330f, y), speed));
    }

    // Moves boss attacks and applies damage if the player misses the correct dodge.
    private void UpdateBossAttacks(float deltaSeconds)
    {
        for (int i = _bossAttacks.Count - 1; i >= 0; i--)
        {
            BossAttack attack = _bossAttacks[i];
            attack.Position = new Vector2(attack.Position.X - attack.Speed * deltaSeconds, attack.Position.Y);
            Vector2 size = GetBossAttackSize(attack);

            bool overlapsRunner = attack.Position.X < RunnerX + PlayerCollisionWidth && attack.Position.X + size.X > RunnerX - PlayerCollisionWidth;
            if (!attack.HasCheckedCollision && overlapsRunner && attack.Row == _playerRow)
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

            if (attack.Position.X + size.X < -40f)
            {
                _bossAttacks.RemoveAt(i);
            }
        }
    }

    // Returns screen-space attack dimensions with lane-depth scaling.
    private Vector2 GetBossAttackSize(BossAttack attack)
    {
        float scale = _rowDepthMapper.GetScale(attack.Row);
        return attack.Kind == BossAttackKind.Spear
            ? new Vector2(160f * scale, 30f * scale)
            : new Vector2(72f * scale, 72f * scale);
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
            if (x > WindowWidth + MapProjectileActivationPadding || x < RunnerX - PlayerCollisionWidth)
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

    // Moves active map projectiles toward the runner, including homing lane changes.
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

            Vector2 size = GetMapProjectileSize(projectile);
            bool overlapsRunner = projectile.Position.X < RunnerX + MapProjectileCollisionWidth
                && projectile.Position.X + size.X > RunnerX - MapProjectileCollisionWidth;
            if (!projectile.HasCheckedCollision && overlapsRunner && projectile.Row == _playerRow)
            {
                bool avoided = projectile.Kind == TileContent.Projectile ? _slideTimer > 0f : _playerJumpOffset >= 56f;
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

            if (projectile.Position.X + size.X < -40f)
            {
                _mapProjectiles.RemoveAt(i);
            }
        }
    }

    // Draws active flying map projectiles in front-view lane space.
    private void DrawMapProjectilesFront()
    {
        foreach (MapProjectile projectile in _mapProjectiles.OrderByDescending(projectile => projectile.Row))
        {
            Vector2 size = GetMapProjectileSize(projectile);
            Rectangle bounds = new((int)projectile.Position.X, (int)projectile.Position.Y, (int)size.X, (int)size.Y);
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

    // Returns the front-view y position for a flying projectile in its current row.
    private float GetMapProjectileY(int row, TileContent kind)
    {
        float scale = _rowDepthMapper.GetScale(row);
        float groundY = _rowDepthMapper.GetGroundY(row);
        return kind == TileContent.HomingProjectile ? groundY - 112f * scale : groundY - 92f * scale;
    }

    // Returns screen-space projectile size with row depth applied.
    private Vector2 GetMapProjectileSize(MapProjectile projectile)
    {
        float scale = _rowDepthMapper.GetScale(projectile.Row);
        return projectile.Kind == TileContent.HomingProjectile
            ? new Vector2(74f * scale, 74f * scale)
            : new Vector2(138f * scale, 30f * scale);
    }

    // Uses different placeholder colors for straight and homing map projectiles.
    private static Color GetMapProjectileColor(MapProjectile projectile)
    {
        return projectile.Kind == TileContent.HomingProjectile ? Color.Magenta : Color.OrangeRed;
    }

    // Checks the runner against nearby tile content and applies pickups or damage.
    private void ResolveGridInteractions()
    {
        // TODO: Replace screen-x proximity checks with ColliderComponent/CollisionHelper so player,
        // items, projectiles, obstacles, boss attacks, jump arcs, and slide bounds share precise hitboxes.
        foreach (Tile tile in _activeStageData.World.AllTiles)
        {
            if (!tile.HasContent || tile.Row != _playerRow)
            {
                continue;
            }

            float x = GetTileScreenX(tile.Column);
            bool isAtRunner = x > RunnerX - PlayerCollisionWidth && x < RunnerX + PlayerCollisionWidth;
            if (!isAtRunner)
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
                    _lives = System.Math.Min(5, _lives + 1);
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
                    _ropeItemCharges++;
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
                    if (_playerJumpOffset < MeteorJumpClearHeight)
                    {
                        DamagePlayer();
                    }
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.Boss:
                    StartBossEncounter();
                    break;
                case TileContent.Obstacle:
                    if (_playerJumpOffset < 56f)
                    {
                        DamagePlayer();
                    }
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
            _gameOverTitle = "Game Over";
            _gameOverDetail = $"Score {_score}";
            SaveStageProgress();
            _screen = GameScreen.GameOver;
        }
    }

    // Draws depth-sorted lane bands for the front-view prototype.
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

            _spriteBatch.Draw(_pixel, new Rectangle(0, (int)(y - 10f * scale), WindowWidth, (int)(70f * scale)), laneColor * 0.5f);
        }
    }

    // Draws one visible tile or tile content marker in the front-view lane space.
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

        int width = (int)(TileVisualWidth(tile) * scale);
        int height = (int)(TileVisualHeight(tile) * scale);
        int y = (int)(groundY - height);
        Rectangle destination = new((int)x, y, width, height);

        Texture2D? texture = GetTileContentTexture(tile.Content);
        if (texture is not null)
        {
            DrawTextureInBounds(texture, destination, Color.White);
            return;
        }

        _spriteBatch.Draw(_pixel, destination, color);
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

        int playerX = (int)(originX + RunnerX * (cell / GameplayTileSpacing));
        int playerDisplayRow = _activeStageData.World.Rows - 1 - _playerRow;
        int playerY = originY + playerDisplayRow * (cell + 18);
        DrawTextureInBounds(GetPlayerRunFrame(), new Rectangle(playerX, playerY, tileSize, tileSize), Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ROW 3", 28, originY + 8, 2, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ROW 2", 28, originY + cell + 26, 2, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ROW 1", 28, originY + (cell + 18) * 2 + 8, 2, Color.White);
    }

    // Converts a stage grid column into the current screen-space x position.
    private float GetTileScreenX(int column)
    {
        return SpawnScreenOffset + column * GameplayTileSpacing - _worldScroller.OffsetX;
    }

    // Returns the current runner frame from the two loaded player poses.
    private Texture2D GetPlayerRunFrame()
    {
        // TODO: Add more run frames and choose separate jump, slide, and rope sprites by player state.
        int frameIndex = (int)(_runAnimationTimer / RunnerFrameTime) % _playerRunFrames.Length;
        return _playerRunFrames[frameIndex];
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
            TileContent.RopeItem => _mysteryBoxTexture,
            TileContent.OutOfStageItem => _mysteryBoxTexture,
            TileContent.Collectible or TileContent.Item => _mysteryBoxTexture,
            TileContent.Projectile => _stageArrowTexture,
            TileContent.HomingProjectile => _bossArrowTexture,
            TileContent.Meteor => _meteorTexture,
            TileContent.Obstacle => _obstacleTexture,
            _ => null
        };
    }

    // Draws a late side-view meteor tell so top-view scouting matters.
    private void DrawMeteorFrontTile(Tile tile, float x, float scale, float groundY)
    {
        float distanceToRunner = x - RunnerX;
        if (distanceToRunner > MeteorFrontRevealDistance)
        {
            return;
        }

        float revealProgress = MathHelper.Clamp(1f - MathF.Max(0f, distanceToRunner) / MeteorFrontRevealDistance, 0f, 1f);
        int targetWidth = (int)(TileVisualWidth(tile) * scale);
        int targetHeight = Math.Max(8, (int)(18f * scale));
        Rectangle target = new((int)x, (int)(groundY - targetHeight), targetWidth, targetHeight);
        Color targetColor = Color.Lerp(Color.OrangeRed, Color.Yellow, revealProgress) * 0.68f;
        _spriteBatch.Draw(_pixel, target, targetColor);
        DrawRectangleOutline(target, Math.Max(2, (int)(3f * scale)), Color.Red * 0.82f);

        int meteorSize = Math.Max(24, (int)(78f * scale));
        float startY = groundY - 250f * scale;
        float impactY = groundY - meteorSize;
        int meteorX = target.X + (target.Width - meteorSize) / 2;
        int meteorY = (int)MathHelper.Lerp(startY, impactY, revealProgress);
        Rectangle meteorBounds = new(meteorX, meteorY, meteorSize, meteorSize);
        Rectangle trail = new(
            meteorBounds.X + meteorBounds.Width / 3,
            meteorBounds.Y - Math.Max(8, (int)(36f * scale)),
            Math.Max(8, meteorBounds.Width / 3),
            Math.Max(12, (int)(44f * scale)));

        _spriteBatch.Draw(_pixel, trail, Color.OrangeRed * 0.45f);
        DrawTextureInBounds(_meteorTexture, meteorBounds, Color.White);
    }

    // Marks meteor impact tiles clearly in top view before the meteor is visible in side view.
    private void DrawMeteorTarget(Rectangle bounds)
    {
        _spriteBatch.Draw(_pixel, bounds, Color.OrangeRed * 0.18f);
        DrawRectangleOutline(bounds, 3, Color.OrangeRed);

        int centerX = bounds.X + bounds.Width / 2;
        int centerY = bounds.Y + bounds.Height / 2;
        _spriteBatch.Draw(_pixel, new Rectangle(centerX - 2, bounds.Y + 6, 4, bounds.Height - 12), Color.Gold);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X + 6, centerY - 2, bounds.Width - 12, 4), Color.Gold);

        Rectangle inner = new(bounds.X + bounds.Width / 4, bounds.Y + bounds.Height / 4, bounds.Width / 2, bounds.Height / 2);
        DrawRectangleOutline(inner, 2, Color.Red);
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

    // Converts new key presses into supported user id characters.
    private static bool TryGetUserIdCharacter(Keys key, KeyboardState keyboard, out char character)
    {
        if (key >= Keys.A && key <= Keys.Z)
        {
            character = (char)('A' + (int)key - (int)Keys.A);
            return true;
        }

        if (key >= Keys.D0 && key <= Keys.D9)
        {
            character = (char)('0' + (int)key - (int)Keys.D0);
            return true;
        }

        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
        {
            character = (char)('0' + (int)key - (int)Keys.NumPad0);
            return true;
        }

        if (key == Keys.OemMinus)
        {
            bool shiftHeld = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
            character = shiftHeld ? '_' : '-';
            return true;
        }

        if (key == Keys.OemPeriod)
        {
            character = '.';
            return true;
        }

        character = '\0';
        return false;
    }

    // Detects an initial key press, then repeats while the key is held for menu navigation.
    private bool IsRepeatingKeyPress(KeyboardState keyboard, Keys key, ref float repeatTimer, float deltaSeconds)
    {
        if (keyboard.IsKeyUp(key))
        {
            repeatTimer = 0f;
            return false;
        }

        if (_previousKeyboard.IsKeyUp(key))
        {
            repeatTimer = MenuKeyRepeatInitialDelay;
            return true;
        }

        repeatTimer -= deltaSeconds;
        if (repeatTimer > 0f)
        {
            return false;
        }

        repeatTimer += MenuKeyRepeatInterval;
        return true;
    }
}

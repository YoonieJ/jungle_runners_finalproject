using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace jungle_runners_finalproject;

public partial class Game1
{
    private const float GameplayTileSpacing = 150f;
    private const float SpawnScreenOffset = 620f;
    private const float PlayerCollisionWidth = 64f;

    // Loads local save data and guarantees the user dictionary is ready to use.
    private void LoadSaveFile()
    {
        _saveFile = SaveManager.LoadData<SaveFile>(_savePath, _jsonOptions);
        _saveFile.Users ??= [];
    }

    // Writes the current local save data to disk.
    private void SaveSaveFile()
    {
        SaveManager.SaveData(_savePath, _saveFile, _jsonOptions);
    }

    // Restores the most recently used local profile when it still exists in the save file.
    private void TryRestoreLastUser()
    {
        if (string.IsNullOrWhiteSpace(_saveFile.LastUserId))
        {
            return;
        }

        if (_saveFile.Users.TryGetValue(_saveFile.LastUserId, out UserProfile? restoredUser))
        {
            _currentUser = restoredUser;
            _typedUserId = _currentUser.UserId;
            _soundEnabled = _currentUser.Settings.SoundEnabled;
            _selectedDifficulty = _currentUser.Settings.Difficulty;
            _viewMode = _currentUser.Settings.ViewMode;
            _menuMessage = $"Welcome back, {_currentUser.UserId}.";
        }
    }

    // Handles main menu navigation, user profile creation, sound toggling, and logout.
    private void UpdateMainMenu(KeyboardState keyboard)
    {
        // TODO: Finish the login/settings menu: handle typing into UserId
        // and expose difficulty instead of only toggling sound.
        if (IsNewKeyPress(keyboard, Keys.Down))
        {
            _mainMenuSelection = (_mainMenuSelection + 1) % _mainMenuOptions.Length;
        }

        if (IsNewKeyPress(keyboard, Keys.Up))
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
                ToggleSound();
            }
            else
            {
                _currentUser = null;
                _saveFile.LastUserId = string.Empty;
                _menuMessage = "Logged out.";
                SaveSaveFile();
            }
        }
    }

    // Handles stage-select navigation and starts the selected stage.
    private void UpdateStageSelect(KeyboardState keyboard)
    {
        if (IsNewKeyPress(keyboard, Keys.Escape))
        {
            _screen = GameScreen.MainMenu;
        }

        if (IsNewKeyPress(keyboard, Keys.Right))
        {
            _selectedStage = (_selectedStage + 1) % _stages.Length;
        }

        if (IsNewKeyPress(keyboard, Keys.Left))
        {
            _selectedStage = (_selectedStage + _stages.Length - 1) % _stages.Length;
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

        if (_awaitingRouteChoice)
        {
            UpdateRouteChoice(keyboard);
            return;
        }

        if (IsNewKeyPress(keyboard, Keys.V) || IsNewKeyPress(keyboard, Keys.Tab))
        {
            _viewMode = _viewMode == ViewMode.Front ? ViewMode.Top : ViewMode.Front;
            if (_currentUser is not null)
            {
                _currentUser.Settings.ViewMode = _viewMode;
            }
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

        if (IsNewKeyPress(keyboard, Keys.R) && _ropeTimer <= 0f)
        {
            _ropeTimer = 1.8f;
            _scoreBoostTimer = 1.8f;
            _boosters++;
        }

        UpdatePlayerActionTimers(deltaSeconds);

        _worldScroller.Speed = Constants.ScrollSpeed * (_ropeTimer > 0f ? 1.6f : 1f);
        _worldScroller.Update(deltaSeconds);
        _distance = _worldScroller.OffsetX;
        _segmentProgress += _worldScroller.Speed * deltaSeconds;
        ResolveGridInteractions();
        UpdateRouteProgress();

        _score = (int)_distance + _coins * CoinsScoreWeight + _boosters * BoostScoreWeight;

        float stageEnd = SpawnScreenOffset + _activeStageData.World.Columns * GameplayTileSpacing - RunnerX;
        if (_worldScroller.OffsetX >= stageEnd)
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
        PixelFont.Draw(_spriteBatch, _pixel, "JUNGLE RUNNERS", 90, 80, 8, Color.Gold);
        PixelFont.Draw(_spriteBatch, _pixel, _menuMessage, 100, 180, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, $"USER {_typedUserId}", 100, 220, 3, _menuFocus == MenuFocus.UserId ? Color.Gold : Color.White);

        for (int i = 0; i < _mainMenuOptions.Length; i++)
        {
            Color color = i == _mainMenuSelection ? Color.LimeGreen : Color.White;
            string prefix = i == _mainMenuSelection ? "> " : "  ";
            PixelFont.Draw(_spriteBatch, _pixel, prefix + _mainMenuOptions[i], 120, 270 + i * 54, 4, color);
        }
    }

    // Draws the current stage card and stage-select instructions.
    private void DrawStageSelect()
    {
        StageDefinition stage = _stages[_selectedStage];
        PixelFont.Draw(_spriteBatch, _pixel, "SELECT STAGE", 90, 80, 7, Color.Gold);
        PixelFont.Draw(_spriteBatch, _pixel, $"STAGE {stage.Number}: {stage.Name}", 100, 210, 4, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, stage.Description, 100, 270, 3, Color.LightGreen);
        PixelFont.Draw(_spriteBatch, _pixel, "LEFT/RIGHT CHOOSE  ENTER START  ESC MENU", 100, 610, 3, Color.White);
    }

    // Draws the side-view prototype using colored rectangles for lanes, contents, and the player.
    private void DrawFrontView()
    {
        // TODO: Move this placeholder rectangle rendering into FrontViewRenderer and
        // draw real sprites/content assets through AssetManager.
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, WindowWidth, WindowHeight), new Color(18, 74, 56));
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

        float playerScale = _rowDepthMapper.GetScale(_playerRow);
        float playerHeight = _slideTimer > 0f ? 46f * playerScale : 88f * playerScale;
        float playerWidth = 46f * playerScale;
        float playerY = _rowDepthMapper.GetGroundY(_playerRow) - playerHeight - _playerJumpOffset;
        Color playerColor = _invulnerableTimer > 0f ? Color.LightPink : Color.Gold;
        _spriteBatch.Draw(_pixel, new Rectangle((int)RunnerX, (int)playerY, (int)playerWidth, (int)playerHeight), playerColor);
    }

    // Draws the overhead grid view of the same stage data.
    private void DrawTopView()
    {
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, WindowWidth, WindowHeight), new Color(23, 91, 67));
        DrawTopGrid();
        PixelFont.Draw(_spriteBatch, _pixel, "TOP VIEW SAME GRID", 60, 620, 4, Color.White);
    }

    // Draws the score, lives, counters, input hints, and route-choice prompt.
    private void DrawHud()
    {
        PixelFont.Draw(_spriteBatch, _pixel, $"SCORE {_score}", 865, 28, 4, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, $"LIVES {_lives}", 970, 76, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, $"COINS {_coins}", 970, 110, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, _soundEnabled ? "SOUND ON" : "SOUND OFF", 970, 144, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "SPACE JUMP  DOWN SLIDE  R ROPE", 36, 28, 3, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "V VIEW  M SOUND  LEFT/RIGHT ROW", 36, 62, 3, Color.White);

        if (_awaitingRouteChoice && _activeStageData.CurrentNode is not null)
        {
            StageNode route = _activeStageData.CurrentNode.Next[_routeChoiceIndex];
            PixelFont.Draw(_spriteBatch, _pixel, "ROUTE CHOICE", 420, 330, 5, Color.Gold);
            PixelFont.Draw(_spriteBatch, _pixel, $"LEFT/RIGHT PICK  ENTER {route.Label}", 270, 405, 3, Color.White);
        }
    }

    // Draws the stage-clear or game-over result screen.
    private void DrawGameOver()
    {
        Color titleColor = _runWon ? Color.Gold : Color.OrangeRed;
        PixelFont.Draw(_spriteBatch, _pixel, _gameOverTitle, 120, 160, 8, titleColor);
        PixelFont.Draw(_spriteBatch, _pixel, _gameOverDetail, 130, 285, 4, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ENTER STAGE SELECT  ESC MENU", 130, 420, 3, Color.White);
    }

    // Resets run state and builds the selected stage for a fresh attempt.
    private void StartRun()
    {
        // TODO: Apply selected difficulty to stage generation, lives, scoring targets, and hazard speed.
        _activeStage = _stages[_selectedStage];
        _activeStageData = _stageFactory.Create(_activeStage);
        _segments = _activeStageData.Segments;
        _activeSegment = _segments[0];
        _activeStageData.CurrentNode = _activeStageData.Graph.Start;
        _worldScroller.Reset();
        _segmentProgress = 0f;
        _distance = 0f;
        _playerJumpOffset = 0f;
        _playerVelocityY = 0f;
        _canDoubleJump = true;
        _playerRow = Constants.MiddleLayer;
        _slideTimer = 0f;
        _ropeTimer = 0f;
        _scoreBoostTimer = 0f;
        _invulnerableTimer = 0f;
        _lives = 3;
        _coins = 0;
        _boosters = 0;
        _score = 0;
        _runWon = false;
        _awaitingRouteChoice = false;
        _routeChoiceIndex = 0;
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

        if (string.IsNullOrWhiteSpace(_typedUserId))
        {
            _typedUserId = "PLAYER1";
        }

        if (!_saveFile.Users.TryGetValue(_typedUserId, out UserProfile? user))
        {
            user = new UserProfile
            {
                UserId = _typedUserId
            };
            _saveFile.Users[_typedUserId] = user;
        }

        _currentUser = user;
        _saveFile.LastUserId = _currentUser.UserId;
        SaveSaveFile();
    }

    // Persists score, stars, lives, and completion data for the current local user.
    private void SaveStageProgress()
    {
        // TODO: Unlock the next stage on clear and cap or summarize score history so saves do not grow forever.
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
        if (_score > progress.BestScore)
        {
            progress.BestScore = _score;
            progress.BestDifficulty = (int)_selectedDifficulty;
            progress.StarRating = CalculateStarRating(_score);
        }
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
        if (_currentUser is not null)
        {
            _currentUser.Settings.SoundEnabled = _soundEnabled;
            SaveSaveFile();
        }

        _menuMessage = _soundEnabled ? "Sound enabled." : "Sound muted.";
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
    }

    // Checks the runner against nearby tile content and applies pickups or damage.
    private void ResolveGridInteractions()
    {
        // TODO: Replace screen-x proximity checks with ColliderComponent/CollisionHelper so player,
        // item, projectile, obstacle, and boss behavior all use the same collision path.
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
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.LifeItem:
                    _lives = System.Math.Min(5, _lives + 1);
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.ScoreBooster:
                    _boosters++;
                    _scoreBoostTimer = 5f;
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.Projectile:
                    if (_slideTimer <= 0f)
                    {
                        DamagePlayer();
                    }
                    tile.Content = TileContent.Empty;
                    break;
                case TileContent.Obstacle:
                case TileContent.Boss:
                    if (_playerJumpOffset < 56f)
                    {
                        DamagePlayer();
                    }
                    tile.Content = TileContent.Empty;
                    break;
            }
        }
    }

    // Applies damage, temporary invulnerability, and the transition to game over.
    private void DamagePlayer()
    {
        if (_invulnerableTimer > 0f || _ropeTimer > 0f)
        {
            return;
        }

        _lives--;
        _invulnerableTimer = 1.2f;

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

            _spriteBatch.Draw(_pixel, new Rectangle(0, (int)(y - 10f * scale), WindowWidth, (int)(70f * scale)), laneColor);
        }
    }

    // Draws one visible tile or tile content marker in the front-view lane space.
    private void DrawFrontTile(Tile tile, float x)
    {
        float scale = _rowDepthMapper.GetScale(tile.Row);
        float groundY = _rowDepthMapper.GetGroundY(tile.Row);
        Color color = GetTileColor(tile);
        int width = (int)(TileVisualWidth(tile) * scale);
        int height = (int)(TileVisualHeight(tile) * scale);
        int y = (int)(groundY - height);

        _spriteBatch.Draw(_pixel, new Rectangle((int)x, y, width, height), color);
    }

    // Draws the scrollable overhead grid and the player's current row.
    private void DrawTopGrid()
    {
        const int originX = 100;
        const int originY = 190;
        const int cell = 54;
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

                int y = originY + row * (cell + 18);
                Color baseColor = tile.Type switch
                {
                    TileType.Branch => new Color(72, 135, 102),
                    TileType.Merge => new Color(85, 148, 105),
                    TileType.Hazard => new Color(75, 55, 50),
                    _ => new Color(30, 94, 64)
                };

                _spriteBatch.Draw(_pixel, new Rectangle(x, y, cell - 4, cell - 4), baseColor);
                if (tile.HasContent)
                {
                    _spriteBatch.Draw(_pixel, new Rectangle(x + 12, y + 12, cell - 28, cell - 28), GetTileColor(tile));
                }
            }
        }

        int playerX = (int)(originX + RunnerX * (cell / GameplayTileSpacing));
        int playerY = originY + _playerRow * (cell + 18);
        _spriteBatch.Draw(_pixel, new Rectangle(playerX, playerY, cell - 4, cell - 4), Color.Gold);
        PixelFont.Draw(_spriteBatch, _pixel, "ROW 1", 28, originY + 8, 2, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ROW 2", 28, originY + cell + 26, 2, Color.White);
        PixelFont.Draw(_spriteBatch, _pixel, "ROW 3", 28, originY + (cell + 18) * 2 + 8, 2, Color.White);
    }

    // Converts a stage grid column into the current screen-space x position.
    private float GetTileScreenX(int column)
    {
        return SpawnScreenOffset + column * GameplayTileSpacing - _worldScroller.OffsetX;
    }

    // Chooses the placeholder draw color for a tile based on content first, then tile type.
    private Color GetTileColor(Tile tile)
    {
        return tile.Content switch
        {
            TileContent.Coin => Color.Gold,
            TileContent.LifeItem => Color.LightPink,
            TileContent.ScoreBooster => Color.Cyan,
            TileContent.Projectile => Color.OrangeRed,
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
            TileContent.Projectile => 72f,
            TileContent.Coin => 34f,
            TileContent.Boss => 118f,
            _ => 62f
        };
    }

    // Returns a content-specific placeholder height for front-view tile rendering.
    private static float TileVisualHeight(Tile tile)
    {
        return tile.Content switch
        {
            TileContent.Projectile => 18f,
            TileContent.Coin => 34f,
            TileContent.Boss => 120f,
            TileContent.ScoreBooster => 44f,
            TileContent.LifeItem => 42f,
            _ => 72f
        };
    }

    // Converts the final score into a bronze, silver, or gold star count.
    private int CalculateStarRating(int score)
    {
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

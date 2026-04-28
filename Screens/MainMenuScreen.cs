using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace jungle_runners_finalproject;

public sealed class MainMenuScreen : IScreen
{
    private static readonly string[] DefaultOptions = ["Start Game", "How to Play", "Sound", "Logout"];
    private float upRepeatTimer;
    private float downRepeatTimer;

    public int SelectedIndex { get; set; }
    public IReadOnlyList<string> Options => DefaultOptions;
    public float UpRepeatTimer { get => upRepeatTimer; set => upRepeatTimer = value; }
    public float DownRepeatTimer { get => downRepeatTimer; set => downRepeatTimer = value; }

    // Updates main menu navigation when it owns input through ScreenManager.
    public void Update(GameTime gameTime)
    {
    }

    public void UpdateInput(
        KeyboardState keyboard,
        KeyboardState previousKeyboard,
        float deltaSeconds,
        Action<int> selectOption)
    {
        if (IsRepeatingKeyPress(keyboard, previousKeyboard, Keys.Down, ref downRepeatTimer, deltaSeconds))
        {
            SelectedIndex = (SelectedIndex + 1) % DefaultOptions.Length;
        }

        if (IsRepeatingKeyPress(keyboard, previousKeyboard, Keys.Up, ref upRepeatTimer, deltaSeconds))
        {
            SelectedIndex = (SelectedIndex + DefaultOptions.Length - 1) % DefaultOptions.Length;
        }

        if (keyboard.IsKeyDown(Keys.Enter) && previousKeyboard.IsKeyUp(Keys.Enter))
        {
            selectOption(SelectedIndex);
        }
    }

    public void ResetSelection()
    {
        SelectedIndex = 0;
        UpRepeatTimer = 0f;
        DownRepeatTimer = 0f;
    }

    public string GetDisplayText(int index, bool soundEnabled)
    {
        return index == 2 ? $"Sound {(soundEnabled ? "On" : "Off")}" : DefaultOptions[index];
    }

    // Draws the standalone main menu when rendering ownership moves fully here.
    public void Draw(SpriteBatch spriteBatch)
    {
    }

    private static bool IsRepeatingKeyPress(KeyboardState keyboard, KeyboardState previousKeyboard, Keys key, ref float repeatTimer, float deltaSeconds)
    {
        const float initialDelay = 0.28f;
        const float repeatInterval = 0.09f;

        if (keyboard.IsKeyUp(key))
        {
            repeatTimer = 0f;
            return false;
        }

        if (previousKeyboard.IsKeyUp(key))
        {
            repeatTimer = initialDelay;
            return true;
        }

        repeatTimer -= deltaSeconds;
        if (repeatTimer > 0f)
        {
            return false;
        }

        repeatTimer += repeatInterval;
        return true;
    }
}

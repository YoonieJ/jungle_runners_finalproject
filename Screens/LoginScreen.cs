using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace jungle_runners_finalproject;

public sealed class LoginScreen : IScreen
{
    public string UserId { get; set; } = string.Empty;
    public string Message { get; set; } = "Enter user id, then press Enter.";
    public bool IsProfileLoaded { get; set; }

    // Updates the user-id-only login screen when it owns input through ScreenManager.
    public void Update(GameTime gameTime)
    {
    }

    // Captures a local profile id and calls submit when Enter is pressed.
    public void UpdateInput(KeyboardState keyboard, KeyboardState previousKeyboard, int maxLength, Action<string> submit)
    {
        foreach (Keys key in keyboard.GetPressedKeys())
        {
            if (!previousKeyboard.IsKeyUp(key))
            {
                continue;
            }

            if (key == Keys.Back && UserId.Length > 0)
            {
                UserId = UserId[..^1];
                continue;
            }

            if (key == Keys.Enter)
            {
                submit(UserId);
                continue;
            }

            if (TryGetUserIdCharacter(key, keyboard, out char character) && UserId.Length < maxLength)
            {
                UserId += character;
            }
        }
    }

    public void MarkLoaded(string userId, string message)
    {
        UserId = userId;
        Message = message;
        IsProfileLoaded = true;
    }

    public void Reset()
    {
        UserId = string.Empty;
        Message = "Enter user id, then press Enter.";
        IsProfileLoaded = false;
    }

    public UserProfile? LoadOrCreateProfile(SaveFile saveFile, Action<string, UserProfile> normalizeProfile, out bool isNewUser)
    {
        isNewUser = false;
        if (string.IsNullOrWhiteSpace(UserId))
        {
            Message = "Type an id first.";
            return null;
        }

        string userId = UserId.Trim().ToUpperInvariant();
        UserId = userId;

        if (!TryGetSavedUser(saveFile, userId, out UserProfile? user) || user is null)
        {
            user = new UserProfile
            {
                UserId = userId
            };
            saveFile.Users[userId] = user;
            isNewUser = true;
        }

        normalizeProfile(userId, user);
        return user;
    }

    // Draws the user-id login UI.
    public void Draw(SpriteBatch spriteBatch)
    {
    }

    private static bool TryGetUserIdCharacter(Keys key, KeyboardState keyboard, out char character)
    {
        bool shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

        if (key >= Keys.A && key <= Keys.Z)
        {
            character = (char)('A' + (int)key - (int)Keys.A);
            if (!shift)
            {
                character = char.ToLowerInvariant(character);
            }

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

        if (key == Keys.OemMinus || key == Keys.Subtract)
        {
            character = shift ? '_' : '-';
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

    private static bool TryGetSavedUser(SaveFile saveFile, string userId, out UserProfile? user)
    {
        if (saveFile.Users.TryGetValue(userId, out user))
        {
            return true;
        }

        string? matchingKey = saveFile.Users.Keys.FirstOrDefault(key => string.Equals(key, userId, StringComparison.OrdinalIgnoreCase));
        if (matchingKey is null)
        {
            return false;
        }

        user = saveFile.Users[matchingKey];
        if (matchingKey != userId)
        {
            saveFile.Users.Remove(matchingKey);
            saveFile.Users[userId] = user;
        }

        return true;
    }
}

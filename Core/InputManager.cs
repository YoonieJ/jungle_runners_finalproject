using Microsoft.Xna.Framework.Input;

namespace jungle_runners_finalproject;

public sealed class InputManager
{
    public KeyboardState CurrentKeyboard { get; private set; }
    public KeyboardState PreviousKeyboard { get; private set; }

    // Captures the latest keyboard state while preserving the previous frame.
    public void Update()
    {
        PreviousKeyboard = CurrentKeyboard;
        CurrentKeyboard = Keyboard.GetState();
    }

    // Returns whether a key is currently held down.
    public bool IsKeyDown(Keys key)
    {
        return CurrentKeyboard.IsKeyDown(key);
    }

    // Returns true only on the frame a key changes from up to down.
    public bool IsNewKeyPress(Keys key)
    {
        return CurrentKeyboard.IsKeyDown(key) && PreviousKeyboard.IsKeyUp(key);
    }
}

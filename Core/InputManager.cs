using Microsoft.Xna.Framework.Input;

namespace jungle_runners_finalproject;

public sealed class InputManager
{
    public KeyboardState CurrentKeyboard { get; private set; }
    public KeyboardState PreviousKeyboard { get; private set; }

    public void Update()
    {
        PreviousKeyboard = CurrentKeyboard;
        CurrentKeyboard = Keyboard.GetState();
    }

    public bool IsKeyDown(Keys key)
    {
        return CurrentKeyboard.IsKeyDown(key);
    }

    public bool IsNewKeyPress(Keys key)
    {
        return CurrentKeyboard.IsKeyDown(key) && PreviousKeyboard.IsKeyUp(key);
    }
}

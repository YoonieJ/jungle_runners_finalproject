using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class ScreenManager
{
    private readonly Stack<IScreen> _screens = new();

    public IScreen? Current => _screens.Count > 0 ? _screens.Peek() : null;

    // Replaces the entire screen stack with a single active screen.
    public void Change(IScreen screen)
    {
        _screens.Clear();
        _screens.Push(screen);
    }

    // Adds a new screen on top of the current one.
    public void Push(IScreen screen)
    {
        _screens.Push(screen);
    }

    // Removes and returns the active screen, if any.
    public IScreen? Pop()
    {
        return _screens.Count > 0 ? _screens.Pop() : null;
    }

    // Updates only the topmost screen.
    public void Update(GameTime gameTime)
    {
        Current?.Update(gameTime);
    }

    // Draws only the topmost screen.
    public void Draw(SpriteBatch spriteBatch)
    {
        Current?.Draw(spriteBatch);
    }
}

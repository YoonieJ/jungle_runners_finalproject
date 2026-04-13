using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace jungle_runners_finalproject;

public sealed class ScreenManager
{
    private readonly Stack<IScreen> _screens = new();

    public IScreen? Current => _screens.Count > 0 ? _screens.Peek() : null;

    public void Change(IScreen screen)
    {
        _screens.Clear();
        _screens.Push(screen);
    }

    public void Push(IScreen screen)
    {
        _screens.Push(screen);
    }

    public IScreen? Pop()
    {
        return _screens.Count > 0 ? _screens.Pop() : null;
    }

    public void Update(GameTime gameTime)
    {
        Current?.Update(gameTime);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        Current?.Draw(spriteBatch);
    }
}

using System.Collections.Generic;
using Microsoft.Xna.Framework.Content;

namespace jungle_runners_finalproject;

public sealed class AssetManager
{
    private readonly Dictionary<string, object> _cache = new();

    public T Load<T>(ContentManager content, string assetName)
    {
        if (_cache.TryGetValue(assetName, out object? asset))
        {
            return (T)asset;
        }

        T loaded = content.Load<T>(assetName);
        _cache[assetName] = loaded!;
        return loaded;
    }

    public bool TryGet<T>(string assetName, out T? asset)
    {
        if (_cache.TryGetValue(assetName, out object? value) && value is T typed)
        {
            asset = typed;
            return true;
        }

        asset = default;
        return false;
    }
}

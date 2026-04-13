using System;
using System.IO;
using System.Text.Json;

namespace jungle_runners_finalproject;

public static class JsonFileHelper
{
    public static T Load<T>(string path, T fallback, JsonSerializerOptions? options = null)
    {
        if (!File.Exists(path))
        {
            return fallback;
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, options) ?? fallback;
        }
        catch (JsonException)
        {
            return fallback;
        }
        catch (NotSupportedException)
        {
            return fallback;
        }
    }

    public static void Save<T>(string path, T data, JsonSerializerOptions? options = null)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(path, json);
    }
}

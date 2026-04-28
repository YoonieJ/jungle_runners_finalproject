using System.Text.Json;

namespace jungle_runners_finalproject;

public sealed class SaveManager
{
    // Loads data from disk, returning a new instance when no valid file exists.
    public T Load<T>(string path, JsonSerializerOptions? options = null) where T : new()
    {
        return JsonFileHelper.Load(path, new T(), options);
    }

    // Saves data to disk through the shared JSON helper.
    public void Save<T>(string path, T data, JsonSerializerOptions? options = null)
    {
        JsonFileHelper.Save(path, data, options);
    }

    // Static convenience wrapper for loading save data.
    public static T LoadData<T>(string path, JsonSerializerOptions? options = null) where T : new()
    {
        return JsonFileHelper.Load(path, new T(), options);
    }

    // Static convenience wrapper for writing save data.
    public static void SaveData<T>(string path, T data, JsonSerializerOptions? options = null)
    {
        JsonFileHelper.Save(path, data, options);
    }
}

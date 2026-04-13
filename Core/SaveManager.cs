using System.Text.Json;

namespace jungle_runners_finalproject;

public sealed class SaveManager
{
    public T Load<T>(string path, JsonSerializerOptions? options = null) where T : new()
    {
        return JsonFileHelper.Load(path, new T(), options);
    }

    public void Save<T>(string path, T data, JsonSerializerOptions? options = null)
    {
        JsonFileHelper.Save(path, data, options);
    }

    public static T LoadData<T>(string path, JsonSerializerOptions? options = null) where T : new()
    {
        return JsonFileHelper.Load(path, new T(), options);
    }

    public static void SaveData<T>(string path, T data, JsonSerializerOptions? options = null)
    {
        JsonFileHelper.Save(path, data, options);
    }
}

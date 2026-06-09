using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Altinn.Notifications.Tests.TestData;

public static class TestDataLoader
{
    private static JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<T> Load<T>(string id)
    {
        string path = GetPath<T>(id);
        string fileContent = await File.ReadAllTextAsync(path);
        T? data = JsonSerializer.Deserialize<T>(fileContent, _options);
        return data!;
    }

    private static string GetPath<T>(string id)
    {
        string? unitTestFolder = Path.GetDirectoryName(new Uri(typeof(T).Assembly.Location).LocalPath);

        if (unitTestFolder is null)
        {
            return string.Empty;
        }

        return Path.Combine(unitTestFolder, "..", "..", "..", "TestData", typeof(T).Name, $"{id}.json");
    }
}

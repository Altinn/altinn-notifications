using System.Text.Json;

namespace Altinn.Notifications.Shared.TestInfrastructure.Utils;

/// <summary>
/// Helper class to load container image configurations from container-images.json.
/// This file is managed by Renovate for automatic version updates.
/// </summary>
public static class ContainerImageUtils
{
    private static readonly Lazy<Dictionary<string, string>> _images = new(LoadImages);
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets the configured image string for a specific container.
    /// </summary>
    /// <param name="imageName">The image name key (e.g., "postgres", "mssql", "serviceBusEmulator").
    /// Note: Key lookup is case-sensitive and must match the exact casing in container-images.json.</param>
    /// <returns>The full image string including registry, name, tag, and optional digest.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the image name is not found in configuration.</exception>
    public static string GetImage(string imageName)
    {
        if (string.IsNullOrWhiteSpace(imageName))
        {
            throw new ArgumentException("Image name must be a non-empty value.", nameof(imageName));
        }

        if (_images.Value.TryGetValue(imageName, out string? image))
        {
            return image;
        }

        throw new KeyNotFoundException($"Container image '{imageName}' not found in container-images.json");
    }

    private static Dictionary<string, string> LoadImages()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "container-images.json");

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                $"Container images configuration file not found at: {configPath}. " +
                "Ensure container-images.json is copied to the output directory.");
        }

        string json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<ContainerImagesConfig>(json, _jsonOptions);

        if (config?.Images == null || config.Images.Count == 0)
        {
            throw new InvalidOperationException("container-images.json does not contain any image configurations");
        }

        return config.Images;
    }

    private class ContainerImagesConfig
    {
        public Dictionary<string, string> Images { get; set; } = new();
    }
}

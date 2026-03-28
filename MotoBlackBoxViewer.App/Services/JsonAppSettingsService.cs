using System.Text.Json;
using MotoBlackBoxViewer.App.Interfaces;
using MotoBlackBoxViewer.App.Models;

namespace MotoBlackBoxViewer.App.Services;

public sealed class JsonAppSettingsService : IAppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _settingsPath;

    public JsonAppSettingsService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MotoBlackBoxViewer",
            "session.json");
    }

    public AppSessionSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new AppSessionSettings();

            string json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSessionSettings>(json, SerializerOptions) ?? new AppSessionSettings();
        }
        catch
        {
            return new AppSessionSettings();
        }
    }

    public void Save(AppSessionSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        string json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(_settingsPath, json);
    }
}

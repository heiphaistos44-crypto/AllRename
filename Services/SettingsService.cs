using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AllRename.Services;

public sealed class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AllRename", "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private SettingsData _data = new();

    public string TmdbApiKey
    {
        get => _data.TmdbApiKey;
        set => _data.TmdbApiKey = value;
    }

    public string PlexServerUrl
    {
        get => _data.PlexServerUrl;
        set => _data.PlexServerUrl = value;
    }

    public string PlexToken
    {
        get => _data.PlexToken;
        set => _data.PlexToken = value;
    }

    public string QbitUrl
    {
        get => _data.QbitUrl;
        set => _data.QbitUrl = value;
    }

    public string QbitUsername
    {
        get => _data.QbitUsername;
        set => _data.QbitUsername = value;
    }

    public string TransmissionUrl
    {
        get => _data.TransmissionUrl;
        set => _data.TransmissionUrl = value;
    }

    public string TransmissionUsername
    {
        get => _data.TransmissionUsername;
        set => _data.TransmissionUsername = value;
    }

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            string json = await File.ReadAllTextAsync(SettingsPath);
            var loaded = JsonSerializer.Deserialize<SettingsData>(json, JsonOpts);
            if (loaded == null) return;

            _data = loaded;
            _data.TmdbApiKey = Unprotect(_data.TmdbApiKey);
            _data.PlexToken = Unprotect(_data.PlexToken);
        }
        catch (Exception ex)
        {
            await LogService.WriteAsync(LogLevel.Error, $"SettingsService.Load: {ex.Message}");
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var toSave = new SettingsData
            {
                TmdbApiKey = Protect(_data.TmdbApiKey),
                PlexServerUrl = _data.PlexServerUrl,
                PlexToken = Protect(_data.PlexToken),
                QbitUrl = _data.QbitUrl,
                QbitUsername = _data.QbitUsername,
                TransmissionUrl = _data.TransmissionUrl,
                TransmissionUsername = _data.TransmissionUsername
            };
            string json = JsonSerializer.Serialize(toSave, JsonOpts);
            await File.WriteAllTextAsync(SettingsPath, json);
        }
        catch (Exception ex)
        {
            await LogService.WriteAsync(LogLevel.Error, $"SettingsService.Save: {ex.Message}");
        }
    }

    private static string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        byte[] bytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plaintext), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    private static string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
        try
        {
            byte[] bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(ciphertext), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return string.Empty; }
    }

    private sealed class SettingsData
    {
        public string TmdbApiKey { get; set; } = string.Empty;
        public string PlexServerUrl { get; set; } = string.Empty;
        public string PlexToken { get; set; } = string.Empty;
        public string QbitUrl { get; set; } = "http://localhost:8080";
        public string QbitUsername { get; set; } = string.Empty;
        public string TransmissionUrl { get; set; } = "http://localhost:9091";
        public string TransmissionUsername { get; set; } = string.Empty;
    }
}

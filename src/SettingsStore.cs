using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BBDesktop;

internal sealed class WorkstationSettings
{
    public string WorkspaceUrl { get; set; } = "https://app.bigbrother.test/big-brother-dashboard/index.html";
    public string BrowserUrl { get; set; } = "https://web.telegram.org/";
    public double PanelX { get; set; } = -1;
    public double PanelY { get; set; } = 64;
    public double PanelWidth { get; set; } = 740;
    public double PanelHeight { get; set; } = 540;
    public List<string> Bookmarks { get; set; } = new();
}

internal static class SettingsStore
{
    internal static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BIG BROTHER", "Desktop");

    internal static string FilePath => Path.Combine(Root, "settings.json");
    internal static string WorkspaceProfile => Path.Combine(Root, "WorkspaceProfile");
    internal static string BrowserProfile => Path.Combine(Root, "BrowserProfile");

    internal static WorkstationSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new WorkstationSettings();
            return JsonSerializer.Deserialize<WorkstationSettings>(File.ReadAllText(FilePath))
                   ?? new WorkstationSettings();
        }
        catch
        {
            return new WorkstationSettings();
        }
    }

    internal static void Save(WorkstationSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Root);
            var temporaryFile = FilePath + ".tmp";
            File.WriteAllText(temporaryFile, JsonSerializer.Serialize(
                settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporaryFile, FilePath, true);
        }
        catch
        {
            // Device storage may be read-only; browsing remains usable for this run.
        }
    }

    // Do not save sensitive URL fragments or query tokens into our preferences file.
    internal static string RememberableUrl(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            return uri.GetLeftPart(UriPartial.Path);
        return "https://web.telegram.org/";
    }

    internal static bool TryWebAddress(string? raw, out Uri uri)
    {
        uri = null!;
        var value = (raw ?? "").Trim();
        if (value.Length == 0) return false;
        if (!value.Contains("://", StringComparison.Ordinal)) value = "https://" + value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) || parsed == null)
            return false;
        if ((parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp) ||
            parsed.Host.Length == 0 || parsed.UserInfo.Length != 0)
            return false;
        uri = parsed;
        return true;
    }
}

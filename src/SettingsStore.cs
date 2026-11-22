using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

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
    public Dictionary<string, string> BookmarkTitles { get; set; } = new();
    public bool BrowserAddressBarVisible { get; set; } = true;
    public bool TelegramShortcutVisible { get; set; } = true;
    public string TelegramShortcutName { get; set; } = "Telegram Web";
}

internal static class SettingsStore
{
    // Online setup saves the chosen folder in HKCU (outside Velopack's
    // replaceable app directory). Existing installs without that setting keep
    // their original LocalAppData profile and Telegram session unchanged.
    private const string RegistryPath = @"Software\BIG BROTHER\Desktop";
    private static readonly string DefaultRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BIG BROTHER", "Desktop");

    internal static string Root { get; } = ResolveProfileDirectory();

    private static string ResolveProfileDirectory()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            var value = key?.GetValue("DataDirectory") as string;
            if (string.IsNullOrWhiteSpace(value)) return DefaultRoot;
            var path = Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(value.Trim()));
            // Browser authentication data should not be on a network share.
            // Avoid paths nested in the replaceable installation directory.
            if (!Path.IsPathRooted(path) || path.StartsWith(@"\\", StringComparison.Ordinal))
                return DefaultRoot;
            var program = key?.GetValue("InstallDirectory") as string;
            if (!string.IsNullOrWhiteSpace(program))
            {
                var install = Path.GetFullPath(program).TrimEnd(Path.DirectorySeparatorChar);
                if (path.Equals(install, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(install + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                    return DefaultRoot;
            }
            return path;
        }
        catch
        {
            // A bad registry value must not make Desktop unusable.
            return DefaultRoot;
        }
    }

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

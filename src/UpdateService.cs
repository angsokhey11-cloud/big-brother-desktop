using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Velopack;
using Microsoft.Win32;

namespace BBDesktop;

/// <summary>
/// Explicit-consent updater. Uses an optional HTTPS Velopack release feed.
/// Never touches accounting data or restarts a working invoice without consent.
/// </summary>
internal static class UpdateService
{
    private sealed class Config
    {
        public string FeedUrl { get; set; } = "";
    }

    internal static async Task PromptLocalUpdateAsync(Window owner, Action<string> status)
    {
        // This development fallback works without a hosted update feed.
        // A release folder contains releases.dev.json and a full .nupkg.
        var picker = new OpenFileDialog
        {
            Title = "Select the downloaded BIG BROTHER Desktop release feed",
            Filter = "BIG BROTHER update feed (releases.dev.json)|releases.dev.json",
            CheckFileExists = true,
            Multiselect = false
        };
        if (picker.ShowDialog(owner) != true) return;
        var directory = Path.GetDirectoryName(picker.FileName);
        if (string.IsNullOrWhiteSpace(directory)) return;
        if (!File.Exists(Path.Combine(directory, "releases.dev.json")) ||
            Directory.GetFiles(directory, "BigBrother.Desktop-*-dev-full.nupkg").Length == 0)
        {
            MessageBox.Show(owner,
                "Select a release folder containing releases.dev.json and the full Desktop .nupkg package.",
                "BIG BROTHER Desktop", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await CheckForUpdatesAsync(owner, status, directory);
    }

    internal static async Task CheckForUpdatesAsync(Window owner, Action<string> status,
        string? localUpdateFolder = null)
    {
        try
        {
            var file = Path.Combine(AppContext.BaseDirectory, "update-source.json");
            var config = File.Exists(file)
                ? JsonSerializer.Deserialize<Config>(File.ReadAllText(file)) : null;
            var feed = localUpdateFolder ?? config?.FeedUrl?.Trim() ?? "";
            if (localUpdateFolder is null &&
                (!Uri.TryCreate(feed, UriKind.Absolute, out var address) ||
                address.Scheme != Uri.UriSchemeHttps))
            {
                status("Update feed not configured");
                MessageBox.Show(owner, "Desktop update checks are not enabled yet. We will configure the secure HTTPS update feed after verifying the installer and the accounting modules.",
                    "BIG BROTHER Desktop", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            status(localUpdateFolder is null ? "Checking for Desktop updates…" : "Checking downloaded update package…");
            var manager = new UpdateManager(feed);
            var update = await manager.CheckForUpdatesAsync();
            if (update == null)
            {
                status("Desktop is up to date");
                MessageBox.Show(owner, "You already have the latest available Desktop release.",
                    "BIG BROTHER Desktop", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            status("New Desktop version available");
            if (MessageBox.Show(owner, "A newer BIG BROTHER Desktop version is available. Download it now? Please finish or save any unfinished accounting work before installing.",
                "BIG BROTHER Desktop Update", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            status("Downloading verified Desktop update…");
            await manager.DownloadUpdatesAsync(update);
            status("Downloaded. Waiting for permission to restart.");
            if (MessageBox.Show(owner, "Update downloaded. Have you saved all unfinished invoices, cash collections, and other work? Choose Yes to install and restart, or No to continue working.",
                "BIG BROTHER Desktop Update", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                manager.ApplyUpdatesAndRestart(update);
        }
        catch (Exception error)
        {
            status("Update check unavailable; app remains usable");
            MessageBox.Show(owner, "Update could not be checked or installed:\n" + error.Message
                + "\n\nYour current Desktop version remains available.",
                "BIG BROTHER Desktop", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

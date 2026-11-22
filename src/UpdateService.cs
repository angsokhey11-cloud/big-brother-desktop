using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Velopack;
using Velopack.Sources;

namespace BBDesktop;

/// <summary>
/// Safe development-channel updater. Checks public GitHub Releases without a
/// browser and obtains Velopack packages, never a new Setup.exe/Actions ZIP.
/// Only approved pre-releases are offered, and restart requires user consent.
/// </summary>
internal static class UpdateService
{
    private const string GithubReleaseRepository =
        "https://github.com/angsokhey11-cloud/big-brother-desktop";

    private static bool _busy;

    internal static async Task PromptLocalUpdateAsync(Window owner, Action<string> status)
    {
        // Keep the offline/downloaded-package option when GitHub is inaccessible.
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
                "Select a release folder containing releases.dev.json and a full Desktop .nupkg package.",
                "BIG BROTHER Desktop", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await CheckForUpdatesAsync(owner, status, localUpdateFolder: directory);
    }

    internal static async Task CheckForUpdatesAsync(Window owner, Action<string> status,
        string? localUpdateFolder = null, bool quietIfCurrent = false)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            // Public GitHub Releases, dev/pre-release channel. There is no
            // GitHub token embedded in the installed application.
            var manager = localUpdateFolder is null
                ? new UpdateManager(new GithubSource(
                    GithubReleaseRepository, accessToken: null, prerelease: true))
                : new UpdateManager(localUpdateFolder);

            if (!manager.IsInstalled)
            {
                status("Desktop update check requires an installed application");
                if (!quietIfCurrent)
                    MessageBox.Show(owner,
                        "This copy of BIG BROTHER Desktop was not started from a Velopack installation. " +
                        "Open it from the Windows Start menu to check for updates.",
                        "BIG BROTHER Desktop", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Bug fix: an update downloaded earlier but deliberately postponed
            // is already ready locally. Do not force the user to download it
            // again, even if GitHub is unavailable on this launch.
            if (manager.UpdatePendingRestart is { } prepared)
            {
                var pendingVersion = prepared.Version.ToString();
                status("BIG BROTHER Desktop v" + pendingVersion + " is ready to install");
                if (MessageBox.Show(owner,
                    "BIG BROTHER Desktop v" + pendingVersion +
                    " is already downloaded on this PC.\n\n" +
                    "Have you saved unfinished invoices, cash collections, and other work? " +
                    "Choose Yes to install and restart now, or No to keep working.",
                    "BIG BROTHER Desktop — Update Ready",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    manager.ApplyUpdatesAndRestart(prepared);
                }
                else
                {
                    status("Previously downloaded update retained for later");
                }
                return;
            }

            status("Checking for BIG BROTHER Desktop updates…");
            var update = await manager.CheckForUpdatesAsync();
            if (update == null)
            {
                status("BIG BROTHER Desktop is up to date");
                if (!quietIfCurrent)
                    MessageBox.Show(owner, "This Desktop installation is up to date.",
                        "BIG BROTHER Desktop", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var newVersion = update.TargetFullRelease.Version.ToString();
            var currentVersion = manager.CurrentVersion?.ToString() ?? "unknown";
            status("BIG BROTHER Desktop v" + newVersion + " available");
            var message = "BIG BROTHER Desktop development update available!\n\n" +
                          "Installed: v" + currentVersion + "\n" +
                          "New: v" + newVersion + "\n\n" +
                          "Download the checksum-verified Desktop update package now? " +
                          "You can continue working while it downloads. " +
                          "The app will ask separately before installing and restarting.";
            if (MessageBox.Show(owner, message,
                "BIG BROTHER Desktop — New Update",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                status("Update postponed");
                return;
            }

            var progress = new ProgressBar
            {
                Minimum = 0, Maximum = 100, Value = 0, Height = 23,
                Margin = new Thickness(18, 9, 18, 12)
            };
            var progressLabel = new TextBlock
            {
                Text = "Downloading BIG BROTHER Desktop v" + newVersion + " from GitHub… 0%",
                Margin = new Thickness(18, 17, 18, 2),
                Foreground = Brushes.DarkSlateGray,
                TextWrapping = TextWrapping.Wrap
            };
            var panel = new StackPanel();
            panel.Children.Add(progressLabel);
            panel.Children.Add(progress);
            var dialog = new Window
            {
                Owner = owner,
                Title = "BIG BROTHER Desktop — Downloading Update",
                Content = panel,
                Width = 440, Height = 140,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };
            // Do not allow an accidental close to imply a canceled download.
            // Download continues to completion or failure before installation
            // is even offered. The main WPF window remains responsive.
            dialog.Closing += (_, e) =>
            {
                if (dialog.Tag is not true) e.Cancel = true;
            };

            try
            {
                dialog.Show();
                status("Downloading BIG BROTHER update from GitHub…");
                await manager.DownloadUpdatesAsync(update, p =>
                {
                    _ = dialog.Dispatcher.BeginInvoke(() =>
                    {
                        progress.Value = Math.Clamp(p, 0, 100);
                        progressLabel.Text = "Downloading BIG BROTHER Desktop v" + newVersion + " from GitHub… " +
                                             Math.Clamp(p, 0, 100) + "%";
                    });
                });
            }
            finally
            {
                dialog.Tag = true;
                dialog.Close();
            }

            status("BIG BROTHER Desktop v" + newVersion + " downloaded; restart requires approval");
            if (MessageBox.Show(owner,
                "BIG BROTHER Desktop v" + newVersion +
                " is downloaded and ready to install.\n\nHave you saved all unfinished invoices, cash collections, and other accounting work? " +
                "Choose Yes to install and restart now, or No to continue working on the current version.",
                "BIG BROTHER Desktop — Install and Restart",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                status("Installing BIG BROTHER Desktop update…");
                manager.ApplyUpdatesAndRestart(update);
            }
            else
            {
                status("BIG BROTHER Desktop v" + newVersion + " saved locally; install postponed");
            }
        }
        catch (Exception error)
        {
            status("Update check unavailable; existing Desktop continues working");
            if (!quietIfCurrent)
                MessageBox.Show(owner, "Update unavailable: " + error.Message +
                    "\n\nYour installed BIG BROTHER Desktop is unchanged. " +
                    "GitHub access is only needed for updates, not to open local app files.",
                    "BIG BROTHER Desktop", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            _busy = false;
        }
    }
}

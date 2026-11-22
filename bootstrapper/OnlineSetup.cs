// BIG BROTHER Desktop — lightweight online setup wizard.
// Framework: Windows built-in .NET Framework 4.8 / Windows Forms.
// This installer downloads an APPROVED PUBLIC GitHub release Setup.exe,
// validates its GitHub SHA-256, and delegates installation to Velopack.
// No Supabase access, invoice writes, server secrets, or accounting data.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BigBrotherOnlineSetup
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupWindow());
        }
    }

    internal sealed class ApprovedRelease
    {
        public Version Version;
        public string Tag;
        public string Url;
        public string Sha256;
        public long Bytes;
    }

    internal sealed class SetupWindow : Form
    {
        private const string Repo = "angsokhey11-cloud/big-brother-desktop";
        private const string InstallerName = "BigBrother.Desktop-dev-Setup.exe";
        private const string RegistryPath = @"Software\BIG BROTHER\Desktop";
        private static readonly string DefaultInstall = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BigBrother.Desktop");
        private static readonly string DefaultData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BIG BROTHER", "Desktop");

        private readonly Panel header = new Panel();
        private readonly Panel content = new Panel();
        private readonly Button back = new Button();
        private readonly Button next = new Button();
        private readonly Button cancel = new Button();
        private readonly Label stepLabel = new Label();
        private readonly Label pageTitle = new Label();
        private readonly Label note = new Label();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly TextBox installPath = new TextBox();
        private readonly TextBox dataPath = new TextBox();
        private readonly CheckBox launchAfter = new CheckBox();
        private readonly Label downloadedVersion = new Label();

        private int step = 0;
        private bool busy = false;
        private bool installing = false;
        private WebClient downloadClient;
        private string temporaryDirectory;
        private ApprovedRelease selectedRelease;

        internal SetupWindow()
        {
            Text = "BIG BROTHER Desktop — Online Setup";
            ClientSize = new Size(680, 520);
            MinimumSize = MaximumSize = Size;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(247, 250, 254);
            Font = new Font("Segoe UI", 10f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            header.Bounds = new Rectangle(0, 0, 680, 107);
            header.BackColor = Color.FromArgb(25, 64, 116);
            Controls.Add(header);
            var brand = new Label
            {
                Text = "BIG BROTHER",
                Font = new Font("Segoe UI", 19f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(28, 18), AutoSize = true
            };
            header.Controls.Add(brand);
            header.Controls.Add(new Label
            {
                Text = "DESKTOP ACCOUNTING  /  WINDOWS INSTALLATION",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 218, 255),
                Location = new Point(30, 66), AutoSize = true
            });

            stepLabel.Location = new Point(27, 121);
            stepLabel.Size = new Size(625, 23);
            stepLabel.ForeColor = Color.FromArgb(40, 86, 147);
            stepLabel.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            Controls.Add(stepLabel);
            content.Bounds = new Rectangle(24, 154, 632, 277);
            Controls.Add(content);

            back.SetBounds(307, 451, 100, 38);
            next.SetBounds(413, 451, 120, 38);
            cancel.SetBounds(540, 451, 111, 38);
            StyleButton(back, Color.FromArgb(225, 232, 243), Color.FromArgb(25, 64, 116));
            StyleButton(next, Color.FromArgb(30, 100, 190), Color.White);
            StyleButton(cancel, Color.FromArgb(225, 232, 243), Color.FromArgb(25, 64, 116));
            back.Text = "Back";
            cancel.Text = "Cancel";
            back.Click += (s, e) => { if (!busy && step > 0) { step--; Render(); } };
            next.Click += NextClicked;
            cancel.Click += (s, e) => Close();
            Controls.Add(back);
            Controls.Add(next);
            Controls.Add(cancel);

            installPath.Text = DefaultInstall;
            dataPath.Text = DefaultData;
            FormClosing += WhenClosing;
            Render();
        }

        private static void StyleButton(Button b, Color bg, Color fg)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = bg; b.ForeColor = fg;
            b.Cursor = Cursors.Hand;
            b.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        }

        private static Label L(string text, int x, int y, int width, int height, float font, bool bold)
        {
            return new Label
            {
                Text = text, Location = new Point(x, y), Size = new Size(width, height),
                Font = new Font("Segoe UI", font, bold ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = Color.FromArgb(36, 57, 83)
            };
        }

        private void Render()
        {
            content.Controls.Clear();
            stepLabel.Text = new[] {
                "STEP 1 OF 3  /  WELCOME",
                "STEP 2 OF 3  /  CHOOSE FOLDERS",
                "STEP 3 OF 3  /  INSTALLING",
                "INSTALLATION COMPLETE"
            }[step];
            back.Visible = step == 1;
            next.Enabled = !busy;
            cancel.Enabled = !installing;
            next.Text = new[] { "Next", "Install", "Working...", "Finish" }[step];

            if (step == 0)
            {
                content.Controls.Add(L("Welcome to BIG BROTHER Desktop", 1, 8, 620, 48, 21, true));
                content.Controls.Add(L("Install the complete Windows accounting application using one small setup file.", 2, 69, 608, 54, 11, false));
                content.Controls.Add(L("• Downloads the approved application package directly from GitHub Releases.", 8, 137, 618, 31, 10, false));
                content.Controls.Add(L("• You can choose where Windows installs the app and stores local settings.", 8, 177, 618, 31, 10, false));
                content.Controls.Add(L("• Your actual accounting data remains in the existing online Supabase database.", 8, 217, 618, 45, 10, false));
            }
            else if (step == 1)
            {
                content.Controls.Add(L("Application installation folder", 2, 7, 590, 24, 11, true));
                PutPathRow(installPath, 40, "Browse...", false);
                content.Controls.Add(L("Includes app code and offline interface files. Updates will use this installation.", 2, 89, 624, 27, 9f, false));
                content.Controls.Add(L("Local settings and browser profiles folder", 2, 130, 606, 25, 11, true));
                PutPathRow(dataPath, 163, "Browse...", true);
                content.Controls.Add(L("Telegram login, browser profile and settings stay here across application upgrades.", 2, 210, 620, 44, 9f, false));
            }
            else if (step == 2)
            {
                content.Controls.Add(L("Installing BIG BROTHER Desktop", 2, 9, 610, 44, 20, true));
                note.Location = new Point(4, 82);
                note.Size = new Size(620, 69);
                note.ForeColor = Color.FromArgb(36, 57, 83);
                note.Text = "Connecting to GitHub Releases...";
                content.Controls.Add(note);
                progress.Location = new Point(4, 174);
                progress.Size = new Size(613, 28);
                progress.Style = ProgressBarStyle.Continuous;
                progress.Minimum = 0; progress.Maximum = 100; progress.Value = 0;
                content.Controls.Add(progress);
                content.Controls.Add(L("Internet is required for first installation. Already installed app files open locally.", 4, 217, 610, 45, 9f, false));
            }
            else if (step == 3)
            {
                content.Controls.Add(L("BIG BROTHER Desktop is installed!", 2, 8, 620, 49, 20, true));
                var version = selectedRelease == null ? "" : "Version " + selectedRelease.Version;
                content.Controls.Add(L(version + "  •  Installed application is ready.", 4, 70, 610, 42, 11, false));
                content.Controls.Add(L("Installation: " + installPath.Text, 4, 127, 614, 47, 9f, false));
                content.Controls.Add(L("Local data: " + dataPath.Text, 4, 177, 614, 50, 9f, false));
                launchAfter.Text = "Open BIG BROTHER Desktop when I click Finish";
                launchAfter.Checked = true;
                launchAfter.AutoSize = true;
                launchAfter.Location = new Point(6, 242);
                content.Controls.Add(launchAfter);
            }
        }

        private void PutPathRow(TextBox box, int y, string buttonText, bool data)
        {
            box.Location = new Point(3, y);
            box.Size = new Size(492, 29);
            box.Font = new Font("Segoe UI", 10f);
            content.Controls.Add(box);
            var browse = new Button();
            browse.Text = buttonText;
            browse.Bounds = new Rectangle(505, y - 2, 110, 34);
            StyleButton(browse, Color.FromArgb(221, 233, 249), Color.FromArgb(25, 64, 116));
            browse.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = data ? "Select the folder for BIG BROTHER local settings and browser login" :
                        "Select the Windows folder for BIG BROTHER Desktop program files";
                    var chosen = data ? dataPath : installPath;
                    if (Directory.Exists(chosen.Text)) dlg.SelectedPath = chosen.Text;
                    if (dlg.ShowDialog(this) == DialogResult.OK) chosen.Text = dlg.SelectedPath;
                }
            };
            content.Controls.Add(browse);
        }

        private async void NextClicked(object sender, EventArgs e)
        {
            if (step == 0) { step = 1; Render(); return; }
            if (step == 1)
            {
                string issue = ValidateDestinations();
                if (issue != null)
                {
                    MessageBox.Show(this, issue, "Check Installation Folders",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                step = 2; busy = true; Render();
                await RunInstallation();
                return;
            }
            if (step == 3)
            {
                if (launchAfter.Checked) StartApp();
                Close();
            }
        }

        private string ValidateDestinations()
        {
            try
            {
                string target = Path.GetFullPath(Environment.ExpandEnvironmentVariables(installPath.Text.Trim()));
                string profile = Path.GetFullPath(Environment.ExpandEnvironmentVariables(dataPath.Text.Trim()));
                if (!Path.IsPathRooted(target) || !Path.IsPathRooted(profile) ||
                    target.StartsWith(@"\\") || profile.StartsWith(@"\\"))
                    return "Please select local drive folders (C:, D:, etc.), not network paths.";
                if (target.Length > 205 || profile.Length > 205)
                    return "Folder path is too long. Choose a shorter directory.";
                if (string.Equals(target.TrimEnd('\\'), profile.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) ||
                    profile.StartsWith(target.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                    return "Choose a separate local data folder outside the application installation folder.";
                if (string.Equals(target.TrimEnd('\\'), Path.GetPathRoot(target).TrimEnd('\\'),
                        StringComparison.OrdinalIgnoreCase))
                    return "Please choose a folder within the drive instead of the drive root itself.";
                var recordedInstall = GetInstalledLocation();
                if (Directory.Exists(Path.Combine(DefaultInstall, "current")) ||
                    (!string.IsNullOrEmpty(recordedInstall) &&
                     Directory.Exists(Path.Combine(recordedInstall, "current"))))
                    return "BIG BROTHER Desktop is already installed. Open that application for updates; this wizard is for fresh installs only.";
                if (Directory.Exists(target) && Directory.GetFileSystemEntries(target).Length > 0)
                    return "The selected application folder is not empty. Choose a new empty folder.";
                EnsureWritableFolder(target);
                EnsureWritableFolder(profile);
                var drive = new DriveInfo(Path.GetPathRoot(target));
                if (drive.IsReady && drive.AvailableFreeSpace < 400L * 1024 * 1024)
                    return "Not enough free space on the chosen application drive. At least 400 MB is recommended.";
                installPath.Text = target;
                dataPath.Text = profile;
                return null;
            }
            catch (Exception e) { return "Cannot use the selected folders: " + e.Message; }
        }

        private static void EnsureWritableFolder(string directory)
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".bb-setup-" + Guid.NewGuid().ToString("N"));
            try { File.WriteAllText(probe, "test"); }
            finally { if (File.Exists(probe)) File.Delete(probe); }
        }

        private static string GetInstalledLocation()
        {
            try
            {
                using (RegistryKey reg = Registry.CurrentUser.OpenSubKey(RegistryPath))
                    return reg == null ? null : reg.GetValue("InstallDirectory") as string;
            }
            catch { return null; }
        }

        private static ApprovedRelease FindLatestApprovedRelease()
        {
            var address = "https://api.github.com/repos/" + Repo + "/releases?per_page=30";
            var req = (HttpWebRequest)WebRequest.Create(address);
            req.UserAgent = "BIG-BROTHER-Desktop-Online-Setup";
            req.Accept = "application/vnd.github+json";
            req.Timeout = 25000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var reader = new StreamReader(resp.GetResponseStream()))
            {
                var serializer = new JavaScriptSerializer();
                serializer.MaxJsonLength = 20 * 1024 * 1024;
                var items = serializer.DeserializeObject(reader.ReadToEnd()) as object[];
                if (items == null) throw new Exception("GitHub did not return a release list.");
                ApprovedRelease best = null;
                foreach (var item in items)
                {
                    var release = item as Dictionary<string, object>;
                    if (release == null || Bool(release, "draft") || !Bool(release, "prerelease")) continue;
                    var tag = Str(release, "tag_name");
                    if (!tag.StartsWith("desktop-dev-v", StringComparison.Ordinal)) continue;
                    Version version;
                    if (!Version.TryParse(tag.Substring("desktop-dev-v".Length), out version)) continue;
                    var assets = Value(release, "assets") as object[];
                    if (assets == null) continue;
                    foreach (var asset in assets)
                    {
                        var data = asset as Dictionary<string, object>;
                        if (data == null || Str(data, "name") != InstallerName) continue;
                        var url = Str(data, "browser_download_url");
                        var digest = Str(data, "digest");
                        var prefix = "https://github.com/" + Repo + "/releases/download/" + tag + "/";
                        if (url != prefix + InstallerName) continue;
                        if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ||
                            digest.Length != 71) continue;
                        var checksum = digest.Substring(7);
                        bool valid = true;
                        foreach (var ch in checksum)
                            if (!Uri.IsHexDigit(ch)) { valid = false; break; }
                        if (!valid) continue;
                        var releaseFound = new ApprovedRelease {
                            Version = version, Tag = tag, Url = url,
                            Sha256 = checksum.ToUpperInvariant(),
                            Bytes = Long(data, "size")
                        };
                        if (best == null || releaseFound.Version.CompareTo(best.Version) > 0)
                            best = releaseFound;
                    }
                }
                if (best == null) throw new Exception("No approved BIG BROTHER Desktop development release is available.");
                return best;
            }
        }

        private static object Value(Dictionary<string, object> dict, string name)
        {
            object v;
            return dict.TryGetValue(name, out v) ? v : null;
        }
        private static string Str(Dictionary<string, object> d, string n)
        {
            var v = Value(d, n);
            return v == null ? "" : Convert.ToString(v);
        }
        private static bool Bool(Dictionary<string, object> d, string n)
        {
            return Value(d, n) is bool && (bool)Value(d, n);
        }
        private static long Long(Dictionary<string, object> d, string n)
        {
            var v = Value(d, n);
            return v == null ? 0 : Convert.ToInt64(v);
        }

        private async Task RunInstallation()
        {
            try
            {
                note.Text = "Checking the approved release on GitHub...";
                selectedRelease = await Task.Run(() => FindLatestApprovedRelease());
                note.Text = "Found BIG BROTHER Desktop " + selectedRelease.Version + ". Downloading the installer...";
                temporaryDirectory = Path.Combine(Path.GetTempPath(), "BB-Desktop-Online-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temporaryDirectory);
                var installerPath = Path.Combine(temporaryDirectory, InstallerName);

                using (var wc = new WebClient())
                {
                    downloadClient = wc;
                    wc.Headers[HttpRequestHeader.UserAgent] = "BIG-BROTHER-Desktop-Online-Setup";
                    wc.DownloadProgressChanged += (s, evt) =>
                    {
                        if (IsDisposed) return;
                        int percent = Math.Max(0, Math.Min(100, evt.ProgressPercentage));
                        progress.Value = Math.Min(80, (int)(percent * 0.8));
                        note.Text = "Downloading approved version " + selectedRelease.Version +
                            " from GitHub... " + percent + "%\r\n" +
                            "This is the only full download required for a fresh installation.";
                    };
                    await wc.DownloadFileTaskAsync(new Uri(selectedRelease.Url), installerPath);
                }
                downloadClient = null;

                note.Text = "Verifying the downloaded installer SHA-256 checksum...";
                progress.Value = 84;
                var actualHash = await Task.Run(() => Sha256(installerPath));
                if (!string.Equals(actualHash, selectedRelease.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("The downloaded installer checksum does not match GitHub. No installation was attempted.");

                note.Text = "Installing to " + installPath.Text + "\r\nPlease keep this window open.";
                progress.Value = 90;
                installing = true;
                cancel.Enabled = false;
                var installDirectory = installPath.Text;
                var exit = await Task.Run(() =>
                {
                    var start = new ProcessStartInfo(installerPath);
                    start.Arguments = "--silent --installto \"" + installDirectory.Replace("\"", "") + "\"";
                    start.UseShellExecute = false;
                    start.CreateNoWindow = true;
                    using (var proc = Process.Start(start))
                    {
                        if (proc == null) throw new Exception("Could not start Windows installation engine.");
                        proc.WaitForExit();
                        return proc.ExitCode;
                    }
                });
                installing = false;
                if (exit != 0)
                    throw new Exception("Windows installation engine returned code " + exit + ".");
                var metadata = Path.Combine(installDirectory, "current", "sq.version");
                if (!File.Exists(metadata))
                    throw new Exception("The installer returned success, but the installed application could not be verified.");

                using (var key = Registry.CurrentUser.CreateSubKey(RegistryPath))
                {
                    if (key == null) throw new Exception("Windows could not store your selected local settings path.");
                    key.SetValue("InstallDirectory", installDirectory, RegistryValueKind.String);
                    key.SetValue("DataDirectory", dataPath.Text, RegistryValueKind.String);
                }
                progress.Value = 100;
                busy = false;
                step = 3;
                Render();
            }
            catch (Exception e)
            {
                installing = false;
                busy = false;
                note.Text = "Installation stopped: " + e.Message +
                            "\r\nNo application folder was manually deleted.";
                progress.Value = 0;
                next.Enabled = true;
                next.Text = "Try Again";
                next.Click -= NextClicked;
                next.Click += RetryClicked;
                back.Visible = true;
                cancel.Enabled = true;
                MessageBox.Show(this, e.Message, "BIG BROTHER Desktop Setup",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (!string.IsNullOrEmpty(temporaryDirectory))
                {
                    try { Directory.Delete(temporaryDirectory, true); } catch { }
                }
            }
        }

        private void RetryClicked(object sender, EventArgs e)
        {
            next.Click -= RetryClicked;
            next.Click += NextClicked;
            step = 1;
            Render();
        }

        private static string Sha256(string path)
        {
            using (var file = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
        }

        private void StartApp()
        {
            try
            {
                string root = installPath.Text;
                var exe = Path.Combine(root, "current", "BIG-BROTHER-Desktop.exe");
                var rootExe = Directory.GetFiles(root, "*.exe");
                foreach (var item in rootExe)
                {
                    var name = Path.GetFileName(item).ToLowerInvariant();
                    if (name.Contains("desktop") && name != "update.exe")
                    {
                        exe = item; break;
                    }
                }
                if (!File.Exists(exe)) throw new FileNotFoundException("Installed Desktop executable not found.");
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            }
            catch (Exception e)
            {
                MessageBox.Show(this, "Installation is complete. You can open BIG BROTHER Desktop from the Start menu.\r\n" +
                    e.Message, "BIG BROTHER Desktop", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void WhenClosing(object sender, FormClosingEventArgs e)
        {
            if (installing)
            {
                e.Cancel = true;
                MessageBox.Show(this, "Windows installation is in progress. Please do not close setup until it finishes.",
                    "BIG BROTHER Desktop", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (busy && downloadClient != null)
            {
                if (MessageBox.Show(this, "Cancel the current installer download?",
                    "BIG BROTHER Desktop", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    e.Cancel = true; return;
                }
                downloadClient.CancelAsync();
            }
        }
    }
}

using Velopack;
using Velopack.Locators;
using Velopack.Sources;

const string repository = "https://github.com/angsokhey11-cloud/big-brother-desktop";
const string expected = "0.2.7";
var directory = Path.Combine(Path.GetTempPath(), "bb-desktop-updater-probe-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);

try
{
    // Test the real, anonymous GithubSource used by the installed WPF app;
    // simulate the existing v0.2.6 development installation without starting
    // the accounting UI or accessing any Supabase credentials/transactions.
    var locator = new TestVelopackLocator(
        appId: "BigBrother.Desktop",
        version: "0.2.6",
        packagesDir: directory,
        appDir: directory,
        rootDir: directory,
        updateExe: null,
        channel: "dev");
    var source = new GithubSource(repository, accessToken: null, prerelease: true);
    var manager = new UpdateManager(source,
        new UpdateOptions { ExplicitChannel = "dev" }, locator);

    if (!manager.IsInstalled)
        throw new Exception("Mock v0.2.6 install was not recognized");
    var candidate = await manager.CheckForUpdatesAsync();
    if (candidate is null)
        throw new Exception("GithubSource did not detect an available Desktop update");
    var newVersion = candidate.TargetFullRelease.Version.ToString();
    if (newVersion != expected)
        throw new Exception("Unexpected GithubSource update: " + newVersion);

    Console.WriteLine("PASS GitHub prerelease feed discovery, channel dev, 0.2.6 -> " + newVersion);
    var last = -20;
    await manager.DownloadUpdatesAsync(candidate, p =>
    {
        if (p - last >= 20 || p == 100)
        {
            Console.WriteLine("Update download: " + p + "%");
            last = p;
        }
    });
    var pending = manager.UpdatePendingRestart?.Version.ToString();
    if (pending != expected)
        throw new Exception("Downloaded update not staged locally: " + (pending ?? "(null)"));
    Console.WriteLine("PASS downloaded package staged and can be installed after user approves restart");

    // A fresh UpdateManager instance should find the already-downloaded
    // update; this is the bugfix tested by the Desktop UI in v0.2.7.
    var secondManager = new UpdateManager(source,
        new UpdateOptions { ExplicitChannel = "dev" }, locator);
    if (secondManager.UpdatePendingRestart?.Version.ToString() != expected)
        throw new Exception("Downloaded package not reusable across update checks");
    Console.WriteLine("PASS saved package survives new UpdateManager instance; no forced second download");
}
finally
{
    try { Directory.Delete(directory, recursive: true); }
    catch (Exception ex) { Console.WriteLine("Temp cleanup: " + ex.Message); }
}

# BIG BROTHER Desktop - One-time GitHub update bootstrap for older installations.
# PowerShell 5.1+, no Administrator rights required, no ZIP extraction.
# Once upgraded to v0.2.4 or later, the Desktop app itself checks GitHub.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = 'angsokhey11-cloud/big-brother-desktop'
$root = Join-Path $env:LOCALAPPDATA 'BigBrother.Desktop'
$updater = Join-Path $root 'Update.exe'
$metaFile = Join-Path $root 'current\sq.version'
$download = $null
$scriptFailed = $false

try {
    Write-Host ''
    Write-Host 'BIG BROTHER Desktop - Enable GitHub Automatic Updates' -ForegroundColor Cyan
    Write-Host '-----------------------------------------------------'
    Write-Host ''

    if (-not (Test-Path $updater -PathType Leaf) -or -not (Test-Path $metaFile -PathType Leaf)) {
        throw 'Existing BIG BROTHER Desktop installation not found. Setup.exe is for first-time installation.'
    }

    [xml]$installedMeta = Get-Content -LiteralPath $metaFile -Raw
    $installedId = [string]$installedMeta.package.metadata.id
    $installedVersion = [string]$installedMeta.package.metadata.version
    if ($installedId -ne 'BigBrother.Desktop' -or $installedVersion -notmatch '^\d+\.\d+\.\d+(?:-[\w.-]+)?$') {
        throw 'Installed application identity/version is invalid; no files were changed.'
    }

    $headers = @{
        'User-Agent' = 'BIG-BROTHER-Desktop-Updater'
        'Accept' = 'application/vnd.github+json'
        'X-GitHub-Api-Version' = '2022-11-28'
    }
    $releases = @(Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases?per_page=30" -Headers $headers)
    $versions = @(
        foreach ($release in $releases) {
            if ($release.draft -or -not $release.prerelease) { continue }
            $tag = [string]$release.tag_name
            if ($tag -notmatch '^desktop-dev-v(\d+\.\d+\.\d+)$') { continue }
            $version = $Matches[1]
            $name = "BigBrother.Desktop-$version-dev-full.nupkg"
            $asset = @($release.assets | Where-Object { $_.name -ceq $name }) | Select-Object -First 1
            if (-not $asset) { continue }
            [pscustomobject]@{ Version = [version]$version; VersionText = $version; Asset = $asset }
        }
    )
    $latest = $versions | Sort-Object Version -Descending | Select-Object -First 1
    if (-not $latest) {
        Write-Host 'No approved GitHub Desktop development updates are published yet.'
        Read-Host 'Press Enter to close'
if ($scriptFailed) { exit 1 }
        exit 0
    }

    $old = [version](($installedVersion -split '-')[0])
    if ($latest.Version -le $old) {
        Write-Host "BIG BROTHER Desktop $installedVersion is already up to date."
        Read-Host 'Press Enter to close'
        exit 0
    }

    if (@(Get-Process -Name 'BIG-BROTHER-Desktop' -ErrorAction SilentlyContinue).Count -gt 0) {
        throw 'Save your accounting work and CLOSE BIG BROTHER Desktop before updating.'
    }

    $url = [string]$latest.Asset.browser_download_url
    $expectedPrefix = "https://github.com/$repo/releases/download/"
    if (-not $url.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Unexpected GitHub release asset URL; update refused.'
    }
    $digest = [string]$latest.Asset.digest
    if ($digest -notmatch '^sha256:([a-fA-F0-9]{64})$') {
        throw 'GitHub did not provide a SHA256 release digest. Update refused to protect this PC.'
    }
    $expectedSha = $Matches[1].ToUpperInvariant()

    Write-Host "Current installed version : $installedVersion"
    Write-Host "Published GitHub version : $($latest.VersionText)"
    Write-Host 'Download type            : Velopack package (not Setup.exe / ZIP)'
    Write-Host 'Source                   : GitHub Releases (development channel)'
    Write-Host ''
    Write-Host 'No accounting data or browser profiles will be deliberately removed.' -ForegroundColor Yellow
    $confirmation = Read-Host 'Type UPDATE to download and install (anything else cancels)'
    if ($confirmation -cne 'UPDATE') {
        Write-Host 'Update cancelled.'
        exit 0
    }

    $download = Join-Path $env:TEMP ("BigBrother.Desktop-$($latest.VersionText)-dev-full.nupkg")
    Write-Host 'Downloading the update package directly from GitHub...'
    Invoke-WebRequest -Uri $url -OutFile $download -UseBasicParsing -MaximumRedirection 8

    # Use .NET directly for SHA256: available even when the PowerShell
    # Get-FileHash cmdlet is missing/restricted on a Windows workstation.
    $stream = [System.IO.File]::OpenRead($download)
    try {
        $algorithm = [System.Security.Cryptography.SHA256]::Create()
        try {
            $sha = ([System.BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '')
        }
        finally { $algorithm.Dispose() }
    }
    finally { $stream.Dispose() }
    if ($sha -cne $expectedSha) {
        throw 'Downloaded GitHub release SHA256 mismatch! No installation attempted.'
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($download)
    try {
        $nuspec = $zip.Entries | Where-Object { $_.FullName -match '^[^/]+\.nuspec$' } | Select-Object -First 1
        if (-not $nuspec) { throw 'Update package metadata is missing.' }
        $reader = New-Object System.IO.StreamReader($nuspec.Open())
        try { [xml]$xml = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ([string]$xml.package.metadata.id -ne 'BigBrother.Desktop' -or
            [string]$xml.package.metadata.version -ne $latest.VersionText) {
            throw 'Downloaded update identity/version does not match its GitHub release.'
        }
    }
    finally { $zip.Dispose() }

    Write-Host 'SHA256 and application identity verified.' -ForegroundColor Green
    Write-Host 'Updating the existing installation with Velopack...'

    # Update.exe is a Windows-subsystem executable. In Windows PowerShell 5.1,
    # invoking it with the call operator may leave $LASTEXITCODE unset even
    # when the upgrade actually succeeded. An unset exit code compares
    # unequal to 0 and previously caused a FALSE "Update stopped" warning.
    # Start-Process -Wait -PassThru has a real process ExitCode and is already
    # exercised by our Windows in-place regression workflow.
    $quotedPackage = '"' + $download + '"'
    $updateProcess = Start-Process -FilePath $updater -ArgumentList @(
        'apply', '--norestart', '--package', $quotedPackage
    ) -Wait -PassThru
    $updateExit = $updateProcess.ExitCode

    # Installed-version metadata is the authoritative result. Do not report
    # a failed update just because Windows did not expose a process exit code,
    # and never report success without verifying the expected version.
    $resultVersion = ''
    for ($attempt = 0; $attempt -lt 12; $attempt++) {
        if (Test-Path -LiteralPath $metaFile -PathType Leaf) {
            try {
                [xml]$result = Get-Content -LiteralPath $metaFile -Raw
                $resultVersion = [string]$result.package.metadata.version
                if ($resultVersion -eq $latest.VersionText) { break }
            }
            catch { }
        }
        Start-Sleep -Milliseconds 500
    }
    if ($resultVersion -ne $latest.VersionText) {
        throw ("Updater did not install the expected version. Installed: " +
            $resultVersion + "; expected: " + $latest.VersionText +
            "; updater exit code: " + $updateExit + ". Do not delete installation files.")
    }
    Write-Host "Verified installed version: $resultVersion" -ForegroundColor Green

    Write-Host ''
    Write-Host "SUCCESS! BIG BROTHER Desktop v$($latest.VersionText) is installed." -ForegroundColor Green
    Write-Host 'From now on, open the Desktop app to check GitHub updates automatically.'
    Write-Host 'No need to download GitHub Actions ZIP files again.'
}
catch {
    Write-Host ''
    Write-Host ("Update stopped: " + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'No uninstall or manual deletion was attempted.'
    $scriptFailed = $true
}
finally {
    if ($download -and (Test-Path $download -PathType Leaf)) {
        try { Remove-Item -LiteralPath $download -Force -ErrorAction SilentlyContinue } catch {}
    }
}
Read-Host 'Press Enter to close'

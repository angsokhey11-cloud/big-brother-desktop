# BIG BROTHER Desktop — Safe manual upgrade from an official development artifact
# Uses the installed Velopack updater; NEVER deletes the app directory.
# Keep this file together with BigBrother.Desktop-<version>-dev-full.nupkg.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$install = Join-Path $env:LOCALAPPDATA 'BigBrother.Desktop'
$updater = Join-Path $install 'Update.exe'
$current = Join-Path $install 'current'
$package = $null
try {
    Write-Host ''
    Write-Host 'BIG BROTHER Desktop - Update Existing Installation' -ForegroundColor Cyan
    Write-Host '--------------------------------------------------'
    Write-Host ''

    if (-not (Test-Path -LiteralPath $updater -PathType Leaf)) {
        throw "No existing Velopack Desktop installation was found at $install. Use the Setup.exe for a first installation."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $current 'sq.version') -PathType Leaf)) {
        throw 'The installed Desktop metadata is missing. Do not modify its files; ask for assistance.'
    }
    $packages = @(Get-ChildItem -LiteralPath $PSScriptRoot -File -Filter 'BigBrother.Desktop-*-dev-full.nupkg')
    if ($packages.Count -ne 1) {
        throw "Expected exactly one BigBrother.Desktop-*-dev-full.nupkg in this folder, found $($packages.Count). Extract only ONE Desktop release artifact into its own folder."
    }
    $package = $packages[0]

    Add-Type -AssemblyName System.IO.Compression
    $zip = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $nuspecEntry = $zip.Entries | Where-Object { $_.FullName -match '^[^/]+\.nuspec$' } | Select-Object -First 1
        if (-not $nuspecEntry) {
            throw "The downloaded update package has no .nuspec metadata; refusing to install it."
        }
        $reader = New-Object System.IO.StreamReader($nuspecEntry.Open())
        try { [xml]$meta = $reader.ReadToEnd() }
        finally { $reader.Dispose() }
        $packageId = [string]$meta.package.metadata.id
        $versionText = [string]$meta.package.metadata.version
        if ($packageId -ne 'BigBrother.Desktop' -or $versionText -notmatch '^\d+\.\d+\.\d+(?:-[\w.-]+)?$') {
            throw "Downloaded package identity/version is unexpected: $packageId $versionText"
        }
    }
    finally { $zip.Dispose() }

    [xml]$currentMeta = Get-Content -LiteralPath (Join-Path $current 'sq.version') -Raw
    $currentId = [string]$currentMeta.package.metadata.id
    $installedText = [string]$currentMeta.package.metadata.version
    if ($currentId -ne 'BigBrother.Desktop') {
        throw "Installed Desktop identity does not match: $currentId. No update was attempted."
    }

    $newBase = ($versionText -split '-')[0]
    $oldBase = ($installedText -split '-')[0]
    $newVersion = [version]$newBase
    $oldVersion = [version]$oldBase
    if ($newVersion -le $oldVersion) {
        throw "This update ($versionText) is not newer than the installed version ($installedText). Nothing changed."
    }

    $running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.ProcessName -eq 'BIG-BROTHER-Desktop'
    })
    if ($running.Count -gt 0) {
        throw 'BIG BROTHER Desktop is still running. Save any work, close the app completely, then run this updater again.'
    }

    Write-Host "Installed version : $installedText"
    Write-Host "Available version : $versionText"
    Write-Host "Install directory : $install"
    Write-Host "Package file      : $($package.Name)"
    Write-Host ''
    Write-Host 'This updates only the application. It does not uninstall the app or delete browser profiles.' -ForegroundColor Yellow
    Write-Host 'This is a DEVELOPMENT release. The online accounting database stays unchanged.'
    Write-Host ''
    $confirm = Read-Host "Enter UPDATE to continue (anything else cancels)"
    if ($confirm -cne 'UPDATE') {
        Write-Host 'Update cancelled. No changes made.'
        exit 0
    }

    Write-Host 'Applying update with the installed Velopack updater...' -ForegroundColor Cyan
    & $updater apply --package $package.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "Installed updater returned exit code $LASTEXITCODE. Your prior version should still be available. Do not delete any installation files."
    }
    Write-Host 'Updater completed. Launch BIG BROTHER Desktop from the Start menu.'
}
catch {
    Write-Host ''
    Write-Host ("Unable to update: " + $_.Exception.Message) -ForegroundColor Red
    Write-Host 'Your existing installation and data folders have not been deliberately removed.'
    Read-Host 'Press Enter to close'
    exit 1
}
Read-Host 'Press Enter to close'

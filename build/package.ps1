<#
.SYNOPSIS
  Builds a MageQuit-DA release.

.DESCRIPTION
  1. Builds the BepInEx plugin (needs the game's Managed DLLs; pass -GameDir if not in the default Steam path).
  2. Stages dist/payload (game overlay + translations) and zips it to dist/payload.zip.
  3. Publishes the Manager as single-file executables (payload embedded) for win-x64 and linux-x64.
  4. Produces dist/MageQuit-DA-<version>-<rid>.zip release archives.

  Use -StageOnly to only stage dist/payload (enough for running the Manager from source).
#>
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\MageQuit",
    [string[]]$Runtimes = @("win-x64", "linux-x64"),
    [switch]$StageOnly
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root "dist"
$stage = Join-Path $dist "payload"
$version = (Get-Content (Join-Path $root "VERSION") -Raw).Trim()

function Copy-Tree($from, $to) {
    New-Item -ItemType Directory -Force $to | Out-Null
    Copy-Item -Path (Join-Path $from "*") -Destination $to -Recurse -Force
    # Copy-Item skips dot files with wildcards
    Get-ChildItem -Path $from -Force -File | Where-Object { $_.Name.StartsWith(".") } |
        ForEach-Object { Copy-Item $_.FullName -Destination $to -Force }
}

Write-Host "== Building plugin"
dotnet build (Join-Path $root "src\MageQuitDA.Plugin") -c Release "-p:GameDir=$GameDir" --nologo -v q
if ($LASTEXITCODE) { throw "Plugin build failed" }

Write-Host "== Staging payload $version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$game = Join-Path $stage "game"
Copy-Tree (Join-Path $root "vendor\BepInEx") $game
Remove-Item (Join-Path $game "changelog.txt") -ErrorAction SilentlyContinue
Copy-Tree (Join-Path $root "vendor\XUnity.AutoTranslator\BepInEx") (Join-Path $game "BepInEx")
Copy-Tree (Join-Path $root "payload\game") $game
$pluginDir = Join-Path $game "BepInEx\plugins\MageQuitDA"
New-Item -ItemType Directory -Force $pluginDir | Out-Null
Copy-Item (Join-Path $root "src\MageQuitDA.Plugin\bin\Release\net46\MageQuitDA.Plugin.dll") $pluginDir
$licenses = Join-Path $game "BepInEx\MageQuit-DA\licenses"
Copy-Tree (Join-Path $root "vendor\licenses") $licenses
Copy-Item (Join-Path $root "LICENSE") (Join-Path $licenses "MageQuit-DA-LICENSE.txt")
Copy-Item (Join-Path $root "translation\strings.json") $stage
Copy-Item (Join-Path $root "translation\labels.json") $stage
Set-Content -Path (Join-Path $stage "VERSION") -Value $version -NoNewline

$zip = Join-Path $dist "payload.zip"
if (Test-Path $zip) { Remove-Item $zip }
# Build the zip with forward slashes (Compress-Archive on PS 5.1 writes backslashes).
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, "Create")
try {
    Get-ChildItem $stage -Recurse -File -Force | ForEach-Object {
        $rel = $_.FullName.Substring($stage.Length + 1).Replace("\", "/")
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $rel) | Out-Null
    }
} finally { $archive.Dispose() }
Write-Host "   $zip"
if ($StageOnly) { return }

foreach ($rid in $Runtimes) {
    Write-Host "== Publishing Manager ($rid)"
    $out = Join-Path $dist "publish\$rid"
    dotnet publish (Join-Path $root "src\MageQuitDA.Manager") -c Release -r $rid -o $out --nologo -v q
    if ($LASTEXITCODE) { throw "Publish failed for $rid" }

    $release = Join-Path $dist "MageQuit-DA-$version-$rid.zip"
    if (Test-Path $release) { Remove-Item $release }
    $files = @(Get-ChildItem $out -File | Where-Object { $_.Extension -ne ".pdb" })
    $files += Get-Item (Join-Path $root "README.md")
    Compress-Archive -Path $files.FullName -DestinationPath $release
    Write-Host "   $release"
}

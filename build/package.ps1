<#
.SYNOPSIS
  Builds a MageQuit Translator release.

.DESCRIPTION
  1. Builds the BepInEx plugin (Unity reference assemblies come from the BepInEx NuGet feed).
  2. Stages dist/payload: the game overlay (BepInEx, XUnity, plugin, configs, font data) and every
     language in translation/<code>/, then zips it to dist/payload.zip.
  3. Publishes the app as single-file executables (payload embedded) for each runtime.
  4. Produces dist/MageQuit-Translator-<version>-<rid>.zip release archives.

  Use -StageOnly to only stage dist/payload (enough for running the app from source).
#>
param(
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
dotnet build (Join-Path $root "src/MageQuitTranslator.Plugin") -c Release --nologo -v q
if ($LASTEXITCODE) { throw "Plugin build failed" }

Write-Host "== Staging payload $version"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$game = Join-Path $stage "game"
Copy-Tree (Join-Path $root "vendor/BepInEx") $game
Remove-Item (Join-Path $game "changelog.txt") -ErrorAction SilentlyContinue
Copy-Tree (Join-Path $root "vendor/XUnity.AutoTranslator/BepInEx") (Join-Path $game "BepInEx")
Copy-Tree (Join-Path $root "payload/game") $game
$pluginDir = Join-Path $game "BepInEx/plugins/MageQuitTranslator"
New-Item -ItemType Directory -Force $pluginDir | Out-Null
Copy-Item (Join-Path $root "src/MageQuitTranslator.Plugin/bin/Release/net46/MageQuitTranslator.Plugin.dll") $pluginDir
$licenses = Join-Path $game "BepInEx/MageQuit-Translator/licenses"
Copy-Tree (Join-Path $root "vendor/licenses") $licenses
Copy-Item (Join-Path $root "LICENSE") (Join-Path $licenses "MageQuit-Translator-LICENSE.txt")

foreach ($lang in Get-ChildItem (Join-Path $root "translation") -Directory) {
    if (-not (Test-Path (Join-Path $lang.FullName "language.json"))) { continue }
    $dest = Join-Path $stage "languages/$($lang.Name)"
    New-Item -ItemType Directory -Force $dest | Out-Null
    foreach ($f in "language.json", "strings.json", "labels.json") {
        $src = Join-Path $lang.FullName $f
        if (Test-Path $src) { Copy-Item $src $dest }
    }
    Write-Host "   language: $($lang.Name)"
}
Set-Content -Path (Join-Path $stage "VERSION") -Value $version -NoNewline

$zip = Join-Path $dist "payload.zip"
if (Test-Path $zip) { Remove-Item $zip }
# Build the zip with forward slashes (Compress-Archive on Windows PowerShell writes backslashes).
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
    Write-Host "== Publishing app ($rid)"
    $out = Join-Path $dist "publish/$rid"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    dotnet publish (Join-Path $root "src/MageQuitTranslator.Manager") -c Release -r $rid -o $out --nologo -v q
    if ($LASTEXITCODE) { throw "Publish failed for $rid" }

    $release = Join-Path $dist "MageQuit-Translator-$version-$rid.zip"
    if (Test-Path $release) { Remove-Item $release }
    $files = @(Get-ChildItem $out -File | Where-Object { $_.Extension -ne ".pdb" })
    $files += Get-Item (Join-Path $root "README.md")
    Compress-Archive -Path $files.FullName -DestinationPath $release
    Write-Host "   $release"
}

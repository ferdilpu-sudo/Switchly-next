param(
    [string]$Version = "0.9.0-beta.1",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "src\Switchly.App\Switchly.App.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\win-x64"
$releaseDir = Join-Path $repoRoot "artifacts\release"

Write-Host "==> Packaging Switchly $Version (win-x64, SelfContained=$($SelfContained.IsPresent))" -ForegroundColor Cyan

if (Test-Path $publishDir) {
    Remove-Item -Path $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

$scFlag = if ($SelfContained.IsPresent) { "true" } else { "false" }

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained $scFlag `
    -p:Version=$Version `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:ContinuousIntegrationBuild=true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish gagal dengan exit code $LASTEXITCODE."
}

# Remove any transitive third-party .pdb files so developer paths and symbols are never shipped (S2-05)
Get-ChildItem -Path $publishDir -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

# Verify required CEF native assets before packaging
$requiredFiles = @(
    "Switchly.App.exe",
    "libcef.dll",
    "CefSharp.BrowserSubprocess.exe",
    "icudtl.dat",
    "locales\en-US.pak"
)

foreach ($rel in $requiredFiles) {
    $full = Join-Path $publishDir $rel
    if (-not (Test-Path $full)) {
        throw "Aset runtime wajib tidak ditemukan pada output publish: $rel"
    }
}

# Bundle legal notices and release notes
$noticesPath = Join-Path $repoRoot "THIRD-PARTY-NOTICES.md"
if (Test-Path $noticesPath) {
    Copy-Item $noticesPath (Join-Path $publishDir "THIRD-PARTY-NOTICES.md") -Force
}
$releaseNotesPath = Join-Path $repoRoot "docs\BETA-RELEASE-NOTES.md"
if (Test-Path $releaseNotesPath) {
    Copy-Item $releaseNotesPath (Join-Path $publishDir "BETA-RELEASE-NOTES.md") -Force
}
$privacyPath = Join-Path $repoRoot "docs\PRIVACY.md"
if (Test-Path $privacyPath) {
    Copy-Item $privacyPath (Join-Path $publishDir "PRIVACY.md") -Force
}
$guidePath = Join-Path $repoRoot "docs\USER-GUIDE-BETA.md"
if (Test-Path $guidePath) {
    Copy-Item $guidePath (Join-Path $publishDir "USER-GUIDE-BETA.md") -Force
}

# 1. Create Portable ZIP
$zipName = "Switchly-$Version-win-x64-portable.zip"
$zipPath = Join-Path $releaseDir $zipName
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
Write-Host "==> Portable ZIP dibuat: $zipPath" -ForegroundColor Green

$checksumLines = @()
$zipHash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumLines += "$zipHash  $zipName"

# 2. Build Per-User Inno Setup Installer (if ISCC.exe is installed)
$isccCandidates = @(
    (Get-Command "ISCC.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }

$iscc = $isccCandidates | Select-Object -First 1
if ($iscc) {
    $issPath = Join-Path $PSScriptRoot "Switchly.iss"
    Write-Host "==> Menjalankan Inno Setup Compiler: $iscc" -ForegroundColor Cyan
    & $iscc "/DAppVersion=$Version" "/DPublishDir=$publishDir" "/DOutputDir=$releaseDir" $issPath
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC.exe gagal dengan exit code $LASTEXITCODE."
    }
    $exeName = "Switchly-$Version-win-x64-setup.exe"
    $exePath = Join-Path $releaseDir $exeName
    if (Test-Path $exePath) {
        $exeHash = (Get-FileHash -Path $exePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $checksumLines += "$exeHash  $exeName"
        Write-Host "==> Setup Installer dibuat: $exePath" -ForegroundColor Green
    }
}
else {
    Write-Warning "ISCC.exe (Inno Setup 6) tidak ditemukan di mesin ini. Portable ZIP sudah dibuat; pasang Inno Setup 6 lalu jalankan ulang skrip ini untuk membangun Setup EXE."
}

$checksumPath = Join-Path $releaseDir "SHA256SUMS.txt"
[System.IO.File]::WriteAllLines($checksumPath, $checksumLines, [System.Text.UTF8Encoding]::new($false))
Write-Host "==> Checksum ditulis (UTF-8 tanpa BOM) ke: $checksumPath" -ForegroundColor Green

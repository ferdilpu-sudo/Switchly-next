<#
.SYNOPSIS
    Builds an unsigned Microsoft Store MSIX update package for Switchly (9PH5FF24TKZR).
    Microsoft Partner Center signs the .msix package automatically for free upon submission.

.EXAMPLE
    .\packaging\package-msix.ps1 `
        -IdentityName "12345Publisher.Switchly" `
        -Publisher "CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX" `
        -PublisherDisplayName "Switchly" `
        -Version "2.0.0.0"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$IdentityName,

    [Parameter(Mandatory = $true)]
    [string]$Publisher,

    [Parameter(Mandatory = $false)]
    [string]$PublisherDisplayName = "Switchly",

    [Parameter(Mandatory = $false)]
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = "2.0.0.0"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$stageDir = Join-Path $repoRoot "artifacts\msix-stage\win-x64"
$releaseDir = Join-Path $repoRoot "artifacts\release"
$outputMsix = Join-Path $releaseDir "Switchly-$Version-win-x64.msix"

# 1. Locate makeappx.exe from installed Windows 10/11 SDK
$kitsBin = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
$makeAppx = Get-ChildItem -Path $kitsBin -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match '\\x64\\makeappx\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $makeAppx) {
    throw "makeappx.exe not found under '$kitsBin'. Install Windows 11 SDK (App Certification Kit / MSIX tools)."
}
Write-Host "Using makeappx.exe: $($makeAppx.FullName)"

# 2. Clean staging folder
if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

# 3. Publish Switchly.App self-contained win-x64 (includes .NET 10 runtime + CEF + proprietary overlay if present)
Write-Host "Publishing Switchly.App (Release, win-x64, self-contained, Version=$Version)..."
dotnet publish (Join-Path $repoRoot "src\Switchly.App\Switchly.App.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:ContinuousIntegrationBuild=true `
    -o $stageDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Get-ChildItem -Path $stageDir -Filter "*.pdb" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force

$requiredCefFiles = @("Switchly.App.exe", "libcef.dll", "CefSharp.BrowserSubprocess.exe", "icudtl.dat", "locales\en-US.pak")
foreach ($rel in $requiredCefFiles) {
    if (-not (Test-Path (Join-Path $stageDir $rel))) {
        throw "Required runtime asset missing in MSIX stage directory: $rel"
    }
}

# Include required third-party notices & privacy notice inside package
Copy-Item (Join-Path $repoRoot "THIRD-PARTY-NOTICES.md") $stageDir -Force
Copy-Item (Join-Path $repoRoot "docs\PRIVACY.md") $stageDir -Force

# 4. Copy or generate required MSIX PNG tile assets
$assetsSrc = Join-Path $PSScriptRoot "msix\Assets"
$assetsDst = Join-Path $stageDir "Assets"
New-Item -ItemType Directory -Force -Path $assetsDst | Out-Null

$requiredTiles = @(
    @{ Name = "StoreLogo.png"; Width = 50; Height = 50 },
    @{ Name = "Square44x44Logo.png"; Width = 44; Height = 44 },
    @{ Name = "Square150x150Logo.png"; Width = 150; Height = 150 },
    @{ Name = "Wide310x150Logo.png"; Width = 310; Height = 150 }
)

Add-Type -AssemblyName System.Drawing
foreach ($tile in $requiredTiles) {
    $customFile = Join-Path $assetsSrc $tile.Name
    $targetFile = Join-Path $assetsDst $tile.Name
    if (Test-Path $customFile) {
        Copy-Item $customFile $targetFile -Force
    } else {
        # Generate a clean Switchly monogram tile if custom PNGs are not yet placed in packaging/msix/Assets/
        $bmp = New-Object System.Drawing.Bitmap($tile.Width, $tile.Height)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.Clear([System.Drawing.Color]::FromArgb(37, 99, 235)) # #2563EB
        $fontSize = [math]::Max(14, [int]($tile.Height * 0.52))
        $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $sf = New-Object System.Drawing.StringFormat
        $sf.Alignment = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $rect = New-Object System.Drawing.RectangleF(0, 0, $tile.Width, $tile.Height)
        $g.DrawString("S", $font, [System.Drawing.Brushes]::White, $rect, $sf)
        $bmp.Save($targetFile, [System.Drawing.Imaging.ImageFormat]::Png)
        $font.Dispose(); $sf.Dispose(); $g.Dispose(); $bmp.Dispose()
    }
}

# 5. Render AppxManifest.xml with XML-escaped Partner Center identity
$templatePath = Join-Path $PSScriptRoot "msix\AppxManifest.template.xml"
$manifest = Get-Content $templatePath -Raw -Encoding UTF8
$manifest = $manifest.Replace("{{IDENTITY_NAME}}", [System.Security.SecurityElement]::Escape($IdentityName.Trim()))
$manifest = $manifest.Replace("{{PUBLISHER}}", [System.Security.SecurityElement]::Escape($Publisher.Trim()))
$manifest = $manifest.Replace("{{PUBLISHER_DISPLAY_NAME}}", [System.Security.SecurityElement]::Escape($PublisherDisplayName.Trim()))
$manifest = $manifest.Replace("{{VERSION}}", [System.Security.SecurityElement]::Escape($Version.Trim()))
[System.IO.File]::WriteAllText((Join-Path $stageDir "AppxManifest.xml"), $manifest, [System.Text.UTF8Encoding]::new($false))

# 6. Pack .msix (unsigned — Microsoft Store signs it automatically on upload)
if (Test-Path $outputMsix) { Remove-Item $outputMsix -Force }
Write-Host "Packing MSIX -> $outputMsix ..."
& $makeAppx.FullName pack /d $stageDir /p $outputMsix /o
if ($LASTEXITCODE -ne 0) {
    throw "makeappx pack failed with exit code $LASTEXITCODE"
}

$hash = (Get-FileHash $outputMsix -Algorithm SHA256).Hash
$sizeMb = [math]::Round((Get-Item $outputMsix).Length / 1MB, 1)
Write-Host "SUCCESS! Built Microsoft Store MSIX ($sizeMb MB): $outputMsix"
Write-Host "SHA-256: $hash"
Write-Host "Upload this .msix directly to Microsoft Partner Center (Store ID 9PH5FF24TKZR)."

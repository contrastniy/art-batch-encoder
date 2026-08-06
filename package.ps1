param(
    [string]$Version = "1.0"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$distRoot = Join-Path $projectRoot "dist"
$releaseName = "ART_Batch_Encoder_v$Version"
$releaseRoot = Join-Path $distRoot $releaseName
$zipPath = Join-Path $distRoot ($releaseName + ".zip")

function Copy-RequiredFile {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        throw "Required package file was not found: $Source"
    }

    $destinationDirectory = Split-Path -Parent $Destination
    if ($destinationDirectory -and -not (Test-Path -LiteralPath $destinationDirectory)) {
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    }

    Copy-Item -LiteralPath $Source -Destination $Destination -Force
}

function Copy-OptionalFile {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (Test-Path -LiteralPath $Source -PathType Leaf) {
        $destinationDirectory = Split-Path -Parent $Destination
        if ($destinationDirectory -and -not (Test-Path -LiteralPath $destinationDirectory)) {
            New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        }

        Copy-Item -LiteralPath $Source -Destination $Destination -Force
    }
}

function Copy-RuntimeDirectory {
    param(
        [string]$Source,
        [string]$Destination,
        [string[]]$FallbackReadme
    )

    New-Item -ItemType Directory -Path $Destination -Force | Out-Null

    # Runtime binaries are optional and intentionally kept outside source control.
    # When present locally, copy the complete directory tree into the release.
    if (Test-Path -LiteralPath $Source -PathType Container) {
        Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
        }
    }

    $readmePath = Join-Path $Destination "README.txt"
    if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf)) {
        Set-Content -LiteralPath $readmePath -Value $FallbackReadme -Encoding ASCII
    }
}

Write-Host "Packaging ART Batch Encoder v$Version..."

if (Test-Path -LiteralPath $releaseRoot) {
    Remove-Item -LiteralPath $releaseRoot -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

Copy-RequiredFile (Join-Path $projectRoot "bin\ARTBatchEncoder.exe") (Join-Path $releaseRoot "ARTBatchEncoder.exe")
Copy-RequiredFile (Join-Path $projectRoot "ARTBatchEncoder.exe.config") (Join-Path $releaseRoot "ARTBatchEncoder.exe.config")
Copy-RequiredFile (Join-Path $projectRoot "artbe_settings.ini") (Join-Path $releaseRoot "artbe_settings.ini")
Copy-RequiredFile (Join-Path $projectRoot "README.md") (Join-Path $releaseRoot "README.md")

Copy-OptionalFile (Join-Path $projectRoot "THIRD_PARTY.md") (Join-Path $releaseRoot "THIRD_PARTY.md")
Copy-OptionalFile (Join-Path $projectRoot "docs\art_batch_encoder.png") (Join-Path $releaseRoot "docs\art_batch_encoder.png")

Copy-RuntimeDirectory (Join-Path $projectRoot "ffmpeg") (Join-Path $releaseRoot "ffmpeg") @(
        "FFmpeg runtime folder",
        "=====================",
        "",
        "Place ffmpeg.exe and any accompanying FFmpeg runtime files here.",
        "Expected path: ffmpeg\ffmpeg.exe"
    )

Copy-RuntimeDirectory (Join-Path $projectRoot "openimageio") (Join-Path $releaseRoot "openimageio") @(
        "OpenImageIO runtime folder",
        "==========================",
        "",
        "Place oiiotool.exe and every DLL or support file distributed with it here.",
        "Expected path: openimageio\oiiotool.exe",
        "Also supported: openimageio\bin\oiiotool.exe"
    )

# Archive the release directory itself so extraction creates one clean top-level folder.
Compress-Archive -LiteralPath $releaseRoot -DestinationPath $zipPath -CompressionLevel Optimal -Force

if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) {
    throw "Release archive was not created: $zipPath"
}

Write-Host "Release folder: $releaseRoot"
Write-Host "Release archive: $zipPath"

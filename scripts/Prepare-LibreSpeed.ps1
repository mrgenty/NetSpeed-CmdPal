param(
    [ValidateSet("x64", "ARM64")]
    [string]$Architecture = "x64",
    [string]$Version = "1.0.13"
)

$ErrorActionPreference = "Stop"

$goArch = if ($Architecture -eq "ARM64") { "arm64" } else { "amd64" }
$archiveName = "librespeed-cli_${Version}_windows_${goArch}.zip"
$baseUrl = "https://github.com/librespeed/speedtest-cli/releases/download/v$Version"
$projectRoot = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $projectRoot "NetSpeed\ThirdParty\LibreSpeed"
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("netspeed-librespeed-" + [Guid]::NewGuid().ToString("N"))

New-Item -ItemType Directory -Force -Path $destination | Out-Null
New-Item -ItemType Directory -Force -Path $temp | Out-Null

try {
    $archivePath = Join-Path $temp $archiveName
    $checksumsPath = Join-Path $temp "checksums.txt"

    Write-Host "Downloading LibreSpeed CLI v$Version for $Architecture..."
    Invoke-WebRequest -Uri "$baseUrl/$archiveName" -OutFile $archivePath
    Invoke-WebRequest -Uri "$baseUrl/checksums.txt" -OutFile $checksumsPath

    $checksumLine = Get-Content $checksumsPath | Where-Object { $_ -match [regex]::Escape($archiveName) } | Select-Object -First 1
    if (-not $checksumLine) {
        throw "Checksum for $archiveName was not found in the upstream checksums file."
    }

    $expected = ($checksumLine -split '\s+')[0].Trim().ToLowerInvariant()
    $actual = (Get-FileHash -Path $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        throw "SHA256 mismatch for $archiveName. Expected $expected, got $actual."
    }

    $expanded = Join-Path $temp "expanded"
    Expand-Archive -Path $archivePath -DestinationPath $expanded -Force

    $exe = Get-ChildItem -Path $expanded -Recurse -Filter "librespeed-cli.exe" | Select-Object -First 1
    if (-not $exe) {
        throw "librespeed-cli.exe was not found in the upstream archive."
    }

    Copy-Item $exe.FullName (Join-Path $destination "librespeed-cli.exe") -Force

    $license = Get-ChildItem -Path $expanded -Recurse -Filter "LICENSE" | Select-Object -First 1
    if ($license) {
        Copy-Item $license.FullName (Join-Path $destination "LICENSE-LGPL-3.0.txt") -Force
    }

    Write-Host "LibreSpeed CLI prepared and checksum verified."
}
finally {
    Remove-Item -Path $temp -Recurse -Force -ErrorAction SilentlyContinue
}

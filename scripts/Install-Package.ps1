param(
    [string]$PackagePath,
    [string]$CertificatePath
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from PowerShell as Administrator."
}

if (-not $PackagePath) {
    $PackagePath = (Get-ChildItem -Path $PSScriptRoot -Filter "*.msix" | Select-Object -First 1).FullName
}
if (-not $CertificatePath) {
    $CertificatePath = (Get-ChildItem -Path $PSScriptRoot -Filter "*.cer" | Select-Object -First 1).FullName
}

if (-not $PackagePath -or -not (Test-Path $PackagePath)) {
    throw "MSIX package not found."
}
if (-not $CertificatePath -or -not (Test-Path $CertificatePath)) {
    throw "Signing certificate not found."
}

Write-Host "Importing development certificate into LocalMachine\TrustedPeople..."
Import-Certificate -FilePath $CertificatePath -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" | Out-Null

Write-Host "Installing $PackagePath ..."
Add-AppxPackage -Path $PackagePath
Write-Host "NetSpeed installed. Open PowerToys Command Palette and search for 'NetSpeed'."

# NetSpeed for PowerToys Command Palette

A compact Internet speed test extension for **Microsoft PowerToys Command Palette**.

NetSpeed runs a speed test on demand and shows the result directly inside Command Palette: download, upload, latency, jitter, selected server, ISP and transferred data.


## Features

- One-command Internet speed test inside Command Palette
- Download and upload speed in Mbps
- Ping and jitter
- Automatic fastest-server selection
- Selected LibreSpeed server information
- ISP and approximate client location returned by the test backend
- Total received/sent data
- No NetSpeed telemetry
- No automatic test on launch: bandwidth is used only after the user explicitly starts a test
- x64 and ARM64 build configuration
- Release MSIX packages digitally signed with Microsoft Artifact Signing (Public Trust)

## Speed-test engine

NetSpeed uses **LibreSpeed CLI 1.0.13** as a separate executable component. LibreSpeed CLI is licensed under LGPL-3.0.

The project does not require LibreSpeed CLI to be installed globally. Before compilation, `scripts/Prepare-LibreSpeed.ps1` downloads the official upstream Windows binary for the requested architecture and verifies its SHA-256 against the checksum file published with the upstream release.

The test uses LibreSpeed's public server list and automatic server selection. NetSpeed does not request result sharing and does not operate a telemetry backend of its own.

See `THIRD_PARTY_NOTICES.md` for licensing details.

## Requirements

- Windows 11
- PowerToys with Command Palette
- .NET 10 SDK / Visual Studio with the Windows development workload for local builds
- Internet access during dependency preparation and speed testing

## Installation

Download the MSIX package for your architecture from the project's GitHub release/artifacts:

- `NetSpeed-<version>-x64.msix` for x64 Windows
- `NetSpeed-<version>-ARM64.msix` for ARM64 Windows

Release packages are digitally signed using **Microsoft Artifact Signing (Public Trust)**. You do **not** need to install or import a separate signing certificate into `TrustedPeople`.

Open the downloaded `.msix` file and install it with Windows App Installer. Then open PowerToys Command Palette; NetSpeed will be available as a Command Palette extension.

> Debug/CI artifacts may use a temporary development certificate and are intended for testing only. For normal installation, use a signed release package.

## Local development

Open PowerShell from the repository root and prepare the LibreSpeed engine for your architecture:

```powershell
.\scripts\Prepare-LibreSpeed.ps1 -Architecture x64
```

Then open `NetSpeed.sln` in Visual Studio and build/deploy the project.

For ARM64:

```powershell
.\scripts\Prepare-LibreSpeed.ps1 -Architecture ARM64
```

## Privacy

NetSpeed has no proprietary analytics or telemetry service. A speed test necessarily transfers a significant amount of test data to/from the selected LibreSpeed server. The backend may also return public-IP/ISP information as part of the test result.

## License

NetSpeed source code: **MIT License**, copyright © 2026 Michele Gentilini.

LibreSpeed CLI: **LGPL-3.0**, distributed as a separate executable in built packages. See `THIRD_PARTY_NOTICES.md` and `NetSpeed/ThirdParty/LibreSpeed/LICENSE-LGPL-3.0.txt`.

## Changelog

See `CHANGELOG.md`. Version 0.1.1 fixes LibreSpeed CLI 1.0.13 JSON-array parsing.

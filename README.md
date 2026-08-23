# NetSpeed for PowerToys Command Palette

A compact Internet speed test extension for **Microsoft PowerToys Command Palette**.

NetSpeed runs a speed test on demand and shows the result directly inside Command Palette: download, upload, latency, jitter, selected server, ISP and transferred data.

**Developer:** Michele Gentilini  
**Website:** https://genty.me

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
- Signed MSIX artifacts from GitHub Actions
- Persistent signing certificate support through GitHub Secrets

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

## GitHub Actions

The included workflow builds both **x64** and **ARM64**.

1. Push the repository to GitHub.
2. Open **Actions → Build NetSpeed**.
3. Run the workflow or push to `main`/`master`.
4. Download `NetSpeed-x64` or `NetSpeed-ARM64` from the workflow artifacts.

Each artifact contains the MSIX, public signing certificate, installation script and third-party notices.

### Development signing

If signing secrets are not configured, CI creates a temporary self-signed certificate with subject:

```text
CN=Michele Gentilini
```

Run `Install-Package.ps1` **as Administrator**. It imports the public certificate into `LocalMachine\TrustedPeople` and installs the MSIX.

For public releases, use a persistent code-signing certificate or Microsoft Store identity. See `docs/PUBLISHING.md`.

## Publishing

Microsoft supports distribution through **WinGet** and the **Microsoft Store**. A WinGet listing intended for Command Palette discovery must include the `windows-commandpalette-extension` tag. The curated Command Palette Gallery links users to the WinGet or Store install source.

See `docs/PUBLISHING.md` for notes specific to this project.

## Privacy

NetSpeed has no proprietary analytics or telemetry service. A speed test necessarily transfers a significant amount of test data to/from the selected LibreSpeed server. The backend may also return public-IP/ISP information as part of the test result.

## License

NetSpeed source code: **MIT License**, copyright © 2026 Michele Gentilini.

LibreSpeed CLI: **LGPL-3.0**, distributed as a separate executable in built packages. See `THIRD_PARTY_NOTICES.md` and `NetSpeed/ThirdParty/LibreSpeed/LICENSE-LGPL-3.0.txt`.

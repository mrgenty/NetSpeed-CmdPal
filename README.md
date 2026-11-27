# NetSpeed for PowerToys Command Palette

A compact Internet speed test extension for **Microsoft PowerToys Command Palette**.

NetSpeed runs a speed test on demand and shows the result directly inside Command Palette: download, upload, idle latency, jitter, latency under load (bufferbloat), test server and transferred data. Tests run on the open **Measurement Lab (M-Lab)** platform.

## Features

- One-command Internet speed test inside Command Palette
- Live Mbps while downloading and uploading, with a Cancel command
- Download and upload in Mbps: steady-state value (after TCP ramp-up) plus full-phase average
- Idle latency and jitter
- Latency under load during download and upload, with a bufferbloat grade
- Multi-stream measurement (MSAK, 4 TCP streams) for fast connections, with automatic ndt7 fallback
- Automatic nearest-server selection (M-Lab Locate + round-trip-time ranking)
- **Copy result** as plain text (your IP address is left out)
- No NetSpeed telemetry, no external executables
- Clear first-run data notice; consent stored locally and revocable
- x64 and ARM64 builds
- Release MSIX packages digitally signed with Microsoft Artifact Signing (Public Trust)

## Speed-test engine

NetSpeed contains its own C# client for M-Lab (`NetSpeed.Core`), using only `HttpClient` and `ClientWebSocket`. It is trimming/AOT compatible (no reflection, JSON source generation).

1. **Server discovery** — [Locate API v2](https://github.com/m-lab/locate/blob/main/USAGE.md) returns up to four nearby servers with short-lived access tokens. NetSpeed measures the TCP handshake time to each and starts with the fastest.
2. **Idle latency and jitter** — 10 TCP handshakes to the selected server's HTTPS port before any load. Jitter is the mean absolute difference between consecutive samples.
3. **Download and upload** — 10 seconds each using [MSAK throughput1](https://github.com/m-lab/msak) with 4 parallel streams. If MSAK has no capacity or every server refuses, NetSpeed falls back to [ndt7](https://github.com/m-lab/ndt-server/blob/main/spec/ndt7-protocol.md), which uses a single TCP stream.
   - Download speed is counted by NetSpeed on received application data.
   - Upload speed comes from the measurement messages sent by the server, because client-side counters include socket buffers.
   - The headline value ignores the first 2 seconds (TCP slow start and stream ramp-up). The average over the whole phase is shown next to it.
4. **Latency under load** — TCP handshakes every 250 ms while data is flowing. *Bufferbloat* is the worst loaded latency minus the idle latency (Low < 30 ms, Moderate < 100 ms, High ≥ 100 ms).

### Limits

- M-Lab servers sit at well-connected sites, usually off your ISP's network. Commercial services with servers inside the ISP network can report higher figures, especially above 1 Gbps.
- On ndt7 fallback, a single TCP stream may not fill very fast links.
- At multi-gigabit speeds, the PC's CPU (TLS) and Wi-Fi can be the bottleneck.
- ISP name and client location are not shown: M-Lab does not return them, and NetSpeed does not call any third-party geolocation service.

## Data and consent

Before the first test, NetSpeed explains that **M-Lab publishes every test result, including your public IP address, as open data**, and asks for your agreement. The choice is stored only on your PC.

NetSpeed follows the [M-Lab Acceptable Use Policy](https://www.measurementlab.net/aup/):

- the client identifies itself as `netspeed-cmdpal`;
- tests run only on demand;
- at most 40 tests run per 24 hours.

See [PRIVACY.md](PRIVACY.md) for details.

## Requirements

- Windows 11 (Windows 10 19041+ supported by the package)
- PowerToys with Command Palette
- .NET 10 SDK / Visual Studio with the Windows development workload for local builds
- Internet access during speed testing

## Installation

Download the MSIX package for your architecture from the project's GitHub release/artifacts:

- `NetSpeed-<version>-x64.msix` for x64 Windows
- `NetSpeed-<version>-ARM64.msix` for ARM64 Windows

Release packages are digitally signed using **Microsoft Artifact Signing (Public Trust)**. You do **not** need to install or import a separate signing certificate into `TrustedPeople`.

Open the downloaded `.msix` file and install it with Windows App Installer. Then open PowerToys Command Palette; NetSpeed will be available as a Command Palette extension.

> Debug/CI artifacts may use a temporary development certificate and are intended for testing only. For normal installation, use a signed release package.

## Local development

Open `NetSpeed.sln` in Visual Studio and build/deploy the `NetSpeed` project. No extra preparation step is needed.

Command line:

```powershell
dotnet publish NetSpeed\NetSpeed.csproj -c Release -p:Platform=x64 -r win-x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false
```

> If the repository lives in a very deep folder, CsWinRT code generation can exceed the Windows path limit. Clone to a short path such as `C:\src\NetSpeed-CmdPal`.

### Tests

`NetSpeed.Core` targets plain `net10.0`, so its tests run on Windows, Linux or macOS:

```powershell
dotnet test tests\NetSpeed.Core.Tests
```

They cover throughput and latency math, Locate and measurement parsing, query building, the consent store and the fair-use limit. A loopback fake WebSocket server exercises the real download/upload code; the tests never contact M-Lab.

## Project layout

- `NetSpeed/` — Command Palette extension (UI, packaging)
- `NetSpeed.Core/` — platform-independent engine (`ISpeedEngine`, `SpeedResult`, M-Lab client)
- `tests/NetSpeed.Core.Tests/` — unit tests

## License

NetSpeed source code: **MIT License**, copyright © 2026 Michele Gentilini. See `THIRD_PARTY_NOTICES.md`.

NetSpeed is not affiliated with or endorsed by Measurement Lab.

## Changelog

See `CHANGELOG.md`. Version 0.2.0 replaces LibreSpeed with a built-in M-Lab client.

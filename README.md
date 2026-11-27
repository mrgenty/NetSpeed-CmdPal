# NetSpeed for PowerToys Command Palette

Internet speed test inside **PowerToys Command Palette**: download, upload, latency, jitter and bufferbloat, measured on the open **Measurement Lab (M-Lab)** platform.

## Features

- Live Mbps while testing, with Cancel
- Download and upload: steady-state value (after TCP ramp-up) and full-phase average
- Idle latency, jitter, latency under load and bufferbloat grade
- Multi-stream test (MSAK, 4 TCP streams) for fast connections, with automatic ndt7 fallback
- Nearest-server selection (M-Lab Locate + round-trip-time ranking)
- **Copy result** as plain text (your IP address is left out)
- No telemetry, no bundled executables
- x64 and ARM64; release packages signed with Microsoft Artifact Signing

## How it measures

`NetSpeed.Core` is a self-contained C# client for M-Lab, built on `HttpClient` and `ClientWebSocket`. It is AOT- and trim-compatible.

1. **Server**: [Locate API v2](https://github.com/m-lab/locate/blob/main/USAGE.md) returns up to four nearby servers. NetSpeed picks the one with the fastest TCP handshake.
2. **Idle latency and jitter**: 10 TCP handshakes before any load.
3. **Download and upload**: 10 s each with [MSAK throughput1](https://github.com/m-lab/msak) over 4 streams. If MSAK is unavailable, NetSpeed falls back to [ndt7](https://github.com/m-lab/ndt-server/blob/main/spec/ndt7-protocol.md), which uses a single stream.
   - Upload speed comes from the server's own counters.
   - The headline value ignores the first 2 s of ramp-up.
4. **Latency under load**: TCP handshakes every 250 ms while data flows. Bufferbloat is the worst loaded latency minus the idle latency.

### Limits

- M-Lab servers are usually off your ISP's network, so services with in-ISP servers can report more, especially above 1 Gbps.
- With the ndt7 fallback, a single stream may not saturate very fast links.
- ISP name and location are not shown: M-Lab does not provide them, and NetSpeed calls no geolocation service.

## Data and consent

**M-Lab publishes every test result, including your public IP address, as open data.** NetSpeed tells you this before the first test and runs only after you agree. Your choice is stored locally and can be withdrawn.

NetSpeed follows the [M-Lab Acceptable Use Policy](https://www.measurementlab.net/aup/):

- the client identifies itself as `netspeed-cmdpal`;
- tests run on demand only;
- at most 40 tests run in 24 hours.

See [PRIVACY.md](PRIVACY.md).

## Installation

Download `NetSpeed-<version>-x64.msix` or `NetSpeed-<version>-ARM64.msix` from [Releases](https://github.com/mrgenty/NetSpeed-CmdPal/releases) and open it with App Installer. Release packages are publicly trusted: no certificate import is needed. Then open Command Palette and search for **NetSpeed**.

## Development

Requires the .NET 10 SDK, or Visual Studio with the Windows development workload. Open `NetSpeed.sln`, or run:

```powershell
dotnet publish NetSpeed\NetSpeed.csproj -c Release -p:Platform=x64 -r win-x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false
```

```powershell
dotnet test tests\NetSpeed.Core.Tests
```

The tests run on any OS and never contact M-Lab: a local fake WebSocket server exercises the real download and upload code. Clone to a short path (e.g. `C:\src`), because very deep folders can exceed the Windows path limit during CsWinRT code generation.

## License

MIT © 2026 g_. Not affiliated with or endorsed by Measurement Lab, Microsoft or Ookla.

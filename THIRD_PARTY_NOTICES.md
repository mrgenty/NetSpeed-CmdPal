# Third-party notices

## Measurement Lab (M-Lab)

NetSpeed is a third-party client of the **Measurement Lab** platform (https://www.measurementlab.net/).

- Server discovery: M-Lab Locate API v2 (`locate.measurementlab.net`).
- Throughput protocols: **MSAK throughput1** and **ndt7**, implemented independently in C# by NetSpeed (`NetSpeed.Core`). No M-Lab source code is included or redistributed.
- Protocol references: [ndt7 specification](https://github.com/m-lab/ndt-server/blob/main/spec/ndt7-protocol.md), [MSAK](https://github.com/m-lab/msak), [Locate API](https://github.com/m-lab/locate).
- Use of the platform is governed by the [M-Lab Acceptable Use Policy](https://www.measurementlab.net/aup/) and [Privacy Policy](https://www.measurementlab.net/privacy/).

NetSpeed is not affiliated with or endorsed by Measurement Lab, Code for Science and Society, Microsoft or Ookla.

## Previous versions

NetSpeed 0.1.x bundled LibreSpeed CLI (LGPL-3.0). Starting with 0.2.0 no LibreSpeed component is included.

## NuGet dependencies

Build-time and runtime NuGet packages (Microsoft Command Palette SDK, CsWinRT, Windows SDK build tools, WinRT server helper; xUnit for tests) are listed in `Directory.Packages.props` and are subject to their own licenses.

NetSpeed itself is licensed under the MIT License.

# Changelog

## 0.2.0 - Unreleased

- New speed-test engine based on **Measurement Lab (M-Lab)**, written in C# with no external executables:
  - Locate API v2 server discovery with access tokens; candidates ranked by measured round-trip time.
  - **MSAK throughput1** with 4 parallel TCP streams for fast connections; automatic fallback to **ndt7** when MSAK has no capacity or refuses the test.
  - Download and upload of 10 seconds each. The headline value excludes the first 2 seconds (TCP ramp-up); the full-phase average is shown as well.
  - Upload speed from the server's own counters, as the ndt7 specification recommends.
  - Idle latency and jitter from TCP handshakes; latency under load during download and upload; bufferbloat grade.
- Live Mbps while the test runs, a Cancel command, and a **Copy result** command (without the IP address).
- First-run notice explaining that M-Lab publishes test results, including the public IP address, as open data; consent is stored locally and can be withdrawn.
- Local limit of 40 tests per 24 hours, as required by the M-Lab Acceptable Use Policy for interactive users.
- Clear error messages for no capacity, busy servers, timeouts and network errors.
- New platform-independent `NetSpeed.Core` library and cross-platform unit tests, run in CI.
- New `PRIVACY.md`.
- **Removed** LibreSpeed CLI, its LGPL notice and `scripts/Prepare-LibreSpeed.ps1`. The package no longer contains third-party executables.

## 0.1.1 - 2026-08-23

- Fixed LibreSpeed CLI 1.0.13 result parsing. The CLI returns a JSON array even when a single server is tested.
- Added compatibility with both array-shaped and object-shaped JSON results.

## 0.1.0 - 2026-08-23

- Initial public test build.

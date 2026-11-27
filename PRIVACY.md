# NetSpeed privacy notice

_Last updated: 2026-11-27 — applies to NetSpeed 0.2.0 and later._

NetSpeed is a free, open-source (MIT) extension for PowerToys Command Palette, developed by Michele Gentilini ([genty.me](https://genty.me/)). It runs Internet speed tests on the **Measurement Lab (M-Lab)** platform. This notice explains what data is involved and who handles it.

## Summary

- NetSpeed has **no telemetry, analytics, accounts or servers of its own**.
- A test runs **only when you start it**, and only after you have accepted the data notice shown before your first test.
- The test itself is performed with M-Lab servers. **M-Lab publishes every test result, including your public IP address, as open data.**

## What happens during a test

When you start a test, NetSpeed:

1. Asks the M-Lab Locate service (`locate.measurementlab.net`) for nearby test servers. Locate uses your IP address to estimate your approximate location and pick a nearby server.
2. Opens a few TCP connections to the candidate servers to measure round-trip time (no data is sent on these connections) and picks the fastest-responding one.
3. Runs a download and an upload test with the selected M-Lab server, using the M-Lab **MSAK throughput1** protocol (or **ndt7** as a fallback). Only random, synthetic data is transferred.

The test servers record the measurement, including:

- your **public IP address** and port as seen by the server;
- throughput, timing and TCP statistics of the test connections;
- the client identification NetSpeed sends: `client_name=netspeed-cmdpal`, the NetSpeed version, the operating system (`windows`) and the CPU architecture, plus a User-Agent naming NetSpeed.

NetSpeed does **not** send hardware identifiers, account data, browsing data or any other personal information.

## Who handles the data

Test data is collected, stored and published by **M-Lab**, which acts as the data controller for test data under the GDPR and releases it into the public domain for research. Please read:

- [M-Lab Privacy Policy](https://www.measurementlab.net/privacy/) — including how EU data subjects can exercise their rights;
- [M-Lab Acceptable Use Policy](https://www.measurementlab.net/aup/).

Because results are public and are linked to an IP address, do not run tests if you do not want that association to be published.

## What NetSpeed stores on your device

NetSpeed keeps one small file, `state.json`, in its local application data folder (inside the package's private storage when installed as MSIX). It contains:

- whether and when you accepted the data notice (and which version of it);
- the start times of your tests in the last 24 hours, used to respect M-Lab's limit of 40 tests per day for interactive users.

This file never leaves your device. Uninstalling NetSpeed removes it.

The **Copy result** command copies a text summary to your clipboard; it deliberately does not include your IP address.

## Your choices

- You can decline the notice: no test is run and nothing is sent.
- You can withdraw consent at any time with **Withdraw data-policy consent** on the NetSpeed page; NetSpeed will ask again before the next test.
- Data already published by M-Lab can only be handled by M-Lab under its privacy policy.

## Contact

For questions about NetSpeed itself, open an issue on the project's GitHub repository or use the contact details at [genty.me](https://genty.me/). For questions about test data, contact M-Lab as described in its privacy policy.

# Security Policy

## Supported Versions

NetSpeed is currently in early development. Security fixes are provided for the latest published release only.

| Version | Supported |
| ------- | --------- |
| Latest release | :white_check_mark: |
| Older releases | :x: |

Users are encouraged to upgrade to the latest available version before reporting an issue.

## Reporting a Vulnerability

Please do not report security vulnerabilities through public GitHub issues, discussions, or pull requests.

If private vulnerability reporting is available for this repository, use the **Security** tab on GitHub and select **Report a vulnerability**.

If private reporting is not available, open a public issue containing **no sensitive technical details** and request a private contact channel.

When reporting a vulnerability, please include:

- A clear description of the issue
- The affected NetSpeed version
- The affected Windows architecture (`x64` or `ARM64`)
- Steps required to reproduce the issue
- The expected and actual behavior
- The potential security impact
- Any relevant logs or screenshots, with sensitive information removed
- A proposed fix or mitigation, if available

Please avoid including credentials, private keys, certificates containing private keys, access tokens, personal data, or other secrets in reports.

## Response Process

Security reports will be reviewed as soon as reasonably possible.

After receiving a report, the maintainer will:

1. Confirm receipt of the report.
2. Attempt to reproduce and validate the issue.
3. Determine whether the issue affects NetSpeed itself or an upstream dependency.
4. Prepare a fix or mitigation when appropriate.
5. Coordinate disclosure after an updated version is available.

Valid reports may be acknowledged in the release notes or security advisory, unless the reporter prefers to remain anonymous.

There is currently no bug bounty or monetary reward program.

## Scope

Security issues affecting the following areas are considered in scope:

- NetSpeed's PowerToys Command Palette extension
- MSIX packaging and installation behavior
- NetSpeed's interaction with LibreSpeed CLI
- Input or output parsing that could result in unintended code execution or unsafe behavior
- Unsafe handling of files, paths, processes, or network responses
- Build and release pipeline issues that could compromise distributed NetSpeed packages
- Signing or package-integrity issues affecting official NetSpeed releases
- Leakage of information that NetSpeed is expected to keep private

## Out of Scope

The following are generally outside the scope of this project:

- Vulnerabilities in Microsoft PowerToys itself
- Vulnerabilities in Windows or the .NET runtime
- Vulnerabilities entirely within LibreSpeed CLI that are not caused or amplified by NetSpeed's integration
- Vulnerabilities in third-party LibreSpeed public servers
- General availability or performance problems with public speed-test servers
- ISP behavior, network filtering, or inaccurate speed-test results
- Social engineering attacks
- Denial-of-service reports that only involve intentionally running repeated bandwidth-intensive speed tests
- Reports requiring a compromised operating system or administrator account before the attack can occur

Upstream vulnerabilities should be reported directly to the affected upstream project whenever possible.

## Security Considerations

NetSpeed performs an Internet speed test only when explicitly requested by the user.

Running a speed test transfers a significant amount of network traffic to and from a selected LibreSpeed server. The selected backend may receive information normally exposed during an Internet connection, such as the user's public IP address, and may return ISP or approximate location information.

NetSpeed does not operate its own telemetry or analytics backend.

Official MSIX packages are signed during the GitHub Actions release process. Users should obtain release packages only from the official NetSpeed repository and should verify that the package is signed with the expected NetSpeed signing certificate.

The project currently uses a self-signed code-signing certificate. The requirement to explicitly trust that certificate before installing an official development release is expected behavior and is not, by itself, considered a security vulnerability.

## Responsible Disclosure

Please allow reasonable time for investigation and remediation before publicly disclosing a vulnerability.

Do not access, modify, or destroy data belonging to other users, and do not perform testing that could disrupt third-party services.

Good-faith security research that follows this policy is welcome.

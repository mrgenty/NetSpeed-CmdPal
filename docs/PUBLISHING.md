# Publishing NetSpeed

## Developer identity

- Developer: Michele Gentilini
- Website: https://genty.me
- Package identity: `MicheleGentilini.NetSpeed.CmdPal`

## Signing

For local/test GitHub Actions builds, the workflow creates a temporary self-signed certificate when no signing secrets are configured.

For public releases, configure these GitHub repository secrets:

- `CODESIGN_PFX_BASE64`: Base64 representation of the persistent `.pfx`
- `CODESIGN_PFX_PASSWORD`: Password of that `.pfx`

The certificate subject must match the Publisher in `Package.appxmanifest`. If your public certificate or Microsoft Store identity uses a different Publisher string, update the manifest and project property before release.

## WinGet / Command Palette discovery

Microsoft requires the WinGet manifest to include the `windows-commandpalette-extension` tag so Command Palette can discover the extension. Windows App SDK must also be declared as a WinGet dependency when applicable.

The Command Palette Gallery is a curated directory and links to the extension's WinGet or Microsoft Store install source; it does not host the package itself.

# Code signing

The Windows build is **unsigned** by default, so Windows SmartScreen and some
antivirus may warn on first run. A code-signing certificate fixes that — it
proves the exe comes from you and hasn't been tampered with.

The CI is **already wired** for signing. You only need to obtain a certificate
and add it to the repo; nothing in the code or workflow needs to change.

## Why you have to do this part

A code-signing certificate certifies **your identity** to Microsoft. Issuing one
requires identity verification tied to you personally (and, for paid options,
billing). That account/identity step is the one thing that can't be automated —
it's the whole point of signing. A self-signed certificate is **not** a
shortcut: Windows doesn't trust it, which makes the warning worse, so we don't
use one.

## Option A — SignPath, free for open source (recommended)

1. Apply at <https://about.signpath.io/product/open-source> with this repository
   (it's public and MIT-licensed). Approval is manual — allow a few days.
2. Once approved, in the SignPath web console:
   - Create a **Project** with slug `purrch-win`.
   - Link this repo's **GitHub Actions** as a trusted build source.
   - Create a **Signing Policy** with slug `release-signing`.
   - Create an **API token** for CI.
3. In GitHub → this repo → **Settings**:
   - **Secrets and variables → Actions → Secrets**: add `SIGNPATH_API_TOKEN` = the token.
   - **Secrets and variables → Actions → Variables**: add `SIGNPATH_ORG_ID` = your organization id.
   - (Only if your policy slug differs) add variable `SIGNPATH_POLICY_SLUG`.
4. Push a `v*` tag (or re-run **build-windows**). The signing steps light up
   automatically and the released `Purrch.exe` is signed. Done.

## Option B — Azure Trusted Signing (~$10/month, faster)

If you'd rather pay for a quicker turnaround: create an Azure subscription and a
**Trusted Signing** account (with identity validation), then replace the
"Code-sign with SignPath" step in `.github/workflows/build-windows.yml` with:

```yaml
      - name: Code-sign with Azure Trusted Signing
        uses: azure/trusted-signing-action@v0
        with:
          azure-tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          azure-client-id: ${{ secrets.AZURE_CLIENT_ID }}
          azure-client-secret: ${{ secrets.AZURE_CLIENT_SECRET }}
          endpoint: <your Trusted Signing endpoint URL>
          trusted-signing-account-name: <account name>
          certificate-profile-name: <profile name>
          files-folder: publish
          files-folder-filter: exe
```

## Verify a signed build

Right-click `Purrch.exe` → **Properties → Digital Signatures** — a valid entry
means it's signed. Once signed, the in-app updater can safely be upgraded to a
one-click, in-place auto-update (Velopack).

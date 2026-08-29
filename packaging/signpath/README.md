# SignPath code signing — one-time setup

YKeys is signed through the same [SignPath Foundation](https://signpath.org/)
organization as [YTile](https://github.com/AegiosOT/YTile) — one OSS
application covers the suite; this repo just needs its own SignPath project
and its own repository secrets. The release workflow
([.github/workflows/release.yml](../../.github/workflows/release.yml))
already contains the signing stage; it is skipped while the
`SIGNPATH_API_TOKEN` secret is absent, so releases keep working before
onboarding.

## 1. In the SignPath organization (created by the YTile application)

1. **Project**: create a project with slug `ykeys` linked to the
   `AegiosOT/YKeys` repository.
2. **Artifact configuration**: paste
   [artifact-configuration.xml](artifact-configuration.xml) as the project's
   **default** artifact configuration (the workflow passes no explicit slug).
3. **Signing policy**: create a policy with slug `release-signing`, manual
   approval required. The approver is AegiosOT.
4. **Trusted build system**: link the predefined *GitHub.com* trusted build
   system to the project, and install the SignPath GitHub App on this
   repository so origin verification works.
5. **CI user**: reuse (or create) a CI user with *submitter* permission on
   this project's `release-signing` policy, and copy its API token.

## 2. In this GitHub repository

| Where | Name | Value |
| --- | --- | --- |
| Actions **secret** | `SIGNPATH_API_TOKEN` | the CI user's API token |
| Actions **variable** | `SIGNPATH_ORGANIZATION_ID` | the organization's GUID |

## 3. Release flow after setup

`git push origin vX.Y.Z` as usual. The workflow pauses at
"Sign binary (SignPath)" — approve the request at <https://app.signpath.io>
within an hour and the run continues: the signed exe replaces the unsigned
one before the zip and `SHA256SUMS.txt` are produced.

## 4. After the first signed release

Update YTile's bundle pin so it ships the signed build: bump
`YKEYS_VERSION` and `YKEYS_SHA256` in the "Bundle ykeys" step of YTile's
release.yml (the hash is the `ykeys.exe` line of this repo's release
`SHA256SUMS.txt`). The YTile winget package then carries all-signed
binaries: ytile/ytiled signed by its own release run, ykeys signed here.

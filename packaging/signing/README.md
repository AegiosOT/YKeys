# Code signing — Azure Artifact Signing

`ykeys.exe` is signed by release CI through Azure Artifact Signing, sharing
the YTile suite's signing setup: same signing account (`aegiosot`, East US,
endpoint `https://eus.codesigning.azure.net/`), same certificate profile
(`release-signing`), same managed identity (`ytile-release-signer`), which
carries a federated credential for this repo's **`release` environment**
(`repo:AegiosOT@2933384/YKeys@1350104222:environment:release` — the immutable
ID form; the classic `repo:AegiosOT/YKeys:...` spelling is rejected with
`AADSTS700213`). Binaries are signed under the **NineFiveB** organization
identity. Auth is GitHub OIDC — no stored
secrets; the repo only holds three plain variables (`AZURE_CLIENT_ID`,
`AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`), and the signing steps are
skipped while `AZURE_CLIENT_ID` is absent.

Full runbook — Azure resources, trust model, renewals:
[YTile's packaging/signing/README.md](https://github.com/AegiosOT/YTile/blob/main/packaging/signing/README.md).

After a signed ykeys release, bump `YKEYS_VERSION`/`YKEYS_SHA256` in YTile's
"Bundle ykeys" step so YTile ships the signed build.

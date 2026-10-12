# Start with Windows: measurements and checks

## StartupApproved layout (task 1.1, 2026-10-11)

Read on the owner's machine (Windows 11 Pro 10.0.26200), `HKCU\...\Explorer\StartupApproved\Run`: 12-byte binary
values. Enabled apps (OneDrive, Steam, Discord, Stream Deck, ...) hold `02 00 ...`; Docker Desktop, which is disabled in
Task Manager, holds `03 00 ...`. Other keys in the same place (`AF_uuid_...`, first byte `01`) are not Run entries. A Run
value with no approval value (UrDeck before it was ever disabled) is enabled. This is design decision 2: an odd first
byte means disabled, even or absent means enabled. Run data in the wild is mostly quoted with arguments, but Docker
Desktop's is an unquoted path with spaces, so the reader takes an unquoted path up to the first `.exe`.

## Publish compared with the build output (task 2.1)

`dotnet publish src/UrDeck.Host -c Release` produced 456 files and no `plugins/`; everything else the build output has
was present (`App.xbf` is folded into `UrDeck.Host.pri`). Only `plugins/` was missing, as expected. A target after
`Publish` now copies the same six assemblies. A copy of the published folder rendered the sample page with `--snapshot`
and the log shows all four widgets and both providers registered.

## Cold-boot checks (section 6, owner)

| Check | Result |
| --- | --- |
| Cold boot 1 (2026-10-11 18:50): from the `--autostart` line to tray icon / placement verified / first weather values | 0.53 s / 0.80 s / 1.49 s, no warnings |
| Cold boot 2 (18:55) | 0.41 s / 0.69 s / 1.35 s, no warnings |
| Cold boot 3 (18:57) | 0.46 s / 0.74 s / 1.41 s, no warnings |
| Sign out and in; restart Explorer: one deck, one tray icon | Done by the owner: as expected |
| Sleep and resume after an autostart | Done by the owner: as expected |
| Development copy beside the installed one, then `Install-Local.ps1` again | Both ran. The script closed the installed copy with `--quit`, replaced the files and started it again; its config was unchanged by hash, and the dev copy kept running with its config unchanged. A hard kill of the dev copy and a new start worked; a failed publish (no `dotnet`) stopped the script before it touched the destination |
| `urdeck.log` growth per day; is 5 MB right? | Waived: the PC is shut down nightly. Lines are dated, so growth can be read back later; 5 MB stays |

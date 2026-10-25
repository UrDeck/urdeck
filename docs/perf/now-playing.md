# Now playing: reaching the media session from a plugin (spike)

Task 1 of the `now-playing` change: can a first-party provider, a plain `net10.0` plugin loaded from `plugins/` in its collectible
load context, use the Windows media session (`Windows.Media.Control`) while the project still builds on Linux? The design's candidate
(a) is embedded C#/WinRT. Throwaway project outside the repository, Windows 11 (10.0.26200), .NET SDK 10.0.401,
`Microsoft.Windows.CsWinRT` 2.1.6, 2026-10-25.

## Result: candidate (a) works, at the price of a 25 MB plugin

It reads the session, `await`s the projected operations, takes thread-pool subscriptions, loads from a shadow copy in a collectible
context and unloads. Nothing about how plugins are built changes. The price is that the projection of the *whole* `Windows`
namespace has to be embedded (see "Size").

### The project that built

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  <CsWinRTEmbedded>true</CsWinRTEmbedded>
  <CsWinRTIncludes>Windows</CsWinRTIncludes>
  <CsWinRTExcludes>Microsoft;Windows.Foundation.PropertyType</CsWinRTExcludes>
  <CsWinRTWindowsMetadata>$(PkgMicrosoft_Windows_SDK_NET_Ref)/winmd</CsWinRTWindowsMetadata>
  <DefineConstants>$(DefineConstants);UAC_VERSION_10</DefineConstants>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.Windows.CsWinRT" Version="2.1.6" />
  <PackageReference Include="Microsoft.Windows.SDK.NET.Ref" Version="10.0.19041.57" GeneratePathProperty="true"
                    PrivateAssets="all" ExcludeAssets="compile;runtime" />
</ItemGroup>
```

What each line is for, all found by failing builds:

- `CsWinRTWindowsMetadata` as the documented `10.0.19041.0` fails: the tool looks the Windows SDK up in the registry
  ("Could not find the Windows SDK path in the registry"). The development PC has only a stub of an SDK, and a CI image may differ.
  The winmd files in the `Microsoft.Windows.SDK.NET.Ref` package (the same version family the host's Windows target framework
  restores) make the build independent of an installed SDK.
- `UAC_VERSION_10`: the generated `ComInteropHelpers.cs` refuses to compile without a Universal API Contract define; 10 is
  the 19041 level.
- `Windows.Foundation.PropertyType` excluded: the projection and the embedded support code both define it.
- `Microsoft` excluded and `Windows` included: the default exclusion `Windows;Microsoft` also removes the included namespace.

### Size

The embedded projection is **24.9 MB** (6.3 MB gzipped), and the debug symbols 9.4 MB. A narrow include list does not shrink it:
`Windows.Media.Control` with `Windows.Storage.Streams` (the thumbnail is an `IRandomAccessStreamReference`) pulls in
`Windows.Storage`, whose members reference `Windows.System`, `Windows.ApplicationModel`, `Windows.Data`, `Windows.Networking`,
`Windows.Security`, `Windows.UI.*` and `Windows.Media.*`; after twelve rounds of adding the missing namespaces the list was
essentially all of `Windows` and still did not compile (`Windows.UI.Color`). The tool projects whole types, not the members a
caller uses.

For comparison the host already carries the same projection as `Microsoft.Windows.SDK.NET.dll` (24.9 MB) next to the Windows App
SDK; a plugin that used it instead of embedding would be a few kilobytes (candidate b, below). The build step also adds about 25-35
seconds on Windows.

### What was checked

| Question | Answer |
|---|---|
| Builds on a plain `net10.0` target | yes, with the properties above |
| Reads the session (manager, current session, properties, playback info, timeline) | yes |
| `await` on projected async operations | yes, no adapters needed |
| Subscribing to the session and manager events from a thread-pool thread | yes (`IsThreadPoolThread` true, no exception) |
| Loads from a shadow copy in a collectible `AssemblyLoadContext`, runs, unloads | yes: 600 ms cold, unloaded after one GC (`WeakReference` dead) |
| Reading the thumbnail bytes with `DataReader` (no Windows TFM stream adapters) | compiles; **not run**, the player at hand had no art |
| Builds on Linux with the Windows file excluded | **not yet built**; it is a conditioned `PackageReference` and `Compile`, proved in task 3.1 with the real project (WSL has .NET 10.0.113) |
| Unpackaged app needs a capability or manifest | **no**: an unpackaged console exe read the session with neither |

### Players checked (task 1.1, partly)

One session only, a Chromium browser tab with a video: the session id was the browser's executable name, playback status
`Playing`, a title, no artist, no album, no thumbnail, a duration of 1:10 and a position stamped with the update time, previous and
next not enabled, play-pause enabled. A music player and a video app are still to be checked on the panel (task 7.1); the status
values to map are `Closed`, `Opened`, `Changing`, `Stopped`, `Playing`, `Paused`.

## Decision and what the build needed

The owner accepted candidate (a) with its 25 MB plugin (about 6 MB compressed in the installer). The real project
(`providers/UrDeck.Providers.Media`) differs from the throwaway in a few ways, all in its `.csproj`:

- The projection properties apply only when the build runs on Windows; elsewhere the project builds without them, the Windows
  file is not compiled and the provider reports the session as unavailable (built and tested in WSL).
- The embedded support code and the generated projection are not ours: a build-time global analyzer config marks each such file
  as generated and silences the compiler warnings they raise (a global config takes no wildcards, so every file is listed), and
  the generated `Module.cs`, which marks the whole assembly Windows-only, is left out of the compilation.
- The central package list pins `Microsoft.Windows.CsWinRT` 2.1.6 and `Microsoft.Windows.SDK.NET.Ref` 10.0.19041.57.

## Checked with real players

A Chromium browser tab with a video, through the finished provider and widget (`UrDeck.Host.exe --snapshot`): title shown,
status, a length and a position, the previous and next buttons dimmed because the player does not accept them, no cover. A
music player and a video application are for the panel (task 7.1). Not yet exercised with a real cover: the thumbnail read.

## For `signing-and-store`

Unpackaged (the installer today): no capability is needed. The documentation lists `globalMediaControl` for these classes, so a
packaged (MSIX) build must declare it; not tried here.

## Candidates not tried

(b) two projects: a neutral provider and a `net10.0-windows10.0.19041.0` platform assembly that uses the host's
`Microsoft.Windows.SDK.NET.dll`. It needs the plugin loader to resolve a sibling assembly from the shadow copy, and the plugin
would depend on the host shipping that assembly, a contract that does not exist for any other plugin. (c) multi-targeting
changes the host's plugin copy step. Both stay as the fallback if 25 MB is not acceptable.

## Measured on the panel (installed build, Release)

The Media page (the widget alone, 4x2) against the same page with a clock of the same size in its place, 45 s each, after the page
settled. CPU is the host process as a share of all logical processors; GPU is the sum of the process's GPU engine utilisation.

| State | CPU | Private bytes | GPU |
| --- | --- | --- | --- |
| Clock instead of the widget (baseline) | 0.15 % | 201 MB | 1.7 % |
| Now playing, a YouTube video playing in Brave | 0.15 % | 196 MB | 1.8 % |
| Now playing, nothing playing | 0.18 % | 194 MB | 1.6 % |

The widget costs nothing measurable beyond the page it sits on; the differences between the rows are noise.

**Track changes.** 24 generated MP3s, each with its own cover, played in Windows' Media Player and skipped with the media key
(200 key presses; the queue ended after about 23 real changes, the later presses changed nothing). Private bytes 203 MB at the
start, 217 MB at the peak, 199 MB eight seconds after the last press; handles 1796, 1814, 1805. The art folder held two files at
most, always. CPU during the run 0.19 %.

**Not measured:** the repaint rate. The widget asks for one repaint a second while playing (`NextAnimationAt`) and none when
paused or idle; the figures above are consistent with that, but no frame counter was read.

**After a quit** the run's folder under `%TEMP%\UrDeck\media-art` and its pictures are gone; the empty `media-art` parent stays.
`urdeck.log` held no title, artist or picture name after the whole session (searched for the test tracks' titles, `.jpg`
and `.bmp`).

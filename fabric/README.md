# Armoci Fabric migration preview

**This is a partial port, not full feature parity.** Targets Minecraft 1.16.5,
Fabric Loader 0.16.10 and Fabric API 0.42.0+1.16. The original Forge/native
recovery project is unchanged. This subproject deliberately does not include
the recovered payload, native libraries, loader executables, cached JARs or
network/authentication code. It uses supported Fabric client APIs only.

## Build and install

From the repository root, with JDK 17 and the existing Gradle 8.8 wrapper:

```sh
bash fabric/build-local.sh
```

On Windows use `fabric\build-local.bat` (or `.\gradlew.bat -p fabric localRelease`).
The `localRelease` task builds, validates and stages a remapped JAR plus its
SHA-256 checksum in `fabric/build/local-release/`. It does not require GitHub
Actions, a GitHub token or repository admin access. Initial dependency downloads
require internet. Re-run after pulling new commits; use `clean localRelease`
if you need a fresh build.

Install the staged `fabric/build/local-release/armoci-fabric-preview.jar` and
Fabric API in a separate Minecraft 1.16.5 Fabric instance.
No injector or external launcher is needed. F7 opens settings, F8 toggles the HUD; the settings
reload key can be bound in Minecraft's Controls screen. Settings are stored in
`config/armoci-fabric.properties`.

## Migration coverage

Implemented: FPS display, rounded XYZ coordinates (horizontal or vertical),
digital local clock (optional date, 12/24h), movement-key display with rebound key labels, in-game HUD settings, persistent
local settings, client lifecycle and configurable keybindings.

Not implemented: original click GUI, analog clock, biome/direction indicators,
mouse/CPS HUD, armor/status HUD, profiles, online features and the other original
modules. No original feature is claimed to work through this preview.

The existing source is a native JNI/JVMTI injection recovery, with version-specific
Forge mappings and LWJGL dependencies, not a normal Forge entrypoint. A complete
migration requires an independent review of the original feature implementations,
then explicit replacement of loader, mapping, rendering, input, and lifecycle
boundaries. Do not copy unreviewed native code, authentication plumbing or
previously compiled artifacts into this build. See the root BACKDOOR_REPORT.md.

## Manual smoke test

1. Start a clean 1.16.5 Fabric instance with Fabric API and the preview JAR.
2. Enter a single-player world; verify FPS, XYZ, clock and movement-key display.
3. Verify F7 settings, F8, F1, F3, pause/menu hiding, rebind and reload settings.
4. Exit and re-enter the world, then restart the game and verify persistence.
5. Report the crash log, loader and Java versions if anything fails.

If enabled, the GitHub workflow publishes only this separate preview, never the old
injection JAR or binaries. A successful build does not establish runtime
compatibility or complete security of the original project.

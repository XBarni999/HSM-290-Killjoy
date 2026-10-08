# HSM-290 Killjoy

An air-launched, two-stage aeroballistic missile for **Nuclear Option 0.34.2**. Killjoy combines a solid-fuel booster with an unpowered guided warhead for long-range attacks on ground and surface targets.

## Features

- **Two separate variants:** conventional HE with a contact fuse, and nuclear with a native 200 m airburst.
- **Two-stage flight:** the booster separates after burnout; the warhead continues on a guided ballistic descent.
- **Ballistic guidance:** a moderate loft followed by an angled dive. Terminal guidance prioritizes the impact point and accounts for velocity, gravity and target movement.
- **Altitude-dependent range:** launching from a higher carrier increases the configured launch range, up to **360 km at 10 km altitude**. Minimum launch range is **48 km**.
- **Animated warhead fins:** four fins unfold after separation and deflect with steering commands.
- **FS-41 Eclipse:** HE and nuclear variants are available on its central pylon (requires the Eclipse aircraft mod).
- **Carrier support:** KR-67 Ifrit carries **1** missile on a custom central heavy pylon; Alkyon AB-4 carries **2** externally; SFB-81 Darkreach carries **4** in its normal internal bays.
- **Native impact and damage:** contact checks exclude the missile's own colliders and trigger volumes, and cover movement between physics steps. The game's damage, penetration and fuse logic handles valid impacts.

At a carrier speed of 250 m/s, the configured maximum launch ranges are:

| Carrier altitude | Configured maximum |
| --- | --- |
| Sea level | 50 km |
| 8 km | 189 km |
| 10 km | 360 km |

These are fire-control range estimates. Actual reach and impact angle depend on launch conditions and game physics; a 360 km flight has not yet been measured. The KR-67 central pylon excludes the conflicting internal bays while equipped.

## Requirements and installation

Requires **BepInEx 5** and **Blueprinter 2.0.1**.

1. Download `HSM-290-Killjoy.dll` from [Releases](https://github.com/XBarni999/HSM-290-Killjoy/releases/latest), or extract it from the release ZIP.
2. Put the DLL in your game's `BepInEx/plugins` directory.
3. Remove any older Killjoy/Kinzhal DLL before installing this version, then restart the game.

The DLL includes the Blueprinter bundle. Install only the DLL; a separate `.nobp` is unnecessary.

## First release: v1.1.0

This is the first public binary release. In-game testing by the author confirms target hits and working impact behavior after the collision correction. Multiplayer behavior and the full long-range envelope still need testing.

See [CHANGELOG.md](CHANGELOG.md) for the release features.

## Building from source

This repository contains runtime source and documentation. Unity assets, models, textures and metadata are kept in the authoring project. Download the complete playable mod from Releases.

1. Place a prepared Blueprinter bundle at `Runtime/Kinzhal/Bundle/Kh47M2.nobp`. The `Kinzhal.Kh47M2.nobp` embedded resource from the corresponding release DLL can also be reused.
2. Adjust the game/assembly reference paths in `Runtime/Kinzhal/Kinzhal.csproj` for your installation.
3. Run `dotnet build Runtime/Kinzhal/Kinzhal.csproj -c Release`.

The output is `Runtime/Kinzhal/bin/Release/net472/HSM-290-Killjoy.dll`. The internal Kh47M2/Kinzhal IDs are retained for compatibility.

This project is an unofficial community modification and is not affiliated with, sponsored by, or endorsed by Shockfront Studios Pty Ltd. Original Nuclear Option assets, vehicle designs, audio, and code are Copyright (c) 2026 Shockfront Studios Pty Ltd. All rights reserved. Nuclear Option and Shockfront Studios are trademarks or registered trademarks of Shockfront Studios Pty Ltd. Original mod content and all other trademarks belong to their respective owners.

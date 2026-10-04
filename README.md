# HSM-290 Killjoy

An air-launched aeroballistic missile mod for **Nuclear Option**, built with Blueprinter and a BepInEx runtime plugin.

- Solid-fuel booster, physical separation and an unpowered guided warhead.
- Range-dependent climb, autonomous INS guidance and terminal radar acquisition.
- **KR-67 Ifrit:** 1 missile; **Alkyon AB-4:** 2; **SFB-81 Darkreach:** 4 in normal internal bays.
- HE penetrator and an optional nuclear variant, disabled by default.
- Launch envelope: **48 km minimum**, up to **220 km from 10 km altitude**. These are configured estimates; flight tuning is ongoing.

Requires **Blueprinter 2.0.1** and **BepInEx 5**. This repository contains the current development source and Unity assets. **No release is published yet.**

## Building

Place the repository at `Assets/Blueprinter/Mods/Kh47M2` in a configured Blueprinter Editor project. Build the bundle with **Blueprinter > Kh-47M2 > Build bundle**, then build `Tools~/Runtime/Kinzhal/Kinzhal.csproj` in Release mode with `GameDir` set to your Nuclear Option installation. Run `Tools~/Package.ps1` to package the DLL with the embedded bundle.

The original game assets and assemblies are required locally and are not included. Materials and the weapon icon are supplied with the mod. In-game guidance, clearance and range still require mission testing.

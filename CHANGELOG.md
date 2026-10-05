# Changelog

## v1.1.0 — 2026-10-05

First public release of HSM-290 "Killjoy".

- Added separate conventional HE and nuclear missile variants.
- Added two-stage flight with booster separation and an unpowered guided warhead.
- Added a moderate ballistic loft, gravity-aware terminal guidance and target-motion compensation.
- Added an altitude-dependent launch envelope: 48 km minimum and a configured maximum of 360 km from 10 km altitude.
- Added KR-67 Ifrit central-pylon support, Alkyon AB-4 external mounts and SFB-81 Darkreach internal mounts.
- Added warhead fin deployment and steering animation, with smooth falling booster debris.
- Corrected false contacts with the missile's own colliders and trigger volumes, and extended contact queries over movement between physics steps.
- Packaged the Blueprinter bundle inside a single BepInEx DLL.

Target hits and impact behavior were confirmed in-game by the author. The full 360 km flight envelope and multiplayer behavior remain unverified.

# NOTICE — AI Companions - ASKA

This is an independent, fan-made modding project. It is **not** affiliated with,
endorsed by, or supported by Sand Sailor Studio or the publishers of **ASKA**.

## Game and game assets

**ASKA** and its assets, code, names, audio and trademarks belong to their
respective owners. This repository does **not** contain or redistribute the game,
game assemblies, extracted assets, models, audio or save files. Every build
references the player's own installation of the game.

## Upstream and prior art

- **AI Companions (Valheim)** — <https://github.com/julianu37/AI-Companions> —
  MIT. This project reuses architectural ideas and, where explicitly marked, code
  from our own AI/voice layer. The Valheim companion base (Offline Companions
  Reborn) is **not** part of this project.
- **Offline Companions Reborn** — <https://github.com/MaddhattrRocks/Offline-Companions-Reborn> —
  MIT. Not bundled here; credited as prior art for companion gameplay.
- **askamods** — <https://github.com/simpsonbm1/askamods> — public ASKA modding
  research notes. We credit the factual findings we used (IL2CPP/BepInEx
  behaviour, ASKA class surface, dead ends). No source code or assets from that
  repository are copied into this project. Verify every inherited claim against
  our own build; see `docs/re/`.
- **ASKA UI** and **Smart Villagers** — community mods used only as ecosystem
  references. No code or assets are copied.

## Runtime dependencies (not bundled in this repository)

The mod is built against the player's BepInEx installation and, for the voice
features, NuGet packages that are downloaded at build time. See
`THIRD_PARTY_NOTICES.md` for the license of each dependency.

## No warranty

ASKA updates can change IL2CPP metadata, class layouts and behaviour. The mod is
provided as-is, without warranty. Back up your saves before testing.

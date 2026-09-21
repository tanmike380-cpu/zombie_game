# Battle sound assets

All five sound assets are CC0 1.0: https://creativecommons.org/publicdomain/zero/1.0/
Retrieved 2026-09-21. No soldier dialogue is included.

| Project file | Author / original | Source |
| --- | --- | --- |
| Musket.wav | fennelliott — musket.wav | https://freesound.org/people/fennelliott/sounds/347647/ |
| Knife.wav | jawbutch — Knife Stab Melon.wav | https://freesound.org/people/jawbutch/sounds/344404/ |
| Zombie1.wav / Zombie2.wav / Zombie3.wav | saturn91 — 01/02/03_zombey.mp3 | https://opengameart.org/content/zomby-sfx-pack |

Musket and knife use the publicly provided HQ MP3 previews, not authenticated original downloads.
All files decoded/downmixed to mono 44.1 kHz PCM WAV with FFmpeg. No music or third-party game audio.
Zombie playback pitch varies 0.8–1.0; guns/knife 0.95–1.05. Recordings are temporary sound direction,
not a claim of historically exact matchlock mechanism or final professional sound design.

Replace these WAV files in place (preserve .meta GUIDs) to upgrade audio without changing gameplay.
Playback code: Assets/_Game/Scripts/Presentation/BattleAudio.cs. Audio never emits AI hearing events.

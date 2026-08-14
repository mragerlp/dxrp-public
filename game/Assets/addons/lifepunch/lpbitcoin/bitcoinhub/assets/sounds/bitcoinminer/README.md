# Bitcoin Miner — sounds (owner-only)

**Ship policy:** LifePunch-owned or owner-licensed audio **only**. No third-party addon sound packs, no copied WAVs from other bitcoin minings.

CC0 fast-path: `Prepare-BitcoinMinerSoundsDrop.ps1` → owner drop in Downloads → `Intake-BitcoinMinerSounds.ps1` → `New-BitcoinMinerSoundResources.ps1` → s&box `.vsnd` compile.

See `sources.md` in this folder for license manifest per slot.

## Semantic slots (not copies of anyone else's files)

| Slot | Path constant | Role |
|------|---------------|------|
| `hub-startup` | `HubStartupSoundPath` | Hub power on |
| `hub-fan-loop` | `HubFanLoopSoundPath` | Hub fan loop |
| `hub-fan-down` | `HubFanDownSoundPath` | Hub power off |
| `server-hum` | `HumSoundPath` | Rack mining hum |
| `keyboard` | `KeyboardSoundPath` | HASHD UI typing |
| `glitch` | `GlitchSoundPath` | Damage glitch |
| `error` | `ErrorSoundPath` | Error beep |

Drop originals in `%USERPROFILE%\Downloads\bitcoinminer-sounds\` then run intake. Archive lands under `C:\lifepunch\reference-intake\bitcoinmining\sounds\` (local, not published).

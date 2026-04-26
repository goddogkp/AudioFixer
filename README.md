# AudioFixer

A Ghostlore mod that adds pitch variance and voice limiting to sound effects, reducing audio fatigue from repetitive sounds. Includes an in-game settings UI.

## Features

- **Pitch variance** — each sound plays at a slightly randomized pitch so repeated hits and death sounds don't feel monotonous
- **Voice limiting** — caps how many instances of the same sound can play within a short time window, preventing audio spam during heavy combat
- **Mute list** — silence specific FMOD event paths via config
- **In-game settings UI** — press **9** (not numpad) while in-game to open the settings panel; changes take effect immediately without restarting
- **Steam Workshop compatible** — config.txt is always created next to the DLL, so it works regardless of where Steam installs the mod

## In-Game UI

Press **9** while in a game session (not the main menu) to open the AudioFixer settings panel.

From the panel you can:
- Enable or disable the mod entirely
- Adjust pitch variance, pitch min/max, max voices, and voice window
- Click **Save to config** to write settings to `config.txt`

Settings and panel state persist between sessions.

## config.txt

A `config.txt` is created automatically next to the mod's DLL on first run. You can edit it manually if you prefer.

| Key | Default | Description |
|---|---|---|
| `ENABLED` | `True` | Master on/off for pitch variance and voice limiting |
| `PITCH_VARIANCE` | `0.15` | How much pitch can vary (0 = none, 0.5 = large) |
| `PITCH_MIN` | `0.85` | Hard lower clamp on pitch (1.0 = normal) |
| `PITCH_MAX` | `1.15` | Hard upper clamp on pitch (1.0 = normal) |
| `MAX_VOICES` | `4` | Max simultaneous instances of the same sound within the voice window |
| `VOICE_WINDOW` | `0.1` | Time window in seconds for voice limiting |
| `MUTE` | *(none)* | Add one line per FMOD event path to silence, e.g. `MUTE = event:/SFX/hit` |

## Building from Source

1. Clone the repo
2. Open `AudioFixer.sln` in Visual Studio
3. Update the reference hint paths in `AudioFixer.csproj` to point to your Ghostlore install if it differs from the default Steam path
4. Build — the DLL outputs to `bin/Debug/AudioFixer.dll`

Requires .NET Framework 4.7.2 and [Lib.Harmony 2.4.2](https://github.com/pardeike/Harmony) (included via NuGet).

# OpenAI Watch

Cross-platform repository for monitoring whether your network can reach an OpenAI
compatible API endpoint through your current VPN/proxy route.

This repo now contains:

- `openai-watch.5s.sh`: macOS menu bar plugin for [xbar/SwiftBar].
- `windows/`: Windows tray application built with WinForms (standalone `EXE` + source/build scripts).

The behavior intent is the same: green indicates normal latency, red indicates slow
or failing checks, and gray indicates one transient miss.

## What it shows

- `AI OK` in green: OpenAI responded within the red threshold.
- `AI 2345ms` in red: OpenAI responded, but latency exceeded the threshold.
- `AI DOWN` in red: repeated connection failures.
- `AI OK` in gray: one transient miss, not enough to alert yet.

HTTP `401` is treated as reachable because the request reached OpenAI without an
API key. This plugin checks connectivity and latency, not account permissions.

## Install

### macOS (xbar / SwiftBar)

Install xbar:

```bash
brew install --cask xbar
```

Copy or symlink the plugin:

```bash
mkdir -p "$HOME/Library/Application Support/xbar/plugins"
ln -sf "$PWD/openai-watch.5s.sh" "$HOME/Library/Application Support/xbar/plugins/openai-watch.5s.sh"
chmod +x openai-watch.5s.sh
open -a xbar
```

SwiftBar users can copy the same script into their SwiftBar plugin folder.

### Windows

See `windows/README.md` for build/run instructions:

- Standalone exe built by `windows\\build-exe.ps1`.
- Launch through `windows\\Run-OpenAI-Watch-EXE.cmd`.

## Thresholds

Open the menu and choose one of:

- `1.5s strict`
- `2s normal`
- `3s relaxed`
- `5s very relaxed`

The chosen value is saved to:

```text
~/.config/openai-watch/config
```

## Environment overrides

```bash
OPENAI_WATCH_URL="https://api.openai.com/v1/models"
OPENAI_WATCH_TIMEOUT=4
OPENAI_WATCH_BAD_MS=2000
OPENAI_WATCH_PROXY_PORTS="7890 7897 1080 8080 6152"
```

On Windows, settings are persisted to `%APPDATA%\OpenAI Watch\config.ini`.
On macOS, settings are persisted to `~/.config/openai-watch/config`.

## License

MIT

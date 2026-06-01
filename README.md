# OpenAI Watch

[English](README.md) | [中文](README.zh-CN.md)

Cross-platform repository for monitoring whether your network can reach an OpenAI
compatible API endpoint through your current VPN/proxy route.

This repo now contains:

- `openai-watch.5s.sh`: macOS menu bar plugin for [xbar/SwiftBar].
- `windows/`: Windows tray application built with WinForms (standalone `EXE` + source/build scripts).

The behavior intent is the same: green indicates normal latency, red indicates slow
or failing checks, and gray indicates one transient miss.

## Releases

- [macOS release](https://github.com/qqzlqqzlqqzl/openai-watch/releases/tag/v0.2.0-macos): xbar / SwiftBar plugin package.
- [Windows release](https://github.com/qqzlqqzlqqzl/openai-watch/releases/tag/v0.2.0-windows): Windows tray app package.

## What it shows

- `AI OK` in green: OpenAI responded within the red threshold.
- `AI 2345ms` in red: OpenAI responded, but latency exceeded the threshold.
- `AI DOWN` in red: repeated connection failures.
- `AI OK` in gray: one transient miss, not enough to alert yet.

HTTP `401` is treated as reachable because the request reached OpenAI without an
API key. This plugin checks connectivity and latency, not account permissions.

## Install

### macOS (xbar / SwiftBar)

Download `openai-watch-macos-v0.2.0.zip` from the
[macOS release](https://github.com/qqzlqqzlqqzl/openai-watch/releases/tag/v0.2.0-macos),
or use the source file in this repo.

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

Download the Windows package from the
[Windows release](https://github.com/qqzlqqzlqqzl/openai-watch/releases/tag/v0.2.0-windows),
or see `windows/README.md` for build/run instructions:

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

## Target endpoints

Open the menu and choose one of:

- `OpenAI API /v1/models`: direct OpenAI API reachability check.
- `OpenAI status JSON`: OpenAI status API reachability check.
- `ChatGPT web`: ChatGPT web reachability check.

If `OPENAI_WATCH_URL` is set, it overrides the menu-selected target for that run.

## License

MIT

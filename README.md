# OpenAI Watch

A tiny xbar / SwiftBar menu bar plugin for monitoring whether your Mac can reach
the OpenAI API through the current VPN or proxy route.

It is intentionally simple: it stays green while OpenAI responds within your
chosen threshold, and turns red when the route is too slow or down.

## What it shows

- `AI OK` in green: OpenAI responded within the red threshold.
- `AI 2345ms` in red: OpenAI responded, but latency exceeded the threshold.
- `AI DOWN` in red: repeated connection failures.
- `AI OK` in gray: one transient miss, not enough to alert yet.

HTTP `401` is treated as reachable because the request reached OpenAI without an
API key. This plugin checks connectivity and latency, not account permissions.

## Install

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

## License

MIT

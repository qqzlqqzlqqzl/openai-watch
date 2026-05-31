# OpenAI Watch for Windows

Windows tray-port of `qqzlqqzlqqzl/openai-watch`.

It monitors whether the current route can reach the OpenAI API and shows a small
`AI` icon in the Windows notification area:

- Green: OpenAI responded within the red threshold.
- Red: OpenAI was reachable but slow, returned 5xx, or failed repeatedly.
- Gray: one transient miss, not enough to alert yet.

HTTP `401` is treated as reachable, matching the original xbar script. This is a
connectivity and latency check, not an account-permission check.

## Run

Double-click:

```text
Run-OpenAI-Watch-EXE.cmd
```

Right-click the tray icon to change the red threshold, change the check interval,
refresh immediately, open the OpenAI status page, or exit.

## Settings

The tray menu saves settings to:

```text
%APPDATA%\OpenAI Watch\config.ini
```

Example config:

```ini
bad_ms=2000
interval_seconds=5
target_url=https://api.openai.com/v1/models
```

`target_url` supports either a full API endpoint or just a base URL.

- `https://api.openai.com/v1/models` uses this exact endpoint.
- `https://openai-proxy.internal` will be auto-converted to `https://openai-proxy.internal/v1/models`.

Optional environment overrides:

```powershell
$env:OPENAI_WATCH_URL = "https://api.openai.com/v1/models"
$env:OPENAI_WATCH_TIMEOUT = "4"
$env:OPENAI_WATCH_BAD_MS = "2000"
$env:OPENAI_WATCH_PROXY_PORTS = "7890 7897 1080 8080 6152"
```

If you set `OPENAI_WATCH_URL`, it overrides `target_url` in `config.ini` for that run.

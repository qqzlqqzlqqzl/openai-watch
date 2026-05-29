#!/usr/bin/env bash
# <xbar.title>OpenAI Watch</xbar.title>
# <xbar.version>v0.1.0</xbar.version>
# <xbar.author>local</xbar.author>
# <xbar.desc>Menu bar OpenAI connectivity and latency probe for VPN/proxy switching.</xbar.desc>
# <xbar.dependencies>bash,curl,nc</xbar.dependencies>
#
# Install:
#   xbar:     copy/symlink this file into ~/Library/Application Support/xbar/plugins/
#   SwiftBar: copy/symlink this file into your SwiftBar plugin folder
#
# Optional environment overrides:
#   OPENAI_WATCH_URL="https://api.openai.com/v1/models"
#   OPENAI_WATCH_TIMEOUT=4
#   OPENAI_WATCH_BAD_MS=2000
#   OPENAI_WATCH_PROXY_PORTS="7890 7897 1080 8080 6152"

set -u

TARGET_URL="${OPENAI_WATCH_URL:-https://api.openai.com/v1/models}"
TIMEOUT="${OPENAI_WATCH_TIMEOUT:-4}"
DEFAULT_BAD_MS="${OPENAI_WATCH_BAD_MS:-2000}"
PROXY_PORTS="${OPENAI_WATCH_PROXY_PORTS:-7890 7897 1080 8080 6152}"
STATE_FILE="${TMPDIR:-/tmp}/openai-watch.state"
CONFIG_DIR="${HOME}/.config/openai-watch"
CONFIG_FILE="${CONFIG_DIR}/config"
SCRIPT_FOR_XBAR="$0"

COLOR_OK="#1a9850"
COLOR_BAD="#d73027"
COLOR_MUTED="#777777"

now_iso() {
  date "+%Y-%m-%d %H:%M:%S"
}

read_bad_ms() {
  if [[ -f "$CONFIG_FILE" ]]; then
    awk -F= '$1 == "bad_ms" && $2 ~ /^[0-9]+$/ { print $2; found=1 } END { if (!found) print "" }' "$CONFIG_FILE" 2>/dev/null
  fi
}

current_bad_ms() {
  local saved
  saved="$(read_bad_ms)"

  if [[ -n "$saved" ]]; then
    printf "%s" "$saved"
  else
    printf "%s" "$DEFAULT_BAD_MS"
  fi
}

set_bad_ms() {
  local value="$1"

  if [[ ! "$value" =~ ^[0-9]+$ ]]; then
    exit 2
  fi

  mkdir -p "$CONFIG_DIR"
  printf "bad_ms=%s\n" "$value" > "$CONFIG_FILE"
}

read_failure_streak() {
  if [[ -f "$STATE_FILE" ]]; then
    awk -F= '$1 == "failures" { print $2 + 0 }' "$STATE_FILE" 2>/dev/null
  else
    printf "0"
  fi
}

write_failure_streak() {
  printf "failures=%s\nupdated=%s\n" "$1" "$(now_iso)" > "$STATE_FILE"
}

probe_openai() {
  curl \
    --silent \
    --show-error \
    --location \
    --output /dev/null \
    --max-time "$TIMEOUT" \
    --write-out "%{http_code} %{time_total} %{remote_ip}" \
    "$TARGET_URL" 2>&1
}

port_is_open() {
  local port="$1"
  nc -G 1 -z 127.0.0.1 "$port" >/dev/null 2>&1
}

proxy_summary() {
  local open_ports=()
  local port

  for port in $PROXY_PORTS; do
    if port_is_open "$port"; then
      open_ports+=("$port")
    fi
  done

  if [[ "${#open_ports[@]}" -eq 0 ]]; then
    printf "none"
  else
    local IFS=","
    printf "%s" "${open_ports[*]}"
  fi
}

if [[ "${1:-}" == "--set-threshold" ]]; then
  set_bad_ms "${2:-}"
  exit 0
fi

BAD_MS="$(current_bad_ms)"
result="$(probe_openai)"
curl_status=$?

http_code="$(awk '{ print $1 }' <<< "$result")"
time_total="$(awk '{ print $2 }' <<< "$result")"
remote_ip="$(awk '{ print $3 }' <<< "$result")"
error_text=""

if [[ "$curl_status" -ne 0 || -z "$http_code" || "$http_code" == "000" ]]; then
  previous_failures="$(read_failure_streak)"
  failures=$((previous_failures + 1))
  write_failure_streak "$failures"

  if [[ "$failures" -ge 2 ]]; then
    title="AI DOWN"
    status="DOWN"
    color="$COLOR_BAD"
  else
    title="AI OK"
    status="MISSED"
    color="$COLOR_MUTED"
  fi

  error_text="$(sed 's/[|]//g' <<< "$result" | tr '\n' ' ' | cut -c 1-160)"
  latency_ms="-"
else
  write_failure_streak 0
  latency_ms="$(awk -v seconds="$time_total" 'BEGIN { printf "%.0f", seconds * 1000 }')"

  if [[ "$http_code" -ge 500 ]]; then
    title="AI ${latency_ms}ms"
    status="OPENAI_5XX"
    color="$COLOR_BAD"
  elif [[ "$latency_ms" -lt "$BAD_MS" ]]; then
    title="AI OK"
    status="OK"
    color="$COLOR_OK"
  else
    title="AI ${latency_ms}ms"
    status="TOO_SLOW"
    color="$COLOR_BAD"
  fi
fi

proxy_ports="$(proxy_summary)"

printf "%s | color=%s\n" "$title" "$color"
printf -- "---\n"
printf "Status: %s | color=%s\n" "$status" "$color"
printf "Latency: %sms\n" "$latency_ms"
printf "Red threshold: %sms\n" "$BAD_MS"
printf "HTTP: %s\n" "${http_code:-unknown}"
printf "Remote IP: %s\n" "${remote_ip:-unknown}"
printf "Target: %s | href=%s\n" "$TARGET_URL" "$TARGET_URL"
printf "Local proxy ports open: %s\n" "$proxy_ports"
printf "Last check: %s\n" "$(now_iso)"

if [[ -n "$error_text" ]]; then
  printf -- "---\n"
  printf "Last error: %s | color=%s\n" "$error_text" "$COLOR_BAD"
fi

printf -- "---\n"
printf "Red threshold\n"
printf "1.5s strict%s | bash=%s param1=--set-threshold param2=1500 terminal=false refresh=true\n" "$([[ "$BAD_MS" == "1500" ]] && printf " ✓")" "$SCRIPT_FOR_XBAR"
printf "2s normal%s | bash=%s param1=--set-threshold param2=2000 terminal=false refresh=true\n" "$([[ "$BAD_MS" == "2000" ]] && printf " ✓")" "$SCRIPT_FOR_XBAR"
printf "3s relaxed%s | bash=%s param1=--set-threshold param2=3000 terminal=false refresh=true\n" "$([[ "$BAD_MS" == "3000" ]] && printf " ✓")" "$SCRIPT_FOR_XBAR"
printf "5s very relaxed%s | bash=%s param1=--set-threshold param2=5000 terminal=false refresh=true\n" "$([[ "$BAD_MS" == "5000" ]] && printf " ✓")" "$SCRIPT_FOR_XBAR"
printf -- "---\n"
printf "Refresh | refresh=true\n"
printf "Open OpenAI status | href=https://status.openai.com/\n"

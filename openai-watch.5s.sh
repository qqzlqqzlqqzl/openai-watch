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

DEFAULT_TARGET_URL="https://api.openai.com/v1/models"
ENV_TARGET_URL="${OPENAI_WATCH_URL:-}"
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

read_config_value() {
  local key="$1"

  if [[ -f "$CONFIG_FILE" ]]; then
    awk -F= -v key="$key" '$1 == key { print substr($0, index($0, "=") + 1); found=1 } END { if (!found) print "" }' "$CONFIG_FILE" 2>/dev/null
  fi
}

read_bad_ms() {
  local value
  value="$(read_config_value "bad_ms")"

  if [[ "$value" =~ ^[0-9]+$ ]]; then
    printf "%s" "$value"
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

current_target_url() {
  local saved

  if [[ -n "$ENV_TARGET_URL" ]]; then
    printf "%s" "$ENV_TARGET_URL"
    return
  fi

  saved="$(read_config_value "target_url")"
  if [[ -n "$saved" ]]; then
    printf "%s" "$saved"
  else
    printf "%s" "$DEFAULT_TARGET_URL"
  fi
}

write_config() {
  local bad_ms="$1"
  local target_url="$2"

  mkdir -p "$CONFIG_DIR"
  printf "bad_ms=%s\ntarget_url=%s\n" "$bad_ms" "$target_url" > "$CONFIG_FILE"
}

set_bad_ms() {
  local value="$1"

  if [[ ! "$value" =~ ^[0-9]+$ ]]; then
    exit 2
  fi

  write_config "$value" "$(current_target_url)"
}

set_target_url() {
  local value="$1"

  case "$value" in
    https://api.openai.com/v1/models|https://status.openai.com/api/v2/status.json|https://chatgpt.com/) ;;
    *) exit 2 ;;
  esac

  write_config "$(current_bad_ms)" "$value"
}

current_interval() {
  local basename
  basename="$(basename "$0")"

  if [[ "$basename" =~ \.([0-9]+s)\.sh$ ]]; then
    printf "%s" "${BASH_REMATCH[1]}"
  else
    printf "unknown"
  fi
}

set_interval() {
  local interval="$1"
  local current_path="$0"
  local current_dir
  local new_path

  case "$interval" in
    5s|10s|30s|60s) ;;
    *) exit 2 ;;
  esac

  current_dir="$(dirname "$current_path")"
  new_path="${current_dir}/openai-watch.${interval}.sh"

  if [[ "$current_path" != "$new_path" ]]; then
    mv "$current_path" "$new_path"
    chmod +x "$new_path"
  fi
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
  local error_file
  local output
  local status

  error_file="$(mktemp "${TMPDIR:-/tmp}/openai-watch-curl.XXXXXX")"

  output="$(
    curl \
      --silent \
      --show-error \
      --location \
      --output /dev/null \
      --max-time "$TIMEOUT" \
      --write-out "%{http_code} %{time_total} %{remote_ip}" \
      "$TARGET_URL" 2>"$error_file"
  )"
  status=$?

  printf "status=%s\n" "$status"
  printf "metrics=%s\n" "$output"
  printf "error=%s\n" "$(tr '\n' ' ' < "$error_file" | cut -c 1-160)"

  rm -f "$error_file"
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

if [[ "${1:-}" == "--set-interval" ]]; then
  set_interval "${2:-}"
  exit 0
fi

if [[ "${1:-}" == "--set-target" ]]; then
  set_target_url "${2:-}"
  exit 0
fi

BAD_MS="$(current_bad_ms)"
TARGET_URL="$(current_target_url)"
INTERVAL="$(current_interval)"
probe_output="$(probe_openai)"
curl_status="$(awk -F= '$1 == "status" { print $2 }' <<< "$probe_output")"
metrics="$(awk -F= '$1 == "metrics" { print substr($0, index($0, "=") + 1) }' <<< "$probe_output")"
error_text="$(awk -F= '$1 == "error" { print substr($0, index($0, "=") + 1) }' <<< "$probe_output")"

if [[ -z "$curl_status" ]]; then
  curl_status=1
fi

http_code="$(awk '{ print $1 }' <<< "$metrics")"
time_total="$(awk '{ print $2 }' <<< "$metrics")"
remote_ip="$(awk '{ print $3 }' <<< "$metrics")"

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

  error_text="$(sed 's/[|]//g' <<< "$error_text")"
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
printf "Check interval: %s\n" "$INTERVAL"
printf "HTTP: %s\n" "${http_code:-unknown}"
printf "Remote IP: %s\n" "${remote_ip:-unknown}"
printf "Target: %s | href=%s\n" "$TARGET_URL" "$TARGET_URL"
if [[ -n "$ENV_TARGET_URL" ]]; then
  printf "Target source: OPENAI_WATCH_URL env override\n"
fi
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
printf "Check interval\n"
printf "5s%s | bash=%s param1=--set-interval param2=5s terminal=false refresh=true\n" "$([[ "$INTERVAL" == "5s" ]] && printf " ✓")" "$SCRIPT_FOR_XBAR"
printf "10s%s | bash=%s param1=--set-interval param2=10s terminal=false refresh=true\n" "$([[ "$INTERVAL" == "10s" ]] && printf " ✓")" "$SCRIPT_FOR_XBAR"
printf "30s%s | bash=%s param1=--set-interval param2=30s terminal=false refresh=true\n" "$([[ "$INTERVAL" == "30s" ]] && printf " ✓")" "$SCRIPT_FOR_XBAR"
printf "60s%s | bash=%s param1=--set-interval param2=60s terminal=false refresh=true\n" "$([[ "$INTERVAL" == "60s" ]] && printf " ✓")" "$SCRIPT_FOR_XBAR"
printf -- "---\n"
printf "Target endpoint\n"
printf "OpenAI API /v1/models%s | bash=%s param1=--set-target param2=https://api.openai.com/v1/models terminal=false refresh=true\n" "$([[ "$TARGET_URL" == "https://api.openai.com/v1/models" ]] && printf " ✓")" "$SCRIPT_FOR_XBAR"
printf "OpenAI status JSON%s | bash=%s param1=--set-target param2=https://status.openai.com/api/v2/status.json terminal=false refresh=true\n" "$([[ "$TARGET_URL" == "https://status.openai.com/api/v2/status.json" ]] && printf " ✓")" "$SCRIPT_FOR_XBAR"
printf "ChatGPT web%s | bash=%s param1=--set-target param2=https://chatgpt.com/ terminal=false refresh=true\n" "$([[ "$TARGET_URL" == "https://chatgpt.com/" ]] && printf " ✓")" "$SCRIPT_FOR_XBAR"
printf -- "---\n"
printf "Refresh | refresh=true\n"
printf "Open OpenAI status | href=https://status.openai.com/\n"

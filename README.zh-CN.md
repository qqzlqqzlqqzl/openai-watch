# OpenAI Watch

[English](README.md) | [中文](README.zh-CN.md)

OpenAI Watch 是一个跨平台的小工具，用来监控当前网络、VPN 或代理节点是否能连通 OpenAI 兼容 API，并在延迟过高或断开时提醒你。

这个仓库现在包含：

- `openai-watch.5s.sh`：macOS 菜单栏插件，适用于 [xbar/SwiftBar]。
- `windows/`：Windows 托盘版本，基于 WinForms，包含独立 `EXE` 和源码/构建脚本。

核心行为一致：绿色表示正常，红色表示延迟过高或连接失败，灰色表示一次偶发失败、暂不报警。

## Release 下载

- [macOS release](https://github.com/qqzlqqzlqqzl/openai-watch/releases/tag/v0.3.0-macos)：xbar / SwiftBar 插件包。
- [Windows release](https://github.com/qqzlqqzlqqzl/openai-watch/releases/tag/v0.3.0-windows)：Windows 源码与构建脚本包。

## 状态含义

- 绿色 `AI OK`：OpenAI 在红色报警阈值内响应，正常。
- 红色 `AI 2345ms`：能连上，但延迟超过阈值，建议切换 VPN 节点。
- 红色 `AI DOWN`：连续连接失败，基本可以认为当前链路不可用。
- 灰色 `AI OK`：单次瞬时失败，还没达到报警条件。

HTTP `401` 会被视为“可连通”，因为这说明请求已经到达 OpenAI，只是没有携带 API key。这个工具检查的是网络连通性和延迟，不检查账号权限。

## 安装

### macOS（xbar / SwiftBar）

从 [macOS release](https://github.com/qqzlqqzlqqzl/openai-watch/releases/tag/v0.3.0-macos) 下载 `openai-watch-macos-v0.3.0.zip`，或者直接使用仓库里的源码脚本。

安装 xbar：

```bash
brew install --cask xbar
```

复制或软链插件：

```bash
mkdir -p "$HOME/Library/Application Support/xbar/plugins"
ln -sf "$PWD/openai-watch.5s.sh" "$HOME/Library/Application Support/xbar/plugins/openai-watch.5s.sh"
chmod +x openai-watch.5s.sh
open -a xbar
```

如果你使用 SwiftBar，把同一个脚本放到 SwiftBar 的插件目录即可。

### Windows

从 [Windows release](https://github.com/qqzlqqzlqqzl/openai-watch/releases/tag/v0.3.0-windows) 下载 Windows 源码与构建脚本包，或者查看 `windows/README.md` 了解构建/运行方式：

- 独立 exe 由 `windows\\build-exe.ps1` 构建。
- 通过 `windows\\Run-OpenAI-Watch-EXE.cmd` 启动。

## 红色报警阈值

打开菜单后可以选择：

- `1.5s strict`
- `2s normal`
- `3s relaxed`
- `5s very relaxed`

所选阈值会保存到：

```text
~/.config/openai-watch/config
```

## 环境变量

```bash
OPENAI_WATCH_URL="https://api.openai.com/v1/models"
OPENAI_WATCH_TIMEOUT=4
OPENAI_WATCH_BAD_MS=2000
OPENAI_WATCH_PROXY_PORTS="7890 7897 1080 8080 6152"
```

Windows 设置保存在 `%APPDATA%\OpenAI Watch\config.ini`。
macOS 设置保存在 `~/.config/openai-watch/config`。

## 探测地址

打开菜单后可以选择：

- `OpenAI API /v1/models`：直接探测 OpenAI API。
- `OpenAI status JSON`：探测 OpenAI 状态页 API。
- `ChatGPT web`：探测 ChatGPT 网页可达性。

如果设置了 `OPENAI_WATCH_URL` 环境变量，它会覆盖菜单里选择的探测地址。

## License

MIT

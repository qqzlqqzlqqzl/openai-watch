$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root 'src\OpenAIWatch.cs'
$legacySrc = Join-Path $root 'OpenAIWatch.cs'
if (-not (Test-Path -LiteralPath $src)) {
  if (Test-Path -LiteralPath $legacySrc) {
    $src = $legacySrc
  }
  else {
    throw "Missing source file. Expected either 'src\\OpenAIWatch.cs' or 'OpenAIWatch.cs' under $root"
  }
}
$dist = Join-Path $root 'dist'
$out = Join-Path $dist 'OpenAIWatch.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $csc)) {
  throw "Missing compiler: $csc"
}

New-Item -ItemType Directory -Force -Path $dist | Out-Null

& $csc `
  /nologo `
  /target:winexe `
  /platform:x64 `
  /optimize+ `
  /out:"$out" `
  /reference:System.dll `
  /reference:System.Core.dll `
  /reference:System.Drawing.dll `
  /reference:System.Windows.Forms.dll `
  "$src"

if ($LASTEXITCODE -ne 0) {
  throw "csc.exe failed with exit code $LASTEXITCODE"
}

Write-Host "Built: $out"

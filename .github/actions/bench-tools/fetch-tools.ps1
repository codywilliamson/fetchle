$ErrorActionPreference = 'Stop'
# windows powershell 5.1: the runner has no pwsh. it needs tls 1.2 named, and its progress bar slows downloads
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
New-Item -ItemType Directory -Force -Path $env:TOOLS_DIR | Out-Null
function Install-Tool($url, $sha256, $exe) {
  $zip = Join-Path $env:RUNNER_TEMP ([IO.Path]::GetFileName($url))
  Invoke-WebRequest -Uri $url -OutFile $zip
  $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $zip).Hash.ToLowerInvariant()
  if ($actual -ne $sha256) { throw "sha256 mismatch for ${url}: got $actual, want $sha256" }
  $unpacked = "$zip.unpacked"
  Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $unpacked
  Expand-Archive -LiteralPath $zip -DestinationPath $unpacked
  $found = Get-ChildItem -Recurse -File -Path $unpacked -Filter $exe | Select-Object -First 1
  if (-not $found) { throw "$exe not in $url" }
  Copy-Item $found.FullName -Destination $env:TOOLS_DIR -Force
}
Install-Tool $env:RG_URL $env:RG_SHA256 'rg.exe'
Install-Tool $env:FD_URL $env:FD_SHA256 'fd.exe'
& (Join-Path $env:TOOLS_DIR 'rg.exe') --version
& (Join-Path $env:TOOLS_DIR 'fd.exe') --version

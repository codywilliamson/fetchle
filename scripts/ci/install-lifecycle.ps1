# install, query, break, doctor --fix, uninstall: twice in a row, against a locally built archive.
# CI calls this on windows-latest. it edits the real user PATH in the registry (and puts it back), so only run it on a throwaway machine
# or a box where you've saved HKCU\Environment Path.
#
#   pwsh scripts/ci/install-lifecycle.ps1 -Archive dist/fetchle-0.0.0-ci-win-x64.zip -Version 0.0.0-ci
param(
    [Parameter(Mandatory)] [string] $Archive,
    [Parameter(Mandatory)] [string] $Version
)

$ErrorActionPreference = 'Stop'
$ScriptRoot = Split-Path -Parent $PSScriptRoot
$Pwsh = (Get-Process -Id $PID).Path
$Work = Join-Path ([System.IO.Path]::GetTempPath()) "fetchle-lifecycle-$([guid]::NewGuid().ToString('N'))"
$LocalAppData = Join-Path $Work 'localappdata'
$Tree = Join-Path $Work 'tree'
# both roots hold a subdirectory: a multi-root rebuild once failed its own parent-order check on exactly that
$Tree2 = Join-Path $Work 'other-root'
$Bin = Join-Path $LocalAppData 'fetchle\bin'
$Data = Join-Path $LocalAppData 'fetchle'
$Exe = Join-Path $Bin 'fetchle.exe'

function Assert-That([bool] $Condition, [string] $Message) {
    if (-not $Condition) { throw "ASSERT FAILED: $Message" }
    Write-Host "ok: $Message"
}

function Get-UserPathRaw {
    $key = Get-Item 'HKCU:\Environment'
    $key.GetValue('Path', $null, 'DoNotExpandEnvironmentNames')
}

function Set-UserPathRaw($Value) {
    $key = Get-Item 'HKCU:\Environment'
    if ($null -eq $Value) {
        Remove-ItemProperty -Path 'HKCU:\Environment' -Name 'Path' -ErrorAction SilentlyContinue
    } else {
        Set-ItemProperty -Path 'HKCU:\Environment' -Name 'Path' -Value $Value
    }
}

function Get-EntryCount([string] $Path, [string] $Entry) {
    if (-not $Path) { return 0 }
    @($Path -split ';' | Where-Object { $_.TrimEnd('\') -ieq $Entry.TrimEnd('\') }).Count
}

# what a new terminal would get: machine PATH then user PATH, read fresh from the registry
function Get-FreshPath {
    $machine = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $user = [Environment]::GetEnvironmentVariable('Path', 'User')
    "$machine;$user"
}

# runs a command line in a new pwsh whose PATH came from the registry, so only the persisted PATH can find fetchle
function Invoke-Fresh([string] $Command) {
    $saved = $env:Path
    try {
        $env:Path = Get-FreshPath
        $output = & $Pwsh -NoProfile -Command "$Command; exit `$LASTEXITCODE" 2>&1 | Out-String
        return @{ Code = $LASTEXITCODE; Output = $output.Trim() }
    } finally {
        $env:Path = $saved
    }
}

function Assert-Fresh([string] $Command, [int] $ExpectedCode, [string] $Message) {
    $result = Invoke-Fresh $Command
    if ($result.Code -ne $ExpectedCode) { Write-Host $result.Output }
    Assert-That ($result.Code -eq $ExpectedCode) "$Message (exit $($result.Code), wanted $ExpectedCode)"
    $result
}

function Invoke-InstallScript {
    & $Pwsh -NoProfile -ExecutionPolicy Bypass -File (Join-Path $ScriptRoot 'install.ps1')
    Assert-That ($LASTEXITCODE -eq 0) 'install.ps1 exits 0'
}

function Invoke-Cycle([int] $Number) {
    Write-Host "=== cycle $Number ==="
    Invoke-InstallScript
    Invoke-InstallScript

    $userPath = Get-UserPathRaw
    Assert-That ((Get-EntryCount $userPath $Bin) -eq 1) "user PATH holds $Bin exactly once after two installs (found $(Get-EntryCount $userPath $Bin): $userPath)"
    Assert-That (Test-Path (Join-Path $Data 'receipt.json')) 'receipt exists'
    $receipt = Get-Content (Join-Path $Data 'receipt.json') -Raw | ConvertFrom-Json
    Assert-That ($receipt.channel -eq 'script') "receipt channel is script (found $($receipt.channel))"
    Assert-That ($receipt.install_dir -ieq $Bin) "receipt install_dir is $Bin (found $($receipt.install_dir))"
    Assert-That (@($receipt.agents).Count -eq 0) 'receipt agents is empty'

    $where = Invoke-Fresh '(Get-Command fetchle).Source'
    Assert-That ($where.Output -ieq $Exe) "a fresh shell resolves fetchle to $Exe (found '$($where.Output)')"
    $query = Invoke-Fresh "fetchle settings --root '$Tree'"
    $expected = Join-Path $Tree 'settings.json'
    Assert-That ($query.Code -eq 0 -and $query.Output -ieq $expected) "query by name through PATH finds $expected (exit $($query.Code), output '$($query.Output)')"

    Assert-Fresh "fetchle roots add '$Tree'" 0 'roots add' | Out-Null
    Assert-Fresh 'fetchle index rebuild' 0 'index rebuild' | Out-Null
    Assert-Fresh 'fetchle doctor' 0 'doctor passes on a healthy install'

    # break PATH
    Set-UserPathRaw ((Get-UserPathRaw) -split ';' | Where-Object { $_.TrimEnd('\') -ine $Bin } | Join-String -Separator ';')
    $broken = Invoke-Fresh "& '$Exe' doctor"
    Assert-That ($broken.Code -eq 5 -and $broken.Output -match 'fail\s+path') "doctor fails when the PATH entry is gone (exit $($broken.Code))"
    Assert-That ((Invoke-Fresh "& '$Exe' doctor --fix").Code -eq 0) 'doctor --fix repairs PATH'
    Assert-That ((Get-EntryCount (Get-UserPathRaw) $Bin) -eq 1) 'PATH entry is back exactly once'
    Assert-Fresh 'fetchle doctor' 0 'doctor passes after the PATH fix' | Out-Null

    # break the index two ways, and add a root the snapshot lacks
    $snapshot = Join-Path $Data 'index\snapshot.fidx'
    [System.IO.File]::WriteAllBytes($snapshot, [byte[]](1, 2, 3))
    Assert-Fresh 'fetchle doctor' 5 'doctor fails on a corrupt snapshot' | Out-Null
    Assert-Fresh 'fetchle doctor --fix' 0 'doctor --fix rebuilds a corrupt snapshot' | Out-Null
    Remove-Item $snapshot
    Assert-Fresh 'fetchle doctor' 5 'doctor fails on a missing snapshot' | Out-Null
    Assert-Fresh 'fetchle doctor --fix' 0 'doctor --fix rebuilds a missing snapshot' | Out-Null
    Assert-Fresh "fetchle roots add '$Tree2'" 0 'roots add second root' | Out-Null
    Assert-Fresh 'fetchle doctor' 5 'doctor fails when a root is not in the snapshot' | Out-Null
    Assert-Fresh 'fetchle doctor --fix' 0 'doctor --fix indexes the new root' | Out-Null
    Assert-Fresh 'fetchle doctor' 0 'doctor passes again' | Out-Null

    Assert-Fresh 'fetchle uninstall --yes' 0 'uninstall' | Out-Null
    # the running exe is removed by a detached cleanup shortly after exit
    $deadline = (Get-Date).AddSeconds(60)
    while ((Test-Path $Data) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    Assert-That (-not (Test-Path $Bin)) "install dir is gone ($Bin)"
    Assert-That (-not (Test-Path $Data)) "data dir is gone ($Data)"
    Assert-That ((Get-UserPathRaw) -eq $script:UserPathAtStart) "user PATH is back to its starting value (now '$(Get-UserPathRaw)')"
    $left = @(Get-ChildItem $LocalAppData -Recurse -File -ErrorAction SilentlyContinue)
    Assert-That ($left.Count -eq 0) "no files left under the temp LOCALAPPDATA (found: $($left.FullName -join ', '))"
}

New-Item -ItemType Directory -Force -Path $Tree, $Tree2, $LocalAppData | Out-Null
Set-Content (Join-Path $Tree 'settings.json') '{}'
Set-Content (Join-Path $Tree2 'notes.txt') 'x'
New-Item -ItemType Directory -Force -Path (Join-Path $Tree 'sub'), (Join-Path $Tree2 'sub') | Out-Null
Set-Content (Join-Path $Tree 'sub.txt') 'x'
Set-Content (Join-Path $Tree2 'sub.txt') 'x'

$script:UserPathAtStart = Get-UserPathRaw
$archivePath = (Resolve-Path $Archive).Path
$env:LOCALAPPDATA = $LocalAppData
$env:FETCHLE_VERSION = $Version
$env:FETCHLE_DOWNLOAD_BASE = Split-Path -Parent $archivePath
$env:FETCHLE_RELEASE_API = 'http://127.0.0.1:1/'
# agent mode adds a footer line to query output
Remove-Item Env:FETCHLE_INSTALL_DIR, Env:FETCHLE_DATA_DIR, Env:CLAUDECODE -ErrorAction SilentlyContinue

try {
    Invoke-Cycle 1
    Invoke-Cycle 2
    Write-Host 'install lifecycle passed twice'
} finally {
    if ((Get-UserPathRaw) -ne $script:UserPathAtStart) {
        Write-Host 'restoring the user PATH'
        Set-UserPathRaw $script:UserPathAtStart
    }
    Remove-Item $Work -Recurse -Force -ErrorAction SilentlyContinue
}

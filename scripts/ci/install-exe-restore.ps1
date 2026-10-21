# install.ps1 moves the old exe aside before copying the new one. if the copy fails, the old exe has to come back.
# pulls Install-FetchleExe out of install.ps1 (the script runs itself on load, so it can't be dot-sourced) and feeds it a
# source that doesn't exist, which makes the copy fail after the move. touches only a temp dir, safe on any machine.
#
#   pwsh scripts/ci/install-exe-restore.ps1
$ErrorActionPreference = 'Stop'
$Installer = Join-Path (Split-Path -Parent $PSScriptRoot) 'install.ps1'

$ast = [System.Management.Automation.Language.Parser]::ParseFile($Installer, [ref]$null, [ref]$null)
$function = $ast.FindAll({ param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Install-FetchleExe' }, $true) | Select-Object -First 1
if (-not $function) { throw 'ASSERT FAILED: Install-FetchleExe not found in install.ps1' }
. ([scriptblock]::Create($function.Extent.Text))

$work = Join-Path ([System.IO.Path]::GetTempPath()) "fetchle-exe-restore-$([guid]::NewGuid().ToString('N'))"
try {
    $bin = Join-Path $work 'bin'
    New-Item -ItemType Directory -Force -Path $bin | Out-Null
    $target = Join-Path $bin 'fetchle.exe'
    Set-Content -LiteralPath $target -Value 'previous exe' -NoNewline

    $threw = $false
    try { Install-FetchleExe (Join-Path $work 'does-not-exist.exe') $bin | Out-Null } catch { $threw = $true }

    if (-not $threw) { throw 'ASSERT FAILED: a failed copy should throw' }
    if (-not (Test-Path -LiteralPath $target)) { throw 'ASSERT FAILED: the previous exe was not put back after the copy failed' }
    if ((Get-Content -LiteralPath $target -Raw) -ne 'previous exe') { throw 'ASSERT FAILED: the restored exe is not the previous one' }
    if (Test-Path -LiteralPath "$target.old") { throw 'ASSERT FAILED: .old left behind after the restore' }
    Write-Host 'ok: a failed copy puts the previous exe back'
} finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}

# usage: ./build.ps1 [build|test|e2e|eval|publish|bench] [-Rid <rid>]
param(
    [ValidateSet('build', 'test', 'e2e', 'eval', 'publish', 'bench')]
    [string]$Target = 'build',
    [string]$Rid = ''
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

# aot link needs vswhere.exe on PATH, see docs/release.md
# $IsWindows doesn't exist in windows powershell 5.1, and ProgramFiles(x86) is unset off windows
if ($env:OS -eq 'Windows_NT') {
    $vsInstaller = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
    if ((Test-Path $vsInstaller) -and ($env:PATH -notlike "*$vsInstaller*")) {
        $env:PATH = "$env:PATH;$vsInstaller"
    }
}

function Invoke-Step([string[]]$cmd) {
    Write-Host "> dotnet $($cmd -join ' ')"
    & dotnet @cmd
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$ridArgs = if ($Rid) { @('-r', $Rid) } else { @() }

switch ($Target) {
    'build' { Invoke-Step @('build', 'fetchle.slnx', '-c', 'Release') }
    'test' { Invoke-Step @('test', '--project', 'tests/Fetchle.Tests', '-c', 'Release') }
    'e2e' { Invoke-Step @('test', '--project', 'tests/Fetchle.E2E', '-c', 'Release') }
    'eval' { Invoke-Step @('run', '--project', 'evals/Fetchle.Evals', '-c', 'Release') }
    'publish' { Invoke-Step (@('publish', 'src/Fetchle.Cli', '-c', 'Release', '-o', 'artifacts/publish') + $ridArgs) }
    'bench' {
        # external harness times the published exe against rg
        Invoke-Step (@('publish', 'src/Fetchle.Cli', '-c', 'Release', '-o', 'artifacts/publish') + $ridArgs)
        Invoke-Step @('run', '--project', 'bench/Fetchle.Bench', '-c', 'Release')
        Invoke-Step @('run', '--project', 'bench/Fetchle.Bench', '-c', 'Release', '--', '--external')
    }
}

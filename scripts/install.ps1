# fetchle installer for Windows.
#   irm https://fetchle.dev/install.ps1 | iex
#
# settings come from env vars, because iex can't take arguments:
#   FETCHLE_VERSION         release to install, e.g. 0.1.0 (default: latest)
#   FETCHLE_DOWNLOAD_BASE   url or local directory holding the archive and its .sha256 (needs FETCHLE_VERSION)
#   FETCHLE_INSTALL_DIR     where the exe goes (default: %LOCALAPPDATA%\fetchle\bin)
#
# the script stays thin: download, verify, unpack, then `fetchle setup` does PATH and the receipt.
# errors throw instead of calling exit, so a piped run doesn't close your shell.

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$Repo = 'codywilliamson/fetchle'

function Get-FetchleRid {
    $arch = $null
    try { $arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString() } catch { }
    if (-not $arch) {
        $arch = if ($env:PROCESSOR_ARCHITEW6432) { $env:PROCESSOR_ARCHITEW6432 } else { $env:PROCESSOR_ARCHITECTURE }
    }
    switch ($arch.ToUpperInvariant()) {
        { $_ -in 'X64', 'AMD64' } { return 'win-x64' }
        'ARM64' { return 'win-arm64' }
        default { throw "fetchle install: unsupported architecture '$arch'. supported: win-x64, win-arm64" }
    }
}

function Get-FetchleVersion {
    if ($env:FETCHLE_VERSION) { return $env:FETCHLE_VERSION.TrimStart('v') }
    if ($env:FETCHLE_DOWNLOAD_BASE) { throw 'fetchle install: set FETCHLE_VERSION when FETCHLE_DOWNLOAD_BASE is set' }
    $release = Invoke-RestMethod -UseBasicParsing -Uri "https://api.github.com/repos/$Repo/releases/latest" -Headers @{ 'User-Agent' = 'fetchle-install' }
    return $release.tag_name.TrimStart('v')
}

function Get-FetchleFile($Base, $Name, $Destination) {
    if (Test-Path -LiteralPath $Base -PathType Container) {
        Copy-Item -LiteralPath (Join-Path $Base $Name) -Destination $Destination
    } else {
        Invoke-WebRequest -UseBasicParsing -Uri "$Base/$Name" -OutFile $Destination
    }
}

function Confirm-FetchleChecksum($Archive, $ChecksumFile) {
    $expected = ((Get-Content -LiteralPath $ChecksumFile -Raw).Trim() -split '\s+')[0].ToLowerInvariant()
    $actual = (Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($expected -ne $actual) {
        Remove-Item -LiteralPath $Archive -Force
        throw "fetchle install: checksum mismatch for $(Split-Path $Archive -Leaf), download deleted`n  expected: $expected`n  actual:   $actual"
    }
}

# a running exe can be renamed but not overwritten, so move it aside first
function Install-FetchleExe($Source, $InstallDir) {
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    $target = Join-Path $InstallDir 'fetchle.exe'
    $old = "$target.old"
    if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $target) { Move-Item -LiteralPath $target -Destination $old -Force }
    try {
        Copy-Item -LiteralPath $Source -Destination $target -Force
    } catch {
        # the old exe is already aside, so put it back before reporting the failure
        Remove-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue
        if (Test-Path -LiteralPath $old) { Move-Item -LiteralPath $old -Destination $target -Force }
        throw
    }
    # the previous exe stays as .old until setup has worked, so a failed setup can put it back
    return $target
}

# setup failed: no receipt records this exe, so leave the machine as it was
function Undo-FetchleExe($Target) {
    $old = "$Target.old"
    Remove-Item -LiteralPath $Target -Force -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $old) {
        Move-Item -LiteralPath $old -Destination $Target -Force
    } else {
        $dir = Split-Path -Parent $Target
        if (-not (Get-ChildItem -LiteralPath $dir -Force -ErrorAction SilentlyContinue)) { Remove-Item -LiteralPath $dir -Force -ErrorAction SilentlyContinue }
    }
}

function Install-Fetchle {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    $rid = Get-FetchleRid
    $version = Get-FetchleVersion
    $name = "fetchle-$version-$rid"
    $base = if ($env:FETCHLE_DOWNLOAD_BASE) { $env:FETCHLE_DOWNLOAD_BASE.TrimEnd('/', '\') } else { "https://github.com/$Repo/releases/download/v$version" }
    $installDir = if ($env:FETCHLE_INSTALL_DIR) { $env:FETCHLE_INSTALL_DIR } else { Join-Path $env:LOCALAPPDATA 'fetchle\bin' }

    $work = Join-Path ([System.IO.Path]::GetTempPath()) "fetchle-install-$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Force -Path $work | Out-Null
    try {
        $archive = Join-Path $work "$name.zip"
        $checksum = "$archive.sha256"
        Write-Host "fetchle install: $name from $base"
        Get-FetchleFile $base "$name.zip" $archive
        Get-FetchleFile $base "$name.zip.sha256" $checksum
        Confirm-FetchleChecksum $archive $checksum

        # fully qualified because modules like Pscx ship their own Expand-Archive
        Microsoft.PowerShell.Archive\Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $work 'unpacked') -Force
        $exe = Install-FetchleExe (Join-Path $work "unpacked\$name\fetchle.exe") $installDir
    } finally {
        Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
    }

    & $exe setup --yes
    if ($LASTEXITCODE -ne 0) {
        $code = $LASTEXITCODE
        Undo-FetchleExe $exe
        throw "fetchle install: 'fetchle setup --yes' exited $code, put back what was there before"
    }
    Remove-Item -LiteralPath "$exe.old" -Force -ErrorAction SilentlyContinue
    Write-Host "fetchle install: done, $exe"
}

Install-Fetchle

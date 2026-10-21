#!/bin/sh
# fetchle installer for Linux and macOS.
#   curl -fsSL https://fetchle.dev/install.sh | sh
#
# settings come from env vars:
#   FETCHLE_VERSION         release to install, e.g. 0.1.0 (default: latest)
#   FETCHLE_DOWNLOAD_BASE   url or local directory holding the archive and its .sha256 (needs FETCHLE_VERSION)
#   FETCHLE_INSTALL_DIR     where the exe goes (default: ~/.local/share/fetchle/bin)
#
# the script stays thin: download, verify, unpack, then `fetchle setup` does PATH and the receipt.
set -eu

REPO="codywilliamson/fetchle"

fail() {
  echo "fetchle install: $*" >&2
  exit 1
}

have() {
  command -v "$1" > /dev/null 2>&1
}

detect_rid() {
  os="$(uname -s)"
  arch="$(uname -m)"
  case "$os $arch" in
    "Linux x86_64" | "Linux amd64") echo "linux-x64" ;;
    "Linux aarch64" | "Linux arm64") echo "linux-arm64" ;;
    "Darwin arm64") echo "osx-arm64" ;;
    *) fail "unsupported platform '$os $arch'. supported: linux-x64, linux-arm64, osx-arm64" ;;
  esac
}

# prints a url's body
fetch_text() {
  if have curl; then
    curl -fsSL -H "User-Agent: fetchle-install" "$1"
  elif have wget; then
    wget -qO- --header="User-Agent: fetchle-install" "$1"
  else
    fail "need curl or wget"
  fi
}

# copies from a local directory or downloads from a url
fetch_file() {
  base="$1"
  file="$2"
  dest="$3"
  if [ -d "$base" ]; then
    cp "$base/$file" "$dest" || fail "can't copy $base/$file"
  elif have curl; then
    curl -fsSL "$base/$file" -o "$dest" || fail "download failed: $base/$file"
  elif have wget; then
    wget -q -O "$dest" "$base/$file" || fail "download failed: $base/$file"
  else
    fail "need curl or wget"
  fi
}

sha256_of() {
  if have sha256sum; then
    sha256sum "$1" | cut -d' ' -f1
  elif have shasum; then
    shasum -a 256 "$1" | cut -d' ' -f1
  else
    fail "need sha256sum or shasum to verify the download"
  fi
}

latest_version() {
  tag="$(fetch_text "https://api.github.com/repos/$REPO/releases/latest" | sed -n 's/.*"tag_name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -n 1)"
  [ -n "$tag" ] || fail "couldn't read the latest release from GitHub, set FETCHLE_VERSION"
  echo "${tag#v}"
}

verify_checksum() {
  archive="$1"
  checksum_file="$2"
  expected="$(cut -d' ' -f1 < "$checksum_file" | tr '[:upper:]' '[:lower:]')"
  actual="$(sha256_of "$archive" | tr '[:upper:]' '[:lower:]')"
  if [ "$expected" != "$actual" ]; then
    rm -f "$archive"
    fail "checksum mismatch for $(basename "$archive"), download deleted
  expected: $expected
  actual:   $actual"
  fi
}

# a running exe can be replaced by renaming over it, never by writing into it. the previous one is kept as
# fetchle.old until setup has worked, so a failed setup can put it back
install_exe() {
  src="$1"
  dir="$2"
  mkdir -p "$dir"
  cp "$src" "$dir/fetchle.new"
  chmod 755 "$dir/fetchle.new"
  rm -f "$dir/fetchle.old"
  if [ -f "$dir/fetchle" ]; then
    cp -p "$dir/fetchle" "$dir/fetchle.old"
  fi
  mv -f "$dir/fetchle.new" "$dir/fetchle"
}

# setup failed: no receipt records this exe, so leave the machine as it was
roll_back_exe() {
  dir="$1"
  if [ -f "$dir/fetchle.old" ]; then
    mv -f "$dir/fetchle.old" "$dir/fetchle"
  else
    rm -f "$dir/fetchle"
    rmdir "$dir" 2> /dev/null || true
  fi
}

main() {
  : "${HOME:?HOME is not set}"
  rid="$(detect_rid)"

  if [ -n "${FETCHLE_VERSION:-}" ]; then
    version="${FETCHLE_VERSION#v}"
  elif [ -n "${FETCHLE_DOWNLOAD_BASE:-}" ]; then
    fail "set FETCHLE_VERSION when FETCHLE_DOWNLOAD_BASE is set"
  else
    version="$(latest_version)"
  fi

  name="fetchle-$version-$rid"
  base="${FETCHLE_DOWNLOAD_BASE:-https://github.com/$REPO/releases/download/v$version}"
  base="${base%/}"
  install_dir="${FETCHLE_INSTALL_DIR:-$HOME/.local/share/fetchle/bin}"

  work="$(mktemp -d)"
  trap 'rm -rf "$work"' EXIT INT TERM

  echo "fetchle install: $name from $base"
  fetch_file "$base" "$name.tar.gz" "$work/$name.tar.gz"
  fetch_file "$base" "$name.tar.gz.sha256" "$work/$name.tar.gz.sha256"
  verify_checksum "$work/$name.tar.gz" "$work/$name.tar.gz.sha256"

  mkdir -p "$work/unpacked"
  tar -xzf "$work/$name.tar.gz" -C "$work/unpacked" || fail "couldn't unpack $name.tar.gz"
  [ -f "$work/unpacked/$name/fetchle" ] || fail "$name.tar.gz doesn't contain $name/fetchle"
  install_exe "$work/unpacked/$name/fetchle" "$install_dir"
  # the embedding model and its notices sit beside the exe; without the model search ranks by words only
  for companion in potion-retrieval-32M.fmodel THIRD-PARTY-NOTICES.md; do
    cp "$work/unpacked/$name/$companion" "$install_dir/$companion" || fail "can't copy $companion"
  done

  if ! "$install_dir/fetchle" setup --yes; then
    roll_back_exe "$install_dir"
    fail "'fetchle setup --yes' failed, put back what was there before"
  fi
  rm -f "$install_dir/fetchle.old"
  echo "fetchle install: done, $install_dir/fetchle"
}

main "$@"

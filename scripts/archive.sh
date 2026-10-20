#!/usr/bin/env bash
# packs a published exe into the release archive and writes its .sha256.
# publish.yml and the install CI job both call this, so the archive layout can't drift from what install.sh and install.ps1 unpack.
#
# usage: scripts/archive.sh <version> <rid> [publish-dir] [out-dir]
# prints the archive path (under out-dir as given) on its last line of stdout
set -euo pipefail

version="${1:?usage: archive.sh <version> <rid> [publish-dir] [out-dir]}"
rid="${2:?usage: archive.sh <version> <rid> [publish-dir] [out-dir]}"
publish_dir="${3:-artifacts/publish}"
out_dir="${4:-dist}"

name="fetchle-${version#v}-${rid}"
stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT

case "$rid" in
  win-*) exe="fetchle.exe" ;;
  *) exe="fetchle" ;;
esac

if [ ! -f "$publish_dir/$exe" ]; then
  echo "archive.sh: $publish_dir/$exe not found, publish first" >&2
  exit 1
fi

mkdir -p "$stage/$name" "$out_dir"
cp "$publish_dir/$exe" "$stage/$name/"

# build an absolute out path so the subshells below can cd around
out_abs="$(cd "$out_dir" && pwd)"

zip_it() {
  if command -v 7z > /dev/null 2>&1; then
    (cd "$stage" && 7z a -tzip "$out_abs/$name.zip" "$name" > /dev/null)
    return
  fi
  # windows runners have 7z, a dev box may only have the bsdtar that ships with windows
  local bsdtar
  bsdtar="$(cygpath -u "${SYSTEMROOT:-C:\\Windows}" 2> /dev/null || echo /c/Windows)/System32/tar.exe"
  if [ -x "$bsdtar" ]; then
    (cd "$stage" && "$bsdtar" -a -c -f "$(cygpath -w "$out_abs/$name.zip")" "$name")
    return
  fi
  echo "archive.sh: need 7z (or windows tar.exe) to make a zip" >&2
  exit 1
}

case "$rid" in
  win-*)
    zip_it
    archive="$out_abs/$name.zip"
    ;;
  *)
    chmod +x "$stage/$name/$exe"
    tar -czf "$out_abs/$name.tar.gz" -C "$stage" "$name"
    archive="$out_abs/$name.tar.gz"
    ;;
esac

if command -v sha256sum > /dev/null 2>&1; then
  hash="$(sha256sum "$archive" | cut -d' ' -f1)"
else
  hash="$(shasum -a 256 "$archive" | cut -d' ' -f1)"
fi
# two spaces, like sha256sum writes it
printf '%s  %s\n' "$hash" "$(basename "$archive")" > "$archive.sha256"

echo "$out_dir/$(basename "$archive")"

#!/usr/bin/env bash
# install, query, break, doctor --fix, uninstall: twice in a row, against a locally built archive.
# CI calls this on ubuntu-latest and macos-latest. everything lives under a temp HOME, nothing outside it is touched.
#
#   scripts/ci/install-lifecycle.sh dist/fetchle-0.0.0-ci-linux-x64.tar.gz 0.0.0-ci
set -euo pipefail

archive="${1:?usage: install-lifecycle.sh <archive> <version>}"
version="${2:?usage: install-lifecycle.sh <archive> <version>}"

script_dir="$(cd "$(dirname "$0")/.." && pwd)"
archive="$(cd "$(dirname "$archive")" && pwd)/$(basename "$archive")"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
export HOME="$work/home"
mkdir -p "$HOME"

if [ "$(uname -s)" = "Darwin" ]; then
  shell_path="/bin/zsh"
  rc_file="$HOME/.zshrc"
  data_dir="$HOME/Library/Application Support/fetchle"
else
  shell_path="/bin/bash"
  rc_file="$HOME/.bashrc"
  data_dir="$HOME/.local/share/fetchle"
fi
bin_dir="$HOME/.local/share/fetchle/bin"
exe="$bin_dir/fetchle"
tree="$work/tree"
tree2="$work/other-root"
# both roots hold a subdirectory: a multi-root rebuild once failed its own parent-order check on exactly that
mkdir -p "$tree/sub" "$tree2/sub"
echo '{}' > "$tree/settings.json"
echo x > "$tree2/notes.txt"
echo x > "$tree/sub/a.txt"
echo x > "$tree2/sub/b.txt"

# agent mode adds a footer line to query output
unset CLAUDECODE FETCHLE_INSTALL_DIR FETCHLE_DATA_DIR XDG_DATA_HOME || true
export SHELL="$shell_path"
export FETCHLE_VERSION="$version"
FETCHLE_DOWNLOAD_BASE="$(dirname "$archive")"
export FETCHLE_DOWNLOAD_BASE
export FETCHLE_RELEASE_API="http://127.0.0.1:1/"

ok() { echo "ok: $*"; }

fail() {
  echo "ASSERT FAILED: $*" >&2
  exit 1
}

count_in() {
  # number of fixed-string matches of $1 in file $2
  if [ -f "$2" ]; then grep -c -F -- "$1" "$2" || true; else echo 0; fi
}

# a new interactive shell with a minimal environment, so only the rc file can put fetchle on PATH
fresh() {
  env -i HOME="$HOME" SHELL="$shell_path" TERM=dumb PATH="/usr/bin:/bin:/usr/sbin:/sbin" HISTFILE=/dev/null SHELL_SESSIONS_DISABLE=1 \
    FETCHLE_RELEASE_API="$FETCHLE_RELEASE_API" "$shell_path" -i -c "$1" 2> /dev/null
}

# runs a fresh shell command and checks its exit code, printing what it said when it's wrong
expect_fresh() {
  local want="$1" message="$2" cmd="$3" out code
  set +e
  out="$(fresh "$cmd")"
  code=$?
  set -e
  if [ "$code" -ne "$want" ]; then
    echo "$out"
    fail "$message (exit $code, wanted $want)"
  fi
  ok "$message"
}

run_install() {
  sh "$script_dir/install.sh" || fail "install.sh exited nonzero"
  ok "install.sh exits 0"
}

cycle() {
  echo "=== cycle $1 ==="
  run_install
  run_install

  local blocks
  blocks="$(count_in '# >>> fetchle >>>' "$rc_file")"
  [ "$blocks" = "1" ] || fail "$rc_file should hold exactly one fetchle block after two installs (found $blocks)"
  ok "one marker block in $rc_file"

  local receipt="$data_dir/receipt.json"
  [ -f "$receipt" ] || fail "receipt missing at $receipt"
  grep -q '"channel": "script"' "$receipt" || fail "receipt channel is not script: $(cat "$receipt")"
  grep -q '"agents": \[\]' "$receipt" || fail "receipt agents is not empty: $(cat "$receipt")"
  ok "receipt exists with channel script and no agents"

  [ -f "$(dirname "$exe")/potion-retrieval-32M.fmodel" ] || fail "the model isn't beside $exe"
  ok "the model sits beside the exe"

  local found
  found="$(fresh 'command -v fetchle')" || true
  [ "$found" = "$exe" ] || fail "a fresh shell should resolve fetchle to $exe (found '$found')"
  ok "a fresh shell resolves fetchle to $exe"

  local out
  out="$(fresh "fetchle settings --root '$tree'")" || fail "query exited nonzero: $out"
  [ "$out" = "$tree/settings.json" ] || fail "query should print $tree/settings.json (found '$out')"
  ok "query by name through PATH finds $tree/settings.json"

  expect_fresh 0 "roots add" "fetchle roots add '$tree'"
  expect_fresh 0 "index rebuild" "fetchle index rebuild"
  expect_fresh 0 "doctor passes on a healthy install" "fetchle doctor"

  # break PATH by removing the block
  sed -i.bak '/# >>> fetchle >>>/,/# <<< fetchle <<</d' "$rc_file"
  rm -f "$rc_file.bak"
  expect_fresh 5 "doctor fails when the PATH block is gone" "'$exe' doctor"
  expect_fresh 0 "doctor --fix repairs PATH" "'$exe' doctor --fix"
  blocks="$(count_in '# >>> fetchle >>>' "$rc_file")"
  [ "$blocks" = "1" ] || fail "doctor --fix should leave exactly one block (found $blocks)"
  ok "marker block is back exactly once"
  expect_fresh 0 "doctor passes after the PATH fix" "fetchle doctor"

  # break the index two ways, then add a root the snapshot lacks
  local snapshot="$data_dir/index/snapshot.fidx"
  printf '\001\002\003' > "$snapshot"
  expect_fresh 5 "doctor fails on a corrupt snapshot" "fetchle doctor"
  expect_fresh 0 "doctor --fix rebuilds a corrupt snapshot" "fetchle doctor --fix"
  rm -f "$snapshot"
  expect_fresh 5 "doctor fails on a missing snapshot" "fetchle doctor"
  expect_fresh 0 "doctor --fix rebuilds a missing snapshot" "fetchle doctor --fix"
  expect_fresh 0 "roots add second root" "fetchle roots add '$tree2'"
  expect_fresh 5 "doctor fails when a root is not in the snapshot" "fetchle doctor"
  expect_fresh 0 "doctor --fix indexes the new root" "fetchle doctor --fix"
  expect_fresh 0 "doctor passes again" "fetchle doctor"

  expect_fresh 0 "uninstall" "fetchle uninstall --yes"
  [ ! -e "$bin_dir" ] || fail "install dir still exists: $bin_dir"
  [ ! -e "$data_dir" ] || fail "data dir still exists: $data_dir"
  ok "install dir and data dir are gone"
  if [ -e "$rc_file" ]; then
    leftover="$(count_in 'fetchle' "$rc_file")"
    [ "$leftover" = "0" ] || fail "$rc_file still mentions fetchle: $(cat "$rc_file")"
  fi
  ok "no fetchle block left in the rc file"

  local files
  # interactive shells may leave history and completion caches behind, those aren't ours
  files="$(find "$HOME" -type f ! -path "$tree/*" ! -path "$tree2/*" ! -name '*_history' ! -name '.zcompdump*' ! -path '*/.zsh_sessions/*' | sort)"
  [ -z "$files" ] || fail "files left under HOME after uninstall: $files"
  local named
  named="$(find "$HOME" -name 'fetchle*' | sort)"
  [ -z "$named" ] || fail "fetchle paths left under HOME after uninstall: $named"
  ok "HOME is back to its starting state"
}

cycle 1
cycle 2
echo "install lifecycle passed twice"

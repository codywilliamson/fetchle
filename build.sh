#!/usr/bin/env bash
# usage: ./build.sh [build|test|e2e|eval|publish|bench] [rid]
set -euo pipefail
cd "$(dirname "$0")"

target="${1:-build}"
rid="${2:-}"

step() { echo "> dotnet $*"; dotnet "$@"; }

case "$target" in
  build)   step build fetchle.slnx -c Release ;;
  test)    step test --project tests/Fetchle.Tests -c Release ;;
  e2e)     step test --project tests/Fetchle.E2E -c Release ;;
  eval)    step run --project evals/Fetchle.Evals -c Release ;;
  publish) step publish src/Fetchle.Cli -c Release -o artifacts/publish ${rid:+-r "$rid"} ;;
  bench)   # external harness times the published exe against rg
           step publish src/Fetchle.Cli -c Release -o artifacts/publish ${rid:+-r "$rid"}
           step run --project bench/Fetchle.Bench -c Release
           step run --project bench/Fetchle.Bench -c Release -- --external ;;
  *) echo "unknown target: $target (build|test|e2e|eval|publish|bench)" >&2; exit 2 ;;
esac

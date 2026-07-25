#!/usr/bin/env bash
#
# Restores the repository to a fresh, just-cloned state.
#
#   ./clean.sh              remove all build output
#   ./clean.sh --dry-run    list what would be removed, delete nothing
#
# Removes: every bin/ and obj/ directory, TestResults/, artifacts/, and *.user
# files. Source, the .git directory and IDE settings (.vs, .idea) are left alone.
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DRY_RUN=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    -n|--dry-run) DRY_RUN=1; shift ;;
    -h|--help)    sed -n '3,10p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

# Refuse to run anywhere that isn't this project, so a stray invocation from
# elsewhere can never delete someone's unrelated directories.
if [[ ! -f "$ROOT/Markdowner.sln" ]]; then
  echo "error: $ROOT does not look like the Markdowner repository" >&2
  exit 1
fi

LIST="$(mktemp)"
trap 'rm -f "$LIST"' EXIT

# -prune on a match stops find descending, so nested obj/ inside bin/ isn't listed twice.
find "$ROOT" \
  -name .git -prune -o \
  -type d \( -name bin -o -name obj -o -name TestResults \) -prune -print > "$LIST"

find "$ROOT" -name .git -prune -o -type f -name '*.user' -print >> "$LIST"

[[ -d "$ROOT/artifacts" ]] && echo "$ROOT/artifacts" >> "$LIST"

count=0
while IFS= read -r target; do
  [[ -n "$target" && -e "$target" ]] || continue

  if [[ "$DRY_RUN" -eq 1 ]]; then
    echo "  would remove ${target#"$ROOT"/}"
  else
    rm -rf "$target"
    echo "  removed ${target#"$ROOT"/}"
  fi

  count=$((count + 1))
done < "$LIST"

if [[ "$count" -eq 0 ]]; then
  echo "Already clean."
elif [[ "$DRY_RUN" -eq 1 ]]; then
  echo "$count item(s) would be removed. Re-run without --dry-run to delete them."
else
  echo "Clean: removed $count item(s)."
fi

#!/usr/bin/env bash
#
# analyze-incoming.sh — READ-ONLY analysis of incoming base-branch changes
# vs. the current branch. Prints a structured summary the skill classifies.
#
# It NEVER mutates the working tree or local branches. The ONLY ref-touching
# command is `git fetch` (updates remote-tracking refs like upstream/main);
# pass --no-fetch to skip even that and analyze against already-fetched refs.
#
# Usage:
#   analyze-incoming.sh [base-ref] [--no-fetch]
#   base-ref defaults to upstream/main (this repo is a fork; upstream = canonical).
#
set -uo pipefail

BASE="upstream/main"
DO_FETCH=1
for arg in "$@"; do
  case "$arg" in
    --no-fetch) DO_FETCH=0 ;;
    *) BASE="$arg" ;;
  esac
done

CUR=$(git branch --show-current 2>/dev/null)
if [ -z "$CUR" ]; then
  echo "ERROR: detached HEAD or not on a branch — checkout a branch first."
  exit 2
fi

REMOTE="${BASE%%/*}"      # e.g. upstream
REF="${BASE#*/}"          # e.g. main

echo "## current-branch: $CUR"
echo "## base: $BASE"

if [ "$DO_FETCH" -eq 1 ]; then
  # Non-destructive: updates only the remote-tracking ref, not local branches or worktree.
  if ! git fetch --quiet "$REMOTE" "$REF" 2>/dev/null; then
    git fetch --quiet "$REMOTE" 2>/dev/null \
      || echo "## warn: fetch failed (offline?) — analyzing against cached refs"
  fi
else
  echo "## fetch: skipped (--no-fetch)"
fi

if ! git rev-parse --verify --quiet "$BASE" >/dev/null; then
  echo "ERROR: base ref '$BASE' not found. Configured remotes:"
  git remote -v | awk '{print "  "$1"\t"$2}' | sort -u
  exit 2
fi

MB=$(git merge-base HEAD "$BASE" 2>/dev/null)
echo "## merge-base: ${MB:-<none>}"
if [ -z "$MB" ]; then
  echo "ERROR: no common ancestor between HEAD and $BASE."
  exit 2
fi

AHEAD=$(git rev-list --count "$MB..HEAD")
BEHIND=$(git rev-list --count "$MB..$BASE")
echo "## branch-ahead-by: $AHEAD   base-ahead-by: $BEHIND"

echo "### incoming-commits"   # commits on base that the branch does not have
git log --oneline --no-decorate "$MB..$BASE" | head -100
[ "$BEHIND" -eq 0 ] && echo "(none — base has no new commits since divergence)"

echo "### incoming-files"     # files the base changed since divergence
git diff --name-only "$MB..$BASE"

# Branch surface = committed diff since divergence PLUS uncommitted working-tree
# changes (staged, unstaged, untracked). Uncommitted matters here because work
# is often not committed yet when you check "is it safe to pull".
branch_files() {
  git diff --name-only "$MB..HEAD"                 # committed since divergence
  git diff --name-only HEAD                        # unstaged + staged vs HEAD
  git ls-files --others --exclude-standard         # untracked (new files)
}

echo "### branch-files"       # everything this branch touches (committed + uncommitted)
branch_files | sort -u

UNCOMMITTED=$(git status --porcelain 2>/dev/null | wc -l)
echo "## uncommitted-change-count: $UNCOMMITTED"

echo "### overlap-files"      # touched by BOTH sides — highest risk
comm -12 \
  <(git diff --name-only "$MB..$BASE" | sort -u) \
  <(branch_files | sort -u)
echo "(end overlap)"

echo "### textual-merge-preview"   # 3-way conflict preview WITHOUT merging
if MT_OUT=$(git merge-tree --write-tree --name-only "$BASE" HEAD 2>/dev/null); then
  echo "NO-TEXTUAL-CONFLICTS"
else
  RC=$?
  if [ "$RC" -ge 2 ]; then
    echo "(merge-tree unsupported or errored; rc=$RC — rely on overlap-files + manual review)"
  else
    echo "TEXTUAL-CONFLICTS (files that will not auto-merge):"
    # drop the first line (resulting tree OID); the rest are conflicted paths
    printf '%s\n' "$MT_OUT" | tail -n +2 | sed '/^$/d'
  fi
fi
echo "### end"

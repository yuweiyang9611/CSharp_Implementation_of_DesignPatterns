#!/bin/sh

set -eu

is_noreply_email() {
    case "$1" in
        *@users.noreply.github.com|noreply@github.com)
            return 0
            ;;
        *)
            return 1
            ;;
    esac
}

if [ "$#" -eq 0 ]; then
    set -- --all
fi

commits=$(git rev-list "$@")
violations=0
commit_count=0

for commit in $commits; do
    commit_count=$((commit_count + 1))
    author_email=$(git show -s --format=%ae "$commit")
    committer_email=$(git show -s --format=%ce "$commit")

    if ! is_noreply_email "$author_email"; then
        printf 'ERROR: commit %s has a non-noreply author email (value redacted).\n' "$commit" >&2
        violations=1
    fi

    if ! is_noreply_email "$committer_email"; then
        printf 'ERROR: commit %s has a non-noreply committer email (value redacted).\n' "$commit" >&2
        violations=1
    fi
done

if [ "$violations" -ne 0 ]; then
    printf '%s\n' 'Use a GitHub noreply address, rewrite the affected commit, and retry.' >&2
    exit 1
fi

printf 'Verified noreply author and committer emails in %s commit(s).\n' "$commit_count"

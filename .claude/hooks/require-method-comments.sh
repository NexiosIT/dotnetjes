#!/usr/bin/env bash
# PreToolUse hook (Bash|PowerShell): blocks Claude's `git commit` until the remove-method-comments skill
# has signed off on exactly the pending .cs/.razor changes.
#
#   require-method-comments.sh          hook mode: reads the tool call JSON on stdin
#   require-method-comments.sh --mark   sign-off: the skill runs this after its checks pass
#
# The sign-off is a fingerprint of the changes in .claude/method-comments.ok (gitignored);
# any later change to a .cs/.razor file invalidates it.
set -euo pipefail

root="${CLAUDE_PROJECT_DIR:-$(git rev-parse --show-toplevel)}"
cd "$root"
marker=".claude/method-comments.ok"
paths=('*.cs' '*.razor' ':(exclude)**/Migrations/**' ':(exclude)*.Designer.cs' ':(exclude)*.g.cs')

fingerprint() {
    {
        git diff HEAD -- "${paths[@]}"
        git ls-files --others --exclude-standard -z -- "${paths[@]}" | xargs -0 -r cat
    } | git hash-object --stdin
}

empty=$(printf '' | git hash-object --stdin)

if [[ "${1:-}" == "--mark" ]]; then
    fingerprint > "$marker"
    echo "remove-method-comments: sign-off recorded for the current changes."
    exit 0
fi

input=$(cat)
if ! grep -qE 'git( +-C +[^ ]+)? +commit' <<<"$input"; then
    exit 0
fi

current=$(fingerprint)
if [[ "$current" == "$empty" ]] || { [[ -f "$marker" ]] && [[ "$(cat "$marker")" == "$current" ]]; }; then
    exit 0
fi

cat <<'JSON'
{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"Commit blocked: run the remove-method-comments skill on the changed .cs/.razor files first (its last step records the sign-off with `bash .claude/hooks/require-method-comments.sh --mark`), then commit again. Any code change after the sign-off needs a new run."}}
JSON

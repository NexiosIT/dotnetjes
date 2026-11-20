---
name: remove-method-comments
description: Use when asked to remove, strip or clean up comments inside methods (method bodies, code blocks) in this repo's C# (.cs) or Razor (.razor) files, for the whole repo or for given files or folders.
---

# Remove comments inside methods

Strip every comment that sits inside executable code in this repo's C# and Razor files, and leave every other comment alone. The scope below is fixed: apply it as written, don't re-decide it per file.

## Which files

- **Target:** the paths the user named; with none, all `*.cs` and `*.razor` under `src/` and `tests/`.
- **Never touch:** anything under `Migrations/`, `*.Designer.cs`, `*.g.cs`, `obj/`, `bin/`, `wwwroot/lib/` (generated or third-party).

## What counts as "inside a method"

| Remove comments inside… | Keep comments on/at… |
|---|---|
| method, constructor, finalizer and operator bodies, including expression-bodied ones (`=> …`, also when the comment sits right after `=>`) | `///` XML doc comments |
| property, indexer and event accessors, incl. expression-bodied ones | comments above or after fields, constants, properties, classes, namespaces, `using`s |
| local functions | comments between members, also inside a Razor `@code { }` block (above a method, field, const or property) |
| lambda and anonymous-method bodies, wherever they are (also in field initializers and `RenderFragment` properties) | comments on attributes, e.g. trailing `// …` after `[InlineData(...)]` |
| methods in Razor `@code { }` blocks | Razor markup comments `@* … *@` and HTML `<!-- … -->` outside `@code` |
| top-level statements in `Program.cs` (they are the generated `Main`) | file-header comments |

Inside those bodies, remove all three forms: whole-line `//` comments, trailing `// …` after code, and `/* … */` blocks (single- or multi-line, also mid-expression: `a * 2 /* x */ + 1` → `a * 2 + 1`). A `@* … *@` inside a Razor `@code` method also goes.

## Not comments: never change

- `//` or `/*` inside string literals, verbatim (`@"…"`), raw (`"""…"""`) or interpolated strings, and char literals: `"https://x.org"`, `"// not a comment"`.
- Preprocessor directives: `#pragma`, `#region`/`#endregion`, `#if`/`#else`/`#endif`, `#nullable`.

## How

1. List the target files and read each one before editing it.
2. Remove a comment completely: every line of a multi-line `//` run and the whole of a `/* … */` block. For a trailing comment, also remove the whitespace before it; the code on that line stays byte-for-byte the same.
3. Clean up whitespace left behind: no two blank lines in a row, no blank line directly after `{` or directly before `}`.
4. When a block is left with only a comment (`catch (…) { // reason }`), remove the comment and leave the empty block. Don't add or change code. List each such block in the report.

## Check before reporting

1. Search the edited files again for `//` and `/*` inside method bodies, and read every remaining hit: it must be in the "keep" column, a string or a directive.
2. Read the full `git diff`. Only `-` lines with comments and whitespace changes may appear; any change to code is a mistake, so undo it.
3. Run `dotnet build`: 0 errors, and no warnings that weren't there before.
4. Run `dotnet test` (Core tests need Docker). All must pass.
5. Only when 1–4 passed: `bash .claude/hooks/require-method-comments.sh --mark`. This records the sign-off that the commit hook checks; a commit with changed `.cs`/`.razor` files is blocked without it, and any later code change needs a new run.

## Report

- Files changed, and how many comments were removed.
- Empty blocks left behind (file:line).
- Anything in the target you were unsure about, with what you did.
- What the checks showed. Don't commit unless the user asks.

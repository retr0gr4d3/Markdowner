# Markdowner

A cross-platform Markdown editor with a live 50/50 preview that renders the way
**GitHub** or **Discord** actually would — not the way a generic Markdown viewer
would. Built with [Avalonia](https://avaloniaui.net), so it runs natively on
macOS, Windows and Linux from one codebase.

The two platforms disagree in ways that matter. `__text__` is bold on GitHub and
an underline on Discord. Discord has spoilers, subtext and localised timestamps;
GitHub has tables, footnotes, alerts and an allow-listed subset of raw HTML. A
single renderer would quietly lie about one of them, so Markdowner ships two.

## What it does

- **Split view.** Source on the left, live preview on the right. The panes
  scroll independently.
- **Two renderers, swapped on the fly.** One document, interpreted either way —
  changing the renderer never rewrites your text.
- **A flavor-aware insert bar.** Grouped quick-insert buttons in the spirit of
  Dreamweaver's insert panel. Only syntax the selected renderer understands is
  offered, so the GitHub palette includes the full allow-listed HTML tag set and
  the Discord palette does not.
- **Formatting Help (F1).** A reference window titled for whichever preview is
  open, listing every supported construct with its syntax beside a *live*
  example — each one rendered by the same engine as the preview.
- **Honest previews.** Tags GitHub's sanitizer strips are reported as removed
  rather than drawn; Mermaid blocks are labelled rather than faked.
- **Editor niceties.** Markdown syntax highlighting that changes with the
  flavor, line numbers, word wrap, word/character counts, and a live
  2000-character budget when Discord is selected.

## Running it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/Markdowner        # macOS, Windows, Linux
```

## Building

| Task | macOS / Linux | Windows |
| :--- | :--- | :--- |
| Build and test | `./build.sh` | `.\build.ps1` |
| Package a release | `./build.sh --publish` | `.\build.ps1 -Publish` |
| Reset the repo | `./clean.sh` | `.\clean.ps1` |

Both build scripts restore, build in Release and run the tests. `--publish`
(`-Publish`) additionally produces a **self-contained, single-file** app —
no .NET runtime needed on the target machine — and packages it as:

```
artifacts/Markdowner-<version>-<runtime>.zip
```

The staging tree is deleted once the archive is written, so `artifacts/` only
ever holds finished packages. Use `--runtime` / `-Runtime` to cross-target
another RID, and `--version` / `-Version` to override the version taken from
`Directory.Build.props`.

```bash
./build.sh --publish --runtime linux-x64
./build.sh --publish --runtime win-x64 --no-test
```

### Cleaning

`clean.sh` / `clean.ps1` restore the repository to a just-cloned state by
removing every `bin/`, `obj/`, `TestResults/`, `artifacts/` and `*.user`. Source,
`.git` and IDE settings (`.vs`, `.idea`) are left alone, and the script refuses
to run outside the repository. Pass `--dry-run` (`-DryRun`) to see what would go
without deleting anything.

## Tests

```bash
dotnet test
```

The suite covers the hand-written Discord parser, the HTML subset parser and
GitHub's sanitizer allow-list, and asserts on the control trees the renderers
build — the renderers only construct visuals, so they can be tested without
standing up a window.

## How it fits together

```
src/Markdowner/
  Discord/        Hand-written parser for Discord's dialect (AST + block/inline passes)
  Html/           Tolerant HTML parser and GitHub's sanitizer allow-list
  Rendering/      Markdig-backed GitHub renderer, Discord renderer, shared visual factories
  Editing/        Insert-bar snippet application and the .xshd highlighting definitions
  Models/         Flavors, the insert palettes, and the formatting reference data
  Views/          Main window and the Formatting Help window
```

GitHub parsing delegates to [Markdig](https://github.com/xoofx/markdig) with an
extension set chosen to match what github.com enables. Discord gets its own
parser because it is not CommonMark: single newlines are hard breaks, `>>>`
quotes the rest of the message, and underscores mean something different.

Both renderers emit native Avalonia controls rather than HTML — links, spoilers
and inline code are ordinary text runs with recorded character ranges, so text
wraps and stays selectable while clicks are still resolved by hit-testing the
laid-out text.

---
name: agents-md
description: Regenerate the root AGENTS.md and README.md from docs/index.html. Both are generated, never hand-edited; run this whenever index.html changes so neither front door can drift from the living document.
tools: Read, Grep, Glob, Write
model: inherit
---

You regenerate two repo-root front doors from the living document `docs/index.html`: `AGENTS.md` (the agent front door) and `README.md` (the GitHub front door). Both are generated, never hand-edited; `docs/index.html` is the source of truth, and you derive them from it, never the reverse. Read `docs/index.html` once, then overwrite both files in the same pass.

## README.md

A tiny GitHub landing page: a single `#` heading (the masthead `h1`), then at most three sentences on what the project is and who it serves, the last sentence linking `docs/index.html`. Then a final line: `Agents: see [AGENTS.md](AGENTS.md).` No build or install steps; those live in the document.

## AGENTS.md

`AGENTS.md` is lean markdown an agent harness auto-discovers, with no HTML token cost. Overwrite it so it carries:

- The project title (the masthead `h1`) and the one-line purpose (the masthead lede).
- A short "generated, do not hand-edit" note pointing back at `docs/index.html`.
- `## Agent brief`: the `#brief` block rendered as plain markdown (drop the HTML chrome; turn `<code>` into backticks, `<strong>` into bold, and the inline ADR references into markdown links to `docs/adr/*.md`).
- `## Decisions`: one markdown link per ADR file under `docs/adr/`, in number order, each with its title and status. Preserve any supersede relationships.
- `## Related docs`: a link to every other markdown file under `docs/` (one line each). Omit the whole section if `docs/adr/` is the only other markdown. Never list `.claude/` build/install SKILL.md files.
- A final line: `Full human document: docs/index.html`.

No em dashes. Keep it lean. Regenerate in the same pass whenever `docs/index.html` changes: a drift between the two means the job is not done.

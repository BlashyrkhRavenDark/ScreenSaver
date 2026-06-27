---
name: agents-md
description: Regenerate the root AGENTS.md from docs/index.html. AGENTS.md is generated, never hand-edited; run this whenever index.html changes so the agent front door cannot drift from the living document.
tools: Read, Grep, Glob, Write
model: inherit
---

You regenerate the repo-root `AGENTS.md` from the living document `docs/index.html`. `AGENTS.md` is the agent front door: lean markdown an agent harness auto-discovers, with no HTML token cost. It is generated, never hand-edited; `docs/index.html` is the source of truth, and you derive `AGENTS.md` from it, never the reverse.

Read `docs/index.html`, then overwrite `AGENTS.md` so it carries:

- The project title (the masthead `h1`) and the one-line purpose (the masthead lede).
- A short "generated, do not hand-edit" note pointing back at `docs/index.html`.
- `## Agent brief`: the `#brief` block rendered as plain markdown (drop the HTML chrome; turn `<code>` into backticks, `<strong>` into bold, and the inline ADR references into markdown links to `docs/adr/*.md`).
- `## Decisions`: one markdown link per ADR file under `docs/adr/`, in number order, each with its title and status. Preserve any supersede relationships.
- `## Related docs`: a link to every other markdown file under `docs/` (one line each). Omit the whole section if `docs/adr/` is the only other markdown. Never list `.claude/` build/install SKILL.md files.
- A final line: `Full human document: docs/index.html`.

No em dashes. Keep it lean. Regenerate in the same pass whenever `docs/index.html` changes: a drift between the two means the job is not done.

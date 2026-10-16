# SpaceScape

## Networking

**Provisionally done (2026-09-28), untested with a second player.** Components, conduits, addons and
switches, hologram occupancy and conduit edits are networked; only the master rolls failures.
`docs/networking.md` has the rules (who owns what, state over messages), what was built step by step,
and what is left (grabbables, the interaction rework). Read it before touching anything that holds
state.

**Networking must ALWAYS be considered and discussed before adding a new feature to the world.**
Before building anything, settle with Vulps who owns its state, how that state reaches other
players and late joiners, and what a non-master's hand does to it (`docs/networking.md` → Rules).
A feature that works for one player and has not had that conversation is not ready to build.

## Where things are

- `docs/README.md`: the project, and every doc grouped by kind. `docs/principles.md`: the design rules. Read first.
- **Planning docs are thin:** `docs/todolist.md` (one line per item, pointing to its doc; removed when done),
  `docs/polish.md` (what the many-player test turned up), and the arcs `docs/the-loop.md` and `docs/flight.md`.
- **One topic, one doc, and text is moved, never copied.** Everything about a thing, including what was tried and
  failed, lives in its own doc; anywhere else that needs it links there in a sentence at most. A rough idea in a
  planning doc moves out to its doc once refined. Links point from the specific to the general: a component or
  conduit doc links to `grid` and `slots`, never the reverse, and a component's doc says which sizes it comes in.
  - Components: `battery`, `reactor`, `jump-drive`, `radar`, `weapons`, `loader`.
  - Conduits: `conduits` (the shared core), `item-conduit`, `addon`. Items: `fuel-rod`, `packs`, `addon`.
  - Structure: `grid`, `slots`, `ship-structure`. Systems: `power`, `data`, `damage`.
  - Mechanics: `loading`, `repair`, `navigation`. Interaction: `interaction`, `2d`, `component-hologram`, `training-room`.
  - Game: `galaxy`. Easter eggs: `felix`. Tech and art: `modules`, `networking`, `tooling`, `component-models`.
    Reference: `assets`, `credits`.
- **A doc holds today's state and the refined plan,** each section or size with its own status line under a short,
  stable heading (`**Status (date):** proposed / refined / built, untested in-world / tested`). Sizes are titled
  `S1: Name` when they have a name (`battery.md` is the model). Each thing's networking is under its own
  **Networking** heading; `networking.md` holds only the shared rules and plumbing.
- **`docs/` is an Obsidian vault.** Link docs as `[[conduits]]`, a section as `[[networking#Rules|networking → Rules]]`
  (Obsidian drops `:` from heading anchors), a bullet by a block id (`^no-commands`). **Inside a table, escape the
  alias pipe** (`[[slots#S1\|S1]]`), or the table splits there and Obsidian's editor reshuffles the columns. Unsettled points are callouts:
  `> [!question] Open`, `> [!info] Proposed`, `> [!warning] Pinned / Untested`, `> [!info] Networking`.
  A new doc gets frontmatter `aliases` (its title) and `tags`: every kind it defines behaviour for, from `component`,
  `conduit`, `item`, `structure`, `system`, `mechanic`, `interaction`, `game`, `easter-egg`, `tech`, `art`, `rules`,
  `arc`, `tracking`, `reference`, `index`. Components get `sizes` (`S0` to `S5`, every size the doc covers at any
  stage). Status is never doc-wide: each section or size carries its own. `Docs.base` has a view per kind. To-do items are `- [ ]`.
- Scene: `Assets/#User/SpaceScape/SpaceScape.unity`. Code: `Assets/#User/SpaceScape/scripts/`.
  Editor-only tools: `Packages/com.spacescape.editor` (never uploaded).

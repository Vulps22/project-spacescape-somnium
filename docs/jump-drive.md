---
aliases:
  - "Jump drive"
tags:
  - component
sizes:
  - S1
  - S2
  - S3
  - S4
  - S5
---
# Jump drive

What moves the ship between systems: what a jump costs, how far each size of drive goes, and the rules it already
follows. Where jumps sit in the game is [[the-loop]]; the galaxy is [[galaxy]]; finding the way is [[navigation]].

**Status (2026-10-11):** nothing built. The fuel rules and the stance on power are decided; sizes and ranges are
being set.

## Rules

**Status (2026-10-11):** decided.

- **One fuel cell per jump,** simply consumed, whatever the jump. **Zero impact on the reactor:** fuel and power
  are fully decoupled. The fiction is deliberately unexplained and will not be mentioned in-world: it is a jump fuel
  cell. Why: [[#Fuel]]. What players see is [[#The jump (Vulps, 2026-10-11)|The jump]].
- **The drive has a power rating, and is an accumulator** like any other module: it charges from the grid before a
  jump. Mismatch it against the reactor and it charges slowly; the consequences are the players', and the design
  has no opinion ([[principles]] §4).
- **No rule forces the ship powerless** (refined with Vulps, 2026-10-11). The drive has no special demand that
  strips power off everything else to make the crew blind and defenceless: the shields-down-to-jump trope, an
  authored beat, is rejected ([[#Tried and rejected]]); do not re-propose.
- **But losing power to it can happen, and should, when the physics says so.** The drive is an ordinary consumer
  on the grid. One bigger than the reactor, wired where it takes most of the reactor's output, leaves the rest
  starved; one charging off small batteries with no reactor running drains them flat. Nobody authored either: it is
  the power model applied normally, and the mismatch is the players' to fix ([[principles]] §4).
- **Jump range is a stat,** and the navigator judges reachability **by eye** off the scanner
  ([[navigation]]). **A failed jump is the navigator's misjudgement, not bad luck.**
- **A worn drive misfires** like any component below 20% condition: it spends its charge and does nothing
  ([[power#Failure|power → Failure]]). "Bad luck on a jump drive already at 15%" is where losing a run comes from.
- **Fuel cells' model:** the Energy Cells (Free) pack ([[assets]]), not imported yet.

## Fuel

**Status (2026-09-17):** decided.

**The job of the fuel cell is range anxiety.** *"We had to divert to a station with no fuel to get
repairs — will we make it to a refuelling station?"* It is a tension that accumulates across a route,
not a decision made at a wall.

Which means fuel needs no tension source of its own. You end up short because damage forced an
unplanned stop, which came from the heat, which came from the routing. Same causal chain as
everything else, one more link on the end.

For it to work at all, the crew must be able to **do the arithmetic** — knowing you have four jumps
and the nearest fuel is five out. Anxiety requires a knowable map plus an unplanned route; without
that it is a fail state that arrives without warning.


> [!info] Proposed (loosely held)
> Storage is lightly physicalised: a box of cells holds ~10 jumps and takes up ~a tenth of the cargo hold. This is a
> detail, not a mechanic.

## The jump (Vulps, 2026-10-11)

**Status (2026-10-11):** proposed; for when the system is built. The neutrino rule below is decided.

**The fuel cell is fired out of the front of the ship, and forms a hyperspace window that pulls the ship in.**

- **The lore, never explained in-world:** the drive pumps its charge into the cell, which powers a reality-altering
  reaction; punching further takes a bigger reaction, so more power. It gives the numbers a reason: one cell seeds
  one window, so **one cell per jump whatever the distance**; the distance is paid in **charge**; and a bigger drive
  puts its energy into the cell with less waste, so **size buys efficiency** ([[#Sizes]]).
- **Shown, not explained:** everyone sees the cell fired and the window open ahead of the ship. That keeps the
  [[#Rules|Rules]]' "deliberately unexplained" stance: nothing in-world says what the reaction is.

> [!info] Networking
> To settle before building ([[networking#Rules|networking → Rules]]): the jump is one event, and every player and late
> joiner sees the same cell fired and the same window.

### Neutrinos negate the reaction (Vulps, 2026-10-11)

**Status (2026-10-11):** decided.

- **The [[reactor#S4 The Bottle|S4 antimatter reactor]]'s neutrinos negate the jump fuel cell's reaction:** no
  window forms. Neutrinos come only from annihilation, so this holds while the reactor runs.
- **They do not stop the drive dumping power into the cell.**
- **Never told to players:** the cell is then **a natural sink for excess power that would otherwise become heat.**
  Not in any in-world text or hint; it is for crews to find.

## Sizes

**Status (2026-10-11):** S1, S2, S3 and S5 proposed (worked through with Vulps); S4 open.

Following [[slots#Rules for every size|slots → Rules for every size]]: one scale of energy, and **size buys capability
opand efficiency.**

- **A small drive** is cheap to charge but makes short hops, so getting anywhere takes more jumps and **burns through
  fuel cells**. **A big drive** is expensive to charge, jumps far, and spends **fewer cells per distance**.
- **Smaller drives are less efficient for their size.** The balancing check, in batteries of the drive's own size
  ([[battery#Capacities (Vulps and Claude, 2026-10-10)|battery → Capacities]]).

**Range goes ×10 per size; efficiency improves on a curve:** a small gain at S2, a decent one at S3, a chunky one
at S5. Charge per jump = energy per light-year × range.

| Size | Range per jump | Energy per ly | Gain on the size below | Charge per jump | Same-size batteries |
|---|---|---|---|---|---|
| [[slots#S1\|S1]] | **5 ly** (just past Sol to Alpha Centauri, 4.4 ly) | 4 MJ | | **20 MJ** | 20 × 1 MJ |
| [[slots#S2\|S2]] | **50 ly** | 2.2 MJ | ×1.8, small | **110 MJ** | 19 × 6 MJ (114 MJ) |
| [[slots#S3\|S3]] | **500 ly** | 1 MJ | ×2.2, decent | **500 MJ** | 10 × 50 MJ |
| [[slots#S5\|S5]] | **5,000 ly** (about 5% of the Milky Way's diameter) | 0.2 MJ | ×5, chunky | **1 GJ** | 2 × 500 MJ |
| [[slots#S4\|S4]] | | | | | relic |

- **The battery check falls 20 → 19 → 10 → 2.** S2 is better for its size than S1, but only just: one battery fewer.
  Above 2.4 MJ/ly it would be worse than S1, breaking the rule; that is why S2 is 2.2.
- **A jump's two costs scale differently.** Fuel is one cell per jump, so **fuel per distance falls ×10 per size**
  (500 ly is 100 cells on an S1, 10 on an S2, 1 on an S3). Charge is the curve above. So a size up is a big win for
  range anxiety ([[#Fuel]]) and a modest one for power: the find feels exciting, and the power bill is what makes it
  a decision rather than a free win.
- **Bigger is better, but not a strict progression.** Bigger drives are likelier deeper in, where the risk is
  higher, but nothing stops an S5 dropping in the second system, like legendary kit in a roguelike. The physics gates
  it softly instead: 1 GJ a jump, which an early ship may not have the reactor or batteries to charge quickly, and an
  S5 slot the reactor also wants (there are three, [[slots#S5|slots → S5]]).

> [!question] Open
> - **Station spacing by depth** ([[galaxy#Difficulty is depth|galaxy → Difficulty is depth]]): these ranges only
>   mean something against how far apart stations are. S1 needs stations a few hops apart where a run starts;
>   deeper, spacing should outgrow the small drives.
> - **Charge rate:** the drive's power rating per size, which with the reactor's sets how long a charge takes. Today's
>   reactor gives 1 kW ([[power]]), which charges even an S1 in about 5.5 hours: rates need rescaling with the
>   batteries' ([[battery#Capacities (Vulps and Claude, 2026-10-10)|battery → Capacities]]).
> - **The [[slots#S4|S4]] drive:** a relic, like every S4 part ([[slots#S4|slots → S4]]).
> - **With nav dead, can the ship still jump?** ([[navigation]])
> - **A jump beyond range:** what happens when the navigator misjudges (no jump, a short jump, a stranding).
> - **Its model,** in the house look ([[component-models]]).

## Tried and rejected

- **The jump drive draining the ship** (do not re-propose). Charging a jump drive demanding more than the reactor can
  comfortably give, forcing the crew to strip power off everything else and be briefly blind and defenceless. This
  is the shields-down-to-jump trope: an authored dramatic beat masquerading as a system, and one no engineer in the
  fiction would accept ([[principles]] §4). What is rejected is the authored rule; a drive that starves the ship
  because the power model says so stays (Rules, above).
- **Fuel cells as cargo-hold contention** (do not re-propose). A deep contract needs N cells, the cells eat a third
  of the hold, and the contract wants that third: "reach is bought with capacity". Not the job of the fuel cell; the
  job is range anxiety across an unplanned route.

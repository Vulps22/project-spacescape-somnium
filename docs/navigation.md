---
aliases:
  - "Navigation"
tags:
  - mechanic
---
# Navigation

How the crew find their way across [[galaxy|the galaxy]]: no route planner, a scanner that shows confidence, and a
human judging range by eye. The drive that makes the jump is [[jump-drive]]; where it sits in the game is
[[the-loop]].

**Status (2026-09-17):** decided, except where marked proposed or open; nothing built.

## No route planning

**Decided.** There is **categorically no route planning system.** A human navigator plans as the ship
goes, making course adjustments off the scanner's probabilities.

- System positions are known, but **which jumps you can actually make is only visible from the system
  you are in.**
- **Jump range is a stat.** The mapper judges reachability **by eye.**

Judging by eye is the first job in the design that **cannot be automated.** A breaker can replace an
engineer's reflexes; nothing replaces *"I think we can make that."* The mapper can be wrong, and being
wrong strands the ship. A real human role with no ceiling on it, produced by a UI decision rather than
a rule.

## The nav scanner: JPR (jump prediction range)

**Decided.** A scanner stat. It is a **confidence** display, not a fuel indicator.

```
JPR 1   green lines to every jump destination in range of the current system
JPR 2   green nearby, orange on the second jump out
JPR 3   red on the third jump out
JPR 10  green fading through orange to red, out to ten jumps
```

Every potential route might be slightly too far or clear as day; the further out you plan, the less
confident the scanner is.

**This is the general solution to the automation ceiling** ([[principles]] §6). A higher JPR does not
plan your route — it hands you *worse* data further out. The uncertainty is preserved by the gradient,
so there is always a frontier where judgment is required. The job moves outward instead of
disappearing.

**Caution on the palette:** red reads as *no*, not as *unsure*. Players will treat a red line as a
wall rather than a coin flip. Confidence probably wants a different visual channel — a line going
fuzzy or dashed as certainty drops — with colour reserved for things that genuinely are binary
(open, below).

## The truth is always there; the scanner just cannot see it

**Decided.** The galaxy is procgen and the distances are already decided. Uncertainty is
**epistemic** — the scanner's ignorance — never the universe deciding at the moment of commitment.
A failed jump is the navigator's misjudgement, not bad luck.

**Proposed** mechanism for obscuring it:

```
displayed(edge) = true(edge) + true(edge) * pct * falloff(hops_away)
```

Three properties, all wanted:

- **Error scales with the edge's own length.** ±10 % on a short hop is nothing; on a long hop it is
  the difference between arriving and not. So uncertainty lands exactly where the decision is — long
  hops near the range limit — and short hops read true. This is the "number of jumps relative to the
  distance between each jump" requirement falling out for free.
- **The error is derived from the seed, not rolled.** `hash(seed, edgeId)` gives each edge a permanent
  lie, so re-scanning returns the same number and nobody defeats it by querying twice and averaging.
- **`falloff` reaches zero at one hop out.** A marginal route **firms up as you approach** — the fuzzy
  line gambled on at six jumps resolves into a solid yes or no by the time the ship is adjacent. The
  navigator watches the plan get confirmed or fall apart in stages, which is better than a single
  reveal.

## The nav system has no memory

**Decided.** No caching. If it is powered, it scans on arrival — and that is the only time it knows
anything. Flying an edge does not make it exact forever after.

So nav is **just another consumer on the power graph, and the power system can blind you.** The trap
writes itself: strip nav's branch to charge the drive, jump, arrive, and nobody restored it — now the
ship sits in an unknown system with no idea which hops are in range. Entirely self-inflicted,
entirely diagnosable, and the first place the macro loop and the power sim collide without anyone
authoring it.

It also means **the route lives in a player's actual memory** rather than in a UI. People will be
saying it out loud and writing it down.


> [!question] Open
> - **With nav dead, can the ship still jump** (point the drive and hope), or is it stuck until someone gets power
>   back to it? Decides whether an unpowered nav system is inconvenient or fatal.
> - **What visual channel carries scanner confidence?** Red reads as *no*, not *unsure*. Fuzzy or dashed lines are
>   the obvious alternative, keeping colour for genuinely binary things.

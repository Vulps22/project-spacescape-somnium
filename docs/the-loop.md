# The galaxy, the loop, and navigation

What the crew actually does. The ship is the content; this layer exists to point them somewhere and
put a clock on them.

## The loop

**Decided.** The crew are basically couriers, crossing a largely empty galaxy station to station.

1. Arrive at a station.
2. **Take a contract off the wall.** Physically. Walking out of the room with it *is* accepting it —
   no confirm dialog. Same design grammar as switch placement and cable routing: the physical act is
   the commitment.
3. Load the cargo hold.
4. Jump to the destination, repairing and refuelling as opportunity allows.
5. Deliver.

Scanning for data is optional along the way. Hostiles are picked off as encountered rather than
sought.

**The contract is a pretext and that is fine.** Euro Truck is a courier game and nobody plays it for
the cargo. What justifies the thinness is principle 3 in `principles.md`: the crises are the ship's
own entropy, not events rolled against the player. The emptiness of the galaxy is what gives that
room to breathe.

Things that go wrong, all of them traceable to a decision somebody made:

- run out of jump fuel
- meet a destroyer
- run out of coolant
- overheat a conduit, pop it, and lose life support

## The galaxy

**Decided.**

- **Procedurally generated**, so every distance is already decided before anyone looks at it.
- **Seeded from the timestamp of midnight on the current day.** The seed is set when the first player
  joins and replicated to every subsequent player on join. The galaxy therefore reseeds daily.
- **System locations are known from the start.** The map is not fogged.
- **One ship, one galaxy.** No instancing, no per-crew vessels, no server-synchronised parallel
  worlds — there are not enough players in Somnium for that to be worth the architecture.

## Difficulty is depth

**Decided.** Difficulty is distance from the galactic centre:

- the deeper in, the more jumps between stations
- the closer to the centre, the harder the enemies

Missions are **finite**, and a **hidden completion level** decides how close to the centre newly
spawned contracts will send the crew.

Why depth is the right axis for this game specifically: more jumps between stations is not "harder
enemies", it is **longer since you could stop and fix anything.** The difficulty curve is accumulated
entropy without a maintenance window, which is exactly the curve a machine-entropy game wants, and it
falls straight out of the map.

**Open:** does the hidden completion level reseed with the galaxy at midnight? See
`open-questions.md`.

## Jump fuel

**Decided.**

- The jump drive consumes **one fuel cell per jump**. The cell is simply consumed.
- **Zero impact on the reactor.** Fuel and power are fully decoupled systems that do not contaminate
  each other.
- The drive has a **power rating** like any other module, and is an accumulator like any other
  module. Mismatch it against your reactor and it charges slowly; see `power.md` → Modules.
- The fiction is deliberately unexplained and will not be mentioned in-world. It is a jump fuel cell.

**The job of the fuel cell is range anxiety.** *"We had to divert to a station with no fuel to get
repairs — will we make it to a refuelling station?"* It is a tension that accumulates across a route,
not a decision made at a wall.

Which means fuel needs no tension source of its own. You end up short because damage forced an
unplanned stop, which came from the heat, which came from the routing. Same causal chain as
everything else, one more link on the end.

For it to work at all, the crew must be able to **do the arithmetic** — knowing you have four jumps
and the nearest fuel is five out. Anxiety requires a knowable map plus an unplanned route; without
that it is a fail state that arrives without warning.

**Proposed** (loosely held): storage is lightly physicalised — a box of cells holds ~10 jumps and
takes up ~a tenth of the cargo hold. This is a detail, not a mechanic. An earlier framing that made
fuel-versus-cargo hold contention *the point* was **rejected** — see `open-questions.md`.

## Navigation

**Decided.** There is **categorically no route planning system.** A human navigator plans as the ship
goes, making course adjustments off the scanner's probabilities.

- System positions are known, but **which jumps you can actually make is only visible from the system
  you are in.**
- **Jump range is a stat.** The mapper judges reachability **by eye.**

Judging by eye is the first job in the design that **cannot be automated.** A breaker can replace an
engineer's reflexes; nothing replaces *"I think we can make that."* The mapper can be wrong, and being
wrong strands the ship. A real human role with no ceiling on it, produced by a UI decision rather than
a rule.

### The nav scanner: JPR (jump prediction range)

**Decided.** A scanner stat. It is a **confidence** display, not a fuel indicator.

```
JPR 1   green lines to every jump destination in range of the current system
JPR 2   green nearby, orange on the second jump out
JPR 3   red on the third jump out
JPR 10  green fading through orange to red, out to ten jumps
```

Every potential route might be slightly too far or clear as day; the further out you plan, the less
confident the scanner is.

**This is the general solution to the automation ceiling** (`principles.md` §6). A higher JPR does not
plan your route — it hands you *worse* data further out. The uncertainty is preserved by the gradient,
so there is always a frontier where judgment is required. The job moves outward instead of
disappearing.

**Caution on the palette:** red reads as *no*, not as *unsure*. Players will treat a red line as a
wall rather than a coin flip. Confidence probably wants a different visual channel — a line going
fuzzy or dashed as certainty drops — with colour reserved for things that genuinely are binary.
**Open.**

### The truth is always there; the scanner just cannot see it

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

### The nav system has no memory

**Decided.** No caching. If it is powered, it scans on arrival — and that is the only time it knows
anything. Flying an edge does not make it exact forever after.

So nav is **just another consumer on the power graph, and the power system can blind you.** The trap
writes itself: strip nav's branch to charge the drive, jump, arrive, and nobody restored it — now the
ship sits in an unknown system with no idea which hops are in range. Entirely self-inflicted,
entirely diagnosable, and the first place the macro loop and the power sim collide without anyone
authoring it.

It also means **the route lives in a player's actual memory** rather than in a UI. People will be
saying it out loud and writing it down.

**Open:** with nav dead, can the ship still jump — point the drive and hope — or is it stuck until
someone gets power back to it?

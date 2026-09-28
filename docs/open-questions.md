# Open questions, concerns, and rejected ideas

Brain dump. Nothing in the "Open questions" section is decided.

## Answered since this was written

- **Cycles.** A loop through a component is fine, because components never forward. A cycle of
  conduits only is a ring, and a ring blows its merge point the tick power reaches it. **Built**
  (2026-09-26, `modules.md`).

- **Does SpaceScape belong in Project Garden's repository?** No. It moved to its own project,
  `~/Documents/project-spacescape-somnium`, on 2026-09-26.

- **Can a source be poppable / does every fault kill the reactor?** Resolved by **durability**: a
  conduit severs, a component takes damage instead and is only lost after four hits. And by the
  **baseline**: damage measures what a tile is minus what it *should* be, so a reactor at its working
  temperature is never damaged for running. **Built.**
- **How do multi-cell components work?** `GridNode` carries a `Size`, registers every cell it
  occupies, and a face is a whole flank so a run can meet a big machine anywhere along its side.
  Overlaps warn. **Built.**
- **Does the reactor need the grid to hold its rods?** Only to *start*. House load comes off the top
  inside the housing, so a running core holds its own magnets. **Built** — and it traded away the
  blackout scenario, see `reactor.md`.
- **Does control power leak into the ship?** It did. No component forwards power any more, which
  stops it, and the same rule stops a cold-start loop
  compounding on every lap. **Built.**

- **Can a damaged module draw more than its rating?** Not by over-drawing — components are endpoints,
  so there is no downstream to starve. A fault raises the **conduit's** `Share` instead, so a shorted
  branch takes three cables' worth where one is fitted and its sibling browns out. The cable count
  and the grid then disagree, which is invisible by eye and is the reason to own a meter. **Built.**
- **How does heat leave the ship?** A healthy system simply loses it: a tile that is not wasting
  sheds toward ambient. No radiation model, no air, no life support. Coolant stays a later thing that
  raises the ceiling. **Built.**
- **Splitter tiers: capacity or ratio?** Neither — a splitter is a module that opens a second face on
  a tile, and the existing share rule does the rest. Ratios come from cable count. **Built.**

## Open questions

Roughly in order of how much depends on the answer.


1. **The fail-safe now fires almost never.** Self-sustaining means no external fault can drop the
   rods. They only fall on a manual SCRAM, out of fuel, throttled below minimum stable output, or
   destroyed. It needs both a way to *fire* and a way to *fail* — see `reactor.md`.

2. **What is the player's counter to a thermal cascade?** Sharper than it was: a cascade does not
   spread outward, it **decapitates**. Heat conducts back toward the source, which carries every watt
   on the grid, so a fault on any branch kills the reactor about ten seconds later. A breaker's job
   is therefore to cut a wasteful branch loose *before its heat reaches the trunk* — it protects
   upstream, not the branch it sits on. That makes breakers close to mandatory rather than optional,
   which is worth deciding on purpose.

3. **Can a switched-off component be damaged by heat?** A tile that is off carries no current, so its
   hazard is zero and you could protect something from a fire by flipping its switch. A *wrecked*
   component is a different case and is already handled: it stays wired in, keeps being fed, keeps
   wasting, and keeps taking rolls.

4. **Is the misfire curve right at the edges?** `FailureChanceWhenSpent = 0.5`, scaling from zero at
   the 20% worn line. At 2% condition that is 95 misfires in 200 cycles, which may be past the point
   of being recoverable.

5. **Conduit capacity.** Worked through and parked. The viable shape is a **restrictor**: a cable
   carries at most its rating and the excess dumps as heat at the fork. The two alternatives both
   cascade the instant one cable is mismatched, with no player input at all. Capacity would make thin
   cable a deliberate regulator whose ratio is visible as thickness — the answer to "no 70/30
   splitter" — at the cost of heat at the junction.

6. **Does the hidden completion level reseed with the galaxy at midnight?** Daily reset means every
   session starts at the rim and nobody ever sees the centre. Persistent completion means tomorrow's
   crew inherits today's difficulty *and* a galaxy they have never mapped.

7. **With nav dead, can the ship jump at all?** Point the drive and hope, or stranded until power is
   restored. Decides whether an unpowered nav system is inconvenient or fatal.

8. **What visual channel carries scanner confidence?** Red reads as *no*, not *unsure*. Fuzzy or
   dashed lines are the obvious alternative, keeping colour for genuinely binary things.


## Concerns

**Flow direction has to be visible in the ship.** Allocation is readable from cable count and branch
depth, but only if you can see *which way power moves* — and a tile's output face is currently
invisible in game. Conduits need etched arrows, a travelling glow, or something equivalent.

**Heat and failure are invisible in the scene.** Temperature shows on the debug labels and nowhere
else. Cable colour still ramps on **load**, so a cooking conduit looks identical to a healthy one and
a blown one looks perfectly normal while silently carrying nothing. `Popped`, `Damaged` and `Lost`
all fire and nothing listens. The reactor lamp is the one real in-world readout that exists.

**Splitters have no in-game direction indicator.** A `SplitterModule` draws an editor gizmo along its
branch and nothing else, so you cannot see which way one is aimed in game. Deferred to the VR pass,
when the thing you physically rotate needs to show its aim. Runs are hardwired until then.

**A tile with no faces is completely invisible.** It renders nothing, which reads as broken rather
than unconfigured. `Validate()` could warn; a tile using no faces is almost always a mistake.

~~**Two-way sync between a serialized field and the sim is a trap.**~~ **Resolved** (2026-09-26). The
old `ReactorModule` pushed `_targetWithdrawal` into the core and read it back, and when the core
zeroed the dial the Inspector could not be changed at all. `ReactorBehaviourModule` only pushes; the
dial is the crew's and the sim never writes it.

**Two runaway loops is the budget.** Conduit cascade and reactor feedback can already trigger each
other. A third positive feedback system would make failures unreadable.

~~**Integer milliwatts, not floats.**~~ **Resolved.** Doubles are fine because flows are recomputed
from scratch every tick rather than accumulated, so nothing drifts. Verified: 200 randomly generated
grids all conserve to 1e-6.

**Scope is large, but it is accreting in the right direction.** The list of modules and fittings grows
while the core model stays fixed. The thing to watch is not the length of the item list, it is any
addition that requires a new rule in the flow model — and so far the only two that looked like they
did (splitters, and a short starving its siblings) turned out to be a tile with a second face and an
edge with a higher share.

**`internal` currently means nothing.** There is no asmdef under `SpaceScape`, so everything compiles
into one assembly and the Unity layer can write `node.Drawn = 999` straight into the sim. An asmdef
around `scripts/Power/` would make `internal` mean "the graph only" and make a Unity type in the sim
a compile error rather than a rule to remember.

**The reactor's fail-safe has no failure mode.** Rods held by electromagnets drop on loss of power,
so the reactor shuts itself down safely — which removes the runaway entirely and takes a game-over
scenario with it. Needs something that can stop the rods falling. See `reactor.md` → Open.

## Blocked — needs something from Somnium

**Physics hands.** Holding the avatar's hand at a surface instead of letting it pass through, which
is what would make a switch feel substantial. Spiked on 2026-09-26 and it cannot be done from a world
today:

- `ISomniumPlayerBody.LeftHand` / `RightHand` are the **tracked controller anchors**
  (`…/XR.Body/FloorOffset/LeftHand/…/LeftHandAnchor`), carrying `TrackedPoseDriver`,
  `ActionBasedController`, `PlayerTransformNetworkDriver` and a kinematic `Rigidbody`. The avatar's
  IK reaches for them; they are not the rendered hand.
- A sphere sweep found the wall fine, but anything written to the anchor is overwritten before render,
  every frame (`overwritten before render` = `blocked frames`, 493 of 493).
- Stopping the pose drivers' before-render update needed reflection, and **the uploader rejected
  that version as using features that are not allowed**. Reflection is the likely cause, not
  confirmed.

So the switches press through for now: `RockerSwitch` follows the hand in and snaps rather than
stopping it. Worth asking Somnium for a supported way to offset or constrain a hand anchor.

## Parked — not for now

**Mid-session joins and newcomer onboarding.** How a stranger who has never seen a conduit is
absorbed into a crew six jumps deep, whether they can pull a conduit, and what unskilled-but-useful
work exists for them. Explicitly out of scope at this stage; the core machine gets settled first.

**Flash point — heat has no ceiling, and that is deliberate.** A tile that is wasting power never
cools, so its temperature climbs without bound. This is **not a bug and must not be "fixed" with a
cap.** It is the hook for fire: at some flash point the tile ignites, and fire becomes a hazard in
its own right that spreads, threatens the crew, and needs putting out rather than rewiring.

Until fire exists, the pop roll is the only thing that ends the climb — a conduit severs and stops
drawing, or a component is worn down to nothing. Both stop the tile being fed, so it cools. Nothing
else bounds it.

When fire is built, the seam is `PowerNode.Celsius` and a threshold beside `PopFloorCelsius`.

## Rejected — do not re-propose

**The jump drive draining the ship.** The idea that charging a jump drive should demand more than the
reactor can comfortably give, forcing the crew to strip power off everything else and be briefly
blind and defenceless. This is the shields-down-to-jump trope: an authored dramatic beat masquerading
as a system, and one no engineer in the fiction would accept. The drive has a power rating, the
reactor has a power rating, and a mismatch is the player's problem. See `principles.md` §4.

**Fuel cells as cargo-hold contention.** The idea that a deep contract needs N cells, the cells eat a
third of the hold, and the contract wants that third — "reach is bought with capacity". Not the job of
the fuel cell. The job is range anxiety across an unplanned route. See `the-loop.md` → Jump fuel.

**Multiple ships / a station as lobby.** Twenty players as four crews of five, with per-ship
progression, or instances synchronised through a server so players see each other across worlds. One
ship, one galaxy — there are not enough players in Somnium to justify the architecture.

**"Automation deletes a job a human was doing."** Wrong framing; automation is progression. The real
constraint is that the difficulty ceiling must rise faster than the automation ceiling, and principle
6 supplies the general mechanism. See `principles.md` §7.

**Throughput heat.** Segments heating by what flows through them. Physically correct, and it would
make the trunk the permanent answer to "what blew up". Dump heat only.

**A status panel for the 2D client.** See `principles.md` §9.

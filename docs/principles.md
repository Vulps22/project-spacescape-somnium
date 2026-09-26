# Principles

The rules the design rests on. Each one was arrived at by working a mechanic through, and each one is
the sort of thing a reasonable-looking change will quietly violate.

## 1. Depth must reach a player's hands

**Decided.** Simulation depth is worth exactly what it produces as physical work for a human being.
A gorgeous power/heat/coolant graph whose entire player experience is *watch number, press button when
number turns red* is a dashboard, not a game.

The test for every system added: **when this fails, what does a person do with their body?** If the
answer is "opens a menu and reallocates", it has failed the test. Barotrauma passes because a breach
means going there with a welder while water rises.

## 2. The sim exists to manufacture information asymmetry

**Decided.** Engineering knows the reactor is running away. Helm does not. Someone has to *say it out
loud*, and be believed, and be believed fast. That is the product.

So every readout is **local to one station or one place in the ship**, and deliberately not globally
visible. No shared HUD. No god-view status screen. The moment a screen exists that shows everyone
everything, the game is deleted.

This is also why **instrumentation is a progression axis** (see `power.md`, fittings): every other
game's upgrades make you stronger, these make you *less blind*.

## 3. Crises are self-inflicted, never rolled

**Decided.** Nothing random happens to the ship. The conduit pops because it was over-provisioned
four jumps ago and nobody read the heat. The coolant is low because nobody topped it up. The destroyer
is lethal because power is still routed for cruise and the laser's accumulator is charging from zero.

Every disaster is **self-inflicted, diagnosable, and was visible in advance if anyone had looked.**

This is what makes "largely empty galaxy" a feature rather than an apology — the emptiness is what
gives the ship's own entropy room to be the antagonist. Spawning random events to fill the quiet is
covering for a machine you do not trust to be interesting.

## 4. Do not author drama. Build rules and let consequences happen

**Decided**, and learned by getting it wrong in this very conversation. The rejected proposal was
"charging the jump drive drains the ship, so you are always vulnerable while jumping" — the
shields-down-to-jump trope. It is a designer-imposed dramatic beat masquerading as a system, and no
engineer in the fiction would accept a drive built that way.

The correct version: **the drive has a power rating, the reactor has a power rating, and the
consequences of a mismatch are the player's.** Run a T5 drive on a T1 reactor and it charges slowly
and you risk overheating. Upgrade to a T10 reactor and jump into eternity. Both are fine. The design
does not have an opinion.

See `open-questions.md` → Rejected, so this does not get re-proposed.

## 5. Priority is physical, never a setting

**Decided, and built.** There is no priority dial anywhere in the game. Allocation is expressed two
ways and only two ways:

- **branch depth** — power splits by share at every fork, so one branch point out gets half and two
  gets a quarter. Routing the long way round demotes something.
- **cable count** — two cables to one endpoint gives it two thirds, because two cables is two shares.

Both are readable by tracing a line with your eyes, under pressure, in peripheral vision. Any
mechanism that replaces this with a number in a UI is a regression, however convenient.

**Retired:** an earlier version of this principle had a third mechanism — consumers drawing greedily
in path order, with the remainder carrying on downstream. That only ever worked because components
were wired *inline*, and components are endpoints: nothing flows through one. The mechanism was
never real and is gone. Do not re-derive it.

The one thing that *does* vary a face's weight is a **fault**. `PowerEdge.Share` is 1 for a sound
cable; a short behaves as several. That is deliberate and it is invisible by eye — the disagreement
between what the cable count says and what the grid does is the entire reason to buy a meter.

## 6. An upgrade may extend reach, but confidence must decay with distance

**Decided**, and it is the general solution to the automation problem. The worked example is the nav
scanner's JPR stat (`the-loop.md`): a higher tier shows you routes further out, but confidence falls
off with distance, so **there is always a frontier where a human has to guess.**

A better scanner does not plan your route. It gives you bad data further away. The navigator's job
never gets automated — it moves outward.

This is reusable for every instrument on the ship, and it is a better answer than putting a ceiling
on automation.

## 7. Automation is progression

**Decided**, and it corrects an earlier framing of mine that automation "deletes a job a human was
doing". Factorio does not get worse when you stop hand-feeding furnaces.

The real constraint is narrower: **automation is progression as long as the difficulty ceiling rises
faster than the automation ceiling.** Barotrauma's problem is not that automation exists, it is that
the sub's challenge is fixed, so automation eventually exceeds it and nothing is left above.

Two things that make it work here:

- **Automation is a component, so it can fail.** A breaker overheats, gets shot, or is set to 800 W
  when tonight needs 900. That last one is the best fault in the game — the power is fine, the wiring
  is fine, and the gun will not fire because of a number someone set last week. Work is not removed,
  it is **re-sited one level up**.
- **Automation is the answer to crew size.** Five people fly manual. Two people *must* automate. It is
  not progression away from the game, it is progression toward running a ship shorthanded, and it
  means one ship scales across wildly different session sizes.

## 8. The ship is mostly guts and barely any bridge

**Decided**, and it is what makes the player count unbounded.

Console work does not scale — six stations and twenty players means fourteen people watching, and
coordination cost on twenty voices is negative capability. **Spatial work scales perfectly**: forty
conduit segments, a dozen modules, coolant loops and a reactor is genuinely parallel work. Four
people rewiring four branches do not collide.

So the architecture is **engine-room-first**, with the bridge as a small room off the side. Every
other game in the genre is bridge-first. This is the inversion, and it is also the answer to a
fixed-size world: a machine with surface area is not four plots.

## 9. A consequence is content, not a risk to design around

**Decided, and stated repeatedly.** When the model produces something painful — a charged gun cooking
its own cable, a ship at rest overheating, a wrecked component still drawing and still wasting — that
is not a problem to smooth over. **It is the pressure that sends a player to find and fit a part.**

A breaker exists because a branch cooks the trunk. A purge exists because a gun that holds its charge
has nowhere to put it. Damage stages exist because components wear. Every one of those is a
consequence first and a component second.

The failure mode to watch for is labelling these as concerns and designing them away. Ask instead:
*does the problem have a discoverable answer the player can install or do?* If yes, it is content.

## 10. Plain, safety, smart — a tier ladder that may generalise

**Observed while building the cells, and worth watching for elsewhere.** The three batteries differ
only in *how much they check before acting*:

| tier | asks |
|---|---|
| **plain** | have I got a cable? |
| **safety** | does this reach something that could use it? |
| **smart** | is anything actually asking, and at what rate do I want to give it? |

Each tier prevents a failure the tier below teaches you about, so the upgrade is **judgement**, not
throughput — a far more interesting purchase than "+20%". It cost five lines per tier because they
share a base and override one question each.

The same ladder plausibly fits a reactor, a breaker, a coolant pump, a splitter. **Do not build tiers
for anything until its plain version exists and its failures are understood** — the whole point is
that each tier answers a specific lesson, and you cannot name the lesson before it has hurt someone.

## 11. A mechanism that makes energy from nothing will find you

**Learned twice in one night, both times in the heat model.**

Making a reactor's baseline temperature a **floor that `Celsius` snapped up to** meant conduction
drained it every tick and the floor refilled it for free. A 400 °C core became an infinite heat
source and climbed to 600 °C on heat bouncing back off its own neighbours.

Letting a component with an input and an output **forward what arrived on top of what it produced**
meant a cold-start loop gained 80 W every lap, without bound.

Both looked like sensible local rules. Both broke conservation, and in both cases the fix was to be
precise about *what* is being conserved — excess heat rather than temperature, produced power rather
than throughput. **If a rule tops something up, ask what pays for it.**

## 12. The 2D client is worse informed, not compensated

**Decided.** VR-first, 2D bolted on — point and click to change, carry, place; WASD to fly at the
helm. That order matters: 2D-first would produce a schematic, and a schematic never becomes a place.

The real risk in the port is not interaction, it is **information bandwidth**. In VR the engine room
is read with the whole body and peripheral vision — flicker rhythm across the room, heat glow at the
edge of sight, sound, where you happen to be standing. On a flat screen there is only a frustum, and
the obvious fix is a status panel.

A status panel would kill the design outright (principle 2). **The 2D player is simply worse
informed.** They walk the ship like everyone else. Resist every instinct to compensate with an
overlay.

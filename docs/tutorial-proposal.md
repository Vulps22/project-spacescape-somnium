---
aliases:
  - "Tutorial proposal"
  - "The Shakedown"
  - "Tutorial plans"
tags:
  - game
  - interaction
---
# Tutorial proposal

Three ways to teach a newcomer the basics. **Plan A** is the Shakedown: short drills on small rigs, taught by ghost hands,
no words. **Plan B** is a wall of cubes, each a short spoken demo of one system. **Plan C** is Plan A with Plan B's
voice: the drills, with a short spoken line the first time each system appears. **Plan D** is the surge: a tutorial
engineering bay, local to each player, broken by a power surge, and a voice walks you through fixing it.

Vulps (2026-10-13): Plan A will probably win; Plan C merges the two. Plan D came after, from Vulps's own script.

## What it teaches

**Status (2026-10-13):** decided (Vulps).

Only the core: how the ship is wired and where things go. Everything else (reactors, fuel, batteries, weapons,
data, heat in depth) is found on the ship.

1. **Opening a conduit** ([[conduits#Opening a conduit|conduits → Opening a conduit]]).
2. **Redirecting a conduit:** swinging a segment to another face, its arrow In or Out, parking and unparking
   ([[conduits#Segments|conduits → Segments]]).
3. **Repairing a conduit,** with a brief mention that too much heat or power pops one ([[power#Failure|power → Failure]]).
4. **Ports:** what one is, and what the shapes and colours mean: round for power and data, **blue In, green Out, red
   Data**; square and a whole cell wide for items ([[loading#The item port|loading → The item port]]).
5. **Wiring a conduit:** start from a port; a new conduit becomes available at the end of a line, or in front of an
   unused port ([[conduits#Installing|conduits → Installing]]).
6. **Installing an addon** ([[addon]]).
7. **The hologram's two slots:** the **blue square** takes an addon, for this cable only; the **green circle** takes
   an upgrade, for the whole branch ([[conduits#The addon slot and the upgrade circle|conduits → The addon slot and the upgrade circle]]).
8. **Slots:** their sizes, and how to unlock one ([[slots]], [[grid#Moving a component|grid → Moving a component]]).

## Plan A: the Shakedown

**Status (2026-10-13):** proposed.

New crew do their **shakedown** in a practice bay beside the ship before they board it. The bay holds a few small
**rigs**, each a tiny working piece of ship on its own little grid: a battery, some cable, a **bulb** whose light
says whether power reached it.

Each rig starts working, then **breaks one thing**: a segment swings away, an arrow flips, a cable pops. The part
that changed flickers once with a sound, so the eye finds it, and a ghost hand shows the fix. Fix it and the bulb
comes back on; the rig breaks the next thing. Nobody reads anything.

At the end there is an airlock to the real ship with one line over it: **"From here on, it's all you."** On the real
ship nothing breaks unless the crew broke it ([[principles#3. Crises are self-inflicted, never rolled|principles → 3]]);
the bay is the one place things break on their own, and that line leaves it behind.

### How it teaches (the ADHD rules)

1. **Words never, verbs first.** Each beat teaches one action. Any text is optional, five words at most, on the rig.
2. **Show it with a ghost hand:** a looping hologram of a hand doing the action, on the part itself. In 2D, a ghost
   cursor that clicks ([[2d]]). It stops the moment a real hand starts.
3. **Feedback within half a second:** a sound, a light or some movement for every right action; the bulb coming on
   for every finished beat.
4. **Nothing to wait for:** the next beat starts as soon as the last one is done.
5. **Breaking it is fine.** No fail states. A **Reset** button on every rig puts it all back.
6. **Leave any time.** The airlock is open from the first second. A veteran passing through loses ten seconds.
7. **Short.** 30 to 90 seconds a drill; the whole bay in under ten minutes.
8. **Solo first, better with friends.** Two people on one rig share it.

### The drills

The first two sit on the path from the entrance; the rest can be done in any order. Where a beat breaks something,
the master makes that edit, exactly as a hand would.

#### 1. Open Up

A battery, a straight run of cable along the floor at hand height, a bulb, lit. One cable's cell is marked by the
ghost hand.

- **Beat:** the ghost hand slips into that cell. You do the same, and the hologram opens around your hand. Take your
  hand away and it closes.

#### 2. Redirect

A battery feeding a cable that ends at a corner, with a bulb to the left and a bulb to the right. The left one is lit;
the right one blinks.

- **Beat 1, swing:** the ghost hand opens the corner cable and swings its segment's handle from the left face to the
  right. You do it. The right bulb lights, the left goes dark.
- **Beat 2, arrow:** an arrow flips inward, so two segments point in at each other. Dark. The ghost hand slides the
  arrow outward. Light.
- **Beat 3, park:** a segment parks itself: its handle tucked in the middle of the cable. Dark. The ghost hand drags it
  out of the middle toward the bulb. Light. Then the ghost hand slides an arrow across to park it: park it yourself to
  turn the bulb off, and back out to turn it on.

#### 3. Ports

A battery and two bulbs, not joined to it. Every port on the rig pulses gently.

- **Beat 1, out and in:** the ghost hand points at the battery's **green** port, then a bulb's **blue** one. Wire the
  green Out to the blue In (as in Lay a Line, below; on this rig the run is one cell). Light.
- **Beat 2, the wrong one:** a cable from the battery's **blue** In to the second bulb is already laid. That bulb stays
  dark: power comes out of green and goes into blue. Move it to green.
- **Beat 3, red and square:** the battery's **red** Data port is shown, not used. A small square **item port** sits on
  a plinth beside the rig; a fuel rod lies in front of it, and its doors open as you pick the rod up near it. Square is
  where things go in. Nothing more about either.

#### 4. Lay a Line

A battery on one side of the rig, a bulb two metres away, nothing between them.

- **Beat 1, start at a port:** the ghost hand reaches into the empty cell in front of the battery's green port. It
  opens as a cable with nothing drawn yet, both segments parked. Pull one segment back toward the port and one out
  toward the bulb.
- **Beat 2, the end of the line:** the cell beyond that segment can now be opened in turn. The ghost hand shows one
  more cell, then stops. You lay the rest. Each cell lands with a click; the last one meets the bulb, and it lights.
- **Bonus:** the middle of the line parks itself. Both halves stay; the bulb goes dark. Join it again.

#### 5. Fix It

A finished run, bulb lit. One cable glows hot, then pops: a bang, sparks, the cable scorched, the bulb dark.

- **Beat:** a popped cable carries nothing. The ghost hand opens it, parks a segment and pulls it back out. Set again,
  it is mended. Light.
- **The brief mention:** the glow before the bang. Too much heat or power pops a cable; the rest is for the ship to
  teach.

#### 6. Addons

A run feeding a bulb that is on and cannot be turned off. Beside the rig, a dispenser holding a switch addon.

- **Beat 1, the two slots:** open any cable. The hologram shows a **blue square** and, above it, a **green circle**,
  both pulsing. Squares take addons, for this cable only. Circles take upgrades, for the whole branch (none in the
  bay). Let go of the switch in the circle and it is not taken.
- **Beat 2, install:** the ghost hand takes the switch from the dispenser and lets go of it in the blue square. You do.
  A rocker appears on the cable. Push it: dark. Push it again: light.

#### 7. Slots

A row of empty slots, **S0 to S5** side by side, each plinth's size engraved in amber on top, from a half-metre cube to
a 5 m one. At the end of the row, an **S1** slot holding a battery that feeds a bulb, its handle bar **red**.

- **Beat 1, sizes:** walking the row shows them. Nothing to do.
- **Beat 2, unlock:** the ghost hand pulls the battery's handle bar up. It turns **green** with a clunk; a soft ticking
  counts down the 10 seconds to take the battery out. The bulb dies.
- **Beat 3, fit:** carry the battery to the empty S1. A see-through box shows where it will go: red, orange, then
  green when it lines up. Let go and it locks. Try it on the S2 and it will not go.
- **The brief mention:** pull one out while power flows through it and it cracks with sparks: that costs it.
  Switch off first.

### What the bay needs that the ship does not have

- **A bulb:** a small, cheap load whose light is its readout.
- **A rig's own grid:** each rig an island of tiles, joined to nothing outside it.
- **Reset:** put a rig back to its starting state: its conduit cells, what is in its slots, charge, addons. The
  biggest new piece.
- **Breaks:** the master makes a beat's edit (a segment swung, an arrow flipped, a cell popped), with a flicker and a
  sound on the part.
- **Nothing leaves the bay:** an addon or battery carried through the airlock fizzles away at the door.
- **Ghost hands.**
- **Sounds** for every beat, on the rig's prefab, on the Environment layer.

The rest is built: conduits and the hologram, parking, popped cells mended by setting their segments again
(`ConduitCells`), ports, the item port's doors, addons and dispensers, slots, their plinths, handles and placement box.

> [!info] Networking
> **Status (2026-10-13):** proposed; to be discussed with Vulps before anything is built.
>
> - **The rigs' parts are ship parts,** networked as on the ship: the master owns them, a non-master's hand asks it.
> - **Each rig has a stage:** which beat it is on and the shared-clock time it began, in the rig's
>   `NetworkBridgeData`, written by the master. A late joiner sees the right beat.
> - **The master moves the stage on** when it sees the beat's goal in the shared state (the bulb lit, the slot locked).
> - **A break is a ship edit,** made by the master through the same path as a hand's.
> - **Ghost hands, flickers and sounds are derived** from the stage. Nothing new is sent.
> - **Reset is the master's:** two minutes with nobody on the rig, or anyone pressing Reset (a request to the master).
> - **Two people on one rig share it;** nothing is kept per player.

> [!question] Open
> - **Where people arrive.** Somnium cannot tell a newcomer from a veteran. Proposed: everyone spawns at the bay's
>   entrance, beside the airlock.
> - **Where the bay sits:** beside the ship, or a room inside it.

## Plan B: the demo wall

**Status (2026-10-13):** proposed (Vulps's idea, refined).

A room with a row of floating cubes along its wall, a question mark on each, slowly turning. Over each cube, two
lines: the system's name, and **slap me**. Slap a cube and a small **live exhibit** unfolds out of it, and a voice
walks you through it in short spoken lines, each asking you to do something. Touch any part of the exhibit to hear
what it is.

### How a demo runs

- **Slap the cube:** a hand passing through it fast. In 2D, a click. It bursts open into the exhibit on a plinth at
  waist height.
- **The exhibit is real:** real cable, battery and bulb on their own little grid, so what you do actually happens.
- **Each line is one or two sentences, under 8 seconds,** and ends by asking for one thing. The next waits until it is
  done. Stall for 15 seconds and a shorter version of the ask plays once.
- **Touch to learn:** every part of the exhibit has its own line. Guided lines take priority.
- **Done or bored:** slap the cube again, or walk two metres away, and the exhibit folds back in.
- **Subtitles** on a strip along the plinth's front: the only text in the demo.

### The cubes

One per thing taught, in the order of [[#What it teaches]]: **Conduit, Redirecting, Repair, Ports, Wiring, Addons,
Slots.** The hologram's two slots are part of Addons.

### The transcript

One file per cube: each line with an id, what triggers it, and what it waits for. Written with Claude in the desktop
app, then voiced. A sample, to set the length and tone:

```
conduit.01      on open        "This is a conduit. It carries power round the ship. Put a hand on it."
                               waits for: the hologram open
conduit.02                     "That's its hologram. Touch any part of it to find out what it does."
                               waits for: any part touched
conduit.handle  on touch       "A handle. Drag it to another face, and the cable goes that way."
conduit.arrow   on touch       "An arrow. Slide it out and power leaves this way. Slide it in and power comes in."
conduit.square  on touch       "The blue square takes an addon. It changes just this cable."
conduit.circle  on touch       "The green circle takes an upgrade. It changes the whole branch."
conduit.03      after 3 parts  "Now make that bulb light up."
                               waits for: the bulb lit
conduit.04                     "There you go. That's wiring. Everything on this ship is just more of that."
```

> [!info] Networking
> **Status (2026-10-13):** proposed.
> - **An exhibit is shared:** anyone can join in, and everyone near it sees and hears the same demo. One exhibit per
>   cube at a time.
> - **Its parts are ship parts,** networked as on the ship.
> - **Each cube has its state:** open or shut, the line it is on and when it began, written by the master. A slap or
>   touch from another client asks the master; the master moves to the next line when it sees the wait met. A late
>   joiner comes in on the current line.
> - **The voice plays from the exhibit** (spatial, Environment layer) on every client, from the cube's state.
> - **Folding back** resets the exhibit, decided by the master.

> [!question] Open
> - **Who or what voices the lines.**

## Plan C: drills with a voice

**Status (2026-10-13):** proposed.

**Plan A, with a spoken line the first time each thing appears.** The drills and ghost hands stay. The voice
covers only what a ghost hand cannot show: what a thing *is*, and what its colour or shape means.

- **The voice comes from a speaker on each rig:** plain and brief.
- **One or two lines a drill, under 8 seconds each.** A line is cut if the player is already fixing it.
- **Subtitles** along the rig's platform edge, as in Plan B.

| Drill | When | Line |
|---|---|---|
| 1. Open Up | the rig is reached | "That's a conduit. It carries power. Put your hand in it." |
| 2. Redirect | the right bulb blinks | "Drag a handle to send it another way. Slide an arrow to flip it." |
| 2. Redirect | the parked segment | "Tucked in the middle is parked. Pull it back out." |
| 3. Ports | the ports pulse | "Ports. Green sends power out, blue takes it in, red is data." |
| 3. Ports | the item port opens | "Square ones take things, not power." |
| 4. Lay a Line | the empty cell | "Start at a port. Reach into the space in front of it." |
| 4. Lay a Line | the second cell | "The end of a line opens the next space. Keep going." |
| 5. Fix It | the bang | "Too much heat or power, and they pop. Reset its segments to fix it." |
| 6. Addons | the hologram opens | "Blue square: an addon, this cable only. Green circle: an upgrade, the whole branch." |
| 7. Slots | the row of plinths | "Every machine sits in a slot. The number is its size." |
| 7. Slots | the handle | "Pull the handle to unlock it. Switch off first." |
| The airlock | walking through | "From here on, it's all you." |

> [!info] Networking
> **Status (2026-10-13):** proposed. As Plan A: the lines follow the rig's stage, so every client plays the same line
> from state it already has.

> [!question] Open
> - **Who or what voices the lines.**

## Plan D: the surge

**Status (2026-10-13):** proposed (Vulps's script, refined).

**You spawn in a tutorial engineering bay and see a cube.** Hit it and a voice says: *"A power surge has wreaked
havoc on engineering. This is a great time to learn the fundamentals."* The bay is broken, and the voice walks you
through fixing it, one thing at a time. The ship's own parts show each fix worked: a conduit changes colour as power
flows through it, a component's screen comes back, a slot's handle turns green.

### The bay is local (Vulps, 2026-10-13)

- **A bay of its own,** laid out for the tutorial: an engineering room with an S2 reactor, a battery bank, a feed behind
  an access panel, and nothing the script does not use. Not the ship's engineering.
- **Inside a trigger zone that turns off the sim's networking.** Everything in the bay is run on each player's own
  client: its conduits, components, addons, the delivered battery. **Every player has their own copy,** so everyone can
  do the tutorial at once without touching anyone else's.
- **The sim in the bay runs only while the local player is in the zone,** and stops when they leave.
- **No reset.** A player who wants to start again rejoins the world.
- Other players in the bay are seen as avatars as usual; what their hands do to their own copy is not seen.

### What the surge breaks

The bay starts broken, on every client the same way:

- **The reactor's feed:** one conduit on the run from the batteries to the [[reactor#S2 The Rail|S2 reactor]]'s In,
  behind an access panel, popped, with one segment parked. Its switch addon is taken off.
- **The reactor:** its lever at zero, so it is cold. It keeps its fuel rod.
- **One S1 battery** in the bank feeding the bridge: wrecked (integrity 0), its screen saying so.
- **The conduits off that battery's green Out port:** every installed conduit within 3 cells of it, uninstalled. Its
  In side is left alone.
- **The end of the bridge feed,** where those conduits used to join it: its segment swung to a wrong face, its arrow
  Out.

### The script

Each line waits for what is in its right-hand column before the next plays. The wording is Vulps's, tidied.

| #   | Line                                                                                                                                                        | Waits for                                                                      |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| 1   | "A power surge has wreaked havoc on engineering. This is a great time to learn the fundamentals."                                                           |                                                                                |
| 2   | "First things first: we need power flowing. Look at the reactor."                                                                                           | the S2 reactor seen                                                            |
| 3   | "Good, it still has a fuel rod. So we just need to repair the conduit that feeds it for a cold start. Open the access panel: wave a hand through the glowing cube." | the panel open                                                                 |
| 4   | "See the sparks? That conduit was damaged. Put a hand on it to open it up."                                                                                 | the hologram open on the popped cell                                           |
| 5   | "Drag the sphere from the centre out to fill the gap and complete the circuit."                                                                             | the cell mended                                                                |
| 6   | "Excellent. You can tell a conduit is powered when it lights up like that. Now try the lever on the reactor. A little goes a long way with an S2."          | the reactor's output above zero                                                |
| 7   | "Great. Now let's put the switch back on the conduit you just repaired."                                                                                    | the switch picked up                                                           |
| 8   | "Open the conduit again and drop the switch in the blue square."                                                                                            | the switch installed                                                           |
| 9   | "It goes in off. That's fine: a running reactor doesn't need its starter."                                                                                  | 2 second pause                                                                 |
| 10  | "Bridge says they aren't getting any power. Let's check the battery banks for damage."                                                                      | the wrecked battery seen                                                       |
| 11  | "There. That S1 battery says it's wrecked. We'll have to replace it. Pull the red handle to unlock it."                                                     | the slot unlocked                                                              |
| 12  | "Now drag it out of its slot."                                                                                                                              | the battery out of the slot                                                    |
| 13  | "Our engineers have just delivered a replacement. Install it in the empty slot."                                                                            | the new battery locked in                                                      |
| 14  | "Hmm. Something's not right. Check the ports on the back of the battery."                                                                                   | its ports seen                                                                 |
| 15  | "That explains it. Those three ports are on every component, at least one of them. Blue takes power in, red is data, and green sends power out."            |                                                                                |
| 16  | "Put a hand next to the green port and drag the spheres out, like you did to repair the conduit."                                                           | a conduit installed on the green port                                          |
| 17  | "Good. Any port or conduit with a dead end lets you install a new conduit like that, power or item."                                                        |                                                                                |
| 18  | "Now lay a line from it to the end of the bridge feed."                                                                                                     | a run of conduits from the green port to the cell beside the feed's end        |
| 19  | "The end of the feed got knocked aside. Open it and drag its handle round to meet your line."                                                               | the feed's end segment on the face toward the new line                         |
| 20  | "Now its arrow. Slide it in, toward the centre, so power comes in from your line."                                                                          | that segment In                                                                |
| 21  | "There. Bridge has power."                                                                                                                                  | the bridge feed carrying power                                                 |

The surge is only cosmetic when it starts: the sparks, a bang and the line. The breaks themselves are what a crew would
see after a real one.

**Seen** (Vulps, 2026-10-13, refined): the target within about 15° of where the head points, in reach, with nothing
solid between, held for half a second. In 2D the same test runs from the camera. Not a single ray from the head: that
counts only dead centre, and some targets (round ports) have no collider to hit.
### What the script covers, and what it does not yet

From [[#What it teaches]]: opening (4), repairing and the pop (4, 5), ports (14 to 16), wiring from a port (16, 17), an
addon (7, 8), slots and unlocking (11 to 13), wiring a line (18), redirecting with a handle and an arrow (19, 20).
**Not yet:** parking on purpose, the green circle, and slot sizes.

### What it needs

- **The zone:** a trigger that keeps everything inside it off the network and stops its sim while the local player is
  outside. The bay's tiles, conduit cells and components must be a grid and a cell list of their own, apart from the
  ship's master-owned ones, and failures inside it decided locally, not by the master.
- **The cube and the script:** run locally, each line waiting on the bay's own state.
- **The broken start:** the bay's layout as built, with the breaks above applied as its sim starts.
- **The voice lines,** recorded, played from where each break is.
- **Highlights** (Vulps, 2026-10-13): whatever the player has to see or find is marked: a **white box** round a
  component, a **white laser** standing above a conduit, item, addon, port or the panel's cube. White, because green
  already means Out, the upgrade circle, an unlocked handle and a component lined up to lock.
- **The seen test** above, from the head (VR) or the camera (2D).
- **Ghost hands** for the actions (open, drag the sphere, pull the handle), as in Plan A.
- **The delivered battery:** a local, never-networked Battery S1 made in the middle of the room.
- **The bay itself:** an S2 reactor in a slot, fed from the batteries through a switch behind a panel; the S2 reactor is
  built but not yet in any slot. The battery's slot stands clear of the wall, so its back panel can be reached.

The rest exists: popped cells, mending by unparking, access panels, the switch addon, slot handles, battery screens with
integrity, conduits changing colour with load.

### Built: the script runner

**Status (2026-10-13):** compiles; untested, not in the scene yet.

`SurgeScript` (`scripts/Ship/Surge`): the cube (a collider; a local hand inside its bounds starts it and hides it),
then the lines in order. Line N plays `stepN` from `Sounds/Surge` (filled into **Clips** in the Editor by name, any
audio type; **Find Step Sounds** in its context menu), through the **Voice** AudioSource, which goes on a child on the
Environment layer. The next line plays once this one has finished and its **Wait** is met. Added fresh, it holds all 21
lines with their waits; each step's **Target** is set in the scene: the thing to see, a marker in a conduit's cell,
or the panel, slot or reactor. **Face** for the segment waits, **Addon** for the switch, **Seconds** for the pause,
**Reveal** to turn on the delivered battery, **Boxes** and **Lasers** for the white highlights (`HighlightWhite.mat`,
line renderers). A wait with nothing to read is skipped with one warning. The head's direction is
`PlayerHands.TryHeadPose`. Local: it reads whatever the bay's state is on this client and sends nothing. Not built:
the zone, the bay's broken start, ghost hands, hitting the cube in 2D.

> [!info] Networking
> **Status (2026-10-13):** decided (Vulps): **none.** The bay is local to each player: nothing in the zone is sent,
> nothing is shared, and the master has no part in it. A late joiner gets a fresh, broken bay.

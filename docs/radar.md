---
aliases:
  - "Radar"
tags:
  - component
sizes:
  - S3
---
# Radar

The component that sees what is outside the ship and lets the crew pick a target. **Seeing, cycling and
selecting are built and tested in-world; publishing to the data network and the gun's auto-aim are built
(2026-10-07), untested in-world.** How its
data reaches the gun is in [[data]]; the gun is in [[weapons]].

## Decided

**Status (2026-10-03):** decided.

- **An S3 component** (S1 at first; its screen sat far too low, 2026-10-03), in a slot like any other: carried,
  swapped, powered, worn.
- **It works from any S slot on board** (Vulps, 2026-10-13): it needs no view of space, and is not a gun, so it
  never goes in a G slot ([[slots#G slots|slots → G slots]]).
- **It has its own screen.** Phase 1 has no separate console: the radar is where a person looks at contacts
  and picks one.
- **It owns detection**, since only it can see contacts, **and selection**, since the selection is made on its
  screen. A bridge console later is just another reader of its contacts that publishes its own selection; the
  gun's question does not change.
- **It publishes `RadarData`** ([[data]]): its **contacts** (what it can detect) and the **selected target**,
  as a read-only record through `IDataSource`. Anything on its data network reads it: a gun, and later a
  bridge monitor, at no extra cost.
- **The gun reads the selected target each tick** and ignores the rest ([[weapons]]). Nothing commands the
  gun; reading is how the radar acts on it.
- **Networking:** the selected target is the radar's state, kept by the master; a hand on any client sends its
  choice to the master, like the disruptor's aim, and late joiners get it with the rest of the radar's state.
  The contacts are worked out on every client from the same targets, so they need nothing sent (Phase 1).

## Phase 1

**Status (2026-10-03):** decided.

- **What it detects:** anything with a **`DetectableModule`** within **1 km** of the radar, found by distance,
  not by raycast: the radar sits inside the ship, so a ray from it would stop at the ship's own walls. Line of
  sight can come with a hull.
- **Test targets:** static objects with a `DetectableModule`, dotted at random from **z -100 to z -500**,
  **x -50 to +50**. Positions come from a fixed seed, so every client places them the same with nothing sent.
  **Hit, a target disappears and comes back where it was one minute later.** The master decides hits
  ([[damage]]: the master resolves every hit) and brings targets back; whether each is down is shared state,
  one bit per target (like the access panels), so late joiners see the same.
- **The screen:** the contacts as markers. Under it, **Previous** and **Next** buttons step through them, with
  a **green square button** between them that selects the one stepped to. The selected marker gets a square
  round it, with horizontal and vertical lines drawn from the square across the screen.
- **Two radars on one network:** a gun takes the **first selected target it finds in range**; a radar with
  nothing selected is ignored. With no selection anywhere, the gun does nothing with radar data.
- **Accuracy:** none lost. A contact on the screen is exactly where it is: the radar reports true
  coordinates. That is not the gun's ability to hit it.
- **The disruptor's beam reaches 500 m** (it was 200 m), which covers the test targets out to z -500.
- **Networking:** the contacts need nothing sent (fixed seed, shared down bits). The selected target is the
  radar's state, kept by the master; Previous, Next and select on any client go to the master, like the gun's
  aim.

- **The gun auto-aims at the selected target** (decided 2026-10-03): its levers move themselves to point at
  it while it is inside the gun's ±45° field of fire; someone still presses fire.
- **Auto and manual are not gated; they fight** (decided 2026-10-07). A hand on a lever and the auto-aim
  both move the aim, every tick, and the result is whatever comes of that: the lever pulls against the hand.
  To aim by hand, a player has to **break the data connection** (open a switch on the data line, pull the
  radar, cut the conduit). Auto-aim turns the aim at a set speed, so a hand can win for a moment.
- **No target in range: the gun returns to centre** (decided 2026-10-07). On a data network with a radar but
  nothing selected, or the selection outside the gun's field of fire or reach, the auto-aim slews it back to
  straight ahead. A gun on no radar's network is left alone: it is a manual gun.

## Open

- **Range and confidence later:** contacts growing less certain with distance, like the nav scanner's JPR
  ([[the-loop]], [[principles]] §6). Not for now: phase 1 reports true positions.

## Built

**Status (2026-10-07):** seeing, cycling and selecting tested in-world; publishing and auto-aim built, untested in-world.

`Prefabs/Components/Radar`: S3 (4 m), 600 kg, its art fitted to the slot, screen centre 1.83 m above the slot's
floor, with everything a component carries (from
the battery: grid node, power, integrity, networking, carrying). One **In** port on the back face, bottom row, middle. In engineering it starts in `Slot S3` (x 25.75), fed by
battery B.
A 50 W load (`LoadBehaviourModule`, 50 J hold): unpowered, the screen is blank and the buttons do nothing.

- **`Radar`** (`scripts/Ship/Sensors`): contacts are every `DetectableModule` within 1 km, in id order; plotted
  top down on `Display` (the screen's front face), the back of the slot up the screen, 1 km to the screen's
  top and bottom edges. Each is an arrowhead (`Meshes/RadarContact`) turned to its local x+. The cursor's
  contact is amber; the selected one has a green square with lines to the screen's edges.
- **Buttons:** Previous and Next (amber triangles) move the cursor, wrapping round; Select (green) selects the
  cursor's contact. Each a `PushButton`.
- **Zoom lever** (added 2026-10-03, after the first test found the targets cramped): a three-quarter-size rod lever under
  the buttons, swinging left and right. Fully left shows the whole 1 km to the screen's top and bottom edges;
  fully right shows 100 m (Zoomed In Range). Evenly by ratio between (a log scale), so every bit of travel zooms
  by the same factor. Detection stays 1 km; contacts past the screen's edge are not drawn.
- **Contacts' ids:** `DetectableModule.Id`, set by whatever places them (`TargetField` numbers its targets), so
  the cursor and selection name the same contact on every client.
- **Networking:** cursor, selection and zoom are the radar's state (`INetworkedState`, through
  `ComponentNetwork`); a button pressed or the lever moved on a client that does not decide is sent to the one
  that does, so everyone sees the same lever and screen.

**Publishing and auto-aim (2026-10-07, untested in-world).** The radar is `RadarBehaviourModule`, its own load
(it was a separate `Radar` script beside one), and while powered publishes `RadarData`: a copy of every contact
(`Contact`: id, position, facing) and the selected one if it is in sight. Unpowered, it publishes nothing. Its
readout says how many contacts it sees and Target N or No target (Off when unpowered). The disruptor's auto-aim
is in [[weapons]].


## Model

**Status (2026-10-09):** proposed, in the house look ([[component-models]]); next after the battery.

- A rig carrying a square phased-array panel on a turret that **sweeps while powered** and stops dead when
  it isn't.
- A tilted console in front with the screen, the buttons and the zoom lever.

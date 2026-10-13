---
aliases:
  - "Interaction"
tags:
  - interaction
---
# Interaction

How hands work things: stock XRI first (Somnium's guidance), and the pieces still driven by our own scripts.

## Interaction rework

**Status (2026-09-27):** planned.

**Found 2026-09-27.** Most of what a hand does in the scene is driven by our own scripts rather than
stock XRI components, which is why it all feels a little clunky, and it will not survive networking:
each piece keeps its own private state that nothing else could sync. Somnium's guidance is
`XRGrabInteractable` for anything grabbed.

| Piece | Today | Stock replacement to try |
|---|---|---|
| Rocker switch | **rebuilt 2026-09-27:** two solid box colliders, one per half of the paddle, on a rigid pivot. Each physics step `Physics.ComputePenetration` measures how far the avatar has pushed square into the raised half, and the paddle rocks in by that much; past the snap point it snaps over, let go early it eases back. No joint or spring (a hinged rigidbody bounced and flexed against the hand) | done; untested in-world |
| Rod lever | `XRSimpleInteractable`, dragged through our own damped spring | `XRGrabInteractable` held to a hinge (a joint or an axis-constrained grab transformer) |
| Hologram handles and arrows | `XRSimpleInteractable`, positions and snapping computed by `ConduitHologram` | `XRGrabInteractable`, reading where it was let go; keep the snapping |
| Addon slot | distance from the slot, checked when an item is let go | `XRSocketInteractor` on the slot |
| Addon item | `XRGrabInteractable` | already stock |

## Holding things

**Status (2026-10-10):** tested in-world, works.

- **Nothing snaps to the hand.** Packs and components have `Far Attach Mode = Far`: caught by the laser, they
  stay out where they were and move with it. Packs also have `Use Dynamic Attach`, so a near grab holds them
  where the hand touched, not by the middle of the base. Components already keep the pose they were grabbed in
  (`HeftGrabTransformer`). Both set in code (`ItemPack.Awake`, `CarriedComponent.Awake`).
- **Two hands.** Both have `Select Mode = Multiple`. Packs turn through XRI's default transformer (aimed from
  the first hand toward the second). Components keep their pose in the frame between the hands (centred on them,
  pointing from the first to the second, up the way both lean): push and pull turns it, raising one hand tilts
  it, twisting both rolls it, and it still sags with weight. Each time a hand takes hold or lets go the pose is
  taken afresh, so nothing jumps. Slotting and pack drops already wait for the last hand.
- **The laser reaches 3 m** (`Player/GrabReach`, on the Players object, `Reach`). When the local player joins
  (`PlayerManager.LocalPlayerJoined`) it lengthens every straight ray interactor (reach and drawn line) and
  near-far interactor's far caster on this client, which can only be the local rig; never shortens, leaves
  curved (teleport) rays alone, and logs what it found and what it was.
- **Open:** which interactor Somnium's rig uses (the `GrabReach:` log line says).

Open: whether Somnium's hands include a poke interactor; and how Somnium syncs an
`XRGrabInteractable` (and whether the rest needs Fusion) before networking.

## Physics hands

**Status (2026-09-26):** blocked: needs something from Somnium.

Holding the avatar's hand at a surface instead of letting it pass through, which
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

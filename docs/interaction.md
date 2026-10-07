# Interaction

How hands work things: stock XRI first (Somnium's guidance), and the pieces still driven by our own scripts.

## Interaction rework (found 2026-09-27)


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

Open: whether Somnium's hands include a poke interactor; and how Somnium syncs an
`XRGrabInteractable` (and whether the rest needs Fusion) before networking.

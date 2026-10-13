---
aliases:
  - "Fuel rod"
tags:
  - item
---
# Fuel rod

The reactor's fuel: one rod at a time, loaded through an item port ([[loading]]), carried in a [[packs|pack]].

## The rod

**Status (2026-10-09):** built, untested in-world.

- **The fuel rod** (`Prefabs/Items/Fuel Rod`): grabbed and networked as the Switch Addon is (`Rigidbody`,
  `XRGrabInteractable`, `IgnoresPlayerBody`, `NetworkObject` with flags 524289, `NetworkRigidbody3D`,
  `NetworkGrabbable`) plus `LoadableFuelRodItem`. **Needs adding to `SceneNetworking`'s network prefabs to be
  spawned.** Its model: 1 × 2 × 1 cells,
  long axis Y, root at its centre. A hexagonal body 0.48 m across the corners with 0.6 m straight sides and a
  0.2 m metal pyramid at each end. Each side is a frame of 3 cm metal bars round a glass panel sunk 1 cm,
  glowing Cherenkov blue (`FuelRodMetal.mat`, `FuelRodGlass.mat`, URP Lit). One box collider on the root.
  Modelled in Blender: `Blender/FuelRod.blend` (outside Assets, never uploaded), exported to
  `Meshes/FuelRod.fbx`. After editing the .blend, export the FBX over that file with Forward -Z, Up Y and
  Apply Transform on; keep the material names, and Unity maps them to the URP materials.

- **The fuel rod is slimmer:** its model is scaled 0.917 across, so 0.44 m across the corners, and its
  collider to match. Its mesh is now Read/Write enabled.

## Its pack: the fuel pack

**Status (2026-10-08):** built; lid, pictures and lamps checked offline; grabbing and networking untested.

- **The fuel pack is a box you take rods out of by hand** (decided 2026-10-08). Futuristic and dramatic, not a
  chest: an angular box, 1.10 × 1.10 × 1.15 m, a blue glowing seam under the lid, and four blue count lamps on
  the front, one lit per rod inside. The rods (2 × 2) hang from clamps under the lid. **A short tug on the lid
  starts it**: it rises straight up on its own, staying horizontal, about 1.05 m, lifting the rods clear of the
  box (about 2.2 m tall open); a short push down starts it sinking shut. Nothing holds the lid up: it is a
  game, not a reality simulator. Grab a rod to pull it from its clamp; let a rod go under an empty clamp and
  it goes back in. **A pack never goes into an item port**: a hand
  feeds the port one rod at a time, as now, and only the loader (step 2) takes whole packs.
- **The prefab:** `Prefabs/Items/Packs/Fuel Rod Pack`, `FuelRodItemPack`, its **Contains** slot holding the Fuel Rod
  prefab; 4 places, 4 count lamps (`FuelPackGlow` lit, `FuelPackDark` unlit). Mass 20 kg (provisional). Origin at the
  bottom of the box. Model: `Blender/FuelPack.blend`, built by `Blender/FuelPack.py`
  (`blender -b --python Blender/FuelPack.py -- <blend> <fbx>`), exported to `Meshes/FuelPack.fbx`. **Needs adding to
  `SceneNetworking`'s network prefabs, and a dispenser,** before it can be spawned in-world.

## Glass: binocular luster

**Status (2026-10-08):** planned; built once Vulps says the loading work is done.

To build once Vulps says the loading work is done. The glass shows **bright blue to one eye and a very
transparent blue to the other.** The brain cannot fuse the two, and the panel seems to shimmer or glow from
within (binocular luster, with some rivalry): an unsettling look for something radioactive.

- **It works in Somnium:** Vulps hit it by accident in Grow A Garden, when a skybox rendered without its stereo
  pass. Across the whole view it was unbearable, so it stays on **small surfaces only** (the rod's glass panels).
- **Keep the two eyes the same hue and pattern; vary brightness and transparency only.** Mismatched patterns are
  what make rivalry uncomfortable. No flashing.
- **Each eye's look must stand on its own:** people with a strong dominant eye or no stereo vision see one of
  them. The 2D client, mirrors and screenshots render one eye, and should get the full blue one (principle 12).
- **How:** a URP shader on `FuelRodGlass` that picks the brightness and alpha by `unity_StereoEyeIndex`. The
  project renders single pass instanced (OpenXR render mode 1).

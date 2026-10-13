---
aliases:
  - "Tooling"
tags:
  - tech
---
# Tooling

How to run the sim tests, work in the Unity Editor, drive it from outside, and build models in Blender.

## Running the sim tests

The sim has no Unity dependency, so it compiles and **runs** outside the Editor — which is why the
numbers in [[power]] are measured rather than asserted:

```bash
cd docs/simtest
dotnet run
```

The harness compiles the **real source files** in `scripts/Power/`, not copies; `-p:SimSrc=<dir>`
points it somewhere else. It lives here rather than in a scratch folder because `/tmp` is tmpfs and
a reboot took the first copy with it.

## Working in the Editor

The `unity` CLI drives the running Editor. Two things that have cost real time:

- **Never edit `SpaceScape.unity` on disk while it is open in Unity.** Unity keeps its own copy in memory
  and the next save writes over the edit (2026-09-29: the tutorial copies' overrides were lost that way).
  Prefabs not open in prefab mode are reimported from disk and are safe to edit.
- **Never recompile while Play mode is running.** A domain reload wipes `PowerGrid._graph` and every
  tile's binding, `Awake` does not run again, and the sim silently stops with every readout frozen
  mid-value. It looks exactly like a logic bug. `PowerGrid` now detects and rebuilds, but check
  `Application.isPlaying` first anyway.
- **Unity defers compilation while playing**, so a change written during a Play session is not
  running until you stop. Check it reached the loaded assembly before believing a behaviour report:
  `typeof(PowerNode).GetField("...")` either exists or it does not.
- **Edit-mode `LateUpdate` only runs when the editor ticks** — a repaint, a mouse move over the scene
  view. `EditorApplication.QueuePlayerLoopUpdate()` forces it. Code that looks dead is often just
  waiting for a frame.
- **`OnValidate` fires only on the component you changed.** Move a tile and its neighbours never hear
  about it, which is why `Conduit` is `[ExecuteAlways]` rather than validation-driven.
- **`RevertPrefabInstance` destroys added children.** It wiped a `SplitterModule` off a conduit.
- Editor state read *after* a scene restart says nothing about what was on screen *before* it.

**Sounds go on the Environment layer.** Somnium sets a sound's volume by its GameObject's layer, so every
AudioSource is a dedicated child object **in the prefab**, on the **Environment** layer, with its volume and
distance set there, and the script holds it in an `AudioSource` field (Item Port's Doors Sound, the Disruptor's
three under its Muzzle, RockerSwitch's Toggle Sound). A source on Default ignores the player's volume controls.
Only the conduit hologram, built in code, still makes its source in code (`EnvironmentSound.Make`), on the
same layer.

> [!warning] Pinned (2026-10-08): this did not fix it
> Uploaded with every source on Environment, the sounds still
> ignore Somnium's volume controls. The cause is unknown; the layer rule stays until it is found.

## Models (Blender, by script)

Script-built models live in `Blender/` at the repo root, tracked in git, so changes to them can be followed and
redone. Convention (decided 2026-10-09, from the loaders):

- **One folder per family** of models: `Blender/Loaders/`, `Reactors/`, `Batteries/`, `Slots/`, and the components to
  come ([[component-models]]) get theirs. `loader_parts.py` also holds what every family shares: `U()` (Unity
  coordinates to Blender's) and `render()` / `shots_for()`, the review shots a draft run saves beside its `.blend`.
- **One script per prefab, named after it without the space:** prefab `Loader S3` → `LoaderS3.py`. It saves
  `LoaderS3.blend` next to itself and exports `Assets/#User/SpaceScape/Meshes/LoaderS3.fbx`, with the same name
  throughout. Delete Blender's `.blend1` backups.
- **Shared parts in one module per family** (`Blender/Loaders/loader_parts.py`): materials, boxes, beams,
  rollers, legs, **the one brace rule** (a brace meets its leg halfway between the floor and the leg's top; its foot lands
  at 45° unless that would leave the volume, when it lands at the edge and the brace is steeper; floor beams run
  out to the feet), the power port panel, and the export. Every script in the family imports it, so all sizes are built
  the same way. A detail that looks right on one size goes into the shared module, not into one script.
- **After re-exporting, check the prefab's material order against the FBX's.** Blender orders a mesh's materials
  by first use, so changing the order parts are built in can reorder them. The prefab keeps the order it was
  made with, and every part then draws in the wrong colour (S3's dark guides all drew amber, 2026-10-09). Copy
  each renderer's `sharedMaterials` from the FBX into the prefab.
- **Run:** `blender -b --python Blender/Loaders/LoaderS3.py -- Blender/Loaders/LoaderS3.blend "Assets/#User/SpaceScape/Meshes/LoaderS3.fbx"`.
- **Axes and materials:**
  - Origin at the centre of the slot volume. Blender is Z up with the front at −Y; after export that is Unity
    +Z, and Blender +X becomes Unity −X.
  - Materials are named `FuelPackMetal`, `FuelPackDark` and `ComponentItem`, and remapped in Unity on import.
- **The power port** is 8.4 cm square and sinks 4 cm. **The item port** is 0.56 m square and sinks 0.29 m. Model
  solid geometry behind every port so it sits on the model.

## Driving the open editor from outside (Unity CLI)

`~/.local/bin/unity` talks to the running editor through the Pipeline package (port 7800). It works while the editor is
unfocused, which a window switch from outside does not (on Wayland it never refreshes):

- `unity command menu --path "Assets/Refresh"` imports new files; `unity command recompile_status` until completed.
- `unity command menu --path "SpaceScape/..."` runs our editor tools; `unity command console --level error` reads errors.
- `unity command eval --code '...'` runs C# in the editor; `unity list` shows every command.
- **`capture_scene_view --save_path` writes under `Assets/`** (`Temp/x.png` lands in `Assets/Temp/`). Move the file out
  and delete the folder and its `.meta` afterwards.

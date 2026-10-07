# Assets

Unity Asset Store packs used in SpaceScape: where each came from and where it sits in the project. Anything
that needs crediting in-world goes in `credits.md` (and the `Credits` prefab) as well.

| Pack | Source | In the project | Used for |
|---|---|---|---|
| UI Mechanical (Free Sample Pack) | https://assetstore.unity.com/packages/audio/sound-fx/ui-mechanical-free-sample-pack-148704 | not imported yet | |
| Sci-Fi Guns SFX Pack | https://assetstore.unity.com/packages/audio/sound-fx/sci-fi-guns-sfx-pack-181144 | `Assets/Scifi Guns SFX Pack` | |
| ProBuilder (Unity package `com.unity.probuilder` 6.1.2, added 2026-10-07) | Package Manager | `Packages/manifest.json` | shaping simple meshes by hand, like the Disruptor's screen. **Before an upload**, export each ProBuilder mesh as an asset (ProBuilder → Export → Asset) or strip its scripts (Tools → ProBuilder → Actions → Strip ProBuilder Scripts): Somnium has no ProBuilder, so a `ProBuilderMesh` arrives as a missing script |
| Modern ProBuilder Window | Asset Store | `Assets/Modern ProBuilder Window` | a replacement ProBuilder window; needs the ProBuilder package above, and without it fails to compile, which blocks every Editor script and Play mode |
| Sci-Fi Styled Modular Pack | https://assetstore.unity.com/packages/3d/environments/sci-fi/sci-fi-styled-modular-pack-82913 | `Assets/Sci-Fi Styled Modular Pack` | wall, panel and ceiling art in the conduit containers and corridor segments |

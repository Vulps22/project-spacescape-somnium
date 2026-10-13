# The empty "Slot S2" prefab's plinth: 6 x 4 x 4 cells, its size engraved in the top. Shared build: slot_parts.py.
#
#   Draft:  blender -b --python Blender/Slots/SlotS2.py -- Blender/Slots/SlotS2.blend
#   Final:  blender -b --python Blender/Slots/SlotS2.py -- Blender/Slots/SlotS2.blend "Assets/#User/SpaceScape/Meshes/SlotS2.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from slot_parts import build

build("S2", (6, 4, 4))

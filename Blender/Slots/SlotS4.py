# The empty "Slot S4" prefab's plinth: 10 x 8 x 8 cells, its size engraved in the top. Shared build: slot_parts.py.
#
#   Draft:  blender -b --python Blender/Slots/SlotS4.py -- Blender/Slots/SlotS4.blend
#   Final:  blender -b --python Blender/Slots/SlotS4.py -- Blender/Slots/SlotS4.blend "Assets/#User/SpaceScape/Meshes/SlotS4.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from slot_parts import build

build("S4", (10, 8, 8))

# The empty "Slot S0" prefab's plinth: 1 x 1 x 1 cells, its size engraved in the top. Shared build: slot_parts.py.
#
#   Draft:  blender -b --python Blender/Slots/SlotS0.py -- Blender/Slots/SlotS0.blend
#   Final:  blender -b --python Blender/Slots/SlotS0.py -- Blender/Slots/SlotS0.blend "Assets/#User/SpaceScape/Meshes/SlotS0.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from slot_parts import build

build("S0", (1, 1, 1))

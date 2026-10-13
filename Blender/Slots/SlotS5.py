# The empty "Slot S5" prefab's plinth: 10 x 10 x 10 cells, its size engraved in the top. Shared build: slot_parts.py.
#
#   Draft:  blender -b --python Blender/Slots/SlotS5.py -- Blender/Slots/SlotS5.blend
#   Final:  blender -b --python Blender/Slots/SlotS5.py -- Blender/Slots/SlotS5.blend "Assets/#User/SpaceScape/Meshes/SlotS5.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from slot_parts import build

build("S5", (10, 10, 10))

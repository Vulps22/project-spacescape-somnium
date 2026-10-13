# The empty "Slot S1" prefab's plinth: 4 x 4 x 4 cells, its size engraved in the top. Shared build: slot_parts.py.
#
#   Draft:  blender -b --python Blender/Slots/SlotS1.py -- Blender/Slots/SlotS1.blend
#   Final:  blender -b --python Blender/Slots/SlotS1.py -- Blender/Slots/SlotS1.blend "Assets/#User/SpaceScape/Meshes/SlotS1.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from slot_parts import build

build("S1", (4, 4, 4))

# The empty "Slot S3" prefab's plinth: 8 x 8 x 8 cells, its size engraved in the top. Shared build: slot_parts.py.
#
#   Draft:  blender -b --python Blender/Slots/SlotS3.py -- Blender/Slots/SlotS3.blend
#   Final:  blender -b --python Blender/Slots/SlotS3.py -- Blender/Slots/SlotS3.blend "Assets/#User/SpaceScape/Meshes/SlotS3.fbx"
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from slot_parts import build

build("S3", (8, 8, 8))

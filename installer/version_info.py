"""Writes installer/version_info.txt (Windows file version info for GameMerger.exe) from version.py."""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from gamemerger.version import VERSION  # noqa: E402

nums = tuple(int(x) for x in VERSION.split(".")) + (0,)
text = """VSVersionInfo(
  ffi=FixedFileInfo(filevers=%(n)s, prodvers=%(n)s, mask=0x3f, flags=0x0, OS=0x40004, fileType=0x1, subtype=0x0, date=(0, 0)),
  kids=[
    StringFileInfo([StringTable('040904B0', [
      StringStruct('CompanyName', 'Game Merger'),
      StringStruct('FileDescription', 'Game Merger'),
      StringStruct('FileVersion', '%(v)s'),
      StringStruct('InternalName', 'GameMerger'),
      StringStruct('LegalCopyright', 'Game Merger contributors'),
      StringStruct('OriginalFilename', 'GameMerger.exe'),
      StringStruct('ProductName', 'Game Merger'),
      StringStruct('ProductVersion', '%(v)s')])]),
    VarFileInfo([VarStruct('Translation', [1033, 1200])])
  ]
)
""" % {"n": repr(nums), "v": VERSION}
with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "version_info.txt"), "w", encoding="utf-8") as f:
    f.write(text)

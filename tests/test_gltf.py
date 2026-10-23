"""The Schedule I mod's own glTF reader (mods/schedule1/Gltf.cs), run outside the game with Mono where it's installed."""
import base64
import json
import os
import shutil
import struct
import subprocess
import tempfile
import unittest

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MOD = os.path.join(ROOT, "mods", "schedule1")
PNG = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==")


def make_glb(path, external=False):
    """A one-quad model: a parent node moved and turned, a child placed by matrix, a textured, glowing material."""
    pos = struct.pack("<12f", 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0)
    nrm = struct.pack("<12f", *([0, 0, 1] * 4))
    uv = struct.pack("<8f", 0, 0, 1, 0, 1, 1, 0, 1)
    idx = struct.pack("<6H", 0, 1, 2, 0, 2, 3) + b"\0\0"
    blob = pos + nrm + uv + idx
    img_off = len(blob)
    if not external:
        blob += PNG + b"\0" * (-len(PNG) % 4)
    views = [{"buffer": 0, "byteOffset": 0, "byteLength": 48}, {"buffer": 0, "byteOffset": 48, "byteLength": 48},
             {"buffer": 0, "byteOffset": 96, "byteLength": 32}, {"buffer": 0, "byteOffset": 128, "byteLength": 12}]
    if not external:
        views.append({"buffer": 0, "byteOffset": img_off, "byteLength": len(PNG)})
    doc = {"asset": {"version": "2.0"}, "scene": 0, "scenes": [{"nodes": [0]}],
           "nodes": [{"name": "body", "translation": [1, 2, 3], "rotation": [0, 0.7071068, 0, 0.7071068], "children": [1]},
                     {"name": "wheel", "mesh": 0, "matrix": [2, 0, 0, 0, 0, 2, 0, 0, 0, 0, 2, 0, 5, 0, 0, 1]}],
           "meshes": [{"primitives": [{"attributes": {"POSITION": 0, "NORMAL": 1, "TEXCOORD_0": 2}, "indices": 3, "material": 0}]}],
           "materials": [{"name": "paint", "pbrMetallicRoughness": {"baseColorFactor": [1, 0, 0, 1], "baseColorTexture": {"index": 0},
                                                                   "metallicFactor": 0.5, "roughnessFactor": 0.25},
                          "emissiveFactor": [1, 1, 0], "alphaMode": "BLEND"}],
           "textures": [{"source": 0}],
           "images": [{"uri": "Textures/colormap.png"}] if external else [{"bufferView": 4, "mimeType": "image/png"}],
           "accessors": [{"bufferView": 0, "componentType": 5126, "count": 4, "type": "VEC3"},
                         {"bufferView": 1, "componentType": 5126, "count": 4, "type": "VEC3"},
                         {"bufferView": 2, "componentType": 5126, "count": 4, "type": "VEC2"},
                         {"bufferView": 3, "componentType": 5123, "count": 6, "type": "SCALAR"}],
           "bufferViews": views, "buffers": [{"byteLength": len(blob)}]}
    js = json.dumps(doc).encode()
    js += b" " * (-len(js) % 4)
    blob += b"\0" * (-len(blob) % 4)
    body = struct.pack("<II", len(js), 0x4E4F534A) + js + struct.pack("<II", len(blob), 0x004E4942) + blob
    with open(path, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, 12 + len(body)) + body)
    if external:
        os.makedirs(os.path.join(os.path.dirname(path), "Textures"), exist_ok=True)
        with open(os.path.join(os.path.dirname(path), "Textures", "colormap.png"), "wb") as f:
            f.write(PNG)


@unittest.skipUnless(shutil.which("mcs") and shutil.which("mono"), "needs Mono to run the mod's reader outside the game")
class GltfReaderTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.dir = tempfile.mkdtemp()
        cls.exe = os.path.join(cls.dir, "check.exe")
        subprocess.check_call(["mcs", "-langversion:latest", "-out:" + cls.exe, os.path.join(MOD, "Gltf.cs"), os.path.join(MOD, "MiniJson.cs"),
                               os.path.join(ROOT, "tests", "schedule1", "GltfCheck.cs")], stdout=subprocess.DEVNULL)

    def run_on(self, path):
        out = subprocess.run(["mono", self.exe, path], capture_output=True, text=True)
        return out.returncode, out.stdout

    def test_reads_and_turns_into_unity_axes(self):
        p = os.path.join(self.dir, "car.glb")
        make_glb(p)
        code, out = self.run_on(p)
        self.assertEqual(code, 0, out)
        self.assertIn("nodes 2 meshes 1 mats 1 images 1 tris 2", out)
        self.assertIn("roots 0", out)
        self.assertIn("node body T[-1,2,3] R[0,-0.707,0,0.707] S[1,1,1] mesh -1 kids 1", out)  # x mirrored
        self.assertIn("node wheel T[-5,0,0] R[0,0,0,1] S[2,2,2] mesh 0", out)  # matrix taken apart
        self.assertIn("pos[0,0,0,-1,0,0,-1,1,0,0,1,0]", out)
        self.assertIn("uv[0,1,1,1,1,0,0,0]", out)  # v flipped
        self.assertIn("idx[0,2,1,0,3,2]", out)     # winding flipped for the mirror
        self.assertIn("mat paint color[1,0,0,1] image 0 metal 0.5 rough 0.25 emit[1,1,0] blend True", out)

    def test_external_texture_next_to_the_model(self):
        d = os.path.join(self.dir, "ext")
        os.makedirs(d)
        make_glb(os.path.join(d, "box.glb"), external=True)
        code, out = self.run_on(os.path.join(d, "box.glb"))
        self.assertEqual(code, 0, out)
        self.assertIn("images 1", out)

    def test_bad_files_are_refused_not_crashed(self):
        p = os.path.join(self.dir, "bad.glb")
        with open(p, "wb") as f:
            f.write(struct.pack("<III", 0x46546C67, 2, 100) + b"\x10\0\0\0JSON{}")
        code, out = self.run_on(p)
        self.assertEqual(code, 1)
        self.assertIn("error", out)


if __name__ == "__main__":
    unittest.main()

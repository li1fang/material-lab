import json
import re
import sys
from hashlib import file_digest
from pathlib import Path

from PIL import Image, ImageChops, ImageStat


directory = Path(sys.argv[1])
log = (directory / "player.log").read_text(encoding="utf-8", errors="replace")
events = (directory / "events.txt").read_text(encoding="utf-8")
assert "LAB_PROOF_FAIL" not in log and "Exception:" not in log
proofs = (
    "wood solver uses picked contact", "same first load bends tip farther",
    "higher release produces greater measured impulse", "distance-driven drag tears ribbon partway",
    "stationary hand does not tear more", "lower ear continues the same tear by dragging",
    "default C offsets the connected bricks", "C dent still obstructs the full vehicle",
    "light stroke leaves intact stepped dent", "reopened wall keeps dent and lane",
    "fresh launch opens branch", "cracked branch still connected", "unloading preserves branch crack",
    "branch separates", "letterbox tap ignored", "off-axis hit settles",
    "opposite ball settles", "actual ceramic chips and completion",
    "released before full split", "reload preserves tear without pulling", "split after two separate pulls",
    "intact wall blocks vehicle", "contact releases fragments before passage",
    "full vehicle traverses expanded gap", "parking undo restores state",
    "fixed-view 15-panel puzzle solved", "apply wall profile using player controls",
    "change an independent connection", "two shared posts receive changes on the same step",
    "duplicate a bay with shared members", "saved recipe restores building and selected member",
    "six unlocked and completed",
)
assert all("PASS " + proof in events for proof in proofs)
contact = re.search(r"WALL_CONTACT .*clearance=(-?\d+\.\d+)", log)
obstacle = re.search(r"WALL_STILL_BLOCKED .*gap=(\d+\.\d+), toy width=(\d+\.\d+)", log)
passed = re.search(r"WALL_VEHICLE_PASSED .*tail=(-?\d+\.\d+), far face=(-?\d+\.\d+)", log)
assert contact and float(contact[1]) >= 0
assert obstacle and float(obstacle[1]) < float(obstacle[2])
assert passed and float(passed[1]) > float(passed[2])
assert contact.start() < log.index("WALL_FIRST_FRAGMENT") < obstacle.start() < passed.start()
names = ("01-branch", "01-cracked", "02-ceramic", "02-chipped", "03-ribbon", "03-half",
         "03-split", "04-wall", "04-crossed", "05-parking", "05-landed", "06-building", "06-connection",
         "06-expanded")
images = {name: Image.open(directory / f"{name}.png").convert("RGB") for name in names}
for name, image in images.items():
    assert image.width >= 960 and image.height >= 540, name
    assert sum(ImageStat.Stat(image).stddev) > 25, name
for before, after in (("02-ceramic", "02-chipped"), ("03-ribbon", "03-split"), ("04-wall", "04-crossed"),
                      ("05-parking", "05-landed"), ("06-building", "06-expanded")):
    change = ImageChops.difference(images[before], images[after])
    assert sum(ImageStat.Stat(change).mean) > .14, (before, after)
result = {"completed": 6, "proofs": proofs, "screens": names}
if len(sys.argv) > 2:
    player = Path(sys.argv[2])
    sources = (player / "AI-RTS-Spectator.exe", player / "Managed/Assembly-CSharp.dll",
               player / "AI-RTS-Spectator_Data/resources.assets")
    result["build_sha256"] = {}
    for path in sources:
        with path.open("rb") as handle:
            result["build_sha256"][path.relative_to(player).as_posix()] = file_digest(handle, "sha256").hexdigest()
(directory / "verified.json").write_text(json.dumps(result, indent=2))
print(directory)

"""Package the MIT sources, UPM tarball and standalone Windows sample."""
from argparse import ArgumentParser
from pathlib import Path
import tarfile
from zipfile import ZIP_DEFLATED, ZipFile
import json
import hashlib


def main():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path(__file__).with_name("Output"))
    parser.add_argument("--campus", action="store_true", help="Package the separate Modular Campus player")
    args = parser.parse_args()
    work = args.output.resolve()
    source = Path(__file__).resolve().parent
    package = source / "Package"
    manifest = json.loads((package / "package.json").read_text(encoding="utf-8"))
    version = manifest["version"]
    player = work / ("CampusWindows" if args.campus else "Windows")
    player_name = "MaterialLab-Campus" if args.campus else "MaterialLab"
    if not (player / "AI-RTS-Spectator.exe").is_file():
        parser.error(f"Build the player first at {player}.")
    release = work / ("campus-release" if args.campus else "release")
    release.mkdir(parents=True, exist_ok=True)
    with tarfile.open(release / f"{manifest['name']}-{version}.tgz", "w:gz") as archive:
        archive.add(package, arcname="package")
    with ZipFile(release / f"MaterialLab-{version}-source.zip", "w", ZIP_DEFLATED) as archive:
        for name in (".gitignore", "README.md", "NOTICES.md", "Run.ps1", "Open-Ribbon.cmd", "Open-Ceramic.cmd",
                     "package_release.py", "examples/ribbon-two-grips.json", "media",
                     "distribution-notices/CoreCLR-LICENSE.md", "Host", "Package", "evidence"):
            path = source / name
            for f in (sorted(path.rglob("*")) if path.is_dir() else [path]):
                if f.is_file():
                    archive.write(f, f.relative_to(source))
        archive.write(package / "LICENSE.md", "LICENSE.md")
    with ZipFile(release / f"{player_name}-{version}-Windows.zip", "w", ZIP_DEFLATED, compresslevel=3) as archive:
        for f in sorted(player.rglob("*")):
            relative = f.relative_to(player)
            if f.is_file() and not any("DontShip" in part or "BurstDebug" in part for part in relative.parts):
                archive.write(f, relative)
        archive.write(package / "LICENSE.md", "MaterialLab-LICENSE.md")
        archive.write(source / "NOTICES.md", "MaterialLab-NOTICES.md")
        archive.write(source / "distribution-notices/CoreCLR-LICENSE.md", "Notices/CoreCLR-LICENSE.md")
        cache = work / "ExampleProject/Library/PackageCache"
        for dependency in sorted(cache.glob("com.unity.*@*")):
            for notice in sorted(dependency.iterdir()):
                if notice.is_file() and notice.name.lower() in {
                    "license.md", "license", "license.txt", "third party notices.md", "third party notices.txt"
                }:
                    archive.write(notice, f"Notices/{dependency.name}/{notice.name}")
        if args.campus:
            guide = ("完整解压后运行 AI-RTS-Spectator.exe。按 V 让履带车压入当前墙跨并退回，"
                     "换上铁栅栏再按 V。停车楼可切换规模、加载连续失撑案例。空格回放，Z 撤回，R 复位。\n")
        else:
            guide = ("完整解压后运行 AI-RTS-Spectator.exe。抓住布带右上角向上拖动，松手后再抓。"
                         "传入 --ceramic 可打开陶碟。先轻碰，再点重击，反面看断口。"
                         "换新碟后可以换到右缘。落枝击碟和把碟移开使用同一段木枝试验。"
                         "点木枝台可亲手拖动枝条。\n")
        archive.writestr("试玩说明.txt", guide)
    outputs = (release / f"MaterialLab-{version}-source.zip", release / f"{player_name}-{version}-Windows.zip",
               release / f"{manifest['name']}-{version}.tgz")
    with (release / "SHA256SUMS").open("w", encoding="ascii", newline="\n") as checksums:
        for item in outputs:
            with item.open("rb") as stream:
                digest = hashlib.sha256()
                for block in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(block)
                checksums.write(f"{digest.hexdigest()}  {item.name}\n")
    for f in sorted(release.iterdir()):
        if f.is_file():
            print(f"{f.name}  {f.stat().st_size:,} bytes")


if __name__ == "__main__":
    main()

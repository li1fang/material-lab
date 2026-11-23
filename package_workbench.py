"""Package the workbench and optionally import the exact tarball in a Unity host."""
from argparse import ArgumentParser
from hashlib import file_digest
import json
from pathlib import Path, PurePosixPath
import shutil
import tarfile
from zipfile import ZIP_DEFLATED, ZipFile

ROOT = Path(__file__).resolve().parent
SAMPLES = {"SixChapters": "MaterialGame", "BranchLab": "BranchLab", "ModularCampus": "ModularCampus"}


def pack(output):
    manifest = json.loads((ROOT / "Package/package.json").read_text(encoding="utf-8"))
    output.mkdir(parents=True, exist_ok=True)
    path = output / f"{manifest['name']}-{manifest['version']}.tgz"
    with tarfile.open(path, "w:gz") as archive:
        archive.add(ROOT / "Package", arcname="package")
    return path


def install(tarball, project):
    for folder in ["Packages", "ProjectSettings", "Assets"]:
        (project / folder).mkdir(parents=True, exist_ok=True)
    manifest = json.loads((ROOT / "Host/manifest.json").read_text(encoding="utf-8"))
    shutil.copyfile(tarball, project / "Packages" / tarball.name)
    manifest["dependencies"]["org.materiallab.destruction"] = "file:" + tarball.name
    (project / "Packages/manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    shutil.copyfile(ROOT / "Host/ProjectVersion.txt", project / "ProjectSettings/ProjectVersion.txt")
    lock = json.loads((ROOT / "Host/six-chapters-lock.json").read_text(encoding="utf-8"))
    lock["dependencies"]["org.materiallab.destruction"]["version"] = "file:" + tarball.name
    (project / "Packages/packages-lock.json").write_text(json.dumps(lock, indent=2), encoding="utf-8")
    with tarfile.open(tarball) as archive:
        for member in archive:
            if not member.isfile() or not member.name.startswith("package/Samples~/"):
                continue
            parts = PurePosixPath(member.name).parts[2:]
            if len(parts) < 2:
                continue
            if ".." in parts or parts[0] not in SAMPLES:
                raise ValueError(member.name)
            target = project.joinpath("Assets", SAMPLES[parts[0]], *parts[1:])
            target.parent.mkdir(parents=True, exist_ok=True)
            with archive.extractfile(member) as source, target.open("wb") as dest:
                shutil.copyfileobj(source, dest)


def bundle(output, project, player, tarball):
    with tarfile.open(tarball) as archive:
        manifest = json.load(archive.extractfile("package/package.json"))
        recipe = archive.extractfile("package/Samples~/SixChapters/Resources/stand-two-bay.json").read()
    if not (player / "AI-RTS-Spectator.exe").is_file():
        raise FileNotFoundError("Build the Windows player first.")
    cache = project / "Library/PackageCache"
    if not cache.is_dir():
        raise FileNotFoundError(cache)
    version = manifest["version"]
    zip_path = output / f"MaterialLab-{version}-Windows.zip"
    with ZipFile(zip_path, "w", ZIP_DEFLATED, compresslevel=3) as archive:
        for path in sorted(player.rglob("*")):
            rel = path.relative_to(player)
            if path.is_file() and not any("DontShip" in p or "BurstDebug" in p for p in rel.parts):
                archive.write(path, rel)
        archive.write(ROOT / "LICENSE.md", "MaterialLab-LICENSE.md")
        archive.write(ROOT / "NOTICES.md", "MaterialLab-NOTICES.md")
        archive.write(ROOT / "distribution-notices/CoreCLR-LICENSE.md", "Notices/CoreCLR-LICENSE.md")
        for notice in sorted(cache.glob("com.unity.*@*/*")):
            if notice.is_file() and notice.name.lower() in {
                "license.md", "license", "license.txt", "third party notices.md", "third party notices.txt"
            }:
                archive.write(notice, "Notices/" + notice.relative_to(cache).as_posix())
        archive.writestr("stand-two-bay.json", recipe)
        archive.writestr("Open-Grandstand.cmd", '@echo off\r\nstart "" "%~dp0AI-RTS-Spectator.exe" --stand --stand-recipe "%~dp0stand-two-bay.json"\r\n')
    recipe_path = output / "stand-two-bay.json"
    recipe_path.write_bytes(recipe)
    with (output / "SHA256SUMS").open("w", encoding="ascii", newline="\n") as hashes:
        for path in [zip_path, tarball, recipe_path]:
            with path.open("rb") as stream:
                hashes.write(f"{file_digest(stream, 'sha256').hexdigest()}  {path.name}\n")


if __name__ == "__main__":
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("project", type=Path, nargs="?")
    parser.add_argument("--bundle-player", type=Path, help="Bundle the already built player with the installed tarball")
    args = parser.parse_args()
    if args.bundle_player:
        if not args.project:
            parser.error("--bundle-player needs a project")
        path, = args.output.glob("org.materiallab.destruction-*.tgz")
        if (args.project / "Packages" / path.name).read_bytes() != path.read_bytes():
            raise ValueError("Tarball differs from installed version")
        bundle(args.output, args.project, args.bundle_player, path)
    else:
        path = pack(args.output)
        if args.project:
            install(path, args.project)
        print(path)

# Building the grandstand

The grandstand is an editable sample in the Six Chapters player. Start with the two-bay model and press one wall face twice. The first press offsets its brick courses. The second releases local pieces. Click the brick jacket on a post to expose its distinct core. The fixed camera also shows the shared floor and its guards.

## Try a different layout

Extract the Windows release and double-click `Open-Grandstand.cmd`. Edit the adjacent `stand-two-bay.json` and relaunch to load your own layout. The onscreen controls can add a bay, move the staircase or build the 85 m model. Changing the layout begins with intact material.

| Recipe field | Meaning |
| --- | --- |
| `bays` | Number of complete bays, 2 through 16. The measured release examples use 2, 3 and 10. |
| `stairBay` | Zero-based index. Moves the stair flight, lower terrace opening and gap in the front guard together. |
| `pitch` | Bay width in model units, 3 through 8. |
| `metersPerUnit` | Drawing scale for the reported length. Ten bays at the supplied settings span 85 m. |
| `brickStyle` | Surface palette: `clay`, `kiln` or `lime`. |
| `fracture` | One of A, B, C or D. Chooses the shape of the matching fracture faces. |
| `pressure` | Use `stepped` for wall response C, the stepped deformation with a retained dent. |

The load-bearing toy frame uses one shared beam at each common edge. The brick jackets and infill, slab-mounted rails and frame participate in interaction. The stair flight, stepped viewing terraces and gabled arcade are architectural dressing. The demonstration uses local toy rules. Character navigation, match adjudication and touch-device measurements are outside this release.

## Rebuild on Windows

The tested editor version is Unity 7000.0.0a20 with URP 17.7.0. Install the Windows build target and Python 3.11 or later. From a checkout in PowerShell, choose an empty output directory.

```powershell
python package_workbench.py Output/release Output/ExampleProject
$env:LAB_GAME_OUTPUT = "$PWD/Output/Windows"
unity run "$PWD/Output/ExampleProject" --no-tail --timeout 900 -- -executeMethod LabGameBuild.Windows
python package_workbench.py Output/release Output/ExampleProject --bundle-player Output/Windows
```

The installer copies the package into the new host's Packages folder and imports all three samples with their original asset GUIDs. In an existing Unity project use Package Manager → Install package from tarball. Import **Material Workbench**, **Modular Campus**, then **Six Chapters and Grandstand**. Open the imported `MaterialLabGame.unity` scene and use the included `MaterialLabPipeline.asset` as the active URP asset. Open the chapter menu to enter the grandstand.

To repeat the standalone measurements, run the executable with `--stand --stand-proof <empty-output-directory>` at 1440 × 900. The player writes an image per configuration and reports the median and 95th percentile of 90 frame intervals. For the six-chapter interaction checks run it with `--lab-proof <another-empty-directory>`. Both runs exit automatically. Pass `-logFile <output-directory>/player.log` to the six-chapter player and run `python audit_chapters.py <output-directory> <player-directory>` with Pillow installed to check its screenshots, interaction events and binary fingerprints. Normal play uses a separate save.

## Measured authoring trial

The starting package supplied the individual fracture meshes, brick jacket, parking-deck support model and the rail. A clean host consumed the distribution tarball. One GPT-6.2 Astra Responses API call produced the initial C# for the approach, stairs, arcade and scale figures from a written visual brief. It used 1,347 input and 15,922 output tokens and took 244 seconds. Package assembly, integration, camera composition and player verification took additional work. The finished player builds the geometry locally.

| Configuration | Actual posts / girders / slabs | Rebuild | Median / p95 frame interval |
| --- | --- | --- | --- |
| Two bays | 6 / 7 / 2 | 201 ms | 6.1 / 12.1 ms |
| Add a third bay | 8 / 10 / 3 | 199 ms | 6.1 / 12.1 ms |
| Move the stair one bay | 8 / 10 / 3 | 211 ms | 6.1 / 6.1 ms |
| Ten bays / 85 m | 22 / 31 / 10 | 623 ms | 18.2 / 42.4 ms |

The stair sill moved 4.8 model units in the three-bay layout. At both positions it met the pavement at deck height and the front guard left a gap. The checks use actual collider bounds and the generated tread position. During the two-bay test a screen-space click left a dent with the bricks attached. A second click opened the local wall, and a separate click removed outer brick from a column.

The first consumer needed two reusable changes: a one-row frame that accepts ten bays and an adapter that applies stepped pressure to the selected fracture mesh. After that, adding a bay and moving the stairs took only changes to recipe fields. Ten bays instantiate 5,947 renderers and 5,831 colliders. The timing sample covers a stationary building on a Windows RTX 3070 Ti Laptop at 1440 × 900. Future work can batch intact brick faces and preserve the per-piece geometry for picking and release.

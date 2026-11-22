# Material Lab · 材料破坏工作台

Material Lab explores how to build game scenes that remember where they broke. Our aim is to let an artist or a coding agent describe an object's materials and connections, then play back the result in Unity. In a campus equipment hall, that means a hole can grow wide enough for a person while the wall around it still holds together and the gallery above stays level.

**Playable today.** The first release contains three small material experiments. [Download the v0.3.0 Windows demo or Unity package](https://github.com/li1fang/material-lab/releases/tag/v0.3.0).

Grab a striped ribbon, tear it halfway, let go, then pick it up again. The loose edges relax and the tear stays where you left it. The other two specimens let you bend a green branch until the fibres separate and chip a glazed saucer by dropping a weight on either rim.

![Striped ribbon held in the clamp, with a close-up of the starter cut](media/ribbon-workbench.jpg)

### From the workbench to a campus

| What you can try in v0.3.0 | How we would use that behaviour |
| --- | --- |
| Bend the green branch until some fibres give way. The ends can still be connected. | A tracked toy vehicle could push a campus fence rail flat while one end stays attached to its post. A window frame could bend and keep its shape after the impact. |
| Chip the left or right rim of the saucer. The missing edge and the loose chip fit together. | Glass could leave a local gap inside that bent frame. A plastered wall could lose a patch and expose the material behind its painted face. |
| Tear the ribbon halfway, release it and pull again. The second tear starts from the first. | A cracked support could hold a gallery in a tilted position. A later event could release the next connection and let one section settle against a surviving support. |

**A measured example.** A separate AI-RTS Windows prototype tested one equipment-hall bay with a 64-piece infill wall and an independently supported gallery. In one recorded run, an opening with 18 pieces released admitted the character gauge and stopped the tracked vehicle. Widening that same opening released 53 pieces and let the vehicle through. Removing the entire infill left the gallery in place. In the support test the gallery tilted, then dropped locally while the neighbouring bay stayed put. The shape and location of the gap mattered. The piece counts describe this particular run. [Player measurements and input events](evidence/equipment-hall-bay.json)

**What AI contributes.** Two OpenAI-generated parts sheets guided the appearance of the equipment hall and repair shed. The prototype built their geometry from dimensions and configuration. A coding agent could change a part, build with Unity CLI, replay the same inputs and check which opening actually admitted the vehicle. The finer wall experiment reused the existing art and ran locally. We want to measure how much of that workflow transfers to the next building. Fences, composite windows and collapse spreading across several bays are design examples for that work.

### Play or build

- **Windows x64 demo** — Extract the entire Windows ZIP and run `AI-RTS-Spectator.exe`. Drag the upper right corner upward, release, and grab either loose end. Pass `--ceramic` or `--wood` to start the other benches. The in-game controls are in Chinese.
- **Unity package** — In a Universal Render Pipeline project, install `org.materiallab.destruction-0.3.0.tgz` from Package Manager, import **Material Workbench** from Samples and open `RibbonWorkbench.unity`. For wood and ceramic, open `Workbench.unity`. The runtime models live in `Package/Runtime`. The interaction, mesh presentation and shaders live in `Package/Samples~/BranchLab`.
- **From source** — On Windows with PowerShell 7, Unity Editor **7000.0.0a20** with Windows Standalone support, and Unity CLI **1.0.0-beta.13** on PATH, run the following in the extracted source directory. The host pins URP **17.7.0**. The first run needs access to the Unity packages listed in the host manifest.

```powershell
./Run.ps1 -Action Build
./Run.ps1 -Action Test
$env:MATERIAL_LAB_RIBBON_TAKE = (Resolve-Path ./examples/ribbon-two-grips.json).Path
./Run.ps1 -Action Verify
Select-String RIBBON_REPLAY_OK ./Output/Verify.log
```

The included recording came from two drags in the Windows player. The first release left 30 broken yarn bundles (22.5 cm beyond the starter cut). The second started at 30 and ended at 46 (34.5 cm). The expected log entry is `RIBBON_REPLAY_OK grips=2 broken=46`. Replay checks the material state at each release. The sample records the peak opening of each grip, so intermediate pointer motion is outside the recording.

### Scope and license

These three procedural specimens have deliberately narrow material models. The ribbon follows one longitudinal tear path, the branch uses a clamped main stem and the saucer chips at the rim. Windows desktop is the tested target. The parameters are tuned for interactive visual behaviour. The original C# code, shaders and procedural specimens use the [MIT license](Package/LICENSE.md). [Dependency and asset notices](NOTICES.md)

## 中文试玩与开发

我们想用这套工作台建一座可破坏的游戏校园。设计一扇窗时，可以给金属框和玻璃指定不同的反应：窗框像鲜枝一样弯折后仍连着，玻璃像瓷盘一样局部崩落，碎片能和缺口对上。换成校园栅栏，设想中的效果是履带车压过后，栏杆倒伏，一端仍挂在立柱上。布带则演示了怎样记住损伤：撕到一半松手，下一次还能沿着旧破口继续。**v0.3.0 里能亲手试的是鲜枝、瓷盘和布带。** [Windows 试玩与 Unity 包](https://github.com/li1fang/material-lab/releases/tag/v0.3.0)

**建筑已经验证到哪一步？** 在独立的 AI-RTS 器材库试玩中，一个开间的填充墙分成 64 块。在一次实测中，脱落 18 块形成的洞能过人，猫猫车停在洞口；沿洞边继续扩大到脱落 53 块，车也能过。整面填充墙卸掉，二层外廊仍然水平。单独改变外廊支撑，才看到这一段先歪挂、后落下，隔壁开间保持原位。记录保留了每一步缺失的墙块编号、人物与原车的通行结果，以及两段楼面的坐标。[实机记录](evidence/equipment-hall-bay.json)

**AI 怎样参与建造？** 器材库和维修棚先用 OpenAI 生成分件参考图，再按尺寸和配置搭建。开发者或 AI 可以改一处构件，用 Unity CLI 构建，回放同一组输入，检查缺口是否真的能过车。细化墙体时沿用了已有美术。下一步值得试的是，这套做法能否低成本地复用到栅栏、门窗，乃至多个开间，让坠落的楼面遇到仍能承托它的构件时停下来。

v0.3 从条纹织带开始。双击 [Open-Ribbon.cmd](Open-Ribbon.cmd)，抓住右上角向上拖，或抓右下角向下拖。松手后可以从同一端再抓，比较两次之间的裂口长度。「换新布带」开始新一轮。最新操作写到 `Application.persistentDataPath/ribbon-take.json`，也可以传入 `--ribbon-log <绝对路径>`。将环境变量 `MATERIAL_LAB_RIBBON_TAKE` 指向记录，再运行 Verify，即可核对每一次松手时断开的纱束数。

双击 `Open-Ceramic.cmd` 进入 v0.2 陶碟台。先在同一侧点「轻碰」和「重击」，点「反面看」观察釉层下露出的陶胎。点「换新碟」再换到右缘可以比较缺口的位置。「落枝击碟」和「把碟移开」从同一根完好的木枝开始。随时可切到「木枝台」亲手压折。

用 Unity 7000.0.0a20 和 Unity CLI 1.0.0-beta.13 构建。运行时与可导入样例在 [Package](Package/README.md)。在此目录用 PowerShell 7 执行：

```powershell
./Run.ps1 -Action Build
./Run.ps1 -Action Test
./Run.ps1 -Action Verify
```

输出默认在 `Output`。可用 `-Output` 指定独立目录，三步使用同一目录；Build 输出 `Windows/AI-RTS-Spectator.exe`，Test 输出 NUnit XML，Verify 输出 `simulation.json`。Verify 默认跑向下深压，可设置环境变量 `MATERIAL_LAB_TRIAL` 指向自定义案例文件。要在编辑器中操作，先执行 Prepare，再用 `unity open` 打开生成的 ExampleProject，一次只运行一个该工程实例。

播放器参数 `--ceramic-proof <空目录>` 录制轻碰左缘、重击左缘、重击右缘、落枝击碟、移开陶碟五案。每案包含播放器输出的逐帧 JPEG 和接触记录 `result.json`。设置 `MATERIAL_LAB_CERAMIC_PROOF` 为五案目录的绝对路径，再运行 Verify，可在批处理中核对实际接触、部位、时序，并从冲量重算断边 ID。一次重放五组需要先跑完一次取帧。

以 `--wood` 启动可直接进入鲜枝台。`--branch-proof <空目录>` 采集浅压、向下深压、向上深压和首裂即脱落的逐帧 JPEG 和 `result.json`，结束时自动关闭。附加 `--branch-trial <JSON 文件>` 可只取证一个案例。单独使用 `--branch-trial` 可在窗口内播放该文件。`--branch-benchmark <输出 JSON 路径>` 在一次预热后播放三次，记录无取帧时的 P95 和断裂时的峰值。

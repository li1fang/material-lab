# Material Destruction · Unity 材料工作台

## Modular Campus

从当前开发源码安装时，在 Unity Package Manager 选择 **Install package from git URL**，填写 `https://github.com/li1fang/material-lab.git?path=/Package`。也可以用 **Install package from tarball** 选择本地构建的 `.tgz`。

在 Unity 7 URP 工程的 Package Manager 导入 **Modular Campus** 样例，打开样例根目录的 `Campus.unity`。输入使用 Unity 的 Input Manager，工程可选 Input Manager 或 Both。本样例的砌块、金属栅栏、停车楼和履带试车均由代码绘制，运行时无需模型服务。

- **围墙**默认两边长 9 和 6 个场景单位，目标柱距 3。点某一跨或点「选下一跨」定位操作。V 驱动车辆按固定路线前压和退回。「换铁栅栏」从完好状态开始同位置对照。点墙面可拆局部砖片，点共用角柱观察与两侧的连接。
- **延长墙线**把第一段改成 10.4 单位，末跨为 1.4。砖排、顶砖、栏杆和碰撞体按跨宽生成。样例里的 `Resources/campus-dimensions.json` 控制长度、柱距和停车楼行列。
- **停车楼**含地面与架空停车层。点击柱体可改变支撑状态，或用「运行连续失撑对照」比较两个规模下的同一组游戏状态。共边楼面可以挂靠；失去相邻支撑后，后续楼面会延迟释放。此处使用离散连接规则和程序化姿态。
- **回看**用空格重放，Z 撤回，R 复位，拖动底部时间线回看。右键拖动旋转机位，滚轮缩放。

从仓库根目录运行 `./Run.ps1 -Action Campus` 构建 Windows 试玩，程序在 `Output/CampusWindows/AI-RTS-Spectator.exe`。带 `--campus-proof --output <目录>` 可复验默认配置并记录帧时。运行 `python package_release.py --campus` 会生成校园 Windows ZIP、源码 ZIP 与 UPM 安装包。

## Material Workbench

导入 Material Workbench 样例后，打开 `RibbonWorkbench.unity` 即可亲手撕布。左端夹住，右端有预开口。抓住上、下任一布角向外拖动，裂口沿织向逐束推进。停手、卸载和再次抓取都会保留已撕开的长度。近景跟随裂尖，可以观察条纹回落和断边的毛纱。目前的样件模拟一条沿织向发展的裂口。使用位移驱动的纱束阈值，未做实物力学校准。

每次松手后，`Application.persistentDataPath/ribbon-take.json` 记录本条布带各次抓取的方向、峰值张开量（米）和断束数。可在连接的编辑器上运行 `unity command destruction ribbon --project-path <工程> --file <记录文件>`。复算验证各次抓取的起止裂口，不包含手势动画。

打开 Package Manager，从磁盘添加解包后的 `package.json` 或安装发行的 `.tgz`。在 Samples 里导入 **Material Workbench**，打开导入目录的 `Workbench.unity`，按 Play。样例使用 Unity 7 URP、Physics、IMGUI、纹理编码和触摸或鼠标输入。程序网格、材质和样例源码均随包提供。

陶碟从左缘开始，用「轻碰」和「重击」比较同一枚锤球在两个高度释放的效果。换边可试右缘；换新碟可清除损伤。「反面看」从对面观察釉层和露出的陶胎。「落枝击碟」用正在脱落的枝梢作碰撞体。「把碟移开」只改陶碟的位置。「木枝台」可以亲手试木材。

碟身固定在桌面，程序化薄壳的外沿分成 36 个可分离区域。PhysX 接触流形提供总冲量，样例用接触位置和冲量方向估算邻近区域收到的载荷。碟沿缺失时即时更新显示网格和碰撞网格，相邻的脱落区合并为一块有厚度的刚体碎片。当前覆盖边缘崩口，材料参数为视觉试验设定。C# 接口 `CeramicRim.Strike(localPoint, localDirection, impulseNs)` 返回新脱离的分区 ID。接触点在以米计的陶碟局部空间中，冲量单位是 N·s。单独重算材料响应无需开启物理场景。

在枝条上选择作用点，上下拖动，松手卸载。按钮提供浅压、向下深压、向上深压，以及相同输入下首裂即脱落的对照。按 R 换新枝，F5 保存操作，F9 按物理步重放。记录位于 `Application.persistentDataPath/branch-take.json`。已断纤维在卸载后仍然保留。

## 自己生成一个样件

场景中的 BranchLab 组件公开 `profile`。长度和根部半径使用米；`sweep` 控制原始枝形的弯曲，`toughness` 缩放失效阈值，`seed` 改变周向差异。也可以在 C# 创建 `new BranchModel(profile)`，每 1/60 秒调用 `Step(drive, contact)`。正负 drive 使枝条向上或向下弯，范围是 −1.6 到 1.6 的归一化载荷；contact 是从夹持端起算的长度比例，范围 0.12 到 1。读取 `Centers`、`IsBroken` 和 `Events` 可接到自己的显示与交互。`BranchSkin.Update(model, startLink, endLink)` 生成有树皮、叶片和断面的网格。当前生成器支持单一夹持主枝的参数化变体。

同一作用点离夹持端越远，产生的弯矩越大。主枝用串联弯曲关节近似，每个潜在断面有 16 根周向纤维。拉伸侧先积累损伤，撤掉载荷后保存损伤。纤维逐渐失效让剩余连接承担更多载荷，全部失效后把脱落段交给 Unity 刚体。该模型用于实时视觉，载荷参数尚未作实物标定。固定步长使材料事件可逐条复验，碎片落地姿态要用物理容差比较。

## Unity CLI

Pipeline 1.0.1 可在打开的样例工程里发现下列命令。将 `$project` 设为该工程的绝对路径，以 `--file` 传入编辑器所在机器上的完整文件路径，长录制也能重放。

```powershell
unity command destruction inspect --project-path $project
$case = (Resolve-Path ./deep.json).Path
unity command destruction run --project-path $project --file $case
unity command destruction ceramic --project-path $project --file <播放器输出的result.json绝对路径>
```

示例案例文件：

```json
{"version":1,"peak":-1.24,"contact":0.83,"rampTicks":108,"holdTicks":40,"releaseTicks":100}
```

脚本运行通过 `BranchTrial.Read(...).Simulate()`，返回断面、断裂方向、每根纤维的稳定序号和物理步。交互场景也调用相同的 `BranchModel.Step`。可仅安装运行时代码，命令程序集在宿主安装 Pipeline 时启用。包测试需要宿主在 `Packages/manifest.json` 的 `testables` 列出 `org.materiallab.destruction`。

## 来源与许可

源码、程序生成网格和材质采用 MIT，见 LICENSE.md。Unity 编辑器和依赖包采用各自发行许可。纤维方向和渐进失效的研究参考为 Li 等的 *Stressful Tree Modeling: Breaking Branches with Strands*，本包独立实现简化弯矩与损伤模型。[作者论文和演示](https://storage.googleapis.com/pirk.io/projects/stressful/index.html)

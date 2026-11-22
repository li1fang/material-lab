# Material Lab notices

The Modular Campus sample generates the maintenance crawler, brickwork, rails, parking decks, paint and ground from original code. Its screenshots and benchmark record come from the standalone Unity player.

The Material Destruction contributors provide the original C# implementation, shaders, procedural mesh generators and Unity sample scenes under the MIT license in `Package/LICENSE.md`. The README screenshot was captured from the Windows sample. The example JSON contains numerical grip measurements recorded from the same workbench.

The host resolves Unity packages using `Host/manifest.json` and `Host/packages-lock.json`. URP provides rendering, Unity Physics provides rigid-body contact, Unity Pipeline exposes the editor CLI commands, and Unity Test Framework runs the package tests. These dependencies keep their own licenses. The Windows demo also contains the Unity Player and its platform runtime libraries. The project MIT license covers the Material Lab contribution.

The sample requests a locally available UI font at runtime. All three specimens and their surface patterns are generated from code. The wood model's research reference is listed in `Package/README.md`.

The Windows ZIP includes the .NET Foundation license distributed with the Unity Editor's x64 CoreCLR runtime and license and notice files found at the top level of the resolved Unity packages. These files are kept under `Notices/` in the ZIP.

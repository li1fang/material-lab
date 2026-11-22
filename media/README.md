# Campus recordings

These clips use the Windows Modular Campus player built with Unity 7000.0.0a20. Each shot uses a fixed camera and the interactive sample's own actions. The player advances two 1/60-second simulation steps for each recorded frame. The labels and side-by-side layouts were added after capture.

| Capture | Input and timing | What to look for |
| --- | --- | --- |
| [Brick and iron](brick-iron-contact.mp4) · [loop](brick-iron-contact.gif) | 0–4 s brick, 4–8 s iron. Both start 26 ticks into the same approach and hold for one second after reversing. | Compare the opening with the rail that stays attached. [End states](boundary-after.jpg) |
| [Parking deck](parking.mp4) | 0–0.83 s intact. The support-loss sequence starts at frame 25 of 120. | Watch the front bays drop in succession and compare the far edge. [Before and after](parking-before-after.jpg) |

The brick and iron footage uses the [revised contact timing](https://github.com/li1fang/material-lab/commit/8c17bc5). Watch the roller pause at the surface before it advances again. [Earlier v0.4.0 footage](https://github.com/li1fang/material-lab/blob/v0.4.0/media/brick-iron.gif)

To capture your own frames from the source checkout, run these commands in PowerShell 7 on Windows. The player writes 120 numbered JPEGs for each of the three shots, checks their final states and exits. The MP4 files play those frames at 30 fps. The README loop uses 15 fps.

```powershell
./Run.ps1 -Action Campus
$player = (Resolve-Path ./Output/CampusWindows/AI-RTS-Spectator.exe).Path
& $player --campus-proof --campus-film --output "$PWD/Output/CampusFilm" -logFile "$PWD/Output/CampusFilm.log" | Out-Null
Select-String MODULAR_PROOF_OK ./Output/CampusFilm.log
```

For the original interactive view, see the [fence](campus-fence.jpg) and [parking deck](campus-parking.jpg) screenshots. The [measurement run](../evidence/modular-campus.json) includes both grid sizes and the separate shared-corner input.

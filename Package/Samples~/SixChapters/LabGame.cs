using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MaterialDestruction.Sample;
using MaterialDestruction.Campus;

public sealed partial class LabGame : MonoBehaviour
{
    [Serializable]
    sealed class Progress
    {
        public int version = 1;
        public int chapter;
        public int completed;
        public string[] snapshots = new string[6];
    }

    static readonly string[] Titles = { "折树枝", "砸瓷盘", "撕布带", "载具过围墙", "降维打击", "建筑生成器" };
    static readonly string[] Notes = {
        "裂口还在，剩下的纤维仍能把两边拉在一起。",
        "钢珠落在不同位置，留下的缺口也不同。",
        "松手后布会回落。裂口留在原处，下一次从那里接着撕。",
        "松脱的砖片让出一条路，缺口够宽时整车才能过去。",
        "每块楼板都已经落定。",
        "把同一套造墙和装配方法带到下一个开间。"
    };
    static readonly Color Paper = new Color32(246, 239, 226, 255);
    static readonly Color Ink = new Color32(30, 50, 47, 255);
    static readonly Color Accent = new Color32(28, 91, 78, 255);
    static readonly Color Soft = new Color32(224, 232, 219, 255);
    const float Width = 1440;
    const float Height = 900;
    const float Head = 104;
    const float Foot = 108;
    Progress progress;
    LabChapter chapter;
    Font font;
    GUIStyle textStyle, noteStyle, titleStyle, buttonStyle;
    Rect canvas;
    float scale;
    int pointerChapter = -1, finger = -1, generation;
    Vector2 pointerDown;
    bool dragged, capturedDrag;
    bool menu;
    bool proof;
    internal bool WallStudy { get; private set; }
    bool traceNative;
    string inputSource;
    string ioNotice, savePath;
    readonly List<string> events = new();

    internal Camera Eye { get; private set; }
    internal string Snapshot => progress.snapshots[progress.chapter];
    internal Rect Canvas => canvas;

    void Start()
    {
        Application.targetFrameRate = 60;
        Input.simulateMouseWithTouches = true;
        string[] arguments = Environment.GetCommandLineArgs();
        if (Array.IndexOf(arguments, "--stand") >= 0 && !StandPlay.ReturnToChapters)
        {
            gameObject.AddComponent<StandPlay>(); enabled = false; return;
        }
        WallStudy = Array.IndexOf(arguments, "--wall-study") >= 0 ||
            File.Exists(Path.Combine(Application.dataPath, "..", "wall-study.txt"));
        int directory = Array.IndexOf(arguments, "--lab-proof");
        int profile = Array.IndexOf(arguments, "--lab-profile");
        proof = directory >= 0 && directory + 1 < arguments.Length;
        traceNative = profile >= 0;
        Application.runInBackground = proof;
        string root = proof ? arguments[directory + 1] : profile >= 0 && profile + 1 < arguments.Length
            ? arguments[profile + 1] : Application.persistentDataPath;
        savePath = Path.Combine(root, "material-lab-game-v1.json");
        Directory.CreateDirectory(Path.GetDirectoryName(savePath));
        progress = WallStudy ? new Progress { chapter = 3 } : ReadProgress();
        font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "PingFang SC", "Noto Sans CJK SC", "Arial" }, 21);
        Eye = new GameObject("Fixed chapter camera").AddComponent<Camera>();
        Eye.transform.SetParent(transform, false);
        Eye.orthographic = true;
        Eye.clearFlags = CameraClearFlags.SolidColor;
        Eye.backgroundColor = new Color32(195, 211, 201, 255);
        Eye.nearClipPlane = .01f;
        Eye.farClipPlane = 160;
        Eye.allowHDR = false;
        var light = new GameObject("Workbench light").AddComponent<Light>();
        light.transform.SetParent(transform, false);
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(46, -33, 0);
        light.intensity = 1.12f;
        light.shadows = LightShadows.Soft;
        Resize();
        Switch(progress.chapter);
        Debug.Log("MATERIAL_LAB_GAME_READY");
        if (proof) StartCoroutine(WallStudy ? VerifyWallStudy(arguments[directory + 1]) : Verify(arguments[directory + 1]));
    }

    Progress ReadProgress()
    {
        try
        {
            if (File.Exists(savePath))
            {
                var value = JsonUtility.FromJson<Progress>(File.ReadAllText(savePath));
                if (value != null && value.version == 1 && value.snapshots?.Length == Titles.Length)
                {
                    value.chapter = Mathf.Clamp(value.chapter, 0, Titles.Length - 1);
                    return value;
                }
            }
        }
        catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
        {
            ioNotice = "暂时无法读取进度";
            Debug.LogWarning(e.Message);
        }
        return new Progress();
    }

    void Persist()
    {
        if (WallStudy) return;
        try
        {
            var temp = savePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(progress, true));
            if (File.Exists(savePath)) File.Replace(temp, savePath, null);
            else File.Move(temp, savePath);
            ioNotice = null;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            ioNotice = "暂时无法保存进度";
            Debug.LogWarning(e.Message);
        }
    }

    internal void SaveSnapshot(string json)
    {
        progress.snapshots[progress.chapter] = json;
        Persist();
    }

    internal void Log(string message)
    {
        var entry = $"{progress.chapter + 1} {message}";
        if (proof) events.Add(entry);
        Debug.Log("LAB " + entry);
    }

    void Switch(int index, bool restart = false)
    {
        if (chapter)
        {
            CancelGesture();
            chapter.Suspend();
            chapter.gameObject.SetActive(false);
            Destroy(chapter.gameObject);
        }
        generation++;
        pointerChapter = -1;
        progress.chapter = index;
        if (restart) progress.snapshots[index] = null;
        menu = false;
        var stage = new GameObject(Titles[index]);
        stage.transform.SetParent(transform, false);
        chapter = index switch
        {
            0 => stage.AddComponent<WoodChapter>(),
            1 => stage.AddComponent<CeramicChapter>(),
            2 => stage.AddComponent<RibbonChapter>(),
            3 => stage.AddComponent<WallChapter>(),
            4 => stage.AddComponent<ParkingChapter>(),
            _ => stage.AddComponent<BuildingChapter>()
        };
        Resize();
        if (index == 5) SetModelWidth(955);
        chapter.Initialize(this);
        Persist();
        Log("entered");
        if (traceNative && chapter is WoodChapter wood)
            Debug.Log(FormattableString.Invariant($"LAB_NATIVE_TARGET {wood.TipScreen.x:F1} {wood.TipScreen.y:F1} {Screen.width} {Screen.height}"));
        if (traceNative && chapter is RibbonChapter ribbon)
        {
            var top = ribbon.EarScreen(1);
            var bottom = ribbon.EarScreen(-1);
            Debug.Log(FormattableString.Invariant($"LAB_NATIVE_EARS {top.x:F1} {top.y:F1} {bottom.x:F1} {bottom.y:F1} {Screen.width} {Screen.height}"));
        }
    }

    void Resize()
    {
        scale = Mathf.Min(Screen.width / Width, Screen.height / Height);
        canvas = new Rect((Screen.width - Width * scale) / 2, (Screen.height - Height * scale) / 2,
            Width * scale, Height * scale);
        Eye.pixelRect = new Rect(canvas.x, canvas.y + Foot * scale, canvas.width, (Height - Foot - Head) * scale);
    }

    internal void SetModelWidth(float logicalWidth)
    {
        var rect = Eye.pixelRect;
        rect.width = logicalWidth * scale;
        Eye.pixelRect = rect;
    }

    void Update()
    {
        Resize();
        if (progress.chapter == 5) SetModelWidth(955);
        int flag = 1 << progress.chapter;
        if (chapter.Complete && (progress.completed & flag) == 0)
        {
            progress.completed |= flag;
            Persist();
            Log("completed");
        }
        if (proof || menu) return;
        if (Input.touchCount > 0)
        {
            foreach (var touch in Input.touches)
            {
                if (touch.phase == TouchPhase.Began && finger < 0)
                {
                    finger = touch.fingerId;
                    inputSource = "touch";
                    BeginTap(touch.position);
                }
                if (touch.fingerId != finger) continue;
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    if (touch.phase == TouchPhase.Ended) EndTap(touch.position);
                    else CancelGesture();
                    pointerChapter = finger = -1;
                }
                else if (touch.phase == TouchPhase.Moved) MovePointer(touch.position);
            }
        }
        else if (finger < 0)
        {
            if (Input.GetMouseButtonDown(0))
            {
                inputSource = "mouse";
                BeginTap(Input.mousePosition);
            }
            if (Input.GetMouseButtonUp(0)) EndTap(Input.mousePosition);
            else if (Input.GetMouseButton(0)) MovePointer(Input.mousePosition);
        }
    }

    void BeginTap(Vector2 point)
    {
        pointerChapter = generation;
        pointerDown = point;
        dragged = capturedDrag = false;
    }

    void MovePointer(Vector2 point)
    {
        if (pointerChapter != generation || !Eye.pixelRect.Contains(pointerDown)) return;
        if (!dragged && Vector2.Distance(point, pointerDown) > 9 * scale)
        {
            dragged = true;
            capturedDrag = chapter.StartDrag(pointerDown);
        }
        if (capturedDrag) chapter.MoveDrag(point);
    }

    void CancelGesture()
    {
        if (capturedDrag) chapter.EndDrag();
        dragged = capturedDrag = false;
        pointerChapter = -1;
    }

    void EndTap(Vector2 point)
    {
        MovePointer(point);
        bool direct = !capturedDrag && pointerChapter == generation && Vector2.Distance(point, pointerDown) <= 18 * scale
            && !(dragged && chapter is RibbonChapter);
        if (capturedDrag && traceNative) Debug.Log("LAB_NATIVE_DRAG " + inputSource);
        CancelGesture();
        if (direct && Eye.pixelRect.Contains(pointerDown) && Eye.pixelRect.Contains(point) && chapter.Tap(point) && traceNative)
            Debug.Log("LAB_NATIVE_ACCEPT " + inputSource);
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused || proof) return;
        CancelGesture();
        finger = -1;
        if (chapter) chapter.Suspend();
    }

    void OnApplicationQuit()
    {
        if (chapter) chapter.Suspend();
    }

    static GUIStyle Style(Font font, int size, Color color, FontStyle weight)
    {
        return new GUIStyle(GUI.skin.label) {
            font = font, fontSize = size, fontStyle = weight, normal = { textColor = color },
            alignment = TextAnchor.MiddleLeft
        };
    }

    internal static void Fill(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    internal bool ActionButton(string label)
    {
        var rect = GUILayoutUtility.GetRect(new GUIContent(label), buttonStyle, GUILayout.Height(49), GUILayout.ExpandWidth(false));
        return Button(rect, label);
    }

    internal void Cue(Vector2 screen, string label, Vector2 offset)
    {
        if (!Eye.pixelRect.Contains(screen)) return;
        var anchor = new Vector2((screen.x - canvas.x) / scale,
            (Screen.height - screen.y - canvas.y) / scale) + offset;
        float width = 20 + noteStyle.CalcSize(new GUIContent(label)).x;
        var box = new Rect(Mathf.Clamp(anchor.x, 8, Width - width - 8),
            Mathf.Clamp(anchor.y, Head + 4, Height - Foot - 34), width, 29);
        Fill(box, Paper);
        GUI.Label(new Rect(box.x + 9, box.y, box.width - 14, box.height), label, noteStyle);
    }

    bool Button(Rect rect, string label, bool primary = false)
    {
        var color = primary ? Accent : Soft;
        if (rect.Contains(Event.current.mousePosition)) color = Color.Lerp(color, Ink, .09f);
        if (!GUI.enabled) color = Color.Lerp(color, Paper, .62f);
        Fill(rect, color);
        buttonStyle.normal.textColor = primary ? Color.white : Ink;
        return GUI.Button(rect, label, buttonStyle);
    }

    void OnGUI()
    {
        if (!chapter) return;
        textStyle ??= Style(font, 20, Ink, FontStyle.Normal);
        noteStyle ??= Style(font, 17, Accent, FontStyle.Bold);
        titleStyle ??= Style(font, 33, Ink, FontStyle.Bold);
        buttonStyle ??= new GUIStyle(Style(font, 20, Ink, FontStyle.Bold)) {
            alignment = TextAnchor.MiddleCenter, padding = new RectOffset(19, 19, 4, 4)
        };
        var savedMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3(canvas.x, canvas.y, 0), Quaternion.identity,
            new Vector3(scale, scale, 1));
        Fill(new Rect(-Width, -Height, Width * 3, Height), Ink);
        Fill(new Rect(-Width, Height, Width * 3, Height), Ink);
        Fill(new Rect(-Width, 0, Width, Height), Ink);
        Fill(new Rect(Width, 0, Width, Height), Ink);
        Fill(new Rect(0, 0, Width, Head), Paper);
        Fill(new Rect(0, Height - Foot, Width, Foot), Paper);
        int current = progress.chapter;
        GUI.Label(new Rect(34, 13, 390, 26), WallStudy ? "MATERIAL LAB     /     第四关材质试验" : $"MATERIAL LAB     {current + 1} / {Titles.Length}", noteStyle);
        GUI.Label(new Rect(34, 36, 480, 51), WallStudy ? "砖墙受压" : Titles[current], titleStyle);
        if (!WallStudy && Button(new Rect(1240, 30, 164, 47), menu ? "返回当前关" : "章节"))
        {
            menu = !menu;
            pointerChapter = -1;
            if (menu) chapter.Suspend();
        }
        if (menu)
        {
            DrawMenu();
        }
        else
        {
            chapter.Overlay();
            string hint = ioNotice ?? (chapter.Complete && current >= 3 && !WallStudy ? Notes[current] : chapter.Hint);
            GUI.Label(new Rect(34, Height - Foot + 5, 1320, 32), hint, textStyle);
            GUILayout.BeginArea(new Rect(34, Height - Foot + 43, 860, 56));
            GUILayout.BeginHorizontal();
            chapter.Panel();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
            if (Button(new Rect(1076, 835, 138, 49), WallStudy ? "三墙复原" : "重新开始")) Switch(current, true);
            if (!WallStudy && (chapter.Complete || IsUnlocked(current + 1)) && current < Titles.Length - 1 &&
                Button(new Rect(1234, 835, 170, 49), "下一关  →", true)) Switch(current + 1);
            if (chapter.Complete && current == Titles.Length - 1 &&
                Button(new Rect(1234, 835, 170, 49), "已通关  ✓", true)) menu = true;
        }
        GUI.matrix = savedMatrix;
    }

    bool IsUnlocked(int index) => index == 0 || index < Titles.Length && (progress.completed & (1 << (index - 1))) != 0;

    void DrawMenu()
    {
        Fill(new Rect(0, Head, Width, Height - Head), Paper);
        GUI.Label(new Rect(165, 152, 400, 55), "章节", titleStyle);
        for (int index = 0; index < Titles.Length; index++)
        {
            GUI.enabled = IsUnlocked(index);
            var rect = new Rect(165 + index % 2 * 560, 240 + index / 2 * 95, 527, 68);
            string mark = (progress.completed & (1 << index)) != 0 ? "   ✓" : "";
            if (Button(rect, $"{index + 1}   {Titles[index]}{mark}", index == progress.chapter))
            {
                if (index == progress.chapter) menu = false;
                else Switch(index);
            }
        }
        GUI.enabled = true;
        if (Button(new Rect(165, 560, 527, 68), "体育场 · 看台工坊"))
        {
            chapter.Suspend();
            gameObject.SetActive(false);
            new GameObject("Grandstand").AddComponent<StandPlay>();
        }
    }
}

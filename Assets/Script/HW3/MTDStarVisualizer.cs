using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// MTDStarVisualizer (2D) — Unity 2D visualizer for MT-D* Lite with three playback modes.
///
/// SETUP (no prefabs, no assets needed):
///   1. File > New Scene (2D template)
///   2. Create empty GameObject "MTDStar"
///   3. Attach MTDStarVisualizer + MTDStarUI to it
///   4. Press Play
///
/// Controls:
///   R key or Run button    → SimState.Running  (auto-advance every stepInterval)
///   P / Space or Pause     → SimState.Paused   (freeze current frame)
///   N key or Step button   → SimState.Stepping (advance ONE sub-step per press)
///   Click a cell           → toggle wall (works in all states except Done)
/// </summary>
public class MTDStarVisualizer : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Grid")]
    public int gridWidth = 10;
    public int gridHeight = 10;
    public float cellSize = 1.0f;
    public float cellGap = 0.05f;

    [Header("Simulation")]
    public float stepInterval = 0.6f;   // seconds between full iterations in Run mode
    public float subStepDelay = 0.18f;  // seconds between sub-steps in Run mode
    public bool randomTarget = true;

    [Header("Start Positions")]
    public Vector2Int hunterStart = new Vector2Int(0, 0);
    public Vector2Int targetStart = new Vector2Int(9, 9);

    // ── Simulation state machine ──────────────────────────────────────────────

    public enum SimState { Idle, Running, Paused, Stepping, Done }
    public SimState State { get; private set; } = SimState.Idle;

    // ── Public readable info (used by MTDStarUI) ──────────────────────────────

    public string CurrentPhaseLabel { get; private set; } = "—";
    public string CurrentStepLabel { get; private set; } = "—";
    public int SubStepIndex { get; private set; }
    public int SubStepTotal { get; private set; }

    // ── Internals ─────────────────────────────────────────────────────────────

    private MTDStarLite _algo;
    private SpriteRenderer[,] _cellSR;
    private BoxCollider2D[,] _cellCol;
    private SpriteRenderer _hunterSR;
    private SpriteRenderer _targetSR;

    // Sub-step queue for stepping / animated run
    private List<MTDStarLite.SubStep> _subSteps;
    private int _subStepCursor;
    private bool _stepRequested;   // set by PressStep(), consumed in Update

    private Sprite _squareSprite;
    private Sprite _circleSprite;
    private Coroutine _runCoroutine;

    // ── Colours ───────────────────────────────────────────────────────────────

    private static readonly Color ColEmpty = new Color(0.18f, 0.18f, 0.24f);
    private static readonly Color ColWall = new Color(0.07f, 0.07f, 0.09f);
    private static readonly Color ColOpen = new Color(0.22f, 0.30f, 0.72f);
    private static readonly Color ColClosed = new Color(0.14f, 0.48f, 0.28f);
    private static readonly Color ColDeleted = new Color(0.52f, 0.14f, 0.62f);
    private static readonly Color ColHunter = new Color(0.10f, 0.95f, 0.35f);
    private static readonly Color ColTarget = new Color(0.95f, 0.20f, 0.15f);
    private static readonly Color ColHighlight = new Color(1.00f, 0.85f, 0.10f); // current sub-step cell

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Start()
    {
        _squareSprite = MakeSquareSprite();
        _circleSprite = MakeCircleSprite();
        BuildScene();
        InitAlgorithm();
        // Start in Paused so player can look at the grid first
        State = SimState.Paused;
        PrepareNextIteration();
    }

    private void Update()
    {
        HandleKeyboard();
        HandleClick();
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    private void HandleKeyboard()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.rKey.wasPressedThisFrame) PressRun();
        if (Keyboard.current.pKey.wasPressedThisFrame ||
            Keyboard.current.spaceKey.wasPressedThisFrame) PressPause();
        if (Keyboard.current.nKey.wasPressedThisFrame) PressStepIteration();
    }

    // ── Scene construction ────────────────────────────────────────────────────

    private void BuildScene()
    {
        _cellSR = new SpriteRenderer[gridWidth, gridHeight];
        _cellCol = new BoxCollider2D[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
            {
                var go = new GameObject($"Cell_{x}_{y}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = CellWorldPos(x, y);
                go.transform.localScale = Vector3.one * (cellSize - cellGap);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _squareSprite; sr.color = ColEmpty; sr.sortingOrder = 0;

                var col = go.AddComponent<BoxCollider2D>();
                col.size = Vector2.one;

                _cellSR[x, y] = sr;
                _cellCol[x, y] = col;
            }

        _hunterSR = CreateMarker("Hunter", ColHunter, sortOrder: 2);
        _targetSR = CreateMarker("Target", ColTarget, sortOrder: 2);
        FitCamera();
    }

    private SpriteRenderer CreateMarker(string name, Color col, int sortOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * (cellSize * 0.55f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _circleSprite; sr.color = col; sr.sortingOrder = sortOrder;
        return sr;
    }

    private void FitCamera()
    {
        if (Camera.main == null) return;
        float cx = (gridWidth - 1) * cellSize * 0.5f;
        float cy = (gridHeight - 1) * cellSize * 0.5f;
        Camera.main.transform.position = new Vector3(cx, cy, -10f);
        Camera.main.orthographic = true;
        float margin = cellSize * 1.2f;
        float vertHalf = gridHeight * cellSize * 0.5f + margin;
        float horzHalf = gridWidth * cellSize * 0.5f + margin;
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
        Camera.main.orthographicSize = Mathf.Max(vertHalf, horzHalf / aspect);
    }

    // ── Algorithm init ────────────────────────────────────────────────────────

    private void InitAlgorithm()
    {
        _algo = new MTDStarLite(gridWidth, gridHeight);
        _algo.SetHunter(hunterStart);
        _algo.SetTarget(targetStart);
        _algo.OnLogMessage += msg => Debug.Log(msg);
        _algo.Initialize();
        UpdateMarkers();
        ResetCellColors();
    }

    // ── Sub-step prep ─────────────────────────────────────────────────────────

    /// <summary>Build the sub-step list for the coming iteration (does NOT apply them yet).</summary>
    private void PrepareNextIteration()
    {
        if (_algo == null || State == SimState.Done) return;

        Vector2Int nextTarget = randomTarget
            ? RandomNeighbour(_algo.TargetPos)
            : _algo.TargetPos;

        _subSteps = _algo.BuildSubSteps(nextTarget);
        _subStepCursor = 0;
        SubStepIndex = 0;
        SubStepTotal = _subSteps.Count;
        CurrentPhaseLabel = "READY";
        CurrentStepLabel = $"Iteration {_algo.SearchCount} — {_subSteps.Count} sub-steps";
        RefreshAllCells();
        UpdateMarkers();
    }

    // ── Apply one sub-step ────────────────────────────────────────────────────

    /// <summary>
    /// Visually apply the next sub-step in the queue.
    /// Returns true when the whole iteration is finished.
    /// </summary>
    private bool ApplyNextSubStep()
    {
        if (_subSteps == null || _subStepCursor >= _subSteps.Count)
            return true;

        // Un-highlight previous highlight
        if (_subStepCursor > 0)
        {
            var prev = _subSteps[_subStepCursor - 1];
            if (prev.CellIdx >= 0) RefreshCell(_algo.IdxToVec(prev.CellIdx));
            if (prev.AuxCells != null)
                foreach (int a in prev.AuxCells) if (a >= 0) RefreshCell(_algo.IdxToVec(a));
        }

        var s = _subSteps[_subStepCursor];
        _subStepCursor++;
        SubStepIndex = _subStepCursor;

        // Update UI labels
        if (s.Kind == MTDStarLite.SubStepKind.PhaseLabel)
            CurrentPhaseLabel = s.Label;
        else
            CurrentStepLabel = s.Label;

        // Apply visual for this sub-step
        switch (s.Kind)
        {
            case MTDStarLite.SubStepKind.PhaseLabel:
                // Just a label — no visual change
                break;

            case MTDStarLite.SubStepKind.Expand:
                HighlightCell(s.CellIdx, ColClosed);
                if (s.AuxCells != null)
                    foreach (int a in s.AuxCells) HighlightCell(a, ColOpen);
                break;

            case MTDStarLite.SubStepKind.Raise:
                HighlightCell(s.CellIdx, ColHighlight);
                break;

            case MTDStarLite.SubStepKind.Relax:
                HighlightCell(s.CellIdx, ColOpen);
                break;

            case MTDStarLite.SubStepKind.DeletePhase1:
                HighlightCell(s.CellIdx, ColDeleted);
                break;

            case MTDStarLite.SubStepKind.DeletePhase2:
                HighlightCell(s.CellIdx, ColOpen);
                if (s.AuxCells != null)
                    foreach (int a in s.AuxCells) HighlightCell(a, ColClosed);
                break;

            case MTDStarLite.SubStepKind.HunterMove:
                UpdateMarkers();
                break;

            case MTDStarLite.SubStepKind.TargetMove:
                UpdateMarkers();
                break;

            case MTDStarLite.SubStepKind.PathFound:
                ResetCellColors();
                UpdateMarkers();
                break;

            case MTDStarLite.SubStepKind.Done:
                State = SimState.Done;
                CurrentPhaseLabel = "DONE";
                CurrentStepLabel = s.Label;
                UpdateMarkers();
                Debug.Log($"[MT-D* Lite] {s.Label} searches={_algo.SearchCount} " +
                          $"expanded={_algo.ExpandCount} deleted={_algo.DeletedCount}");
                return true;
        }

        return _subStepCursor >= _subSteps.Count;
    }

    // ── Public controls (buttons / keyboard) ──────────────────────────────────

    public void PressRun()
    {
        if (State == SimState.Done) return;
        State = SimState.Running;
        CurrentPhaseLabel = "RUNNING";
        if (_runCoroutine != null) StopCoroutine(_runCoroutine);
        _runCoroutine = StartCoroutine(RunCoroutine());
    }

    public void PressPause()
    {
        if (State == SimState.Done) return;
        if (_runCoroutine != null) { StopCoroutine(_runCoroutine); _runCoroutine = null; }
        State = SimState.Paused;
        CurrentPhaseLabel = "PAUSED";
    }

    /// <summary>
    /// Advance ONE internal sub-step (original fine-grained stepping).
    /// Bound to the N key.
    /// </summary>
    public void PressStep()
    {
        if (State == SimState.Done) return;
        if (State == SimState.Running)
        {
            if (_runCoroutine != null) { StopCoroutine(_runCoroutine); _runCoroutine = null; }
        }
        State = SimState.Stepping;

        bool iterDone = ApplyNextSubStep();
        if (iterDone && State != SimState.Done)
        {
            PrepareNextIteration();
        }
    }

    /// <summary>
    /// Advance ONE complete iteration: run all sub-steps until PathFound/Done,
    /// then show only the final path and the hunter's new position.
    /// This is what the "→ Step" UI button calls.
    /// </summary>
    public void PressStepIteration()
    {
        if (State == SimState.Done) return;
        if (State == SimState.Running)
        {
            if (_runCoroutine != null) { StopCoroutine(_runCoroutine); _runCoroutine = null; }
        }
        State = SimState.Stepping;

        // Flush all remaining sub-steps in the current iteration silently,
        // then apply the final PathFound / Done visual.
        while (_subSteps != null && _subStepCursor < _subSteps.Count)
        {
            var s = _subSteps[_subStepCursor];
            _subStepCursor++;
            SubStepIndex = _subStepCursor;

            // Update labels
            if (s.Kind == MTDStarLite.SubStepKind.PhaseLabel)
                CurrentPhaseLabel = s.Label;
            else
                CurrentStepLabel = s.Label;

            // Only apply visuals for the "important" terminal events
            switch (s.Kind)
            {
                case MTDStarLite.SubStepKind.PathFound:
                    ResetCellColors();
                    UpdateMarkers();
                    break;

                case MTDStarLite.SubStepKind.HunterMove:
                case MTDStarLite.SubStepKind.TargetMove:
                    UpdateMarkers();
                    break;

                case MTDStarLite.SubStepKind.Done:
                    State = SimState.Done;
                    CurrentPhaseLabel = "DONE";
                    CurrentStepLabel = "Hunter reached target!";
                    ResetCellColors();
                    UpdateMarkers();
                    return;
            }
        }

        // Iteration finished — prepare the next one (but stay Paused/Stepping)
        if (State != SimState.Done)
            PrepareNextIteration();
    }

    public void PressRestart()
    {
        if (_runCoroutine != null) { StopCoroutine(_runCoroutine); _runCoroutine = null; }
        foreach (Transform child in transform) Destroy(child.gameObject);
        _cellSR = null; _cellCol = null;
        BuildScene();
        InitAlgorithm();
        State = SimState.Paused;
        CurrentPhaseLabel = "READY";
        CurrentStepLabel = "Press ▶ Run or → Step";
        PrepareNextIteration();
    }

    // ── Auto-run coroutine ─────────────────────────────────────────────────────

    private IEnumerator RunCoroutine()
    {
        while (State == SimState.Running)
        {
            bool iterDone = ApplyNextSubStep();
            if (State == SimState.Done) yield break;

            if (iterDone)
            {
                // Pause briefly between full iterations
                yield return new WaitForSeconds(stepInterval);
                if (State != SimState.Running) yield break;
                PrepareNextIteration();
            }
            else
            {
                yield return new WaitForSeconds(subStepDelay);
            }
        }
    }

    // ── Mouse click → toggle wall ─────────────────────────────────────────────

    private void HandleClick()
    {
        if (State == SimState.Done) return;
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (_algo == null || _cellCol == null) return;

        Vector2 screen = Mouse.current.position.ReadValue();
        Vector3 world3 = Camera.main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 0f));
        Collider2D hit = Physics2D.OverlapPoint(new Vector2(world3.x, world3.y));
        if (hit == null) return;

        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
            {
                if (_cellCol[x, y] != hit) continue;
                var pos = new Vector2Int(x, y);
                if (pos == _algo.HunterPos || pos == _algo.TargetPos) return;

                bool nowWall = !_algo.IsWall(x, y);
                _algo.NotifyEdgeCostChange(x, y, nowWall);
                _cellSR[x, y].color = nowWall ? ColWall : ColEmpty;

                // Rebuild sub-steps since environment changed
                if (State == SimState.Paused || State == SimState.Stepping)
                    PrepareNextIteration();
                return;
            }
    }

    // ── Colour helpers ─────────────────────────────────────────────────────────

    private void ResetCellColors()
    {
        if (_algo == null || _cellSR == null) return;
        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                _cellSR[x, y].color = _algo.IsWall(x, y) ? ColWall : ColEmpty;
    }

    private void RefreshAllCells()
    {
        if (_algo == null || _cellSR == null) return;
        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                RefreshCell(new Vector2Int(x, y));
    }

    private void RefreshCell(Vector2Int v)
    {
        int x = v.x, y = v.y;
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight) return;
        if (_algo.IsWall(x, y)) _cellSR[x, y].color = ColWall;
        else if (_algo.InDeleted(x, y)) _cellSR[x, y].color = ColDeleted;
        else if (_algo.InClosed(x, y)) _cellSR[x, y].color = ColClosed;
        else if (_algo.InOpen(x, y)) _cellSR[x, y].color = ColOpen;
        else _cellSR[x, y].color = ColEmpty;
    }

    private void HighlightCell(int idx, Color col)
    {
        if (idx < 0) return;
        var v = _algo.IdxToVec(idx);
        if (v.x < 0 || v.x >= gridWidth || v.y < 0 || v.y >= gridHeight) return;
        _cellSR[v.x, v.y].color = col;
    }

    private void SetCell(Vector2Int v, Color col)
    {
        if (v.x < 0 || v.x >= gridWidth || v.y < 0 || v.y >= gridHeight) return;
        _cellSR[v.x, v.y].color = col;
    }

    private void UpdateMarkers()
    {
        if (_hunterSR != null)
            _hunterSR.transform.position = CellWorldPos(_algo.HunterPos.x, _algo.HunterPos.y);
        if (_targetSR != null)
            _targetSR.transform.position = CellWorldPos(_algo.TargetPos.x, _algo.TargetPos.y);
    }

    // ── Procedural sprites ─────────────────────────────────────────────────────

    private static Sprite MakeSquareSprite()
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), pixelsPerUnit: 2f);
    }

    private static Sprite MakeCircleSprite()
    {
        const int res = 64; const float h = res * 0.5f, r = h - 1.5f, fe = 2f;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[res * res];
        for (int py = 0; py < res; py++)
            for (int x = 0; x < res; x++)
            {
                float dx = x - h + 0.5f, dy = py - h + 0.5f;
                byte a = (byte)(Mathf.Clamp01((r - Mathf.Sqrt(dx * dx + dy * dy)) / fe) * 255f);
                px[py * res + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), pixelsPerUnit: res);
    }

    // ── Utilities ──────────────────────────────────────────────────────────────

    private Vector3 CellWorldPos(int x, int y)
        => new Vector3(x * cellSize, y * cellSize, 0f);

    private Vector2Int RandomNeighbour(Vector2Int p)
    {
        var opts = new List<Vector2Int>();
        if (p.x > 0 && !_algo.IsWall(p.x - 1, p.y)) opts.Add(new Vector2Int(p.x - 1, p.y));
        if (p.x < gridWidth - 1 && !_algo.IsWall(p.x + 1, p.y)) opts.Add(new Vector2Int(p.x + 1, p.y));
        if (p.y > 0 && !_algo.IsWall(p.x, p.y - 1)) opts.Add(new Vector2Int(p.x, p.y - 1));
        if (p.y < gridHeight - 1 && !_algo.IsWall(p.x, p.y + 1)) opts.Add(new Vector2Int(p.x, p.y + 1));
        return opts.Count > 0 ? opts[Random.Range(0, opts.Count)] : p;
    }

    // ── Public setters ─────────────────────────────────────────────────────────

    public void SetStepInterval(float s) => stepInterval = s;
    public void SetSubStepDelay(float s) => subStepDelay = s;
}
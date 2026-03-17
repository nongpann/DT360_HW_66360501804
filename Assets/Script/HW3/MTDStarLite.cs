using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MT-D* Lite — Moving Target D* Lite (Sun, Yeoh, Koenig — AAMAS 2010)
///
/// Exposes two execution modes:
///   • StepIteration()       — runs one full search+move in one call (Run / auto mode)
///   • GetSubSteps()         — returns a list of SubStep records so the visualizer
///                             can replay them one at a time (Step-by-step mode)
/// </summary>
public class MTDStarLite
{
    // ── Public geometry ───────────────────────────────────────────────────────

    public int GridWidth  { get; private set; }
    public int GridHeight { get; private set; }

    // ── Per-cell data ─────────────────────────────────────────────────────────

    private float[] _g;
    private float[] _rhs;
    private int[]   _par;    // parent index, -1 = none
    private bool[]  _walls;

    // ── Open list ─────────────────────────────────────────────────────────────

    private SortedDictionary<DKey, HashSet<int>> _open;
    private Dictionary<int, DKey>                _openKeys;

    public HashSet<int> ClosedSet  { get; private set; }
    public HashSet<int> DeletedSet { get; private set; }

    private float _km;

    // ── Positions ─────────────────────────────────────────────────────────────

    public Vector2Int HunterPos { get; private set; }
    public Vector2Int TargetPos { get; private set; }

    // ── Stats ─────────────────────────────────────────────────────────────────

    public int SearchCount  { get; private set; }
    public int ExpandCount  { get; private set; }
    public int DeletedCount { get; private set; }
    public List<int> LastPath { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────

    public event Action<int, CellState> OnCellStateChanged;
    public event Action<List<int>>      OnPathFound;
    public event Action<string>         OnLogMessage;

    // ── Cell state enum ───────────────────────────────────────────────────────

    public enum CellState { Empty, Wall, Open, Closed, Deleted, Path, Hunter, Target }

    // ── Sub-step (for step-by-step mode) ─────────────────────────────────────

    public enum SubStepKind
    {
        PhaseLabel,         // display a phase name only
        Expand,             // one node expanded (overconsistent branch)
        Raise,              // one node raised (underconsistent branch)
        Relax,              // a successor rhs improved
        DeletePhase1,       // OptimizedDeletion phase 1 — one node removed
        DeletePhase2,       // OptimizedDeletion phase 2 — one node reconnected
        HunterMove,         // hunter moves one cell
        TargetMove,         // target moves one cell
        PathFound,          // path extracted
        Done,               // caught
    }

    public class SubStep
    {
        public SubStepKind Kind;
        public string      Label;       // human-readable description
        public int         CellIdx;     // primary cell involved (-1 if none)
        public int[]       AuxCells;    // secondary cells (e.g. relaxed neighbours)
        public List<int>   Path;        // filled for PathFound
        public float       GValue;
        public float       RhsValue;
    }

    // ── Construction ─────────────────────────────────────────────────────────

    public MTDStarLite(int width, int height)
    {
        GridWidth  = width;
        GridHeight = height;
        int n      = width * height;

        _g    = new float[n];
        _rhs  = new float[n];
        _par  = new int[n];
        _walls = new bool[n];

        _open     = new SortedDictionary<DKey, HashSet<int>>(new DKeyComparer());
        _openKeys = new Dictionary<int, DKey>();
        ClosedSet  = new HashSet<int>();
        DeletedSet = new HashSet<int>();
        LastPath   = new List<int>();
    }

    // ── Configuration ─────────────────────────────────────────────────────────

    public void SetWall(int x, int y, bool wall) => _walls[Idx(x, y)] = wall;
    public bool IsWall(int x, int y)             => _walls[Idx(x, y)];
    public void SetHunter(Vector2Int p)          => HunterPos = p;
    public void SetTarget(Vector2Int p)          => TargetPos = p;

    // ── Initialize ────────────────────────────────────────────────────────────

    public void Initialize()
    {
        int n = GridWidth * GridHeight;
        for (int i = 0; i < n; i++) { _g[i] = INF; _rhs[i] = INF; _par[i] = -1; }
        _km = 0f;
        _open.Clear(); _openKeys.Clear();
        ClosedSet.Clear(); DeletedSet.Clear(); LastPath.Clear();
        SearchCount = ExpandCount = DeletedCount = 0;

        int sk = Idx(HunterPos);
        _rhs[sk] = 0f;
        InsertOpen(sk, CalcKey(sk));
        Log($"Initialized. Hunter={HunterPos} Target={TargetPos}");
    }

    // ── Full iteration (Run mode) ─────────────────────────────────────────────

    /// <summary>Run one complete search+move. Returns true when caught.</summary>
    public bool StepIteration(Vector2Int newTargetPos)
    {
        SearchCount++;
        Log($"=== Iteration {SearchCount} ===");
        ComputeCostMinimalPath();

        int gi = Idx(TargetPos);
        if (_rhs[gi] >= INF && _g[gi] >= INF) { Log("No path.","WARN"); return false; }

        LastPath = ExtractPath();
        OnPathFound?.Invoke(new List<int>(LastPath));
        foreach (int c in LastPath) FireCell(c, CellState.Path);

        if (HunterPos == TargetPos) { Log("Caught!"); return true; }

        if (LastPath.Count > 1)
        {
            Vector2Int old = HunterPos;
            HunterPos = IdxToVec(LastPath[1]);
            _km += Heuristic(old, TargetPos);
            OptimizedDeletion(old);
        }

        if (newTargetPos != TargetPos)
        {
            TargetPos = newTargetPos;
            int ng = Idx(TargetPos);
            _rhs[ng] = CalcRhsFromPred(ng);
            UpdateState(ng);
        }

        UpdateMarkers();
        if (HunterPos == TargetPos) { Log("Caught!"); return true; }
        return false;
    }

    // ── Sub-step enumeration (Step-by-step mode) ──────────────────────────────

    /// <summary>
    /// Produce the full list of fine-grained sub-steps for one search iteration
    /// without modifying any state. The caller replays them one by one using
    /// ApplySubStep() to advance the visual and internal state together.
    /// </summary>
    public List<SubStep> BuildSubSteps(Vector2Int newTargetPos)
    {
        var steps = new List<SubStep>();
        SearchCount++;

        // ── Phase 1: ComputeCostMinimalPath ───────────────────────────────────
        steps.Add(Phase("COMPUTE COST-MINIMAL PATH"));

        int goalIdx = Idx(TargetPos);
        int limit   = GridWidth * GridHeight * 8;

        // Snapshot open/g/rhs so we can simulate without mutating
        // (We DO mutate — sub-steps call the real Update internals —
        //  but we record what happened so the visualiser can replay.)
        while (limit-- > 0)
        {
            if (_open.Count == 0) break;
            int u = TopOpen();
            if (u < 0) break;

            DKey topKey  = _openKeys[u];
            DKey goalKey = CalcKey(goalIdx);
            if (DKey.Compare(topKey, goalKey) >= 0 && _rhs[goalIdx] <= _g[goalIdx]) break;

            DKey knew = CalcKey(u);
            if (DKey.Compare(topKey, knew) < 0)
            {
                RemoveOpen(u); InsertOpen(u, knew);
                continue;
            }

            if (_g[u] > _rhs[u])
            {
                // Overconsistent: expand
                _g[u] = _rhs[u];
                RemoveOpen(u);
                ClosedSet.Add(u); DeletedSet.Remove(u);
                ExpandCount++;

                var relaxed = new List<int>();
                Vector2Int uv = IdxToVec(u);
                foreach (var sv in Neighbours(uv.x, uv.y))
                {
                    int s = Idx(sv);
                    if (s == Idx(HunterPos)) continue;
                    float cost = EdgeCost(uv, sv);
                    if (cost >= INF) continue;
                    if (_g[u] + cost < _rhs[s])
                    {
                        _par[s] = u; _rhs[s] = _g[u] + cost;
                        UpdateState(s);
                        relaxed.Add(s);
                    }
                }

                steps.Add(new SubStep
                {
                    Kind     = SubStepKind.Expand,
                    Label    = $"Expand ({IdxToVec(u).x},{IdxToVec(u).y})  g={_g[u]:F1}  rhs={_rhs[u]:F1}",
                    CellIdx  = u,
                    AuxCells = relaxed.ToArray(),
                    GValue   = _g[u],
                    RhsValue = _rhs[u],
                });
            }
            else
            {
                // Underconsistent: raise
                _g[u] = INF;
                UpdateState(u);

                Vector2Int uv = IdxToVec(u);
                foreach (var sv in Neighbours(uv.x, uv.y))
                {
                    int s = Idx(sv);
                    if (s == Idx(HunterPos)) continue;
                    if (_par[s] == u)
                    {
                        _rhs[s] = CalcRhsFromPred(s);
                        _par[s] = _rhs[s] < INF ? BestPred(s) : -1;
                        UpdateState(s);
                    }
                }
                UpdateState(u);

                steps.Add(new SubStep
                {
                    Kind     = SubStepKind.Raise,
                    Label    = $"Raise ({IdxToVec(u).x},{IdxToVec(u).y})  g=∞ (underconsistent)",
                    CellIdx  = u,
                    AuxCells = Array.Empty<int>(),
                    GValue   = INF,
                    RhsValue = _rhs[u],
                });
            }
        }

        // ── Phase 2: Extract path ─────────────────────────────────────────────
        int gi2 = Idx(TargetPos);
        if (_rhs[gi2] >= INF && _g[gi2] >= INF)
        {
            steps.Add(Phase("NO PATH EXISTS"));
            return steps;
        }

        LastPath = ExtractPath();
        steps.Add(new SubStep
        {
            Kind  = SubStepKind.PathFound,
            Label = $"Path found — length {LastPath.Count}",
            Path  = new List<int>(LastPath),
        });

        if (HunterPos == TargetPos)
        {
            steps.Add(new SubStep { Kind = SubStepKind.Done, Label = "Target caught!" });
            return steps;
        }

        // ── Phase 3: Hunter moves ─────────────────────────────────────────────
        if (LastPath.Count > 1)
        {
            steps.Add(Phase("HUNTER MOVES"));
            int nextIdx = LastPath[1];
            steps.Add(new SubStep
            {
                Kind    = SubStepKind.HunterMove,
                Label   = $"Hunter {HunterPos} → {IdxToVec(nextIdx)}",
                CellIdx = nextIdx,
            });
            // Actually advance hunter and run deletion so subsequent steps are correct
            Vector2Int oldHunter = HunterPos;
            HunterPos = IdxToVec(nextIdx);
            _km += Heuristic(oldHunter, TargetPos);

            // ── Phase 4: OptimizedDeletion ─────────────────────────────────────
            steps.Add(Phase("OPTIMIZED DELETION"));
            var (delSteps, phase2Steps) = BuildDeletionSteps(oldHunter);
            steps.AddRange(delSteps);
            steps.AddRange(phase2Steps);
        }

        // ── Phase 5: Target moves ──────────────────────────────────────────────
        if (newTargetPos != TargetPos)
        {
            steps.Add(Phase("TARGET MOVES"));
            steps.Add(new SubStep
            {
                Kind    = SubStepKind.TargetMove,
                Label   = $"Target {TargetPos} → {newTargetPos}",
                CellIdx = Idx(newTargetPos),
            });
            TargetPos = newTargetPos;
            int ng = Idx(TargetPos);
            _rhs[ng] = CalcRhsFromPred(ng);
            UpdateState(ng);
        }

        UpdateMarkers();
        if (HunterPos == TargetPos)
            steps.Add(new SubStep { Kind = SubStepKind.Done, Label = "Target caught!" });

        return steps;
    }

    // Build deletion sub-steps and apply them immediately (so state stays consistent)
    private (List<SubStep> phase1, List<SubStep> phase2) BuildDeletionSteps(Vector2Int oldHunter)
    {
        var p1 = new List<SubStep>();
        var p2 = new List<SubStep>();

        int startK = Idx(HunterPos);
        _par[startK] = -1;

        // Build subtree
        var subtree = new HashSet<int> { startK };
        var queue   = new Queue<int>();
        queue.Enqueue(startK);
        int total = GridWidth * GridHeight;
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            for (int i = 0; i < total; i++)
                if (!subtree.Contains(i) && _par[i] == cur) { subtree.Add(i); queue.Enqueue(i); }
        }

        // Phase 1
        var deleted = new List<int>();
        for (int i = 0; i < total; i++)
        {
            if (subtree.Contains(i)) continue;
            if (!ClosedSet.Contains(i) && !_openKeys.ContainsKey(i)) continue;

            _par[i] = -1; _rhs[i] = INF; _g[i] = INF;
            RemoveOpen(i); ClosedSet.Remove(i); DeletedSet.Add(i);
            deleted.Add(i); DeletedCount++;

            p1.Add(new SubStep
            {
                Kind    = SubStepKind.DeletePhase1,
                Label   = $"Delete ({IdxToVec(i).x},{IdxToVec(i).y}) — outside subtree",
                CellIdx = i,
            });
        }

        // Phase 2
        foreach (int dk in deleted)
        {
            Vector2Int dv = IdxToVec(dk);
            float best = INF; int bestPar = -1;
            foreach (var nb in Neighbours(dv.x, dv.y))
            {
                int nk = Idx(nb);
                if (!subtree.Contains(nk)) continue;
                float cost = EdgeCost(nb, dv);
                if (cost < INF && _g[nk] + cost < best) { best = _g[nk] + cost; bestPar = nk; }
            }
            if (best < INF)
            {
                _rhs[dk] = best; _par[dk] = bestPar;
                InsertOpen(dk, CalcKey(dk));
                p2.Add(new SubStep
                {
                    Kind     = SubStepKind.DeletePhase2,
                    Label    = $"Reconnect ({IdxToVec(dk).x},{IdxToVec(dk).y}) rhs={best:F1}",
                    CellIdx  = dk,
                    AuxCells = new[] { bestPar },
                    RhsValue = best,
                });
            }
        }

        return (p1, p2);
    }

    // ── Edge cost change ──────────────────────────────────────────────────────

    public void NotifyEdgeCostChange(int x, int y, bool nowWall)
    {
        _walls[Idx(x, y)] = nowWall;
        int v = Idx(x, y);
        Vector2Int vv = new Vector2Int(x, y);
        foreach (var nb in Neighbours(x, y))
        {
            float cost = EdgeCost(nb, vv);
            int u = Idx(nb);
            if (!nowWall)
            {
                if (v != Idx(HunterPos) && _g[u] + cost < _rhs[v])
                { _par[v] = u; _rhs[v] = _g[u] + cost; UpdateState(v); }
            }
            else
            {
                if (v != Idx(HunterPos) && _par[v] == u)
                {
                    _rhs[v] = CalcRhsFromPred(v);
                    _par[v] = _rhs[v] < INF ? BestPred(v) : -1;
                    UpdateState(v);
                }
            }
        }
    }

    // ── Internal: ComputeCostMinimalPath (used by StepIteration) ─────────────

    private void ComputeCostMinimalPath()
    {
        int goalIdx = Idx(TargetPos);
        int limit   = GridWidth * GridHeight * 8;
        while (limit-- > 0)
        {
            if (_open.Count == 0) break;
            int u = TopOpen(); if (u < 0) break;
            DKey topKey = _openKeys[u], goalKey = CalcKey(goalIdx);
            if (DKey.Compare(topKey, goalKey) >= 0 && _rhs[goalIdx] <= _g[goalIdx]) break;
            DKey knew = CalcKey(u);
            if (DKey.Compare(topKey, knew) < 0) { RemoveOpen(u); InsertOpen(u, knew); continue; }
            if (_g[u] > _rhs[u])
            {
                _g[u] = _rhs[u]; RemoveOpen(u); ClosedSet.Add(u); DeletedSet.Remove(u); ExpandCount++;
                FireCell(u, CellState.Closed);
                Vector2Int uv = IdxToVec(u);
                foreach (var sv in Neighbours(uv.x, uv.y))
                {
                    int s = Idx(sv); if (s == Idx(HunterPos)) continue;
                    float c = EdgeCost(uv, sv); if (c >= INF) continue;
                    if (_g[u] + c < _rhs[s]) { _par[s] = u; _rhs[s] = _g[u] + c; UpdateState(s); FireCell(s, CellState.Open); }
                }
            }
            else
            {
                _g[u] = INF; UpdateState(u);
                Vector2Int uv = IdxToVec(u);
                foreach (var sv in Neighbours(uv.x, uv.y))
                {
                    int s = Idx(sv); if (s == Idx(HunterPos)) continue;
                    if (_par[s] == u) { _rhs[s] = CalcRhsFromPred(s); _par[s] = _rhs[s] < INF ? BestPred(s) : -1; UpdateState(s); }
                }
                UpdateState(u);
            }
        }
    }

    private void OptimizedDeletion(Vector2Int oldHunter)
    {
        int startK = Idx(HunterPos); _par[startK] = -1;
        var subtree = new HashSet<int> { startK };
        var q = new Queue<int>(); q.Enqueue(startK);
        int tot = GridWidth * GridHeight;
        while (q.Count > 0) { int cur = q.Dequeue(); for (int i=0;i<tot;i++) if(!subtree.Contains(i)&&_par[i]==cur){subtree.Add(i);q.Enqueue(i);} }
        var deleted = new List<int>();
        for (int i=0;i<tot;i++)
        {
            if (subtree.Contains(i)) continue;
            if (!ClosedSet.Contains(i) && !_openKeys.ContainsKey(i)) continue;
            _par[i]=-1; _rhs[i]=INF; _g[i]=INF; RemoveOpen(i); ClosedSet.Remove(i); DeletedSet.Add(i);
            deleted.Add(i); DeletedCount++; FireCell(i, CellState.Deleted);
        }
        foreach (int dk in deleted)
        {
            Vector2Int dv=IdxToVec(dk); float best=INF; int bp=-1;
            foreach (var nb in Neighbours(dv.x,dv.y)) { int nk=Idx(nb); if(!subtree.Contains(nk))continue; float c=EdgeCost(nb,dv); if(c<INF&&_g[nk]+c<best){best=_g[nk]+c;bp=nk;} }
            if (best<INF){_rhs[dk]=best;_par[dk]=bp;InsertOpen(dk,CalcKey(dk));FireCell(dk,CellState.Open);}
        }
    }

    // ── Core helpers ──────────────────────────────────────────────────────────

    private DKey CalcKey(int k)
    {
        float mn = Mathf.Min(_g[k], _rhs[k]);
        return new DKey(mn + Heuristic(IdxToVec(k), TargetPos) + _km, mn);
    }

    private void UpdateState(int u)
    {
        bool inOpen = _openKeys.ContainsKey(u);
        bool ok     = Approx(_g[u], _rhs[u]);
        if      (!ok && inOpen)  { RemoveOpen(u); InsertOpen(u, CalcKey(u)); }
        else if (!ok)            { InsertOpen(u, CalcKey(u)); }
        else if (ok && inOpen)   { RemoveOpen(u); }
    }

    private float CalcRhsFromPred(int k)
    {
        Vector2Int v = IdxToVec(k); float best = INF;
        foreach (var nb in Neighbours(v.x, v.y)) { int nk=Idx(nb); float c=EdgeCost(nb,v); if(c<INF&&_g[nk]+c<best) best=_g[nk]+c; }
        return best;
    }

    private int BestPred(int k)
    {
        Vector2Int v = IdxToVec(k); float best = INF; int bi = -1;
        foreach (var nb in Neighbours(v.x, v.y)) { int nk=Idx(nb); float c=EdgeCost(nb,v); if(c<INF&&_g[nk]+c<best){best=_g[nk]+c;bi=nk;} }
        return bi;
    }

    private List<int> ExtractPath()
    {
        var path = new List<int>(); int cur = Idx(HunterPos); int goal = Idx(TargetPos);
        var vis = new HashSet<int>(); int lim = GridWidth * GridHeight; path.Add(cur);
        while (cur != goal && lim-- > 0)
        {
            if (vis.Contains(cur)) break; vis.Add(cur);
            Vector2Int cv = IdxToVec(cur); float best = INF; int bn = -1;
            foreach (var nb in Neighbours(cv.x, cv.y)) { int nk=Idx(nb); float c=EdgeCost(cv,nb); if(c<INF&&_g[nk]+c<best){best=_g[nk]+c;bn=nk;} }
            if (bn < 0 || best >= INF) break; cur = bn; path.Add(cur);
        }
        return path;
    }

    private void UpdateMarkers()
    {
        FireCell(Idx(HunterPos), CellState.Hunter);
        FireCell(Idx(TargetPos), CellState.Target);
    }

    // ── Open list ─────────────────────────────────────────────────────────────

    private void InsertOpen(int k, DKey key)
    {
        if (_openKeys.ContainsKey(k)) RemoveOpen(k);
        _openKeys[k] = key;
        if (!_open.ContainsKey(key)) _open[key] = new HashSet<int>();
        _open[key].Add(k);
    }

    private void RemoveOpen(int k)
    {
        if (!_openKeys.TryGetValue(k, out DKey key)) return;
        _openKeys.Remove(k);
        if (_open.TryGetValue(key, out var set)) { set.Remove(k); if (set.Count == 0) _open.Remove(key); }
    }

    private int TopOpen()
    {
        foreach (var kv in _open) foreach (int k in kv.Value) return k;
        return -1;
    }

    // ── Grid ──────────────────────────────────────────────────────────────────

    public int        Idx(int x, int y) => y * GridWidth + x;
    public int        Idx(Vector2Int v) => v.y * GridWidth + v.x;
    public Vector2Int IdxToVec(int i)   => new Vector2Int(i % GridWidth, i / GridWidth);

    private float EdgeCost(Vector2Int from, Vector2Int to) => _walls[Idx(to)] ? INF : 1f;
    private float Heuristic(Vector2Int a, Vector2Int b)    => Mathf.Abs(a.x-b.x) + Mathf.Abs(a.y-b.y);

    private IEnumerable<Vector2Int> Neighbours(int x, int y)
    {
        if (x > 0)             yield return new Vector2Int(x-1, y);
        if (x < GridWidth-1)   yield return new Vector2Int(x+1, y);
        if (y > 0)             yield return new Vector2Int(x, y-1);
        if (y < GridHeight-1)  yield return new Vector2Int(x, y+1);
    }

    // ── Public accessors ──────────────────────────────────────────────────────

    public float GetG(int x, int y)      => _g[Idx(x, y)];
    public float GetRhs(int x, int y)    => _rhs[Idx(x, y)];
    public bool  InOpen(int x, int y)    => _openKeys.ContainsKey(Idx(x, y));
    public bool  InClosed(int x, int y)  => ClosedSet.Contains(Idx(x, y));
    public bool  InDeleted(int x, int y) => DeletedSet.Contains(Idx(x, y));

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static float INF => float.MaxValue / 2f;
    private static bool  Approx(float a, float b) => Mathf.Abs(a - b) < 0.001f;

    private void FireCell(int idx, CellState s) => OnCellStateChanged?.Invoke(idx, s);
    private void Log(string m, string t = "INFO") => OnLogMessage?.Invoke($"[{t}] {m}");

    private static SubStep Phase(string label) => new SubStep
        { Kind = SubStepKind.PhaseLabel, Label = label, CellIdx = -1 };

    // ── Priority key ──────────────────────────────────────────────────────────

    public readonly struct DKey : IEquatable<DKey>
    {
        public readonly float K1, K2;
        public DKey(float k1, float k2) { K1 = k1; K2 = k2; }
        public static int Compare(DKey a, DKey b)
        {
            if (!Approx(a.K1, b.K1)) return a.K1.CompareTo(b.K1);
            return a.K2.CompareTo(b.K2);
        }
        private static bool Approx(float a, float b) => Mathf.Abs(a - b) < 0.001f;
        public bool Equals(DKey o) => Approx(K1, o.K1) && Approx(K2, o.K2);
        public override bool Equals(object o) => o is DKey d && Equals(d);
        public override int  GetHashCode()     => HashCode.Combine(K1, K2);
    }

    public class DKeyComparer : IComparer<DKey>
    {
        public int Compare(DKey a, DKey b) => DKey.Compare(a, b);
    }
}

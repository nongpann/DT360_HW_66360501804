using UnityEngine;

/// <summary>
/// MTDStarUI — compact overlay: Play / Pause / Step / Restart buttons,
/// keyboard shortcuts, and a short colour legend.
///
/// FIXES:
///   • "→ Step" now advances ONE full iteration (path-found + hunter move)
///     instead of a single internal sub-step.
///   • Legend colour swatches now render correctly: each swatch uses its own
///     GUIStyle with the target colour baked into the background texture,
///     so GUI.color tinting no longer interferes.
///
/// Attach to the same GameObject as MTDStarVisualizer.
/// </summary>
[RequireComponent(typeof(MTDStarVisualizer))]
public class MTDStarUI : MonoBehaviour
{
    private MTDStarVisualizer _vis;

    private GUIStyle _panel;
    private GUIStyle _btn;
    private GUIStyle _btnOn;
    private GUIStyle _label;
    private GUIStyle _hint;
    private GUIStyle _title;

    // One cached swatch style per legend entry (avoids per-frame allocs)
    private GUIStyle[] _swatchStyles;
    private bool _ready;

    // Legend data (colour + label) — kept in sync with MTDStarVisualizer colours
    private static readonly (Color col, string text)[] LegendEntries =
    {
        (new Color(0.22f, 0.30f, 0.72f), "OPEN  — waiting"),
        (new Color(0.14f, 0.48f, 0.28f), "CLOSED — expanded"),
        (new Color(0.52f, 0.14f, 0.62f), "DELETED — bad nodes"),
    };

    private void Awake() => _vis = GetComponent<MTDStarVisualizer>();

    private void OnGUI()
    {
        Build();
        if (_vis == null) return;

        const float PW = 210f;
        float px = Screen.width - PW - 10f;
        float py = 10f;
        float ph = 260f;

        GUI.Box(new Rect(px, py, PW, ph), GUIContent.none, _panel);

        float x = px + 10f;
        float y = py + 10f;
        float iw = PW - 20f;

        // ── Buttons row 1: Play  Pause  Step ──────────────────────────────────
        float bw = (iw - 8f) / 3f;
        bool running = _vis.State == MTDStarVisualizer.SimState.Running;
        bool paused = _vis.State == MTDStarVisualizer.SimState.Paused
                     || _vis.State == MTDStarVisualizer.SimState.Stepping;

        if (GUI.Button(new Rect(x, y, bw, 26f), "▶ Play", running ? _btnOn : _btn)) _vis.PressRun();
        if (GUI.Button(new Rect(x + bw + 4f, y, bw, 26f), "⏸ Pause", paused ? _btnOn : _btn)) _vis.PressPause();

        // Step = one full iteration (path + move), not a single sub-step
        if (GUI.Button(new Rect(x + (bw + 4f) * 2f, y, bw, 26f), "→ Step", _btn))
            _vis.PressStepIteration();
        y += 32f;

        // ── Button row 2: Restart ──────────────────────────────────────────────
        if (GUI.Button(new Rect(x, y, iw, 26f), "↺ Restart", _btn)) _vis.PressRestart();
        y += 34f;

        // ── Keyboard hints ─────────────────────────────────────────────────────
        HRule(px + 6f, y, PW - 12f); y += 8f;
        GUI.Label(new Rect(x, y, iw, 16f), "R = Play   P / Space = Pause", _hint); y += 17f;
        GUI.Label(new Rect(x, y, iw, 16f), "N = Step   Click cell = wall", _hint); y += 22f;

        // ── Colour legend ──────────────────────────────────────────────────────
        HRule(px + 6f, y, PW - 12f); y += 8f;
        for (int i = 0; i < LegendEntries.Length; i++)
            LegRow(ref y, x, _swatchStyles[i], LegendEntries[i].text);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Draw one legend row using a pre-built swatch style so the colour is
    /// encoded in the texture — not dependent on GUI.color tinting.
    /// </summary>
    private void LegRow(ref float y, float x, GUIStyle swatchStyle, string text)
    {
        GUI.Box(new Rect(x, y + 2f, 11f, 11f), GUIContent.none, swatchStyle);
        GUI.Label(new Rect(x + 15f, y, 175f, 16f), text, _label);
        y += 18f;
    }

    private void HRule(float x, float y, float w)
    {
        var old = GUI.color;
        GUI.color = new Color(0.3f, 0.3f, 0.45f, 0.45f);
        GUI.Box(new Rect(x, y, w, 1f), GUIContent.none);
        GUI.color = old;
    }

    // ── Style builder (lazy, one-time) ────────────────────────────────────────

    private void Build()
    {
        if (_ready) return;
        _ready = true;

        _panel = new GUIStyle(GUI.skin.box)
        {
            normal = { background = Tex(new Color(0.07f, 0.07f, 0.11f, 0.93f)) }
        };

        _title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.70f, 0.82f, 1.00f) }
        };

        _label = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            normal = { textColor = new Color(0.78f, 0.82f, 0.96f) }
        };

        _hint = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            normal = { textColor = new Color(0.48f, 0.52f, 0.65f) }
        };

        _btn = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            normal = { textColor  = Color.white,
                       background = Tex(new Color(0.18f, 0.20f, 0.38f)) },
            hover = { textColor  = Color.white,
                       background = Tex(new Color(0.26f, 0.30f, 0.55f)) },
        };

        _btnOn = new GUIStyle(_btn)
        {
            normal = { textColor  = new Color(0.25f, 1.00f, 0.45f),
                       background = Tex(new Color(0.10f, 0.26f, 0.16f)) }
        };

        // Build one swatch style per legend entry — colour lives in the texture
        _swatchStyles = new GUIStyle[LegendEntries.Length];
        for (int i = 0; i < LegendEntries.Length; i++)
        {
            _swatchStyles[i] = new GUIStyle(GUI.skin.box)
            {
                normal = { background = Tex(LegendEntries[i].col) },
                hover = { background = Tex(LegendEntries[i].col) },
            };
            _swatchStyles[i].border = new RectOffset(0, 0, 0, 0);
            _swatchStyles[i].padding = new RectOffset(0, 0, 0, 0);
            _swatchStyles[i].margin = new RectOffset(0, 0, 0, 0);
        }
    }

    private static Texture2D Tex(Color c)
    {
        var t = new Texture2D(2, 2);
        var p = new Color[] { c, c, c, c };
        t.SetPixels(p);
        t.Apply();
        return t;
    }
}
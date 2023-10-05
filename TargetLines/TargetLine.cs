using Dalamud.Game.ClientState.Objects.Types;
using DrahsidLib;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ImGuiNET;
using System;
using System.Numerics;
using static TargetLines.ClassJobHelper;

namespace TargetLines;

internal struct LinePoint {
    public Vector2 Pos;
    public bool Visible;

    public LinePoint(Vector2 pos, bool visible) {
        Pos = pos;
        Visible = visible;
    }
}

internal class TargetLine {
    public enum LineState {
        NewTarget, // new target (from no target)
        Dying, // no target, fading away
        Switching, // switching to different target
        Idle // being targeted
    };

    public GameObjectHelper Self;
    private GameObjectHelper? Target;

    public LineState State = LineState.NewTarget;
    public bool ShouldDelete = false;

    private Vector2 ScreenPos = new Vector2();
    private Vector2 MidScreenPos = new Vector2();
    private Vector2 TargetScreenPos = new Vector2();

    private Vector3 Position = new Vector3();
    private Vector3 MidPosition = new Vector3();
    private Vector3 TargetPosition = new Vector3();
    private ABGR LineColor = new ABGR(0, 0, 0, 0);
    private ABGR OutlineColor = new ABGR(0, 0, 0, 0);

    private Vector3 LastTargetPosition = new Vector3();
    private Vector3 LastTargetPosition2 = new Vector3();
    private ABGR LastLineColor = new ABGR(0, 0, 0, 0);
    private ABGR LastOutlineColor = new ABGR(0, 0, 0, 0);
    
    private bool UseQuad = false;
    private bool Visible = true;

    private bool HadTarget = false;
    private ulong LastTargetId = 0;

    private bool DrawBeginCap = false;
    private bool DrawMid = false;
    private bool DrawEndCap = false;

    private float StateTime = 0.0f;
    private float MidHeight = 0.0f;
    private float LastMidHeight = 0.0f;
    private float LastTargetHeight = 0.0f;

    private unsafe Framework* Framework = null;

    private LinePoint[] Points;
    private float LinePointStep;

    private const float HPI = MathF.PI * 0.5f;

    public TargetLine(GameObjectHelper obj) {
        Self = obj;
        if (Target != null) {
            LastTargetId = Target.TargetObjectId;
            LastTargetPosition = Target.Position;
            LastTargetPosition2 = LastTargetPosition;
        }
        else {
            LastTargetPosition = Self.Position;
            LastTargetPosition2 = LastTargetPosition;
        }

        Points = new LinePoint[Globals.Config.saved.TextureCurveSampleCount];
        LinePointStep = 1.0f / (float)(Points.Length - 1);
    }

    Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) {
        float t2 = t * t;
        float t3 = t2 * t;
        float mt = 1 - t;
        float mt2 = mt * mt;
        float mt3 = mt2 * mt;
        Vector3 point =
            mt3 * p0 +
            3 * mt2 * t * p1 +
            3 * mt * t2 * p2 +
            t3 * p3;
        return point;
    }

    Vector3 EvaluateQuadratic(Vector3 p0, Vector3 p1, Vector3 p2, float t) {
        float mt = 1 - t;
        Vector3 point = mt * mt * p0 + 2 * mt * t * p1 + t * t * p2;
        return point;
    }

    private void DrawSolidLine() {
        ImDrawListPtr drawlist = ImGui.GetWindowDrawList();
        float outlineThickness = Globals.Config.saved.OutlineThickness;
        float lineThickness = Globals.Config.saved.LineThickness;

/*
#if DEBUG || UNLOCKED
        drawlist.AddCircleFilled(ScreenPos, 5.0f, 0xFFFFFF00);
        drawlist.AddCircleFilled(MidScreenPos, 5.0f, 0xFF00FFFF);
        drawlist.AddCircleFilled(TargetScreenPos, 5.0f, 0xFFFF00FF);
        drawlist.AddBezierQuadratic(ScreenPos, MidScreenPos, TargetScreenPos, 0x80FFFFFF, 5.0f);
#endif
*/

        if (UseQuad) {
            if (outlineThickness > 0) {
                drawlist.AddBezierQuadratic(ScreenPos, MidScreenPos, TargetScreenPos, OutlineColor.GetRaw(), outlineThickness);
            }
            if (lineThickness > 0) {
                drawlist.AddBezierQuadratic(ScreenPos, MidScreenPos, TargetScreenPos, LineColor.GetRaw(), lineThickness);
            }
        }
        else {
            if (outlineThickness > 0) {
                drawlist.AddBezierCubic(ScreenPos, MidScreenPos, MidScreenPos, TargetScreenPos, OutlineColor.GetRaw(), outlineThickness);
            }
            if (lineThickness > 0) {
                drawlist.AddBezierCubic(ScreenPos, MidScreenPos, MidScreenPos, TargetScreenPos, LineColor.GetRaw(), lineThickness);
            }
        }
    }

    private void DrawFancyLine() {
        ImDrawListPtr drawlist = ImGui.GetWindowDrawList();
        Vector2 uv1 = new Vector2(0, 0);
        Vector2 uv2 = new Vector2(1.0f, 1.0f);
        int sampleCount = Points.Length;

        float currentTime = (float)Globals.Runtime;
        float pulsatingSpeed = Globals.Config.saved.WaveFrequencyScalar;
        float max = LineColor.a;
        float min = max * 0.5f;
        float pulsatingAmplitude = (max - min) * (1.0f - Globals.Config.saved.WaveAmplitudeOffset);

        bool shouldCalculatePulsatingEffect = Globals.Config.saved.PulsingEffect;
        bool shouldFadeToEnd = Globals.Config.saved.FadeToEnd;
        float lineThickness = Globals.Config.saved.LineThickness * 2.0f;
        float outlineThickness = Globals.Config.saved.OutlineThickness * 2.0f;

        for (int index = 0; index < sampleCount - 1; index++) {
            LinePoint point = Points[index];
            LinePoint nextpoint = Points[index + 1];
            if (!point.Visible && !nextpoint.Visible) {
                continue;
            }

            Vector2 p1 = point.Pos;
            Vector2 p2 = nextpoint.Pos;

            Vector2 dir = Vector2.Normalize(p2 - p1);
            Vector2 perp = new Vector2(-dir.Y, dir.X);

            Vector2 p1_perp = p1 + perp * lineThickness;
            Vector2 p2_perp = p2 + perp * lineThickness;
            Vector2 p1_perp_inv = p1 - perp * lineThickness;
            Vector2 p2_perp_inv = p2 - perp * lineThickness;

            Vector2 p1_perpo = p1 + perp * outlineThickness;
            Vector2 p2_perpo = p2 + perp * outlineThickness;
            Vector2 p1_perp_invo = p1 - perp * outlineThickness;
            Vector2 p2_perp_invo = p2 - perp * outlineThickness;

            ABGR linecolor_index = LineColor;
            ABGR outlinecolor_index = OutlineColor;

            if (shouldCalculatePulsatingEffect) {
                float p = index * LinePointStep;
                float pulsatingAlpha = MathF.Sin(-currentTime * pulsatingSpeed + (p * MathF.PI) + HPI);
                pulsatingAlpha = Math.Clamp(pulsatingAlpha * pulsatingAmplitude + min, min, max);
                linecolor_index.a = (byte)pulsatingAlpha;
                outlinecolor_index.a = (byte)pulsatingAlpha;
            }

            if (shouldFadeToEnd) {
                float alphaFade = MathUtils.Lerpf((float)outlinecolor_index.a,
                    (float)(outlinecolor_index.a * Globals.Config.saved.FadeToEndScalar),
                    (float)index * LinePointStep);

                outlinecolor_index.a = (byte)alphaFade;
            }

            if (linecolor_index.a != 0 && lineThickness != 0) {
                drawlist.AddImageQuad(Globals.LineTexture.ImGuiHandle, p1_perp_inv, p2_perp_inv, p2_perp, p1_perp, uv1, new Vector2(uv1.X, uv2.Y), uv2, new Vector2(uv2.X, uv1.Y), linecolor_index.GetRaw());
            }

            if (outlinecolor_index.a != 0 && outlineThickness != 0) {
                drawlist.AddImageQuad(Globals.OutlineTexture.ImGuiHandle, p1_perp_invo, p2_perp_invo, p2_perpo, p1_perpo, uv1, new Vector2(uv1.X, uv2.Y), uv2, new Vector2(uv2.X, uv1.Y), outlinecolor_index.GetRaw());
            }
        }

        Vector2 start_dir = Vector2.Normalize(Points[1].Pos - Points[0].Pos);
        Vector2 end_dir = Vector2.Normalize(Points[sampleCount - 1].Pos - Points[sampleCount - 2].Pos);
        Vector2 start_perp = new Vector2(-start_dir.Y, start_dir.X) * lineThickness;
        Vector2 end_perp = new Vector2(-end_dir.Y, end_dir.X) * lineThickness;

        Vector2 start_p1 = Points[0].Pos - start_perp;
        Vector2 start_p2 = Points[0].Pos + start_perp;
        Vector2 end_p1 = Points[sampleCount - 1].Pos - end_perp;
        Vector2 end_p2 = Points[sampleCount - 1].Pos + end_perp;

        ABGR linecolor_end = new ABGR(0, 0, 0, 0);
        linecolor_end.CopyValues(LineColor);
        if (shouldFadeToEnd) {
            linecolor_end.a = (byte)(linecolor_end.a * Globals.Config.saved.FadeToEndScalar);
        }

        if (DrawBeginCap) {
            drawlist.AddImage(Globals.EdgeTexture.ImGuiHandle, start_p1, start_p2, uv1, uv2, LineColor.GetRaw());
        }
        if (DrawEndCap) {
            drawlist.AddImage(Globals.EdgeTexture.ImGuiHandle, end_p1, end_p2, uv1, uv2, LineColor.GetRaw());
        }
    }

    private void UpdateMidPosition() {
        MidPosition = (Position + TargetPosition) * 0.5f;

        if (Self.IsPlayerCharacter) {
            MidPosition.Y += Globals.Config.saved.PlayerHeightBump;
        }
        else if (Self.IsBattleChara) {
            MidPosition.Y += Globals.Config.saved.EnemyHeightBump;
        }

        float height_fix = 0.75f;
        if (UseQuad) {
            height_fix = 1.0f;
        }

        if (State == LineState.Dying) {
            float alpha = StateTime / Globals.Config.saved.NoTargetFadeTime;
            height_fix *= 1.0f - alpha;
        }
        else if (State == LineState.NewTarget) {
            float alpha = StateTime / Globals.Config.saved.NewTargetEaseTime;
            height_fix *= alpha;
        }

        MidPosition.Y += (MidHeight * Globals.Config.saved.ArcHeightScalar) * height_fix;
    }


    private void UpdateStateNewTarget() {
        Vector3 start = Self.Position;
        Vector3 end = Target.Position;
        float start_height = Self.CursorHeight;
        float end_height = Target.CursorHeight;
        float mid_height = (start_height + end_height) * 0.5f;
        float alpha = Math.Max(0, Math.Min(1, StateTime / Globals.Config.saved.NewTargetEaseTime));

        LastTargetHeight = end_height;
        MidHeight = mid_height;

        start.Y += start_height * Globals.Config.saved.HeightScale;
        end.Y += end_height * Globals.Config.saved.HeightScale;

        if (alpha >= 1) {
            State = LineState.Idle;
            LastTargetId = Target.ObjectId;
        }

        Position = start;
        TargetPosition = Vector3.Lerp(start, end, alpha);
        LastTargetPosition2 = Vector3.Lerp(Self.Position, Target.Position, alpha);
    }

    private void UpdateStateDying_Anim(float mid_height) {
        float alpha = Math.Min(1, (StateTime / Globals.Config.saved.NoTargetFadeTime) * Globals.Config.saved.DeathAnimationTimeScale);

        switch (Globals.Config.saved.DeathAnimation) {
            case (LineDeathAnimation.Linear):
                MidHeight = MathUtils.Lerpf(mid_height, 0, alpha);
                break;
            case (LineDeathAnimation.Square):
                MidHeight = MathUtils.QuadraticLerpf(mid_height, 0, alpha);
                break;
            case (LineDeathAnimation.Cube):
                MidHeight = MathUtils.CubicLerpf(mid_height, 0, alpha);
                break;
        }
    }

    private void UpdateStateDying() {
        Vector3 start = Self.Position;
        Vector3 end = LastTargetPosition;
        float start_height = Self.CursorHeight;
        float end_height = LastTargetHeight;
        float mid_height = (start_height + end_height) * 0.5f;
        float alpha = Math.Max(0, Math.Min(1, StateTime / Globals.Config.saved.NoTargetFadeTime));

        UpdateStateDying_Anim(mid_height);

        start.Y += start_height * Globals.Config.saved.HeightScale;
        end.Y += end_height * Globals.Config.saved.HeightScale;

        if (alpha >= 1) {
            ShouldDelete = true;
        }

        Position = start;
        TargetPosition = Vector3.Lerp(end, start, alpha);
        LastTargetPosition2 = Vector3.Lerp(Self.Position, LastTargetPosition, alpha);
    }

    private void UpdateStateSwitching() {
        Vector3 start = LastTargetPosition;
        Vector3 end = Target.Position;
        float start_height = Self.CursorHeight;
        float end_height = Target.CursorHeight;
        float mid_height = (start_height + end_height) * 0.5f;
        Vector3 target_position = Target.Position;
        float alpha = Math.Max(0, Math.Min(1, StateTime / Globals.Config.saved.NewTargetEaseTime));

        start.Y += LastTargetHeight * Globals.Config.saved.HeightScale;
        end.Y += end_height * Globals.Config.saved.HeightScale;

        if (alpha >= 1) {
            State = LineState.Idle;
            LastTargetId = Target.ObjectId;
        }

        Position = Self.Position;
        Position.Y += start_height * Globals.Config.saved.HeightScale;

        TargetPosition = Vector3.Lerp(start, end, alpha);
        LastTargetPosition2 = Vector3.Lerp(LastTargetPosition, target_position, alpha);
        MidHeight = MathUtils.Lerpf(LastMidHeight, mid_height, alpha);
    }

    private void UpdateStateIdle() {
        float start_height = Self.CursorHeight;
        float end_height = Target.CursorHeight;
        float start_height_scaled = start_height * Globals.Config.saved.HeightScale;
        float end_height_scaled = end_height * Globals.Config.saved.HeightScale;
        float mid_height = (start_height + end_height) * 0.5f;

        LastTargetHeight = end_height;
        MidHeight = mid_height;

        Position = Self.Position;

        TargetPosition = Target.Position;
        LastTargetPosition = TargetPosition;
        LastTargetPosition2 = LastTargetPosition;

        Position.Y += start_height_scaled;
        TargetPosition.Y += end_height_scaled;
    }

    private unsafe void UpdateState() {
        bool has_target = Target != null;
        bool new_target = false;

        if (Framework == null) {
            Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        }

        if (has_target != HadTarget) {
            if (has_target) {
                if (State == LineState.Dying) {
                    LastTargetPosition = LastTargetPosition2;
                }

                LastTargetId = Target.ObjectId;
                State = LineState.NewTarget;
                StateTime = 0;
            }
            else {
                if (State == LineState.Switching || State == LineState.NewTarget) {
                    LastTargetPosition = LastTargetPosition2;
                }

                State = LineState.Dying;
                StateTime = 0;
            }
        }

        if (has_target && HadTarget) {
            if (Target.ObjectId != LastTargetId) {
                LastTargetId = Target.ObjectId;
                new_target = true;
            }

            if (new_target) {
                if (State == LineState.Switching) {
                    LastTargetPosition = LastTargetPosition2;
                }

                State = LineState.Switching;
                LastMidHeight = MidHeight;
                StateTime = 0;
            }
        }

        switch (State) {
            case LineState.NewTarget:
                UpdateStateNewTarget();
                break;
            case LineState.Dying:
                UpdateStateDying();
                break;
            case LineState.Switching:
                UpdateStateSwitching();
                break;
            case LineState.Idle:
                UpdateStateIdle();
                break;
        }

        UpdateMidPosition();

        StateTime += Framework->FrameDeltaTime;
        HadTarget = has_target;
    }

    private void UpdateColors() {
        float alpha = 1.0f;

        if (Globals.Config.saved.LineColor.Visible) {
            LineColor.CopyValues(Globals.Config.saved.LineColor.Color);
            OutlineColor.CopyValues(Globals.Config.saved.LineColor.OutlineColor);
        }

        if (Target == null) {
            LineColor.CopyValues(LastLineColor);
            OutlineColor.CopyValues(LastOutlineColor);
        }
        else {
            ABGR tempLineColor = new ABGR(0, 0, 0, 0);
            ABGR tempOutlineColor = new ABGR(0, 0, 0, 0);
            int highestPriority = -1;
            foreach (TargetSettingsPair settings in Globals.Config.LineColors) {
                int priority = settings.GetPairPriority();
                if (priority > highestPriority) {
                    bool should_copy = CompareTargetSettings(ref settings.From, ref Self.Settings);
                    if (should_copy) {
                        should_copy = CompareTargetSettings(ref settings.To, ref Target.Settings);
                    }
                    if (should_copy) {
                        highestPriority = priority;
                        tempLineColor.CopyValues(settings.LineColor.Color);
                        tempOutlineColor.CopyValues(settings.LineColor.OutlineColor);
                        UseQuad = settings.LineColor.UseQuad;
                        Visible = settings.LineColor.Visible;
                    }
                }
            }

            LineColor.CopyValues(tempLineColor);
            OutlineColor.CopyValues(tempOutlineColor);
            LastLineColor.CopyValues(LineColor);
            LastOutlineColor.CopyValues(OutlineColor);
        }

        if (Globals.Config.saved.BreathingEffect) {
            alpha = (1.0f - Globals.Config.saved.WaveAmplitudeOffset) + (float)Math.Cos(Globals.Runtime * Globals.Config.saved.WaveFrequencyScalar) * Globals.Config.saved.WaveAmplitudeOffset;
        }

        LineColor.a = (byte)((float)LineColor.a * alpha);
        OutlineColor.a = (byte)((float)LineColor.a * alpha);
    }

    private bool UpdateVisibility() {
        bool occlusion = Globals.Config.saved.OcclusionCulling;

#if (!PROBABLY_BAD)
        if (ThisObject.IsBattleNPC()) {
            occlusion = true;
        }
#endif

        bool vis0 = Self.IsVisible(occlusion);
        bool vis1 = false;

        if (Target != null) {
            vis1 = Target.IsVisible(occlusion);
        }
        else {
            vis1 = Globals.IsVisible(TargetPosition, occlusion);
        }

        DrawBeginCap = Service.GameGui.WorldToScreen(Position, out ScreenPos);
        DrawEndCap = Service.GameGui.WorldToScreen(TargetPosition, out TargetScreenPos);
        DrawMid = Service.GameGui.WorldToScreen(MidPosition, out MidScreenPos);

        if (Globals.Config.saved.SolidColor == false) {
            for (int index = 0; index < Points.Length; index++) {
                float t = index * LinePointStep;
                Vector3 point = UseQuad
                    ? EvaluateQuadratic(Position, MidPosition, TargetPosition, t)
                    : EvaluateCubic(Position, MidPosition, MidPosition, TargetPosition, t);

                bool vis = Service.GameGui.WorldToScreen(point, out Vector2 screenPoint);
                Points[index] = new LinePoint(screenPoint, vis);
            }
        }

        if (!(DrawBeginCap || DrawEndCap || DrawMid)) {
            return false;
        }

        if (occlusion) {
            if (!DrawBeginCap) {
                vis0 = false;
            }

            if (!DrawEndCap) {
                vis1 = false;
            }

            if (!(vis0 && vis1)) {
                return false;
            }
        }

        return true;
    }

    public unsafe void Draw() {
        GameObject? _target = Service.ObjectTable.SearchById(Self.TargetObjectId);
        if (_target != null) {
            Target = new GameObjectHelper(_target.Address);
        }
        else {
            Target = null;
        }

        Self.UpdateTargetSettings();
        UpdateState();
        UpdateColors();

        if (!UpdateVisibility()) {
            return;
        }

        if (Visible) {
            if (Globals.Config.saved.SolidColor) {
                DrawSolidLine();
            }
            else {
                DrawFancyLine();
            }
        }
    }
}

using Dalamud.Game.ClientState.Objects.Types;
using DrahsidLib;
using ImGuiNET;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using static TargetLines.ClassJobHelper;

namespace TargetLines;

internal struct LinePoint {
    public Vector2 Pos;
    public bool Visible;
    public float Dot;

    public LinePoint(Vector2 pos, bool visible, float dot) {
        Pos = pos;
        Visible = visible;
        Dot = dot;
    }
}

internal class TargetLine {
    public enum LineState {
        NewTarget, // new target (from no target)
        Dying, // no target, fading away
        Switching, // switching to different target
        Idle // being targeted
    };

    public GameObject Self;

    public LineState State = LineState.NewTarget;
    public bool ShouldDelete = false;

    private Vector2 ScreenPos = new Vector2();
    private Vector2 MidScreenPos = new Vector2();
    private Vector2 TargetScreenPos = new Vector2();

    private Vector3 Position = new Vector3();
    private Vector3 MidPosition = new Vector3();
    private Vector3 TargetPosition = new Vector3();
    private RGBA LineColor = new RGBA(0, 0, 0, 0);
    private RGBA OutlineColor = new RGBA(0, 0, 0, 0);

    private Vector3 LastTargetPosition = new Vector3();
    private Vector3 LastTargetPosition2 = new Vector3();
    private RGBA LastLineColor = new RGBA(0, 0, 0, 0);
    private RGBA LastOutlineColor = new RGBA(0, 0, 0, 0);
    
    private bool UseQuad = false;
    private bool Visible = true;

    private bool HasTarget = false;
    private bool HadTarget = false;
    private ulong LastTargetId = 0;

    private bool DrawBeginCap = false;
    private bool DrawMid = false;
    private bool DrawEndCap = false;

    private float StateTime = 0.0f;
    private float MidHeight = 0.0f;
    private float LastMidHeight = 0.0f;
    private float LastTargetHeight = 0.0f;

    private Stopwatch FPPTransition = new Stopwatch();
    private float FPPLastTransition = 0.0f;

    private LinePoint[] Points;
    private float LinePointStep;

    private const float HPI = MathF.PI * 0.5f;

    private readonly Vector2 uv1 = new Vector2(0, 0);
    private readonly Vector2 uv2 = new Vector2(0, 1.0f);
    private readonly Vector2 uv3 = new Vector2(1.0f, 1.0f);
    private readonly Vector2 uv4 = new Vector2(1.0f, 0);

    RGBA tempLineColor = new RGBA(0, 0, 0, 0);
    RGBA tempOutlineColor = new RGBA(0, 0, 0, 0);

    public TargetLine(GameObject obj) {
        Self = obj;
        if (Self.TargetObject != null) {
            LastTargetId = Self.TargetObject.TargetObjectId;
            LastTargetPosition = Self.TargetObject.Position;
            LastTargetPosition2 = LastTargetPosition;
        }
        else {
            LastTargetPosition = Self.Position;
            LastTargetPosition2 = LastTargetPosition;
        }

        InitializeLinePoints();
    }

    private void InitializeLinePoints(int sampleCount = 0) {
        Points = new LinePoint[Globals.Config.saved.TextureCurveSampleCount];
        Points = new LinePoint[sampleCount];
        LinePointStep = 1.0f / (float)(Points.Length - 1);
    }

    private Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) {
        if (t == 0) {
            return p0;
        }

        if (t == 1) {
            return p3;
        }

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

    private Vector3 EvaluateQuadratic(Vector3 p0, Vector3 p1, Vector3 p2, float t) {
        float mt = 1 - t;

        if (t == 0) {
            return p0;
        }

        if (t == 1) {
            return p2;
        }

        Vector3 point = mt * mt * p0
            + 2 * mt * t * p1
            + t * t * p2;
        return point;
    }

    private void DrawSolidLine() {
        ImDrawListPtr drawlist = ImGui.GetWindowDrawList();
        float outlineThickness = Globals.Config.saved.OutlineThickness;
        float lineThickness = Globals.Config.saved.LineThickness;

        if (UseQuad) {
            if (outlineThickness > 0) {
                drawlist.AddBezierQuadratic(ScreenPos, MidScreenPos, TargetScreenPos, OutlineColor.raw, outlineThickness);
            }
            if (lineThickness > 0) {
                drawlist.AddBezierQuadratic(ScreenPos, MidScreenPos, TargetScreenPos, LineColor.raw, lineThickness);
            }
        }
        else {
            if (outlineThickness > 0) {
                drawlist.AddBezierCubic(ScreenPos, MidScreenPos, MidScreenPos, TargetScreenPos, OutlineColor.raw, outlineThickness);
            }
            if (lineThickness > 0) {
                drawlist.AddBezierCubic(ScreenPos, MidScreenPos, MidScreenPos, TargetScreenPos, LineColor.raw, lineThickness);
            }
        }
    }

    private unsafe void DrawFancyLine() {
        ImDrawListPtr drawlist = ImGui.GetWindowDrawList();
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

        bool segmentOccluded;
        bool firstSegmentOccluded = false;
        bool lastSegmentOccluded = false;

        for (int index = 0; index < sampleCount - 1; index++) {
            LinePoint point = Points[index];
            LinePoint nextpoint = Points[index + 1];
            if (!point.Visible && !nextpoint.Visible) {
                continue;
            }

            // skip lines that intersect the camera in first person
            if (!Globals.IsAngleThetaInsidePerspective(point.Dot) || !Globals.IsAngleThetaInsidePerspective(nextpoint.Dot)) {
                if (index == 0) {
                    firstSegmentOccluded = true;
                }
                else if (index == sampleCount - 2) {
                    lastSegmentOccluded = true;
                }
                continue;
            }

            Vector2 p1 = point.Pos;
            Vector2 p2 = nextpoint.Pos;

            Vector2 dir = Vector2.Normalize(p2 - p1);
            Vector2 perp = new Vector2(-dir.Y, dir.X) * lineThickness;
            Vector2 perpo = new Vector2(-dir.Y, dir.X) * outlineThickness;

            Vector2 p1_perp = p1 + perp;
            Vector2 p2_perp = p2 + perp;
            Vector2 p1_perp_inv = p1 - perp;
            Vector2 p2_perp_inv = p2 - perp;

            Vector2 p1_perpo = p1 + perpo;
            Vector2 p2_perpo = p2 + perpo;
            Vector2 p1_perp_invo = p1 - perpo;
            Vector2 p2_perp_invo = p2 - perpo;

            RGBA* linecolor_index = stackalloc RGBA[1];
            RGBA* outlinecolor_index = stackalloc RGBA[1];

            linecolor_index->raw = LineColor.raw;
            outlinecolor_index->raw = OutlineColor.raw;

            if (shouldCalculatePulsatingEffect) {
                float p = index * LinePointStep;
                float pulsatingAlpha = MathF.Sin(-currentTime * pulsatingSpeed + (p * MathF.PI) + HPI);
                pulsatingAlpha = Math.Clamp(pulsatingAlpha * pulsatingAmplitude + min, min, max);
                linecolor_index->a = (byte)pulsatingAlpha;
                outlinecolor_index->a = (byte)pulsatingAlpha;
            }

            if (shouldFadeToEnd) {
                float alphaFade = MathUtils.Lerpf((float)outlinecolor_index->a,
                    (float)(outlinecolor_index->a * Globals.Config.saved.FadeToEndScalar),
                    (float)index * LinePointStep);

                outlinecolor_index->a = (byte)alphaFade;
            }

            segmentOccluded = linecolor_index->a == 0 || lineThickness == 0;
            if (Globals.Config.saved.UIOcclusion && !segmentOccluded) {
                segmentOccluded = UICollision.OcclusionCheck(p1_perp_inv, p2_perp_inv, p2_perp, p1_perp);
                if (index == 0) {
                    firstSegmentOccluded = segmentOccluded;
                }
                if (index == sampleCount - 2) {
                    lastSegmentOccluded = segmentOccluded;
                }
            }

            if (!segmentOccluded) {
                drawlist.AddImageQuad(Globals.LineTexture.ImGuiHandle, p1_perp_inv, p2_perp_inv, p2_perp, p1_perp, uv1, uv2, uv3, uv4, linecolor_index->raw);

                if (outlinecolor_index->a != 0 && outlineThickness != 0) {
                    drawlist.AddImageQuad(Globals.OutlineTexture.ImGuiHandle, p1_perp_invo, p2_perp_invo, p2_perpo, p1_perpo, uv1, uv2, uv3, uv4, outlinecolor_index->raw);
                }
            }
        }

        Vector2 start_dir = Vector2.Normalize(Points[1].Pos - Points[0].Pos);
        Vector2 end_dir = Vector2.Normalize(Points[sampleCount - 1].Pos - Points[sampleCount - 2].Pos);
        Vector2 start_perp = new Vector2(-start_dir.Y, start_dir.X) * lineThickness;
        Vector2 end_perp = new Vector2(-end_dir.Y, end_dir.X) * lineThickness;

        Vector2 start_p1 = Points[0].Pos - start_dir;
        Vector2 start_p2 = Points[0].Pos + start_dir;

        start_p1.X -= lineThickness * 0.45f;
        start_p1.Y -= lineThickness * 0.45f;
        start_p2.X += lineThickness * 0.45f;
        start_p2.Y += lineThickness * 0.45f;

        Vector2 end_p1 = Points[sampleCount - 1].Pos - end_dir;
        Vector2 end_p2 = Points[sampleCount - 1].Pos + end_dir;

        end_p1.X -= lineThickness * 0.45f;
        end_p1.Y -= lineThickness * 0.45f;
        end_p2.X += lineThickness * 0.45f;
        end_p2.Y += lineThickness * 0.45f;

        RGBA* linecolor_end = stackalloc RGBA[1];
        linecolor_end->raw = LineColor.raw;
        if (shouldFadeToEnd) {
            linecolor_end->a = (byte)(linecolor_end->a * Globals.Config.saved.FadeToEndScalar);
        }

        if (DrawBeginCap && !firstSegmentOccluded) {
            drawlist.AddImage(Globals.EdgeTexture.ImGuiHandle, start_p1, start_p2, uv1, uv3, LineColor.raw);
        }

        if (DrawEndCap && !lastSegmentOccluded) {
            drawlist.AddImage(Globals.EdgeTexture.ImGuiHandle, end_p1, end_p2, uv1, uv3, linecolor_end->raw);
        }
    }

    private void UpdateMidPosition() {
        MidPosition = (Position + TargetPosition) * 0.5f;

        if (Self.GetIsPlayerCharacter()) {
            MidPosition.Y += Globals.Config.saved.PlayerHeightBump;
        }
        else if (Self.GetIsBattleChara()) {
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

    private unsafe Vector3 GetTransitionPosition(Vector3 startPosition, Vector3 endPosition, float transition, bool isFPP) {
        if (transition == 0) {
            FPPTransition.Stop();
            FPPTransition.Reset();
            if (isFPP) {
                return endPosition;
            }
        } else {
            if (!FPPTransition.IsRunning || MathF.Sign(transition) != MathF.Sign(FPPLastTransition)) {
                FPPTransition.Restart();
            }
            FPPLastTransition = transition;
        }
    
        float t = FPPTransition.ElapsedMilliseconds / 1000.0f / 0.49f;

        if (transition < 0) {
            t *= 0.5f;
        }
        else {
            t *= 2.0f;
        }

        if (t > 1) {
            t = 1;
        }

        return Vector3.Lerp(transition > 0 ? startPosition : endPosition, transition > 0 ? endPosition : startPosition, t);
    }

    private unsafe Vector3 CalculatePosition(Vector3 tppPosition, float height, bool isPlayer, out bool fpp) {
        Vector3 position = tppPosition;
        fpp = false;
        if (isPlayer) {
            fpp = Globals.IsInFirstPerson();
            var cam = Service.CameraManager->Camera;
            float transition = Marshal.PtrToStructure<float>(((IntPtr)cam) + 0x1E0); // TODO: place in struct
            if (fpp || transition != 0 || FPPTransition.IsRunning) {
                Vector3 cameraPosition = Globals.WorldCamera_GetPos() + (-2.0f * Globals.WorldCamera_GetForward());
                cameraPosition.Y -= height;
                position = GetTransitionPosition(tppPosition, cameraPosition, transition, fpp);
            }
        }
        return position;
    }
    
    public unsafe Vector3 GetSourcePosition(out bool fpp) {
        return CalculatePosition(Self.Position, Self.GetCursorHeight() - 0.2f, Self.ObjectId == Service.ClientState.LocalPlayer.ObjectId, out fpp);
    }
    
    public unsafe Vector3 GetTargetPosition(out bool fpp) {
        return CalculatePosition(Self.TargetObject.Position, Self.TargetObject.GetCursorHeight() - 0.2f, Self.TargetObject.ObjectId == Service.ClientState.LocalPlayer.ObjectId, out fpp);
    }


    private void UpdateStateNewTarget() {
        bool fpp0;
        bool fpp1;
        Vector3 _source = GetSourcePosition(out fpp0);
        Vector3 _target = GetTargetPosition(out fpp1);
        Vector3 start = _source;
        Vector3 end = _target;

        float start_height = Self.GetCursorHeight();
        float end_height = Self.TargetObject.GetCursorHeight();
        float mid_height = (start_height + end_height) * 0.5f;

        float start_height_scaled = (fpp0 ? 0 : start_height) * Globals.Config.saved.HeightScale;
        float end_height_scaled = (fpp1 ? 0 : end_height) * Globals.Config.saved.HeightScale;

        float alpha = Math.Max(0, Math.Min(1, StateTime / Globals.Config.saved.NewTargetEaseTime));

        LastTargetHeight = end_height;
        MidHeight = mid_height;

        start.Y += start_height_scaled;
        end.Y += end_height_scaled;

        if (alpha >= 1) {
            State = LineState.Idle;
            LastTargetId = Self.TargetObject.ObjectId;
        }

        Position = start;
        TargetPosition = Vector3.Lerp(start, end, alpha);
        LastTargetPosition2 = Vector3.Lerp(_source, _target, alpha);
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
        bool fpp;
        Vector3 _source = GetSourcePosition(out fpp);

        Vector3 start = _source;
        Vector3 end = LastTargetPosition;

        float start_height = Self.GetCursorHeight();
        float end_height = LastTargetHeight;
        float mid_height = (start_height + end_height) * 0.5f;

        float start_height_scaled = (fpp ? 0 : start_height) * Globals.Config.saved.HeightScale;
        float end_height_scaled = end_height * Globals.Config.saved.HeightScale;

        float alpha = Math.Max(0, Math.Min(1, StateTime / Globals.Config.saved.NoTargetFadeTime));

        UpdateStateDying_Anim(mid_height);

        start.Y += start_height_scaled;
        end.Y += end_height_scaled;

        if (alpha >= 1) {
            ShouldDelete = true;
        }

        Position = start;
        TargetPosition = Vector3.Lerp(end, start, alpha);
        LastTargetPosition2 = Vector3.Lerp(_source, LastTargetPosition, alpha);
    }

    private void UpdateStateSwitching() {
        bool fpp0;
        bool fpp1;
        Vector3 _source = GetSourcePosition(out fpp0);
        Vector3 _target = GetTargetPosition(out fpp1);

        Vector3 start = LastTargetPosition;
        Vector3 end = _target;

        float start_height = Self.GetCursorHeight();
        float end_height = Self.TargetObject.GetCursorHeight();
        float mid_height = (start_height + end_height) * 0.5f;

        float start_height_scaled = (fpp0 ? 0 : start_height) * Globals.Config.saved.HeightScale;
        float end_height_scaled = (fpp1 ? 0 : end_height) * Globals.Config.saved.HeightScale;

        float alpha = Math.Max(0, Math.Min(1, StateTime / Globals.Config.saved.NewTargetEaseTime));

        start.Y += LastTargetHeight * Globals.Config.saved.HeightScale;
        end.Y += end_height_scaled * Globals.Config.saved.HeightScale;

        if (alpha >= 1) {
            State = LineState.Idle;
            LastTargetId = Self.TargetObject.ObjectId;
        }

        Position = _source;
        Position.Y += start_height_scaled * Globals.Config.saved.HeightScale;

        TargetPosition = Vector3.Lerp(start, end, alpha);
        LastTargetPosition2 = Vector3.Lerp(LastTargetPosition, _target, alpha);
        MidHeight = MathUtils.Lerpf(LastMidHeight, mid_height, alpha);
    }

    private void UpdateStateIdle() {
        bool fpp0;
        bool fpp1;
        Vector3 _source = GetSourcePosition(out fpp0);
        Vector3 _target = GetTargetPosition(out fpp1);

        float start_height = Self.GetCursorHeight();
        float end_height = Self.TargetObject.GetCursorHeight();
        float mid_height = (start_height + end_height) * 0.5f;

        float start_height_scaled = (fpp0 ? 0 : start_height) * Globals.Config.saved.HeightScale;
        float end_height_scaled = (fpp1 ? 0 : end_height) * Globals.Config.saved.HeightScale;

        LastTargetHeight = end_height;
        MidHeight = mid_height;

        Position = _source;

        TargetPosition = _target;
        LastTargetPosition = TargetPosition;
        LastTargetPosition2 = LastTargetPosition;

        Position.Y += start_height_scaled;
        TargetPosition.Y += end_height_scaled;
    }

    private unsafe void UpdateState() {
        bool new_target = false;

        if (HasTarget != HadTarget) {
            if (HasTarget) {
                if (State == LineState.Dying) {
                    LastTargetPosition = LastTargetPosition2;
                }

                LastTargetId = Self.TargetObject.ObjectId;
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

        if (HasTarget && HadTarget) {
            if (Self.TargetObject.ObjectId != LastTargetId) {
                LastTargetId = Self.TargetObject.ObjectId;
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

        StateTime += Globals.Framework->FrameDeltaTime;
        HadTarget = HasTarget;
    }

    private void UpdateColors() {
        float alpha = 1.0f;

        if (Globals.Config.saved.LineColor.Visible) {
            LineColor.raw = Globals.Config.saved.LineColor.Color.raw;
            OutlineColor.raw = Globals.Config.saved.LineColor.OutlineColor.raw;
        }

        if (Self.TargetObject == null) {
            LineColor.raw = LastLineColor.raw;
            OutlineColor.raw = LastOutlineColor.raw;
        }
        else {
            int highestPriority = -1;
            foreach (TargetSettingsPair settings in Globals.Config.LineColors) {
                int priority = settings.GetPairPriority();
                if (priority > highestPriority) {
                    TargetSettings SelfSettings = Self.GetTargetSettings();
                    TargetSettings TargSettings = Self.TargetObject.GetTargetSettings();

                    bool should_copy = CompareTargetSettings(ref settings.From, ref SelfSettings);
                    if (should_copy) {
                        should_copy = CompareTargetSettings(ref settings.To, ref TargSettings);
                    }
                    if (should_copy) {
                        highestPriority = priority;
                        tempLineColor.raw = settings.LineColor.Color.raw;
                        tempOutlineColor.raw = settings.LineColor.OutlineColor.raw;
                        UseQuad = settings.LineColor.UseQuad;
                        Visible = settings.LineColor.Visible;
                    }
                }
            }

            LineColor.raw = tempLineColor.raw;
            OutlineColor.raw = tempOutlineColor.raw;
            LastLineColor.raw = LineColor.raw;
            LastOutlineColor.raw = OutlineColor.raw;
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
        if (Self.GetIsBattleNPC()) {
            occlusion = true;
        }
#endif

        bool vis0 = Self.IsVisible(occlusion);
        bool vis1 = false;
        bool vis2 = false;

        if (HasTarget) {
            vis1 = Self.TargetObject.IsVisible(occlusion);
        }
        else {
            vis1 = Globals.IsVisible(TargetPosition, occlusion);
        }

        vis2 = Globals.IsVisible(MidPosition, occlusion);

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
                Points[index].Pos = screenPoint;
                Points[index].Visible = vis;
                Points[index].Dot = Globals.GetAngleThetaToCamera(point);
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

            if (vis0 || vis1 || vis2) {
                return true;
            }
            return false;
        }

        return true;
    }

    public unsafe void Draw() {
        int sampleCountTarget;

        if (Globals.Config.saved.SolidColor == false) {
            if (Globals.Config.saved.DynamicSampleCount) {
                int min = Globals.Config.saved.TextureCurveSampleCountMin;
                int max = Globals.Config.saved.TextureCurveSampleCountMax;
                float thickScalar = Globals.Config.saved.LineThickness / 32.0f;
                if (thickScalar < 1.0f) {
                    thickScalar = 1.0f;
                }

                sampleCountTarget = min + ((int)MathF.Floor(1.5f + (TargetPosition - Position).Length())) * 2;

                if (sampleCountTarget > max) {
                    sampleCountTarget = max;
                }

                sampleCountTarget = (int)MathF.Floor(sampleCountTarget * thickScalar);

                // less chonky lines in first person
                if (Globals.IsInFirstPerson()) {
                    sampleCountTarget *= 2;
                }

                sampleCountTarget -= (~sampleCountTarget & 1); // make it odd so there is a peak
            }
            else {
                sampleCountTarget = Globals.Config.saved.TextureCurveSampleCount;
            }

            

            if (Points.Length != sampleCountTarget) {
                InitializeLinePoints(sampleCountTarget);
            }

            if (Globals.Config.saved.DebugDynamicSampleCount) {
                ImDrawListPtr drawlist = ImGui.GetWindowDrawList();
                drawlist.AddText(ScreenPos, 0xFF000000, sampleCountTarget.ToString());
                drawlist.AddText(MidScreenPos, 0xFF00FFFF, sampleCountTarget.ToString());
                drawlist.AddText(TargetScreenPos, 0xFFFFFFFF, sampleCountTarget.ToString());
            }
        }

        HasTarget = Self.TargetObject != null;

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

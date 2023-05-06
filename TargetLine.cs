using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using static TargetLines.ClassJobHelper;

namespace TargetLines;

internal class TargetLine
{
    public enum LineState {
        NewTarget, // new target (from no target)
        Dying, // no target, fading away
        Switching, // switching to different target
        Idle // being targeted
    };

    public GameObjectHelper ThisObject;
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
    private bool Visible = false;

    private bool HadTarget = false;
    private ulong LastTargetId = 0;

    private bool DrawBeginCap = false;
    private bool DrawEndCap = false;

    private float StateTime = 0.0f;
    private float MidHeight = 0.0f;
    private float LastMidHeight = 0.0f;
    private float LastTargetHeight = 0.0f;

    private unsafe Framework* Framework = null;

    public TargetLine(GameObjectHelper obj) {
        ThisObject = obj;
        if (ThisObject.TargetObject != null) {
            LastTargetId = ThisObject.TargetObject.TargetObjectId;
            LastTargetPosition = ThisObject.TargetObject.Position;
            LastTargetPosition2 = LastTargetPosition;
        }
        else {
            LastTargetPosition = ThisObject.Position;
            LastTargetPosition2 = LastTargetPosition;
        }
    }

    Vector2 EvaluateCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) {
        float t2 = t * t;
        float t3 = t2 * t;
        float mt = 1 - t;
        float mt2 = mt * mt;
        float mt3 = mt2 * mt;
        Vector2 point =
            mt3 * p0 +
            3 * mt2 * t * p1 +
            3 * mt * t2 * p2 +
            t3 * p3;
        return point;
    }

    Vector2 EvaluateQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, float t) {
        float t2 = t * t;
        float mt = 1 - t;
        float mt2 = mt * mt;
        Vector2 point =
            mt2 * p0 +
            2 * mt * t * p1 +
            t2 * p2;
        return point;
    }

    private void DrawSolidLine() {
        ImDrawListPtr drawlist = ImGui.GetWindowDrawList();

        if (UseQuad) {
            if (Globals.Config.saved.OutlineThickness > 0) {
                drawlist.AddBezierQuadratic(ScreenPos, MidScreenPos, TargetScreenPos, OutlineColor.GetRaw(), Globals.Config.saved.OutlineThickness);
            }
            if (Globals.Config.saved.LineThickness > 0) {
                drawlist.AddBezierQuadratic(ScreenPos, MidScreenPos, TargetScreenPos, LineColor.GetRaw(), Globals.Config.saved.LineThickness);
            }
        }
        else {
            if (Globals.Config.saved.OutlineThickness > 0) {
                drawlist.AddBezierCubic(ScreenPos, MidScreenPos, MidScreenPos, TargetScreenPos, OutlineColor.GetRaw(), Globals.Config.saved.OutlineThickness);
            }
            if (Globals.Config.saved.LineThickness > 0) {
                drawlist.AddBezierCubic(ScreenPos, MidScreenPos, MidScreenPos, TargetScreenPos, LineColor.GetRaw(), Globals.Config.saved.LineThickness);
            }
        }
    }

    private void DrawFancyLine() {
        ImDrawListPtr drawlist = ImGui.GetWindowDrawList();
        Vector2[] points = new Vector2[Globals.Config.saved.TextureCurveSampleCount];
        Vector2 uv1;
        Vector2 uv2;

        for (int index = 0; index < Globals.Config.saved.TextureCurveSampleCount; index++) {
            float t = (float)index / (Globals.Config.saved.TextureCurveSampleCount - 1);
            if (UseQuad) {
                points[index] = EvaluateQuadratic(ScreenPos, MidScreenPos, TargetScreenPos, t);
            }
            else {
                points[index] = EvaluateCubic(ScreenPos, MidScreenPos, MidScreenPos, TargetScreenPos, t);
            }
        }

        float currentTime = (float)Globals.Runtime;
        float pulsatingSpeed = Globals.Config.saved.WaveFrequencyScalar;
        float max = LineColor.a;
        float min = LineColor.a * 0.5f;
        float pulsatingAmplitude = (max - min) * (1.0f - Globals.Config.saved.WaveAmplitudeOffset);

        for (int index = 0; index < Globals.Config.saved.TextureCurveSampleCount - 1; index++) {
            ABGR linecolor_index = new ABGR(0, 0, 0, 0);
            ABGR outlinecolor_index = new ABGR(0, 0, 0, 0);
            Vector2 p1 = points[index];
            Vector2 p2 = points[index + 1];
            uv1 = new Vector2(0, 0);
            uv2 = new Vector2(1.0f, 1.0f);

            Vector2 dir = Vector2.Normalize(p2 - p1);
            Vector2 perp = new Vector2(-dir.Y, dir.X);
            Vector2 p1_perp = p1 + perp * Globals.Config.saved.LineThickness * 2.0f;
            Vector2 p2_perp = p2 + perp * Globals.Config.saved.LineThickness * 2.0f;
            Vector2 p1_perp_inv = p1 - perp * Globals.Config.saved.LineThickness * 2.0f;
            Vector2 p2_perp_inv = p2 - perp * Globals.Config.saved.LineThickness * 2.0f;

            Vector2 p1_perpo = p1 + perp * Globals.Config.saved.OutlineThickness * 2.0f;
            Vector2 p2_perpo = p2 + perp * Globals.Config.saved.OutlineThickness * 2.0f;
            Vector2 p1_perp_invo = p1 - perp * Globals.Config.saved.OutlineThickness * 2.0f;
            Vector2 p2_perp_invo = p2 - perp * Globals.Config.saved.OutlineThickness * 2.0f;

            linecolor_index.CopyValues(LineColor);
            outlinecolor_index.CopyValues(OutlineColor);


            // Calculate pulsating alpha value
            if (Globals.Config.saved.PulsingEffect) {
                float p = (float)index / ((float)Globals.Config.saved.TextureCurveSampleCount - 1);
                float pulsatingAlpha = MathF.Sin(-currentTime * pulsatingSpeed + (p * MathF.PI) + (MathF.PI / 2.0f));
                pulsatingAlpha *= pulsatingAmplitude;
                pulsatingAlpha += min;

                if (pulsatingAlpha < min) {
                    pulsatingAlpha = min;
                }
                if (pulsatingAlpha > max) {
                    pulsatingAlpha = max;
                }

                linecolor_index.a = (byte)pulsatingAlpha;
                outlinecolor_index.a = (byte)pulsatingAlpha;
            }

            if (Globals.Config.saved.FadeToEnd) {
                outlinecolor_index.a = (byte)MathUtils.Lerpf((float)outlinecolor_index.a,
                    (float)(outlinecolor_index.a * Globals.Config.saved.FadeToEndScalar),
                    (float)index / (float)(Globals.Config.saved.TextureCurveSampleCount - 1)
                );

                outlinecolor_index.a = (byte)MathUtils.Lerpf((float)outlinecolor_index.a,
                    (float)(outlinecolor_index.a * Globals.Config.saved.FadeToEndScalar),
                    (float)index / (float)(Globals.Config.saved.TextureCurveSampleCount - 1)
                );
            }

            if (linecolor_index.a != 0 && Globals.Config.saved.LineThickness != 0) {
                drawlist.AddImageQuad(Globals.LineTexture.ImGuiHandle, p1_perp_inv, p2_perp_inv, p2_perp, p1_perp, uv1, new Vector2(uv1.X, uv2.Y), uv2, new Vector2(uv2.X, uv1.Y), linecolor_index.GetRaw());
            }

            if (outlinecolor_index.a != 0 && Globals.Config.saved.OutlineThickness != 0) {
                drawlist.AddImageQuad(Globals.OutlineTexture.ImGuiHandle, p1_perp_invo, p2_perp_invo, p2_perpo, p1_perpo, uv1, new Vector2(uv1.X, uv2.Y), uv2, new Vector2(uv2.X, uv1.Y), outlinecolor_index.GetRaw());
            }
        }

        Vector2 start_dir = Vector2.Normalize(points[1] - points[0]);
        Vector2 end_dir = Vector2.Normalize(points[Globals.Config.saved.TextureCurveSampleCount - 1] - points[Globals.Config.saved.TextureCurveSampleCount - 2]);
        Vector2 start_perp = new Vector2(-start_dir.Y, start_dir.X) * Globals.Config.saved.LineThickness * 2.0f;
        Vector2 end_perp = new Vector2(-end_dir.Y, end_dir.X) * Globals.Config.saved.LineThickness * 2.0f;
        uv1 = new Vector2(0, 0);
        uv2 = new Vector2(1.0f, 1.0f);

        Vector2 start_p1 = points[0] - start_perp;
        Vector2 start_p2 = points[0] + start_perp;
        Vector2 end_p1 = points[Globals.Config.saved.TextureCurveSampleCount - 1] - end_perp;
        Vector2 end_p2 = points[Globals.Config.saved.TextureCurveSampleCount - 1] + end_perp;

        ABGR linecolor_end = new ABGR(0, 0, 0, 0);
        linecolor_end.CopyValues(LineColor);
        if (Globals.Config.saved.FadeToEnd) {
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
        GameObjectHelper target = ThisObject.Target;
        bool has_target = target != null;
        float height_fix = 1.0f;

        MidPosition = (Position + TargetPosition) * 0.5f;

        if (ThisObject.IsPlayerCharacter()) {
            MidPosition.Y += Globals.Config.saved.PlayerHeightBump;
        }
        else if (ThisObject.IsBattleChara()) {
            MidPosition.Y += Globals.Config.saved.EnemyHeightBump;
        }

        if (!UseQuad) {
            height_fix = 0.75f; // something wrong with my cubic math, this "fixes" it
        }

        if (State == LineState.Dying) {
            float alpha = StateTime / Globals.Config.saved.NoTargetFadeTime;
            height_fix *= 1.0f - alpha;
        }

        if (State == LineState.NewTarget) {
            float alpha = StateTime / Globals.Config.saved.NewTargetEaseTime;
            height_fix *= alpha;
        }

        MidPosition.Y += (MidHeight * Globals.Config.saved.ArcHeightScalar) * height_fix;
    }

    private void UpdateStateNewTarget() {
        Vector3 start = ThisObject.Position;
        Vector3 end = ThisObject.Target.Position;
        float start_height = ThisObject.GetHeight();
        float end_height = ThisObject.Target.GetHeight();
        float start_height_scaled = start_height * Globals.Config.saved.HeightScale;
        float end_height_scaled = end_height * Globals.Config.saved.HeightScale;
        float mid_height = (start_height + end_height) * 0.5f;
        float alpha = StateTime / Globals.Config.saved.NewTargetEaseTime;

        LastTargetHeight = end_height;
        MidHeight = mid_height;

        start.Y += start_height_scaled;
        end.Y += end_height_scaled;
        
        if (alpha < 0) {
            alpha = 0;
        }

        if (alpha >= 1) {
            alpha = 1.0f;
            State = LineState.Idle;
            LastTargetId = ThisObject.Target.ObjectId;
        }

        Position = start;
        TargetPosition = Vector3.Lerp(start, end, alpha);
        LastTargetPosition2 = Vector3.Lerp(ThisObject.Position, ThisObject.Target.Position, alpha);
    }

    private void UpdateStateDying_Anim(float mid_height) {
        float alpha = (StateTime / Globals.Config.saved.NoTargetFadeTime) * Globals.Config.saved.DeathAnimationTimeScale;

        if (alpha > 1.0f) {
            alpha = 1.0f;
        }

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
        Vector3 start = ThisObject.Position;
        Vector3 end = LastTargetPosition;
        float start_height = ThisObject.GetHeight();
        float end_height = LastTargetHeight;
        float start_height_scaled = start_height * Globals.Config.saved.HeightScale;
        float end_height_scaled = end_height * Globals.Config.saved.HeightScale;
        float mid_height = (start_height + end_height) * 0.5f;
        float alpha = StateTime / Globals.Config.saved.NoTargetFadeTime;

        UpdateStateDying_Anim(mid_height);

        start.Y += start_height_scaled;
        end.Y += end_height_scaled;

        if (alpha < 0) {
            alpha = 0;
        }

        if (alpha >= 1) {
            alpha = 1.0f;
            ShouldDelete = true;
        }

        Position = start;
        TargetPosition = Vector3.Lerp(end, start, alpha);
        LastTargetPosition2 = Vector3.Lerp(ThisObject.Position, LastTargetPosition, alpha);
    }

    private void UpdateStateSwitching() {
        Vector3 start = LastTargetPosition;
        Vector3 end = ThisObject.Target.Position;
        float start_height = ThisObject.GetHeight();
        float end_height = ThisObject.Target.GetHeight();
        float start_height_scaled = start_height * Globals.Config.saved.HeightScale;
        float end_height_scaled = end_height * Globals.Config.saved.HeightScale;
        float mid_height = (start_height + end_height) * 0.5f;
        float alpha = StateTime / Globals.Config.saved.NewTargetEaseTime;

        start.Y += LastTargetHeight * Globals.Config.saved.HeightScale;
        end.Y += end_height_scaled;

        if (alpha < 0) {
            alpha = 0;
        }

        if (alpha >= 1) {
            alpha = 1.0f;
            State = LineState.Idle;
            LastTargetId = ThisObject.Target.ObjectId;
        }

        Position = ThisObject.Position;
        Position.Y += start_height_scaled;

        TargetPosition = Vector3.Lerp(start, end, alpha);
        LastTargetPosition2 = Vector3.Lerp(LastTargetPosition, ThisObject.Target.Position, alpha);
        MidHeight = MathUtils.Lerpf(LastMidHeight, mid_height, alpha);
    }

    private void UpdateStateIdle() {
        float start_height = ThisObject.GetHeight();
        float end_height = ThisObject.Target.GetHeight();
        float start_height_scaled = start_height * Globals.Config.saved.HeightScale;
        float end_height_scaled = end_height * Globals.Config.saved.HeightScale;
        float mid_height = (start_height + end_height) * 0.5f;

        LastTargetHeight = end_height;
        MidHeight = mid_height;

        Position = ThisObject.Position;

        TargetPosition = ThisObject.Target.Position;
        LastTargetPosition = TargetPosition;
        LastTargetPosition2 = LastTargetPosition;

        Position.Y += start_height_scaled;
        TargetPosition.Y += end_height_scaled;
    }

    private unsafe void UpdateState() {
        GameObjectHelper target = ThisObject.Target;
        bool has_target = target != null;
        bool new_target = false;

        if (Framework == null) {
            Framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        }

        if (has_target != HadTarget) {
            if (has_target) {
                if (State == LineState.Dying) {
                    LastTargetPosition = LastTargetPosition2;
                }

                LastTargetId = target.ObjectId;
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
            if (target.ObjectId != LastTargetId) {
                LastTargetId = target.ObjectId;
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
        GameObjectHelper target = ThisObject.Target;

        if (Globals.Config.saved.LineColor.Visible) {
            LineColor.CopyValues(Globals.Config.saved.LineColor.Color);
            OutlineColor.CopyValues(Globals.Config.saved.LineColor.OutlineColor);
        }

        if (target == null) {
            LineColor.CopyValues(LastLineColor);
            OutlineColor.CopyValues(LastOutlineColor);
        }
        else {
            int highestPriority = -1;
            foreach (TargetSettingsPair settings in Globals.Config.LineColors) {
                var from = settings.From;
                var to = settings.To;
                var value = settings.LineColor;

                // entries further from 0 are more pedantic, so this should help use choose the most specific entry
                int priority = settings.GetPairPriority();
                if (priority > highestPriority) {
                    bool should_copy = CompareTargetSettings(ref from, ref ThisObject.Settings);
                    if (should_copy) {
                        should_copy = CompareTargetSettings(ref to, ref target.Settings);
                    }
                    if (should_copy) {
                        highestPriority = priority;
                        LineColor.CopyValues(value.Color);
                        OutlineColor.CopyValues(value.OutlineColor);
                        UseQuad = value.UseQuad;
                        Visible = value.Visible;
                    }
                }
            }

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
        GameObjectHelper target = ThisObject.Target;
        bool vis0 = ThisObject.IsVisible(Globals.Config.saved.OcclusionCulling);
        bool vis1 = false;
        if (target != null && target.IsBattleChara() && !target.IsPlayerCharacter()) {
#if (PROBABLY_BAD)
            // for debug
            if (!target.IsVisible(Globals.Config.saved.OcclusionCulling)) {
                vis1 |= target.IsVisible(Globals.Config.saved.OcclusionCulling);
            }
#else
            // if target is an enemy, and it is not visible, ignore all other checks and abort
            if (!target.IsVisible(true)) {
                return false;
            }
#endif
        }
        else {
            vis1 |= Globals.IsVisible(TargetPosition, Globals.Config.saved.OcclusionCulling);
        }

        DrawBeginCap = Service.Gui.WorldToScreen(Position, out ScreenPos);
        DrawEndCap = Service.Gui.WorldToScreen(TargetPosition, out TargetScreenPos);
        Service.Gui.WorldToScreen(MidPosition, out MidScreenPos);

        if (Globals.Config.saved.OcclusionCulling) {
            if (DrawBeginCap == false) {
                vis0 = false;
            }

            if (DrawEndCap == false) {
                vis1 = false;
            }

            if (!(vis0 && vis0)) {
                return false;
            }
        }

        return true;
    }

    public unsafe void Draw() {
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

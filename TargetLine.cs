using ImGuiNET;
using System;
using System.Numerics;

namespace TargetLines;

internal class TargetLine
{
    public GameObjectHelper ThisObject;
    public Vector3 LastTargetPosition = new Vector3();
    public ulong LastTargetId = 0;
    public bool Switching = false;
    public bool HadTarget = false;
    public bool ShouldDelete = false;
    public float LivingTime = 0.0f;
    public float DyingTime = 0.0f;

    private float LastHeight = 0.0f;
    private float LastYOffset = 0.0f;

    public TargetLine(GameObjectHelper obj) {
        ThisObject = obj;
        if (ThisObject.TargetObject != null) {
            LastTargetId = ThisObject.TargetObject.TargetObjectId;
            LastTargetPosition = ThisObject.TargetObject.Position;
        }
        else {
            LastTargetPosition = ThisObject.Position;
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

    public void DrawSolidLine(ref bool usequad, ref Vector2 my_screen_pos, ref Vector2 mid_screen_pos, ref Vector2 t_screen_pos, ref ABGR outlinecolor, ref ABGR linecolor) {
        ImDrawListPtr drawlist = ImGui.GetWindowDrawList();

        if (usequad) {
            if (Globals.Config.saved.OutlineThickness > 0) {
                drawlist.AddBezierQuadratic(my_screen_pos, mid_screen_pos, t_screen_pos, outlinecolor.GetRaw(), Globals.Config.saved.OutlineThickness);
            }
            if (Globals.Config.saved.LineThickness > 0) {
                drawlist.AddBezierQuadratic(my_screen_pos, mid_screen_pos, t_screen_pos, linecolor.GetRaw(), Globals.Config.saved.LineThickness);
            }
        }
        else {
            if (Globals.Config.saved.OutlineThickness > 0) {
                drawlist.AddBezierCubic(my_screen_pos, mid_screen_pos, mid_screen_pos, t_screen_pos, outlinecolor.GetRaw(), Globals.Config.saved.OutlineThickness);
            }
            if (Globals.Config.saved.LineThickness > 0) {
                drawlist.AddBezierCubic(my_screen_pos, mid_screen_pos, mid_screen_pos, t_screen_pos, linecolor.GetRaw(), Globals.Config.saved.LineThickness);
            }
        }
    }

    public void DrawFancyLine(ref bool usequad, ref Vector2 my_screen_pos, ref Vector2 mid_screen_pos, ref Vector2 t_screen_pos, ref ABGR linecolor, ref bool draw_start, ref bool draw_end) {
        ImDrawListPtr drawlist = ImGui.GetWindowDrawList();
        Vector2[] points = new Vector2[Globals.Config.saved.TextureCurveSampleCount];
        Vector2 uv1;
        Vector2 uv2;

        for (int index = 0; index < Globals.Config.saved.TextureCurveSampleCount; index++) {
            float t = (float)index / (Globals.Config.saved.TextureCurveSampleCount - 1);
            if (usequad) {
                points[index] = EvaluateQuadratic(my_screen_pos, mid_screen_pos, t_screen_pos, t);
            }
            else {
                points[index] = EvaluateCubic(my_screen_pos, mid_screen_pos, mid_screen_pos, t_screen_pos, t);
            }
        }

        float currentTime = (float)Globals.Runtime;
        float pulsatingSpeed = Globals.Config.saved.WaveFrequencyScalar;
        float max = linecolor.a;
        float min = linecolor.a * 0.5f;
        float pulsatingAmplitude = (max - min) * (1.0f - Globals.Config.saved.WaveAmplitudeOffset);

        for (int index = 0; index < Globals.Config.saved.TextureCurveSampleCount - 1; index++) {
            ABGR linecolor_index = new ABGR(0, 0, 0, 0);
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

            linecolor_index.CopyValues(linecolor);
            

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
            }

            if (Globals.Config.saved.FadeToEnd) {
                linecolor_index.a = (byte)MathUtils.Lerpf((float)linecolor_index.a,
                    (float)(linecolor_index.a * Globals.Config.saved.FadeToEndScalar),
                    (float)index / (float)(Globals.Config.saved.TextureCurveSampleCount - 1)
                );
            }

            drawlist.AddImageQuad(Globals.LineTexture.ImGuiHandle, p1_perp_inv, p2_perp_inv, p2_perp, p1_perp, uv1, new Vector2(uv1.X, uv2.Y), uv2, new Vector2(uv2.X, uv1.Y), linecolor_index.GetRaw());
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
        linecolor_end.CopyValues(linecolor);
        if (Globals.Config.saved.FadeToEnd) {
            linecolor_end.a = (byte)(linecolor_end.a * Globals.Config.saved.FadeToEndScalar);
        }

        if (draw_start) {
            drawlist.AddImage(Globals.EdgeTexture.ImGuiHandle, start_p1, start_p2, uv1, uv2, linecolor.GetRaw());
        }
        if (draw_end) {
            drawlist.AddImage(Globals.EdgeTexture.ImGuiHandle, end_p1, end_p2, uv1, uv2, linecolor.GetRaw());
        }
    }

    public unsafe void Draw() {
        FFXIVClientStructs.FFXIV.Client.System.Framework.Framework* framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
        ImDrawListPtr drawlist = ImGui.GetWindowDrawList();
        GameObjectHelper target = null;
        Vector3 mypos = Vector3.Zero;
        Vector3 tpos = Vector3.Zero;
        Vector3 midpos = Vector3.Zero;
        ABGR linecolor = new ABGR(0, 0, 0, 0);
        ABGR outlinecolor = new ABGR(0, 0, 0, 0);
        float height = ThisObject.GetHeight(); // for midpoint
        float alpha = 1.0f;
        bool usequad = false;
        bool easing = false;
        bool has_target = ThisObject.TargetObject != null;

        if (Globals.Config.saved.StartAtFeet) {
            mypos = ThisObject.Position;
        }
        else {
            mypos = ThisObject.GetTargetPosition();
            mypos.Y -= 0.1f;
        }

        if (Globals.Config.saved.BreathingEffect) {
            alpha = (1.0f - Globals.Config.saved.WaveAmplitudeOffset) + (float)Math.Cos(Globals.Runtime * Globals.Config.saved.WaveFrequencyScalar) * Globals.Config.saved.WaveAmplitudeOffset;
        }

        if (has_target) {
            if (ThisObject.TargetObject.IsValid() == false) {
                has_target = false;
            }
        }

        linecolor.CopyValues(Globals.Config.saved.OtherLineColor);
        outlinecolor.CopyValues(Globals.Config.saved.OutlineColor);

        if (!has_target) {
            if (DyingTime > Globals.Config.saved.NoTargetFadeTime) {
                LastTargetId = 0;
                ShouldDelete = true;
                return;
            }

            alpha = 1.0f - (DyingTime / Globals.Config.saved.NoTargetFadeTime);
            if (alpha < 0.0f) {
                alpha = 0.0f;
            }

            tpos = Vector3.Lerp(LastTargetPosition, ThisObject.Position, 1.0f - alpha);
            easing = true;
            DyingTime += framework->FrameDeltaTime;
            LivingTime = 0.0f;
        }
        else if (has_target) {
            target = new GameObjectHelper(ThisObject.TargetObject);
            if (Globals.Config.saved.StartAtFeet) {
                tpos = target.Position;
            }
            else {
                tpos = target.GetTargetPosition();
                tpos.Y -= 0.1f;
            }
            if (ThisObject.TargetObjectId != LastTargetId && HadTarget) {
                Switching = true;
                LivingTime = 0.0f;
            }

            if (Switching && LivingTime < Globals.Config.saved.NewTargetEaseTime) {
                tpos = LastTargetPosition;
            }
            if (Switching && LivingTime >= Globals.Config.saved.NewTargetEaseTime) {
                Switching = false;
            }

            LivingTime += framework->FrameDeltaTime;
            DyingTime = 0.0f;
            LastTargetId = target.Object.ObjectId;
            LastYOffset = target.GetHeight();
            height = (height + LastYOffset) * 0.5f; // average height between targets
        }

        HadTarget = has_target;

        if (has_target && LivingTime < Globals.Config.saved.NewTargetEaseTime) {
            easing = true;
            alpha = LivingTime / Globals.Config.saved.NewTargetEaseTime;
            if (alpha > 1.0f) {
                alpha = 1.0f;
            }
            if (Switching) {
                tpos = Vector3.Lerp(tpos, target.Object.Position, alpha);
            }
            else {
                tpos = Vector3.Lerp(mypos, tpos, alpha);
            }
            
        }
        midpos = (mypos + tpos) * 0.5f;

        if (easing) {
            float start = alpha;
            float end = height;

            if (Switching) {
                start = LastHeight;
            }

            if (!has_target) {
                end = 0.0f;
            }

            height = MathUtils.Lerpf(start, height, alpha);
        }

        if (!easing) {
            LastTargetPosition = tpos;
            LastHeight = height;
        }

        if (ThisObject.IsPlayerCharacter()) {
            if (target != null && target.IsPlayerCharacter()) {
                linecolor.CopyValues(Globals.Config.saved.PlayerPlayerLineColor);
            }
            else if (target != null && target.IsBattleChara()) {
                linecolor.CopyValues(Globals.Config.saved.PlayerEnemyLineColor);
            }
            midpos.Y += Globals.Config.saved.PlayerHeightBump;
        }
        else if (ThisObject.IsBattleChara()) {
            if (target != null && target.IsPlayerCharacter()) {
                linecolor.CopyValues(Globals.Config.saved.EnemyPlayerLineColor);
            }
            else if (target != null && target.IsBattleChara()) {
                linecolor.CopyValues(Globals.Config.saved.EnemyEnemyLineColor);
            }
            midpos.Y += Globals.Config.saved.EnemyHeightBump;
            usequad = true;
        }

        midpos.Y += (height * Globals.Config.saved.ArcHeightScalar);

        if (Switching) {
            alpha = 1.0f;
        }

        linecolor.a = (byte)((float)linecolor.a * alpha);
        if (easing) {
            outlinecolor.a = (byte)((float)outlinecolor.a * alpha);
        }

        if (has_target && target.IsBattleChara() && !target.IsPlayerCharacter()) {
#if (PROBABLY_BAD)
            if (!target.IsVisible(Globals.Config.saved.OcclusionCulling)) return;
#else
            if (!target.IsVisible(true)) return;
#endif
        }

        if (!ThisObject.IsVisible(Globals.Config.saved.OcclusionCulling) && !Globals.IsVisible(tpos + new Vector3(0.0f, LastYOffset, 0.0f), Globals.Config.saved.OcclusionCulling)) {
            return;
        }

        bool draw_begin_cap = Service.Gui.WorldToScreen(mypos, out Vector2 my_screen_pos);
        bool draw_end_cap = Service.Gui.WorldToScreen(tpos, out Vector2 t_screen_pos);
        Service.Gui.WorldToScreen(midpos, out Vector2 mid_screen_pos);

        if (Globals.Config.saved.SolidColor) {
            DrawSolidLine(ref usequad, ref my_screen_pos, ref mid_screen_pos, ref t_screen_pos, ref outlinecolor, ref linecolor);
        }
        else {
            DrawFancyLine(ref usequad, ref my_screen_pos, ref mid_screen_pos, ref t_screen_pos, ref linecolor, ref draw_begin_cap, ref draw_end_cap);
        }
    }
}

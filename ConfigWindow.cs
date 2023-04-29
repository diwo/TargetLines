using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using static TargetLines.ClassJobHelper;

namespace TargetLines;

internal class ConfigWindow : Window, IDisposable
{
    public static string ConfigWindowName = "Target Lines Config";

    public ConfigWindow() : base(ConfigWindowName) { }

    private string AddSpacesToCamelCase(string text) {
        if (string.IsNullOrEmpty(text)) {
            return string.Empty;
        }

        StringBuilder result = new StringBuilder(text.Length * 2);
        result.Append(text[0]);

        for (int index = 1; index < text.Length; index++) {
            if (char.IsUpper(text[index]) && !char.IsUpper(text[index - 1])) {
                result.Append(' ');
            }
            result.Append(text[index]);
        }

        return result.ToString();
    }


    private bool DrawTargetFlagEditor(ref TargetFlags flags, string guard) {
        int flag_count = Enum.GetValues(typeof(TargetFlags)).Length;
        bool should_save = false;

        for (int index = 0; index < flag_count; index++) {
            TargetFlags current_flag = (TargetFlags)(1 << index);
            string label = AddSpacesToCamelCase(current_flag.ToString());
            int flags_dirty = (int)flags;
            if (ImGui.CheckboxFlags($"{label}##{guard}{index}", ref flags_dirty, (int)current_flag)) {
                flags = (TargetFlags)flags_dirty;
                should_save = true;
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip(TargetFlagDescriptions[index]);
            }

            if ((index + 1) % 4 != 0) {
                ImGui.SameLine();
            }
        }

        return should_save;
    }

    public override void Draw() {
        bool should_save = false;
        
        int selected = (int)Globals.Config.saved.OnlyInCombat;
        if (ImGui.ListBox("Combat setting", ref selected, Enum.GetNames(typeof(InCombatOption)), (int)InCombatOption.Count)) {
            Globals.Config.saved.OnlyInCombat = (InCombatOption)selected;
            should_save = true;
        }

        should_save |= ImGui.Checkbox("Only show target lines when unsheathed", ref Globals.Config.saved.OnlyUnsheathed);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("If enabled, target lines will stop drawing if your weapon is sheathed");
        }

        should_save |= ImGui.Checkbox("Occlusion Culling (Always Enabled for enemies!)", ref Globals.Config.saved.OcclusionCulling);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("If enabled, target lines will stop drawing if both their start and end points are not visible");
        }

        should_save |= ImGui.Checkbox("Use solid color instead of texture", ref Globals.Config.saved.SolidColor);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("If enabled, the target lines will appear more flat, and the pulsing effect will be disabled. This is essentially a simple/clearer lines mode");
        }

        should_save |= ImGui.Checkbox("Use Breathing Effect", ref Globals.Config.saved.BreathingEffect);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("If enabled, the opacity of the lines with fade in-and-out based on the alpha values below");
        }

        if (Globals.Config.saved.SolidColor == false) {
            should_save |= ImGui.Checkbox("Use Pulsing Effect", ref Globals.Config.saved.PulsingEffect);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("If enabled, while not using solid color lines, the lines will periodically pulse from the source to the target");
            }

            should_save |= ImGui.Checkbox("Fade line as it approaches target", ref Globals.Config.saved.FadeToEnd);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("If enabled, the line will become more transparent as it approaches the target");
            }

            if (Globals.Config.saved.FadeToEnd) {
                should_save |=  ImGui.SliderFloat("End point opacity %", ref Globals.Config.saved.FadeToEndScalar, 0.0f, 1.0f);
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("The opacity of the line at the furthest point from the source");
            }

            should_save |= ImGui.SliderInt("Texture Smoothness Steps", ref Globals.Config.saved.TextureCurveSampleCount, 2, 512);
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("While not using solid color lines, this value represents how many samples are used to produce the target line effect. Lower values may have better performance");
            }
        }
        else {
            ImGui.Spacing(); ImGui.Spacing();
        }

        should_save |= ImGui.SliderFloat("Height Scale", ref Globals.Config.saved.HeightScale, 0.0f, 1.0f);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("This value scales the height of the source and target. 0 is the feet, 1 is the head");
        }

        should_save |= ImGui.SliderFloat("Arc Scale", ref Globals.Config.saved.ArcHeightScalar, 0.0f, 2.0f);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("This value scales middle point of the line. 0 will make the line flat, 1 will make the middle point of the line the average height of the source and target higher");
        }

        should_save |= ImGui.SliderFloat("Line Thickness", ref Globals.Config.saved.LineThickness, 0.0f, 64.0f);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("The thickness of the line. 0 will disable the line");
        }
        should_save |= ImGui.SliderFloat("Outline Thickness", ref Globals.Config.saved.OutlineThickness, 0.0f, 72.0f);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("The thickness of the outline. 0 will disable the outline");
        }

        should_save |= ImGui.SliderFloat("New Target Easing Time", ref Globals.Config.saved.NewTargetEaseTime, 0.0f, 5.0f);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("When switching targets, this represents the time (in seconds) the line will spend shifting to the new target");
        }

        should_save |= ImGui.SliderFloat("No Target Fading Time", ref Globals.Config.saved.NoTargetFadeTime, 0.0f, 5.0f);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("When there is no longer a target, this represents the time (in seconds) the line will spend fading out");
        }

        should_save |= ImGui.SliderFloat("Player Arc Height Bump", ref Globals.Config.saved.PlayerHeightBump, 0.0f, 10.0f);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("If the source of the line is a player, it's starting point will be moved up by this amount");
        }

        should_save |= ImGui.SliderFloat("Enemy Arc Height Bump", ref Globals.Config.saved.EnemyHeightBump, 0.0f, 10.0f);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("If the source of the line is an enemy, it's starting point will be moved up by this amount");
        }

        should_save |= ImGui.SliderFloat("Alpha Fade Amplitude", ref Globals.Config.saved.WaveAmplitudeOffset, 0.0f, 0.5f);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("This value represents the maximum difference that the breathing and pulsing effect will have on the opacity of the line");
        }

        should_save |= ImGui.SliderFloat("Alpha Frequency", ref Globals.Config.saved.WaveFrequencyScalar, 0.0f, 10.0f);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("This value represents the speed in which the breathing and pulsing effect will happen");
        }

        Vector4 color = Globals.Config.saved.LineColor.Color.Color;
        Vector4 ocolor = Globals.Config.saved.LineColor.OutlineColor.Color;

        should_save |= ImGui.Checkbox("Fallback visible", ref Globals.Config.saved.LineColor.Visible);
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("If enabled, whenever none of your filters are met, these settings will be used for the target line");
        }

        if (Globals.Config.saved.LineColor.Visible) {
            if (ImGui.ColorEdit4("Fallback Color", ref color)) {
                Globals.Config.saved.LineColor.Color.Color = color;
                should_save = true;
            }

            if (ImGui.ColorEdit4("Fallback Outline Color", ref ocolor)) {
                Globals.Config.saved.LineColor.OutlineColor.Color = ocolor;
                should_save = true;
            }
        }

        ImGui.Spacing();
        ImGui.Separator();

        ImGui.Text("Filter & Color settings");
        if (ImGui.Button("New")) {
            Globals.Config.LineColors.Add(new TargetSettingsPair(new TargetSettings(), new TargetSettings(), new LineColor()));
            should_save = true;
        }

        ImGui.Spacing();

        for (int qndex = 0; qndex < Globals.Config.LineColors.Count; qndex++) {
            var settings = Globals.Config.LineColors[qndex];
            int flag_count = Enum.GetValues(typeof(TargetFlags)).Length;
            List<string> from = new List<string>();
            List<string> to = new List<string>();

            color = settings.LineColor.Color.Color;
            ocolor = settings.LineColor.OutlineColor.Color;

            for (int index = 0; index < flag_count; index++) {
                TargetFlags current_flag = (TargetFlags)(1 << index);
                if (((int)settings.From.Flags & (int)current_flag) != 0) {
                    from.Add(AddSpacesToCamelCase(current_flag.ToString()));
                }
                if (((int)settings.To.Flags & (int)current_flag) != 0) {
                    to.Add(AddSpacesToCamelCase(current_flag.ToString()));
                }
            }

            if (ImGui.TreeNode($"{string.Join('|', from)} -> {string.Join('|', to)}###LineColorsEntry{qndex}")) {
                if (ImGui.TreeNode($"Source Filters##From{qndex}")) {
                    if (DrawTargetFlagEditor(ref settings.From.Flags, $"From{qndex}Flags")) {
                        should_save = true;
                    }
                    ImGui.TreePop();
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Conditions that the targeting entity must satisfy to use these settings");
                }

                if (ImGui.TreeNode($"Target Filters##To{qndex}")) {
                    if (DrawTargetFlagEditor(ref settings.To.Flags, $"To{qndex}Flags")) {
                        should_save = true;
                    }
                    ImGui.TreePop();
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("Conditions that the targeted entity must satisfy to use these settings");
                }

                if (ImGui.ColorEdit4($"Color##Color{qndex}", ref color)) {
                    settings.LineColor.Color.Color = color;
                    should_save = true;
                }

                if (ImGui.ColorEdit4($"Outline Color##OColor{qndex}", ref ocolor)) {
                    settings.LineColor.OutlineColor.Color = ocolor;
                    should_save = true;
                }

                if (ImGui.Checkbox($"Use Quadratic Line##UseQuad{qndex}", ref settings.LineColor.UseQuad)) {
                    should_save = true;
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("If enabled, this line will use a quadratic formula (as opposed to the default cubic formula). Useful if you would like different lines to have slightly different shapes. Quadratic lines look more like a half circle");
                }

                if (ImGui.Checkbox($"Visible##Visible{qndex}", ref settings.LineColor.Visible)) {
                    should_save = true;
                }
                if (ImGui.IsItemHovered()) {
                    ImGui.SetTooltip("If disabled, this line will not render");
                }

                if (ImGui.Button($"Delete##DeleteEntry{qndex}")) {
                    Globals.Config.LineColors.RemoveAt(qndex);
                    should_save = true;
                    ImGui.TreePop();
                    break;
                }

                ImGui.TreePop();
            }

            ImGui.Separator();
        }

        if (ImGui.Button("Reset To Default")) {
            Globals.Config.saved = new SavedConfig();
            Globals.Config.InitializeDefaultLineColorsConfig();
            should_save = true;
        }
        if (ImGui.IsItemHovered()) {
            ImGui.SetTooltip("Set all of the values to the plugin defaults. This will delete any custom entries that you have made!");
        }

        if (should_save) {
            Globals.Config.Save();
        }
    }

    public void Dispose() { }
}

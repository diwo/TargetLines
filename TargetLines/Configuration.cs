using Dalamud.Configuration;
using DrahsidLib;
using FFXIVClientStructs.FFXIV.Common.Math;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

using static TargetLines.ClassJobHelper;

namespace TargetLines;

public enum InCombatOption {
    None = 0,
    InCombat,
    NotInCombat,
    Count
}

public enum LineDeathAnimation {
    Linear,
    Square,
    Cube,
    Count
};

public class TargetSettings {
    public TargetFlags Flags = 0;
    public UInt64 Jobs = 0; // bitfield from ClassJob

    public TargetSettings() {
        Flags = 0;
        Jobs = 0;
    }

    public TargetSettings(TargetFlags flags, UInt64 jobs = 0) {
        Flags = flags;
        Jobs = jobs;
    }
};

public class ABGR {
    public byte a;
    public byte b;
    public byte g;
    public byte r;

    public ABGR(byte A, byte B, byte G, byte R) {
        a = A;
        b = B;
        g = G;
        r = R;
    }

    [JsonIgnore]
    public Vector4 Color {
        get {
            return new Vector4((float)r / 255.0f, (float)g / 255.0f, (float)b / 255.0f, (float)a / 255.0f);
        }
        set {
            a = (byte)(value.W * 255.0f);
            b = (byte)(value.Z * 255.0f);
            g = (byte)(value.Y * 255.0f);
            r = (byte)(value.X * 255.0f);
        }
    }

    public void CopyValues(ABGR from) {
        a = from.a;
        b = from.b;
        g = from.g;
        r = from.r;
    }

    public uint GetRaw() {
        return (uint)((a << 24) | (b << 16) | (g << 8) | r);
    }
}

public class LineColor {
    public ABGR Color = new ABGR(0x80, 0x00, 0xBF, 0xFF);
    public ABGR OutlineColor = new ABGR(0x80, 0x00, 0x00, 0x00);
    public bool Visible = true;
    public bool UseQuad = false;

    public LineColor() {
        Color = new ABGR(0x80, 0x00, 0xBF, 0xFF);
        OutlineColor = new ABGR(0x80, 0x00, 0x00, 0x00);
        Visible = true;
        UseQuad = false;
    }

    public LineColor(ABGR color, ABGR outline, bool visible = true, bool usequad = false) {
        Color = color;
        OutlineColor = outline;
        Visible = visible;
        UseQuad = usequad;
    }

    public LineColor(ABGR color, bool visible = true, bool usequad = false) {
        Color = color;
        Visible = visible;
        UseQuad = usequad;
    }
}

public class TargetSettingsPair {
    public TargetSettings From;
    public TargetSettings To;
    public LineColor LineColor;
    public int Priority = -1;
    public Guid UniqueId = Guid.Empty;

    public TargetSettingsPair(TargetSettings from, TargetSettings to, LineColor lineColor) {
        From = from;
        To = to;
        LineColor = lineColor;
        Priority = -1;
        UniqueId = Guid.NewGuid();
    }

    public int GetPairPriority() {
        int priority = Priority;

        if (priority == -1) {
            for (int index = 0; index < 16; index++) {
                int bit = 1 << index;
                if (((int)From.Flags & bit) != 0) {
                    priority += index;
                }
                if (((int)To.Flags & bit) != 0) {
                    priority += index;
                }
            }

            if (From.Jobs != 0) {
                priority += 1;
            }

            if (To.Jobs != 0) {
                priority += 1;
            }
        }

        return priority;
    }
}

public class SavedConfig {
    public float ArcHeightScalar = 1.0f;
    public float PlayerHeightBump = 0.0f;
    public float EnemyHeightBump = 0.0f;
    public float LineThickness = 16.0f;
    public float OutlineThickness = 20.0f;
    public float NewTargetEaseTime = 0.25f;
    public float NoTargetFadeTime = 0.25f;
    public float WaveAmplitudeOffset = 0.175f;
    public float WaveFrequencyScalar = 3.0f;
    public float FadeToEndScalar = 0.2f;
    public float HeightScale = 1.0f;
    public int TextureCurveSampleCount = 47;
    public InCombatOption OnlyInCombat = InCombatOption.None;
    public bool OnlyUnsheathed = false;
    public bool SolidColor = false;
    public bool FadeToEnd = true;
    public bool ToggledOff = false;
    public bool OcclusionCulling = false;
    public bool PulsingEffect = true;
    public bool BreathingEffect = true;
    public bool CompactFlagDisplay = false;
    public LineColor LineColor = new LineColor(new ABGR(0xC0, 0x80, 0x80, 0x80), new ABGR(0x80, 0x00, 0x00, 0x00), true); // fallback color
    public LineDeathAnimation DeathAnimation = LineDeathAnimation.Linear;
    public float DeathAnimationTimeScale = 1.0f;

    [Obsolete] public ABGR PlayerPlayerLineColor;
    [Obsolete] public ABGR PlayerEnemyLineColor;
    [Obsolete] public ABGR EnemyPlayerLineColor;
    [Obsolete] public ABGR EnemyEnemyLineColor;
    [Obsolete] public ABGR OtherLineColor;
    [Obsolete] public ABGR OutlineColor;
    [Obsolete] public bool OnlyTargetingPC = false;
}

public class Configuration : IPluginConfiguration {
    int IPluginConfiguration.Version { get; set; }

    #region Saved configuration values
    public SavedConfig saved = new SavedConfig();
    public List<TargetSettingsPair> LineColors;
    public bool HideTooltips = false;
    #endregion

    public void SortLineColors() {
        Globals.Config.LineColors = Globals.Config.LineColors.OrderByDescending(obj => obj.GetPairPriority()).ToList();
    }

    public void InitializeDefaultLineColorsConfig() {
        LineColors = new List<TargetSettingsPair>() {
                // player -> player default
                new TargetSettingsPair(
                    new TargetSettings(TargetFlags.Player),
                    new TargetSettings(TargetFlags.Player),
                    new LineColor(new ABGR(0xC0, 0x50, 0xAF, 0x4C)) // greenish
                ),
                // player -> enemy default
                new TargetSettingsPair(
                    new TargetSettings(TargetFlags.Player),
                    new TargetSettings(TargetFlags.Enemy),
                    new LineColor(new ABGR(0x80, 0x36, 0x43, 0xF4)) // reddish
                ), 
                // enemy -> player default
                new TargetSettingsPair(
                    new TargetSettings(TargetFlags.Enemy),
                    new TargetSettings(TargetFlags.Player),
                    new LineColor(new ABGR(0xC0, 0x00, 0x00, 0xFF), true, true) // red
                ),
                // enemy -> enemy default
                new TargetSettingsPair(
                    new TargetSettings(TargetFlags.Enemy),
                    new TargetSettings(TargetFlags.Enemy),
                    new LineColor(new ABGR(0xC0, 0xB0, 0x27, 0x9C), true, true) // purpleish
                )
            };
    }

    public void Initialize() {
        int LineColorsWasNull = 0;

        // manage upgrades
        if (saved == null) {
            saved = new SavedConfig();
        }

        if (LineColors == null) {
            LineColors = new List<TargetSettingsPair>();
            LineColorsWasNull = 1;
        }

        if (saved.OnlyInCombat is bool) {
            saved.OnlyInCombat = InCombatOption.None;
            Service.ChatGui.Print("Warning! If you had the \"OnlyInCombat\" setting set, you will have to reenable it!");
        }

        if (saved.PlayerPlayerLineColor != null) {
            TargetSettings from =  new TargetSettings(TargetFlags.Player, 0);
            TargetSettings to = new TargetSettings(TargetFlags.Player, 0);

            LineColorsWasNull++;
            LineColors.Add(new TargetSettingsPair(from, to, new LineColor(saved.PlayerPlayerLineColor, saved.OutlineColor, true)));

            saved.PlayerPlayerLineColor = null;
        }

        if (saved.PlayerEnemyLineColor != null) {
            TargetSettings from = new TargetSettings(TargetFlags.Player, 0);
            TargetSettings to = new TargetSettings(TargetFlags.Enemy, 0);

            LineColorsWasNull++;
            LineColors.Add(new TargetSettingsPair(from, to, new LineColor(saved.PlayerEnemyLineColor, saved.OutlineColor, true)));
            saved.PlayerEnemyLineColor = null;
        }

        if (saved.EnemyPlayerLineColor != null) {
            TargetSettings from = new TargetSettings(TargetFlags.Enemy, 0);
            TargetSettings to = new TargetSettings(TargetFlags.Player, 0);

            LineColorsWasNull++;
            LineColors.Add(new TargetSettingsPair(from, to, new LineColor(saved.EnemyPlayerLineColor, saved.OutlineColor, true)));
            saved.EnemyPlayerLineColor = null;
        }

        if (saved.EnemyEnemyLineColor != null) {
            TargetSettings from = new TargetSettings(TargetFlags.Enemy, 0);
            TargetSettings to = new TargetSettings(TargetFlags.Enemy, 0);

            LineColorsWasNull++;
            LineColors.Add(new TargetSettingsPair(from, to, new LineColor(saved.EnemyEnemyLineColor, saved.OutlineColor, true)));
            saved.EnemyEnemyLineColor = null;
        }

        // Initialize values if we didn't upgrade from an old config
        if (LineColorsWasNull == 1) {
            InitializeDefaultLineColorsConfig();
        }

        for (int index = 0; index < LineColors.Count; index++) {
            if (LineColors[index].UniqueId == null || LineColors[index].UniqueId == Guid.Empty) {
                LineColors[index].UniqueId = Guid.NewGuid();
            }
        }
    }

    public void Save() {
        Service.Interface.SavePluginConfig(this);
    }
}

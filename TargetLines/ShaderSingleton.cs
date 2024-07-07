using DrahsidLib;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace TargetLines;

internal static class ShaderSingleton {
    public enum Shader {
#if HELLOTRI_TEST
        HelloTri,
#endif
        Line,
        Count
    }

    public const int SHADER_COUNT = (int)Shader.Count;

    private const string ShaderPreambleVertex = "VertexShader";
    private const string ShaderPreamblePixel = "PixelShader";
    private const string ShaderPreambleGeometry = "GeometryShader";

    public static ShaderBytecode[] VertexByteCode = new ShaderBytecode[SHADER_COUNT];
    public static ShaderBytecode[] PixelByteCode = new ShaderBytecode[SHADER_COUNT];
    public static ShaderBytecode[] GeometryByteCode = new ShaderBytecode[SHADER_COUNT];
    public static VertexShader[] VertexShaders = new VertexShader[SHADER_COUNT];
    public static PixelShader[] PixelShaders = new PixelShader[SHADER_COUNT];
    public static GeometryShader[] GeometryShaders = new GeometryShader[SHADER_COUNT];

    public static bool Initialized = false;

    public static void Initialize(Device device) {
        if (Initialized) {
            return;
        }

        var shaderPath = Path.Combine(Service.Interface.AssemblyLocation.Directory?.FullName!, "Data/Shaders");
        for (int index = 0; index < (int)Shader.Count; index++) {
            var vertexFile = $"{shaderPath}/{ShaderPreambleVertex}{((Shader)index)}.hlsl";
            var pixelFile = $"{shaderPath}/{ShaderPreamblePixel}{((Shader)index)}.hlsl";
            var geoFile = $"{shaderPath}/{ShaderPreambleGeometry}{((Shader)index)}.hlsl";

            try {
                if (File.Exists(vertexFile))
                {
                    Service.Logger.Verbose($"Compiling {vertexFile}...");
                    VertexByteCode[index] = ShaderBytecode.CompileFromFile(vertexFile, "Main", "vs_5_0");
                    VertexShaders[index] = new VertexShader(device, VertexByteCode[index]);
                    Service.Logger.Verbose("OK!");
                }
                else
                {
                    Service.Logger.Verbose($"No {ShaderPreambleVertex} for {(Shader)index}!");
                }
            }
            catch (Exception ex) {
                Service.Logger.Error($"Failed to compile {ShaderPreambleVertex}? {vertexFile}: {ex.Message}");
            }

            try {
                if (File.Exists(pixelFile))
                {
                    Service.Logger.Verbose($"Compiling {pixelFile}...");
                    PixelByteCode[index] = ShaderBytecode.CompileFromFile(pixelFile, "Main", "ps_5_0");
                    PixelShaders[index] = new PixelShader(device, PixelByteCode[index]);
                    Service.Logger.Verbose("OK!");
                }
                else
                {
                    Service.Logger.Verbose($"No {ShaderPreamblePixel} for {(Shader)index}!");
                }
            }
            catch (Exception ex) {
                Service.Logger.Error($"Failed to compile {ShaderPreamblePixel}? {pixelFile}: {ex.Message}");
            }

            try
            {
                if (File.Exists(geoFile))
                {
                    Service.Logger.Verbose($"Compiling {geoFile}...");
                    GeometryByteCode[index] = ShaderBytecode.CompileFromFile(geoFile, "Main", "gs_5_0");
                    GeometryShaders[index] = new GeometryShader(device, GeometryByteCode[index]);
                    Service.Logger.Verbose("OK!");
                }
                else
                {
                    Service.Logger.Verbose($"No {ShaderPreambleGeometry} for {(Shader)index}!");
                }
            }
            catch (Exception ex)
            {
                Service.Logger.Error($"Failed to compile {ShaderPreambleGeometry}? {geoFile}: {ex.Message}");
            }
        }

        Initialized = true;
    }

    public static void Dispose() {
        for (int index = 0; index < (int)Shader.Count; index++) {
            VertexShaders[index]?.Dispose();
            VertexByteCode[index]?.Dispose();
            PixelShaders[index]?.Dispose();
            PixelByteCode[index]?.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShaderBytecode GetVertexShaderBytecode(Shader id) {
        return VertexByteCode[(int)id];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShaderBytecode GetPixelShaderBytecode(Shader id) {
        return PixelByteCode[(int)id];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ShaderBytecode GetGeometryShaderBytecode(Shader id)
    {
        return GeometryByteCode[(int)id];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VertexShader GetVertexShader(Shader id) {
        return VertexShaders[(int)id];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PixelShader GetPixelShader(Shader id)
    {
        return PixelShaders[(int)id];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GeometryShader GetGeometryShader(Shader id)
    {
        return GeometryShaders[(int)id];
    }
}

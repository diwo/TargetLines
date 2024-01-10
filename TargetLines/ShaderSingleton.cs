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

    public static ShaderBytecode[] VertexByteCode = new ShaderBytecode[SHADER_COUNT];
    public static ShaderBytecode[] PixelByteCode = new ShaderBytecode[SHADER_COUNT];
    public static VertexShader[] VertexShaders = new VertexShader[SHADER_COUNT];
    public static PixelShader[] PixelShaders = new PixelShader[SHADER_COUNT];

    public static bool Initialized = false;

    public static void Initialize(Device device) {
        if (Initialized) {
            return;
        }

        var shaderPath = Path.Combine(Service.Interface.AssemblyLocation.Directory?.FullName!, "Data/Shaders");
        for (int index = 0; index < (int)Shader.Count; index++) {
            var vertexFile = $"{shaderPath}/{ShaderPreambleVertex}{((Shader)index)}.hlsl";
            var pixelFile = $"{shaderPath}/{ShaderPreamblePixel}{((Shader)index)}.hlsl";

            try {
                Service.Logger.Verbose($"Compiling {vertexFile}...");
                VertexByteCode[index] = ShaderBytecode.CompileFromFile(vertexFile, "Main", "vs_5_0");
                VertexShaders[index] = new VertexShader(device, VertexByteCode[index]);
                Service.Logger.Verbose("OK!");
            }
            catch (Exception ex) {
                Service.Logger.Error($"Failed to compile VertexShader? {vertexFile}: {ex.Message}");
            }

            try {
                Service.Logger.Verbose($"Compiling {pixelFile}...");
                PixelByteCode[index] = ShaderBytecode.CompileFromFile(pixelFile, "Main", "ps_5_0");
                PixelShaders[index] = new PixelShader(device, PixelByteCode[index]);
                Service.Logger.Verbose("OK!");
            }
            catch (Exception ex) {
                Service.Logger.Error($"Failed to compile PixelShader? {pixelFile}: {ex.Message}");
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
    public static VertexShader GetVertexShader(Shader id) {
        return VertexShaders[(int)id];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PixelShader GetPixelShader(Shader id) {
        return PixelShaders[(int)id];
    }
}

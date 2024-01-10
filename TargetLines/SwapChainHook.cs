using Dalamud.Hooking;
using DrahsidLib;
using System;
using System.Runtime.InteropServices;

namespace TargetLines;

public static class SwapChainHook {
    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate IntPtr PresentDelegate(IntPtr swapChain, uint syncInterval, uint presentFlags);

    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate IntPtr ResizeBuffersDelegate(IntPtr swapChain, uint bufferCount, uint width, uint height, uint newFormat, uint swapChainFlags);

    private static Hook<PresentDelegate>? PresentHook { get; set; } = null;
    private static Hook<ResizeBuffersDelegate>? ResizeBuffersHook { get; set; } = null;

    public static Renderer? Renderer { get; set; }
    public static RawDX11Scene? Scene { get; set; }

    public static unsafe void Setup() {
        PresentHook = Service.GameInteropProvider.HookFromAddress<PresentDelegate>(SwapChainResolver.PresentMethod, PresentDetour);
        ResizeBuffersHook = Service.GameInteropProvider.HookFromAddress<ResizeBuffersDelegate>(SwapChainResolver.ResizeBuffers, ResizeDetour);
        PresentHook.Enable();
        ResizeBuffersHook.Enable();
        Service.Logger.Info("PresentHook installed?");
    }

    public static void Dispose() {
        PresentHook?.Dispose();
        ResizeBuffersHook?.Dispose();
        Renderer?.Dispose();
    }

    private static IntPtr PresentDetour(IntPtr swapChain, uint syncInterval, uint presentFlags) {
        if (Scene == null) {
            Scene = new RawDX11Scene(swapChain);
            Renderer = new Renderer(Scene.Device, Scene.SwapChain);
            ShaderSingleton.Initialize(Scene.Device);
            Scene.OnNewFrame = Renderer.OnFrame;
        }

        Scene.Render();

        return PresentHook.Original(swapChain, syncInterval, presentFlags);
    }

    private static IntPtr ResizeDetour(IntPtr swapChain, uint bufferCount, uint width, uint height, uint newFormat, uint swapChainFlags) {
        Scene.OnPreResize();
        var ret = ResizeBuffersHook.Original(swapChain, bufferCount, width, height, newFormat, swapChainFlags);
        Scene.OnPostResize((int)width, (int)height);
        return ret;
    }
}

using System;
using System.Runtime.InteropServices;
using DrahsidLib;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Matrix4x4 = FFXIVClientStructs.FFXIV.Common.Math.Matrix4x4;

namespace TargetLines;

[StructLayout(LayoutKind.Explicit)]
struct MatrixSingleton {
    [FieldOffset(0x1B4)] public Matrix4x4 ViewProjectionMatrix;
}

// real struct is shifted
[StructLayout(LayoutKind.Explicit, Size = 0x130)]
public struct RenderCamera {
    [FieldOffset(0x10)] public Matrix4x4 ViewMatrix;
    [FieldOffset(0x50)] public Matrix4x4 ProjectionMatrix;
    [FieldOffset(0xA8)] public float FoV;
    [FieldOffset(0xAC)] public float AspectRatio;
    [FieldOffset(0xB0)] public float NearPlane;
    [FieldOffset(0xB4)] public float FarPlane;
}

public class RawDX11Scene {
    public bool Initialized { get; private set; } = false;
    public Device Device { get; private set; }
    public SwapChain SwapChain { get; private set; }
    public IntPtr WindowHandlePtr { get; private set; }
    
    public ViewportF Viewport { get; internal set; }


    public delegate void NewFrameDelegate();

    public NewFrameDelegate OnNewFrame;

    private DeviceContext deviceContext;
    private RenderTargetView rtv;

    private int targetWidth;
    private int targetHeight;


    private unsafe delegate MatrixSingleton* GetMatrixSingletonDelegate();
    private IntPtr GetMatrixSingletonPtr { get; set; }
    private GetMatrixSingletonDelegate GetMatrixSingleton { get; set; }


    public RawDX11Scene(IntPtr nativeSwapChain) {
        GetMatrixSingletonPtr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8D 4C 24 ?? 48 89 4c 24 ?? 4C 8D 4D ?? 4C 8D 44 24 ??");
        GetMatrixSingleton = Marshal.GetDelegateForFunctionPointer<GetMatrixSingletonDelegate>(GetMatrixSingletonPtr);

        SwapChain = new SwapChain(nativeSwapChain);
        Device = SwapChain.GetDevice<Device>();
        deviceContext = Device.ImmediateContext;

        using (var backbuffer = SwapChain.GetBackBuffer<Texture2D>(0))
        {
            rtv = new RenderTargetView(Device, backbuffer);
        }

        targetWidth = SwapChain.Description.ModeDescription.Width;
        targetHeight = SwapChain.Description.ModeDescription.Height;
        Viewport = new ViewportF(0, 0, SwapChain.Description.ModeDescription.Width, SwapChain.Description.ModeDescription.Height, 0, 1.0f);
        WindowHandlePtr = SwapChain.Description.OutputHandle;

        Initialized = true;
    }

    private void Dispose(bool disposing) {
        rtv?.Dispose();
    }

    public void Dispose() {
        Dispose(true);
    }


    public void Render() {
        if (targetWidth <= 0 || targetHeight <= 0) {
            return;
        }

        deviceContext.OutputMerger.SetRenderTargets(rtv);
        deviceContext.Rasterizer.SetViewport(Viewport);

        var blendStateDescription = new BlendStateDescription();
        blendStateDescription.RenderTarget[0].IsBlendEnabled = true;
        blendStateDescription.RenderTarget[0].SourceBlend = BlendOption.SourceAlpha;
        blendStateDescription.RenderTarget[0].DestinationBlend = BlendOption.InverseSourceAlpha;
        blendStateDescription.RenderTarget[0].BlendOperation = BlendOperation.Add;
        blendStateDescription.RenderTarget[0].SourceAlphaBlend = BlendOption.One;
        blendStateDescription.RenderTarget[0].DestinationAlphaBlend = BlendOption.Zero;
        blendStateDescription.RenderTarget[0].AlphaBlendOperation = BlendOperation.Add;
        blendStateDescription.RenderTarget[0].RenderTargetWriteMask = ColorWriteMaskFlags.All;

        var blendState = new BlendState(Device, blendStateDescription);
        deviceContext.OutputMerger.SetBlendState(blendState);


        OnNewFrame?.Invoke();
        deviceContext.OutputMerger.SetRenderTargets((RenderTargetView)null);
    }

    public void OnPreResize() {
        deviceContext.OutputMerger.SetRenderTargets((RenderTargetView)null);

        rtv?.Dispose();
        rtv = null;
    }

    public void OnPostResize(int newWidth, int newHeight) {
        using (var backbuffer = SwapChain.GetBackBuffer<Texture2D>(0))
        {
            rtv = new RenderTargetView(Device, backbuffer);
        }

        targetWidth = newWidth;
        targetHeight = newHeight;
        Viewport = new ViewportF(0, 0, newWidth, newHeight, 0, 1.0f);
    }
}

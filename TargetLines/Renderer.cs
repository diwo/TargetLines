using System;
using System.Diagnostics;
using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;
using SwapChain = SharpDX.DXGI.SwapChain;

namespace TargetLines;

public class Renderer : IDisposable {
    public delegate void OnFrameDelegate(double time);
    public event OnFrameDelegate OnFrameEvent;

    public double Time;

    private Device device;
    private SwapChain swapChain;
    private DeviceContext deviceContext;
    private Stopwatch stopwatch;

    public Renderer(Device _device, SwapChain _swapChain) {
        device = _device;
        swapChain = _swapChain;
        deviceContext = _device.ImmediateContext;
        stopwatch = new Stopwatch();
        stopwatch.Start();
    }

    public void Dispose() {
        stopwatch.Stop();
    }

    public void OnFrame() {
        Time = stopwatch.Elapsed.TotalSeconds;
        OnFrameEvent?.Invoke(Time);
    }
}

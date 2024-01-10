using DrahsidLib;
using Lumina.Models.Models;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Runtime.InteropServices;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using SwapChain = SharpDX.DXGI.SwapChain;
using Vector3 = SharpDX.Vector3;


namespace TargetLines;


#if HELLOTRI_TEST
[StructLayout(LayoutKind.Sequential)]
internal struct TriangleVertex {
    public Vector3 Position;
    public Color4 Color;
}

public class HelloTri {
    public Vector3 Position = new Vector3();
    public Vector3 Velocity = new Vector3();
    public Vector3 DefaultPosition = new Vector3();
    public Vector3 Rotation = new Vector3();
    public float Scale = 1.0f;
    public float Initial = 0.0f;
    public float Lifetime = 0.0f;

    private Buffer triangleMatrixBuffer { get; set; }
    private Buffer triangleVertexBuffer { get; set; }
    private InputLayout triangleInputLayout { get; set; }
    private VertexBufferBinding triangleVertexBufferBinding { get; set; }
    private Matrix triWorldMatrix;
    private TriangleVertex[] vertexArray;

    private Device device;
    private SwapChain swapChain;
    private DeviceContext deviceContext;

    private Random random = new Random();

    public void Dispose() {
        triangleInputLayout?.Dispose();
        triangleVertexBuffer?.Dispose();
        triangleMatrixBuffer?.Dispose();
        if (SwapChainHook.Renderer != null) {
            SwapChainHook.Renderer.OnFrameEvent -= OnFrame;
        }
    }

    public HelloTri(Device _device, SwapChain _swapChain, Vector3 position) {
        device = _device;
        swapChain = _swapChain;
        deviceContext = _device.ImmediateContext;
        Position = position;
        DefaultPosition = position;
        random = new Random();

        InitializeTriangle();

        if (SwapChainHook.Renderer != null) {
            SwapChainHook.Renderer.OnFrameEvent += OnFrame;
        }
    }

    private float RandomFloat() {
        return (float)random.NextDouble();
    }

    private void SetupParticle() {
        Position = DefaultPosition;
        Velocity = new Vector3((RandomFloat() - 0.5f) * 16, (RandomFloat() - 0.5f) * 32, (RandomFloat() - 0.5f) * 16);
        Lifetime = RandomFloat() * 6.0f;
    }

    private void InitializeTriangle() {
        if (ShaderSingleton.Initialized == false) {
            Service.Logger.Error("ShaderSingleton is not initialized?");
        }
        Service.Logger.Verbose("Creating triangleMatrixBuffer...");
        triangleMatrixBuffer = new Buffer(device, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

        Service.Logger.Verbose("Creating triangleVertexBuffer...");
        vertexArray = new TriangleVertex[3];

        vertexArray[0].Position = new Vector3(0, 0, 0);
        vertexArray[1].Position = new Vector3(0.5f, 0, 0);
        vertexArray[2].Position = new Vector3(0.5f, 0.5f, 0);
        vertexArray[0].Color = new Color4(1.0f, 0, 0, 1.0f);
        vertexArray[1].Color = new Color4(0.0f, 1.0f, 0, 1.0f);
        vertexArray[2].Color = new Color4(0, 0, 1.0f, 1.0f);

        triangleVertexBuffer = Buffer.Create(device, BindFlags.VertexBuffer, vertexArray);

        Service.Logger.Verbose("Creating triangleInputLayout...");
        triangleInputLayout = new InputLayout(
            device,
            ShaderSingleton.GetVertexShaderBytecode(ShaderSingleton.Shader.HelloTri).Data,
            new[] {
                new InputElement("POSITION", 0, Format.R32G32B32_Float, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, Utilities.SizeOf<Vector3>(), 0)
            }
        );

        Service.Logger.Verbose("Creating triangleVertexBufferBinding...");
        triangleVertexBufferBinding = new VertexBufferBinding(triangleVertexBuffer, Utilities.SizeOf<TriangleVertex>(), 0);
    }

    public void OnFrame(double _time) {
        float time = (float)_time;
        float frametime = 0;

        unsafe {
            frametime = Globals.Framework->FrameDeltaTime;
        }

        Lifetime -= frametime;
        Velocity -= new Vector3(0, 4.405f * frametime, 0);
        Velocity *= 0.991f;
        Position += Velocity * frametime;

        if (Lifetime <= 0) {
            SetupParticle();
        }

        Rotation = new Vector3(MathF.Cos(time + Initial) * 2.0f, MathF.Sin(time + Initial), -MathF.Cos(time + Initial * 2) * 1.5f);
        Scale = (MathF.Sin(time + Initial) + 1.0f) / 1.5f;
        triWorldMatrix = Matrix.Identity * Matrix.AffineTransformation(Scale, Quaternion.RotationYawPitchRoll(Rotation.X, Rotation.Y, Rotation.Z), Position);

        triWorldMatrix.Transpose();

        deviceContext.UpdateSubresource(ref triWorldMatrix, triangleMatrixBuffer);

        deviceContext.InputAssembler.InputLayout = triangleInputLayout;
        deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
        deviceContext.InputAssembler.SetVertexBuffers(0, triangleVertexBufferBinding);

        deviceContext.VertexShader.Set(ShaderSingleton.GetVertexShader(ShaderSingleton.Shader.HelloTri));
        deviceContext.PixelShader.Set(ShaderSingleton.GetPixelShader(ShaderSingleton.Shader.HelloTri));

        deviceContext.VertexShader.SetConstantBuffer(0, triangleMatrixBuffer);
        deviceContext.VertexShader.SetConstantBuffer(1, SwapChainHook.Scene?.ViewProjectionBuffer);

        deviceContext.Draw(vertexArray.Length, 0);
    }
}
#endif


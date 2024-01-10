using DrahsidLib;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using SwapChain = SharpDX.DXGI.SwapChain;
using Vector3 = SharpDX.Vector3;

namespace TargetLines;

// Big TODO list:
// - Finish writing shaders and effects
// - End caps
// - Hook up with existing system, implement state machine
// - UI collision
// - Possibly hack my way into rendering with depth in the context of the game for occlusion
// - Try using round joins intstead of miter joins (allegedly look beter at straight angles)

[StructLayout(LayoutKind.Explicit)]
internal struct LineVertex {
    [FieldOffset(0x00)] public Vector4 Position;
    [FieldOffset(0x10)] public Color4 Color;
    [FieldOffset(0x20)] public Vector2 StartPos;
    [FieldOffset(0x30)] public Vector2 Dir;
}

internal class LineActor {
    public Vector3 Source;
    public Vector3 Destination;
    public int NumSegments = 7;
    public bool IsQuadratic;

    private Vector3 Middle;

    private Device device;
    private SwapChain swapChain;
    private DeviceContext deviceContext;

    private Buffer vertexBuffer { get; set; }
    private Buffer indexBuffer { get; set; }
    private InputLayout layout { get; set; }
    private VertexBufferBinding vertexBufferBinding { get; set; }

    private Vector3[] linePoints {  get; set; }
    private Vector2[] linePoints2D { get; set; }
    private LineVertex[] lineVertices { get; set; }
    private int[] lineIndices { get; set; }

    private int _numSegments { get; set; }
    private int _frame = 0;

    private bool fail = false;

    private int vertexCount {
        get {
            return _numSegments * 4;
        }
    }   

    private  int indexCount {
        get {
            return (_numSegments - 1) * 6;
        }
    }

    public LineActor(Device _device, SwapChain _swapChain) {
        device = _device;
        swapChain = _swapChain;
        deviceContext = _device.ImmediateContext;

        _numSegments = NumSegments = 127;
        IsQuadratic = false;

        InitializeMemory();

        layout = new InputLayout(
            device,
            ShaderSingleton.GetVertexShaderBytecode(ShaderSingleton.Shader.Line).Data,
            new InputElement[] {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0),
                new InputElement("COLOR", 0,    Format.R32G32B32A32_Float, Utilities.SizeOf<Vector4>(), 0),
                new InputElement("START", 0,    Format.R32G32_Float, Utilities.SizeOf<Vector4>() + Utilities.SizeOf<Color4>(), 0),
                new InputElement("DIR", 0,      Format.R32G32_Float, Utilities.SizeOf<Vector4>() + Utilities.SizeOf<Color4>() + Utilities.SizeOf<Vector2>(), 0),
            }
        );

        if (SwapChainHook.Renderer != null) {
            SwapChainHook.Renderer.OnFrameEvent += OnFrame;
        }
    }

    public void Dispose() {
        layout?.Dispose();
        vertexBuffer?.Dispose();
        indexBuffer?.Dispose();
        layout = null;
        vertexBuffer = null;
        indexBuffer = null;
        if (SwapChainHook.Renderer != null) {
            SwapChainHook.Renderer.OnFrameEvent -= OnFrame;
        }
    }

    private void InitializeMemory() {
        _numSegments = NumSegments;

        linePoints = new Vector3[NumSegments];
        linePoints2D = new Vector2[NumSegments];
        lineVertices = new LineVertex[vertexCount];
        lineIndices = new int[indexCount];

        var vertexBufferDesc = new BufferDescription()
        {
            SizeInBytes = Utilities.SizeOf<LineVertex>() * vertexCount,
            Usage = ResourceUsage.Dynamic,
            BindFlags = BindFlags.VertexBuffer,
            CpuAccessFlags = CpuAccessFlags.Write,
            OptionFlags = ResourceOptionFlags.None,
            StructureByteStride = Utilities.SizeOf<LineVertex>()
        };

        vertexBuffer = new Buffer(device, vertexBufferDesc);

        var indexBufferDesc = new BufferDescription()
        {
            SizeInBytes = Utilities.SizeOf<int>() * indexCount,
            Usage = ResourceUsage.Dynamic,
            BindFlags = BindFlags.IndexBuffer,
            CpuAccessFlags = CpuAccessFlags.Write,
            OptionFlags = ResourceOptionFlags.None,
            StructureByteStride = Utilities.SizeOf<int>()
        };

        indexBuffer = new Buffer(device, indexBufferDesc);
        vertexBufferBinding = new VertexBufferBinding(vertexBuffer, Utilities.SizeOf<LineVertex>(), 0);

        Service.Logger.Info("New memory");
    }


    private Vector3 EvaluateCubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) {
        if (t == 0) {
            return p0;
        }

        if (t == 1) {
            return p3;
        }

        float t2 = t * t;
        float t3 = t2 * t;
        float mt = 1 - t;
        float mt2 = mt * mt;
        float mt3 = mt2 * mt;

        Vector3 point =
            mt3 * p0 +
            3 * mt2 * t * p1 +
            3 * mt * t2 * p2 +
            t3 * p3;
        return point;
    }

    private Vector3 EvaluateQuadratic(Vector3 p0, Vector3 p1, Vector3 p2, float t) {
        float mt = 1 - t;

        if (t == 0) {
            return p0;
        }

        if (t == 1) {
            return p2;
        }

        Vector3 point = mt * mt * p0
            + 2 * mt * t * p1
            + t * t * p2;
        return point;
    }

    private void CreateLinePoints() {
        Vector3 point = Vector3.Zero;
        for (int index = 0; index < linePoints.Length; index++) {
            float time = (float)index / ((float)linePoints.Length - 1);

            if (IsQuadratic) {
                point = EvaluateQuadratic(Source, Middle, Destination, time);
            }
            else {
                point = EvaluateCubic(Source, Middle, Middle, Destination, time);
            }

            linePoints[index] = point;

            System.Numerics.Vector2 twoD;
            System.Numerics.Vector3 threeD;
            Vector2 dxTwoD = new Vector2();

            threeD.X = point.X;
            threeD.Y = point.Y;
            threeD.Z = point.Z;

            Service.GameGui.WorldToScreen(threeD, out twoD);
            dxTwoD.X = twoD.X;
            dxTwoD.Y = twoD.Y;

            linePoints2D[index] = dxTwoD;
        }
    }

    Vector2 ScreenCoordsToNDC(Vector2 coords) {
        float screenWidth = SwapChainHook.Scene.Viewport.Width;
        float screenHeight = SwapChainHook.Scene.Viewport.Height;

        return new Vector2(
            (coords.X / screenWidth) * 2.0f - 1.0f,
            1.0f - (coords.Y / screenHeight) * 2.0f
        );
    }

    private Vector2[] CalculateArcVertices(Vector2 center, Vector2 start, Vector2 end, float radius, int segments) {
        List<Vector2> arcPoints = new List<Vector2>();
        float angleStart = MathF.Atan2(start.Y - center.Y, start.X - center.X);
        float angleEnd = MathF.Atan2(end.Y - center.Y, end.X - center.X);

        float angle = angleStart;
        float angleStep = (angleEnd - angleStart) / segments;

        for (int index = 0; index <= segments; index++) {
            float x = center.X + radius * MathF.Cos(angle);
            float y = center.Y + radius * MathF.Sin(angle);
            arcPoints.Add(new Vector2(x, y));
            angle += angleStep;
        }

        return arcPoints.ToArray();
    }

#if HELLOTRI_TEST
    private Vector3[] triVertices = new[] {
        new Vector3(0.0f, 0.5f, 0.0f), // Top vertex
        new Vector3(0.45f, -0.5f, 0.0f), // Bottom right
        new Vector3(-0.45f, -0.5f, 0.0f) // Bottom left
    };

    private float angle = 0.0f;

    private void CreateMesh() {
        angle += 0.01f;
        // am I even sane?
        for (int i = 0; i < 3; i++) {
            float x = triVertices[i].X;
            float y = triVertices[i].Y;

            // Calculate the rotated position
            lineVertices[i].Position.X = (float)(x * Math.Cos(angle) - y * Math.Sin(angle));
            lineVertices[i].Position.Y = (float)(x * Math.Sin(angle) + y * Math.Cos(angle));
            lineVertices[i].Position.Z = 0.0f;
            lineVertices[i].Position.W = 1.0f;
            lineVertices[i].Color = Color.White;
            lineIndices[i] = i;
        }
#else
    private void CreateMesh() {
        float lineThickness = 0.01f;

        Vector2 prevPerp = Vector2.Zero;
        Vector2 dir = Vector2.Zero;
        Vector2 perp = Vector2.Zero;
        for (int index = 0; index < _numSegments - 1; index++) {
            Vector2 p1 = ScreenCoordsToNDC(linePoints2D[index]);
            Vector2 p2 = ScreenCoordsToNDC(linePoints2D[index + 1]);

            dir = Vector2.Normalize(p2 - p1);
            perp = new Vector2(-dir.Y, dir.X);

            if (index > 0) {
                perp = Vector2.Normalize(perp + prevPerp); // Average the normals (perpendiculars) with the previous segment's to create a miter join
            }

            float overlap = 0.01f;
            perp *= lineThickness * 0.5f + overlap;
            prevPerp = new Vector2(-dir.Y, dir.X); // Store the current perpendicular for the next iteration

            int vertIndex = index * 2;
            lineVertices[vertIndex + 0].Position = new Vector4(p1 - perp, 0, 1.0f);
            lineVertices[vertIndex + 1].Position = new Vector4(p1 + perp, 0, 1.0f);
            lineVertices[vertIndex + 0].Color = Color.White;
            lineVertices[vertIndex + 1].Color = Color.White;

            lineVertices[vertIndex + 0].StartPos = p1;
            lineVertices[vertIndex + 1].StartPos = p1;
            lineVertices[vertIndex + 0].Dir = dir;
            lineVertices[vertIndex + 1].Dir = dir;


            if (index < _numSegments - 2) {
                int idxIndex = index * 6;
                lineIndices[idxIndex + 0] = vertIndex + 0;
                lineIndices[idxIndex + 1] = vertIndex + 1;
                lineIndices[idxIndex + 2] = vertIndex + 2;
                lineIndices[idxIndex + 3] = vertIndex + 2;
                lineIndices[idxIndex + 4] = vertIndex + 1;
                lineIndices[idxIndex + 5] = vertIndex + 3;
            }
        }

        // Handle the last segment separately
        int lastSegmentIndex = _numSegments - 2;
        int lastVertIndex = lastSegmentIndex * 2;
        Vector2 lastP1 = ScreenCoordsToNDC(linePoints2D[lastSegmentIndex]);
        Vector2 lastP2 = ScreenCoordsToNDC(linePoints2D[lastSegmentIndex + 1]);
        Vector2 lastCenter = (lastP1 + lastP2) * 0.5f;

        dir = Vector2.Normalize(lastP2 - lastP1);
        perp = new Vector2(-dir.Y, dir.X);

        // Use the perpendicular from the second to last segment if available
        if (_numSegments > 2) {
            Vector2 secondLastDir = Vector2.Normalize(lastP1 - ScreenCoordsToNDC(linePoints2D[lastSegmentIndex - 1]));
            Vector2 secondLastPerp = new Vector2(-secondLastDir.Y, secondLastDir.X);
            perp = Vector2.Normalize(perp + secondLastPerp);
        }

        perp *= lineThickness * 0.5f;

        lineVertices[lastVertIndex + 2].Position = new Vector4(lastP2 - perp, 0, 1.0f);
        lineVertices[lastVertIndex + 3].Position = new Vector4(lastP2 + perp, 0, 1.0f);
        lineVertices[lastVertIndex + 2].Color = Color.White;
        lineVertices[lastVertIndex + 3].Color = Color.White;
        lineVertices[lastVertIndex + 2].StartPos = lastP1;
        lineVertices[lastVertIndex + 3].StartPos = lastP1;
        lineVertices[lastVertIndex + 2].Dir = dir;
        lineVertices[lastVertIndex + 3].Dir = dir;

        int lastIdxIndex = lastSegmentIndex * 6;
        lineIndices[lastIdxIndex + 0] = lastVertIndex + 0;
        lineIndices[lastIdxIndex + 1] = lastVertIndex + 1;
        lineIndices[lastIdxIndex + 2] = lastVertIndex + 2;
        lineIndices[lastIdxIndex + 3] = lastVertIndex + 2;
        lineIndices[lastIdxIndex + 4] = lastVertIndex + 1;
        lineIndices[lastIdxIndex + 5] = lastVertIndex + 3;
    }
#endif

    private void UpdateBufferContents() {
        DataStream stream;

        // Update vertex buffer
        var vertexBox = deviceContext.MapSubresource(vertexBuffer, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out stream);
        Utilities.Write(vertexBox.DataPointer, lineVertices, 0, lineVertices.Length);
        deviceContext.UnmapSubresource(vertexBuffer, 0);

        // Update index buffer
        var indexBox = deviceContext.MapSubresource(indexBuffer, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out stream);
        Utilities.Write(indexBox.DataPointer, lineIndices, 0, lineIndices.Length);
        deviceContext.UnmapSubresource(indexBuffer, 0);
    }


    private void Render() {
        // if the segment count changes, we need to reallocate the relevant memory
        if (_numSegments != NumSegments) {
            InitializeMemory();
        }

        if (linePoints2D.Length < 2 || lineVertices.Length <= 0) {
            Service.Logger.Error($"Line is invalid");
            fail = true;
            return;
        }

        CreateLinePoints();
        CreateMesh();
        UpdateBufferContents();

        vertexBufferBinding = new VertexBufferBinding(vertexBuffer, Utilities.SizeOf<LineVertex>(), 0);

        deviceContext.InputAssembler.InputLayout = layout;
        deviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;

        deviceContext.InputAssembler.SetVertexBuffers(0, vertexBufferBinding);
        deviceContext.InputAssembler.SetIndexBuffer(indexBuffer, Format.R32_UInt, 0);

        deviceContext.VertexShader.Set(ShaderSingleton.GetVertexShader(ShaderSingleton.Shader.Line));
        deviceContext.PixelShader.Set(ShaderSingleton.GetPixelShader(ShaderSingleton.Shader.Line));

        deviceContext.DrawIndexed(indexCount, 0, 0);
    }

    public void OnFrame(double _time) {
        if (fail) { return; }
        try {
            Middle = (Source + Destination) * 0.5f;
            Middle.Y += 0.5f;

            Render();
        }
        catch (Exception ex) {
            Service.Logger.Error($"Line error?\n{ex.ToString()}");
            fail = true;
        }
    }
}   

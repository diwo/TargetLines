struct VertexInput {
    float3 Position : POSITION;
    float4 Color : COLOR;
};

struct PixelInput {
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
};

cbuffer ModelMatrixBuffer : register(b0) {
    float4x4 ModelMatrix;
};

cbuffer ViewProjectionBuffer : register(b1) {
    float4x4 ViewProjectionMatrix;
};

PixelInput Main(VertexInput input) {
    PixelInput output;

    float4 pos = float4(input.Position, 1.0);
    pos = mul(pos, ModelMatrix);    
    pos = mul(pos, ViewProjectionMatrix);
    
    output.Position = pos;
    output.Color = input.Color;

    return output;
}


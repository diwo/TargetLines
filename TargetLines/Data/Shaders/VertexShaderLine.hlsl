struct VertexInputType {
    float4 Position : POSITION;
    float4 Color : COLOR;
	float2 StartPos : START;
    float2 Dir : DIR;
};

struct PixelInputType {
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
    float2 StartPos : TEXCOORD0;
    float2 Dir : TEXCOORD1;
};

PixelInputType Main(VertexInputType input) {
    PixelInputType output;

    output.Position = input.Position;
    output.Color = input.Color;
	output.StartPos = input.StartPos;
    output.Dir = input.Dir;
    return output;
}

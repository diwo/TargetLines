struct PixelInputType {
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
    float2 StartPos : TEXCOORD0;
    float2 Dir : TEXCOORD1;
};

float4 Main(PixelInputType input) : SV_TARGET {
    float4 color = input.Color;
    float2 fragToStart = input.Position.xy - input.StartPos;
    float distanceAlongLine = dot(fragToStart, input.Dir);
    float gradient = saturate(1.0f - abs(distanceAlongLine));
    color.a *= gradient;
    return color;
}

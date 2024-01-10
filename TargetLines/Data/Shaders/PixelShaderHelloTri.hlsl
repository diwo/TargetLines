struct PixelInput {
    float4 Position : SV_POSITION;
    float4 Color : COLOR;
};

float4 Main(PixelInput input) : SV_TARGET {
    return input.Color;
}


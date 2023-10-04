struct VS_INPUT
{
    float4 Position : POSITION;
};

struct VS_OUTPUT
{
    float4 Position : SV_POSITION;
};

VS_OUTPUT main(VS_INPUT input)
{
    VS_OUTPUT output;
    output.Position = input.Position;
    return output;
}

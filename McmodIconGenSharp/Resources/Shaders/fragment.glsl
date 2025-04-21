#version 450

layout(location = 0) in vec2 fsin_TexCoord;
layout(location = 1) in flat vec3 fsin_Normal;
layout(location = 2) in flat uint fsin_TexIndex;

layout(location = 0) out vec4 fsout_Color;

layout(set = 0, binding = 1) uniform LightingInfo {
    vec4 LightDirection; // Using vec4 for safety
    vec4 LightColor;
    vec4 AmbientLightColor;
} lighting;
layout(set = 1, binding = 0) uniform texture2D Textures[6];
layout(set = 1, binding = 6) uniform sampler SharedSampler;

void main()
{
    vec4 color = texture(sampler2D(Textures[fsin_TexIndex], SharedSampler), fsin_TexCoord);
    color.rgb *= lighting.AmbientLightColor.rgb + lighting.LightColor.rgb * max(0.0, dot(lighting.LightDirection.xyz, fsin_Normal));
    color.rgb = min(color.rgb, 1.0);

    fsout_Color = color;
}
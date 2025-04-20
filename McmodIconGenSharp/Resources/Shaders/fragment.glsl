#version 450

layout(location = 0) in vec2 fsin_TexCoord;
layout(location = 1) in vec3 fsin_Normal;
layout(location = 2) in uint fsin_TexIndex;

layout(location = 0) out vec4 fsout_Color;

uniform sampler2D Texture[6];
uniform vec3 LightDirection;
uniform vec3 LightColor;
uniform vec3 AmbientLightColor;


void main()
{
    vec4 color = texture(Texture, fsin_TexCoord);
    color.rgb *= AmbientLightColor + LightColor * max(0.0, dot(LightDirection, fsin_Normal));
    color.rgb = min(color.rgb, 1.0);

    fsout_Color = color;
}
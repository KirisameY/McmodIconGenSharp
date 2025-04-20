#version 450

layout(location = 0) in vec3 Position;
layout(location = 1) in vec3 Normal;
layout(location = 2) in vec2 TexCoord;
layout(location = 3) in uint TexIndex;

layout(location = 0) out vec2 fsin_TexCoord;
layout(location = 1) out vec3 fsin_Normal;
layout(location = 2) out uint fsin_TexIndex;

uniform mat4 Model;
uniform mat4 View;
uniform mat4 Projection;

void main()
{
    gl_Position = Projection * View * Model * vec4(Position, 1.0);
    fsin_Normal = mat3(transpose(inverse(model))) * Normal;
    fsin_TexCoord = TexCoord;
    fsin_TexIndex = TexIndex;
}
#version 330 core

layout(location = 0) in vec4 color;
layout(location = 1) in vec2 texCoord;

uniform sampler2D MainTexture;

layout(location = 0) out vec4 outputColor;

void main()
{
    outputColor = color * texture(MainTexture, texCoord);
}

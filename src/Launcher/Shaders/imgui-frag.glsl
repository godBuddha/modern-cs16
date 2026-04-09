#version 150

uniform sampler2D MainTexture;

in vec4 color;
in vec2 texCoord;

out vec4 outputColor;

void main()
{
    outputColor = color * texture(MainTexture, texCoord);
}

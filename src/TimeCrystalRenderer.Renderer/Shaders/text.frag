#version 410 core

in vec2 TexCoord;

uniform sampler2D uFontAtlas;
uniform vec4 uColor;

out vec4 FragColor;

void main()
{
    float alpha = texture(uFontAtlas, TexCoord).r;
    if (alpha < 0.5)
        discard;
    FragColor = vec4(uColor.rgb, uColor.a * alpha);
}

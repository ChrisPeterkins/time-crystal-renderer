#version 410 core

in vec3 FragPos;
in vec3 Normal;
in vec3 VertexColor;

uniform vec3 uLightDir;
uniform vec3 uViewPos;

out vec4 FragColor;

void main()
{
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(-uLightDir);

    // Ambient
    vec3 ambient = 0.15 * VertexColor;

    // Diffuse (Lambertian)
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * VertexColor;

    // Specular (Blinn-Phong)
    vec3 viewDir = normalize(uViewPos - FragPos);
    vec3 halfwayDir = normalize(lightDir + viewDir);
    float spec = pow(max(dot(norm, halfwayDir), 0.0), 32.0);
    vec3 specular = 0.3 * spec * vec3(1.0);

    FragColor = vec4(ambient + diffuse + specular, 1.0);
}

// Плоскость, невидимая сама по себе, но затемняющая видео с камеры там, где на неё
// падает тень от направленного света — «ловит» тень персонажа на реальный пол,
// без чего он выглядит наклейкой, а не объектом, стоящим в комнате.
// Единственный файл в проекте, который я не могу проверить компиляцией офлайн —
// в отличие от всего C#-кода этой сессии, шейдеры Unity компилирует только сам
// редактор при импорте. Если материал окажется розовым (ошибка компиляции)
// или тень не появится — смотрите Console в Unity, а не полагайтесь на этот файл вслепую.
Shader "ARCharacter/ShadowCatcher"
{
    Properties
    {
        _Color ("Shadow Color", Color) = (0, 0, 0, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ShadowCatcher"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _ShadowStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                half lit = MainLightRealtimeShadow(shadowCoord);
                half alpha = (half(1.0) - lit) * _ShadowStrength;
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}

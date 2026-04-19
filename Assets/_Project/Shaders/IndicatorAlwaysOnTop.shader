Shader "Custom/IndicatorAlwaysOnTop"
{
    Properties
    {
        _Color          ("Color",           Color)         = (1, 0.5, 0, 0.6)
        _Fresnel        ("Fresnel Power",   Range(0.1, 5)) = 1.5
        [Toggle] _UseFresnelOnly ("Fresnel Only (hollow)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Overlay"
            "RenderPipeline"  = "UniversalPipeline"
        }

        // ─── Pass 1: ZTest Always — her şeyin önünde ─────────────
        Pass
        {
            Name "IndicatorOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest  Always
            Cull   Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Fresnel;
                float  _UseFresnelOnly;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS   = normalize(GetCameraPositionWS() - posInputs.positionWS);
                OUT.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float NdotV   = saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS)));
                float fresnel = pow(1.0 - NdotV, _Fresnel);

                half4 col = _Color;

                if (_UseFresnelOnly > 0.5)
                    col.a = fresnel * _Color.a;         // Sadece kenarlar görünür (hollow)
                else
                    col.a = lerp(_Color.a * 0.4, _Color.a, fresnel);  // Merkez yarı saydam, kenar tam

                col.rgb = MixFog(col.rgb, IN.fogFactor);
                return col;
            }
            ENDHLSL
        }
    }
}

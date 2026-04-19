Shader "Custom/ThermalEffect"
{
    Properties
    {
        _MainTex        ("Source Texture",    2D)           = "white" {}
        _Intensity      ("Thermal Intensity", Range(0, 1))  = 1.0
        _Contrast       ("Contrast",          Range(0.1, 3)) = 1.2
        _NoiseStrength  ("Sensor Noise",      Range(0, 0.05)) = 0.008
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off  ZWrite Off  ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Intensity;
            float _Contrast;
            float _NoiseStrength;

            // ─── Termal renk paleti ───────────────────────────────────
            // Siyah → Lacivert → Camgöbeği → Yeşil → Sarı → Turuncu → Beyaz
            float3 ThermalGradient(float t)
            {
                const float3 c0 = float3(0.00, 0.00, 0.00);  // 0.00  siyah
                const float3 c1 = float3(0.00, 0.00, 0.55);  // 0.15  lacivert
                const float3 c2 = float3(0.00, 0.60, 1.00);  // 0.30  camgöbeği
                const float3 c3 = float3(0.00, 1.00, 0.40);  // 0.45  yeşil
                const float3 c4 = float3(1.00, 1.00, 0.00);  // 0.65  sarı
                const float3 c5 = float3(1.00, 0.25, 0.00);  // 0.82  turuncu-kırmızı
                const float3 c6 = float3(1.00, 1.00, 1.00);  // 1.00  beyaz

                if (t < 0.15) return lerp(c0, c1, t / 0.15);
                if (t < 0.30) return lerp(c1, c2, (t - 0.15) / 0.15);
                if (t < 0.45) return lerp(c2, c3, (t - 0.30) / 0.15);
                if (t < 0.65) return lerp(c3, c4, (t - 0.45) / 0.20);
                if (t < 0.82) return lerp(c4, c5, (t - 0.65) / 0.17);
                              return lerp(c5, c6, (t - 0.82) / 0.18);
            }

            // ─── Sensör gürültüsü ─────────────────────────────────────
            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float4 src = tex2D(_MainTex, i.uv);

                // Algılanan parlaklık (perceived luminance)
                float lum = dot(src.rgb, float3(0.2126, 0.7152, 0.0722));

                // Kontrast
                lum = pow(saturate(lum), 1.0 / max(_Contrast, 0.01));

                // Zamana bağlı sensör gürültüsü
                float noise = (Hash21(i.uv * 384.0 + frac(_Time.xz)) - 0.5) * _NoiseStrength;
                lum = saturate(lum + noise);

                float3 thermal = ThermalGradient(lum);

                // Orijinal görüntü ile karıştır
                return fixed4(lerp(src.rgb, thermal, _Intensity), 1.0);
            }
            ENDCG
        }
    }
}

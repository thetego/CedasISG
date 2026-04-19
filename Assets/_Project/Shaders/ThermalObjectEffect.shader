Shader "Custom/ThermalObjectEffect"
{
    Properties
    {
        [Header(Base)]
        _MainTex        ("Albedo (RGB)",        2D)            = "white" {}
        _Color          ("Tint",                Color)         = (1,1,1,1)

        [Header(Thermal)]
        _Intensity      ("Thermal Intensity",   Range(0,1))    = 1.0
        _Contrast       ("Contrast",            Range(0.1,3))  = 1.3
        _Brightness     ("Brightness Offset",   Range(-0.5,0.5)) = 0.0

        [Header(Rim Edge Heat)]
        _RimPower       ("Rim Sharpness",       Range(0.5,8))  = 2.5
        _RimBoost       ("Rim Heat Boost",      Range(0,1))    = 0.35
        _RimColor       ("Rim Color",           Color)         = (1,0.3,0,1)

        [Header(UV Transform)]
        _Rotation       ("Rotation (deg)",      Range(0,360))  = 0.0
        [Toggle] _FlipX ("Flip Horizontal",     Float)         = 0
        [Toggle] _FlipY ("Flip Vertical",       Float)         = 0

        [Header(Noise)]
        _NoiseStrength  ("Sensor Noise",        Range(0,0.06)) = 0.008
        _NoiseScale     ("Noise UV Scale",      Range(64,512)) = 256
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Tags { "LightMode"="UniversalForward" }   // URP
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            // ─── Input ────────────────────────────────────────────
            struct appdata
            {
                float4 vertex   : POSITION;
                float2 uv       : TEXCOORD0;
                float3 normal   : NORMAL;
            };

            struct v2f
            {
                float4 pos          : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 worldNormal  : TEXCOORD1;
                float3 worldView    : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            // ─── Properties ───────────────────────────────────────
            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4    _Color;

            float _Intensity;
            float _Contrast;
            float _Brightness;

            float _RimPower;
            float _RimBoost;
            float4 _RimColor;

            float _Rotation;
            float _FlipX;
            float _FlipY;
            float _NoiseStrength;
            float _NoiseScale;

            float2 TransformUV(float2 uv)
            {
                // Flip
                uv.x = lerp(uv.x, 1.0 - uv.x, _FlipX);
                uv.y = lerp(uv.y, 1.0 - uv.y, _FlipY);

                // Merkez etrafında döndür
                float rad = _Rotation * (3.14159265 / 180.0);
                float s   = sin(rad);
                float c   = cos(rad);
                uv -= 0.5;
                uv  = float2(uv.x * c - uv.y * s,
                             uv.x * s + uv.y * c);
                uv += 0.5;
                return uv;
            }

            // ─── Termal Palet ─────────────────────────────────────
            // Siyah (soğuk) → Lacivert → Camgöbeği → Yeşil → Sarı → Turuncu → Beyaz (sıcak)
            float3 ThermalGradient(float t)
            {
                const float3 c0 = float3(0.00, 0.00, 0.00);   // 0.00  siyah
                const float3 c1 = float3(0.00, 0.00, 0.55);   // 0.15  lacivert
                const float3 c2 = float3(0.00, 0.60, 1.00);   // 0.30  camgöbeği
                const float3 c3 = float3(0.00, 1.00, 0.40);   // 0.45  yeşil
                const float3 c4 = float3(1.00, 1.00, 0.00);   // 0.65  sarı
                const float3 c5 = float3(1.00, 0.25, 0.00);   // 0.82  turuncu-kırmızı
                const float3 c6 = float3(1.00, 1.00, 1.00);   // 1.00  beyaz

                if (t < 0.15) return lerp(c0, c1, t / 0.15);
                if (t < 0.30) return lerp(c1, c2, (t - 0.15) / 0.15);
                if (t < 0.45) return lerp(c2, c3, (t - 0.30) / 0.15);
                if (t < 0.65) return lerp(c3, c4, (t - 0.45) / 0.20);
                if (t < 0.82) return lerp(c4, c5, (t - 0.65) / 0.17);
                              return lerp(c5, c6, (t - 0.82) / 0.18);
            }

            // ─── Sensör Gürültüsü ─────────────────────────────────
            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            // ─── Vertex ───────────────────────────────────────────
            v2f vert(appdata v)
            {
                v2f o;
                o.pos         = UnityObjectToClipPos(v.vertex);
                o.uv          = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldView     = normalize(_WorldSpaceCameraPos - worldPos);

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // ─── Fragment ─────────────────────────────────────────
            fixed4 frag(v2f i) : SV_Target
            {
                // 1. Albedo örneği (flip + döndürülmüş UV ile)
                float2 rotUV = TransformUV(i.uv);
                float4 src = tex2D(_MainTex, rotUV) * _Color;

                // 2. Algılanan parlaklık (perceived luminance)
                float lum = dot(src.rgb, float3(0.2126, 0.7152, 0.0722));
                lum = saturate(lum + _Brightness);

                // 3. Rim (kenar ısısı) — yüzeyin kameraya göre açısından hesaplanır
                float NdotV = saturate(dot(normalize(i.worldNormal), normalize(i.worldView)));
                float rim   = pow(1.0 - NdotV, _RimPower);
                lum = saturate(lum + rim * _RimBoost);

                // 4. Kontrast eğrisi
                lum = pow(saturate(lum), 1.0 / max(_Contrast, 0.01));

                // 5. Zamana bağlı sensör gürültüsü
                float noise = (Hash21(i.uv * _NoiseScale + frac(_Time.xz)) - 0.5) * _NoiseStrength;
                lum = saturate(lum + noise);

                // 6. Termal renge dönüştür
                float3 thermalColor = ThermalGradient(lum);

                // 7. Rim renk tonu karıştır (kenarlar sıcak görünsün)
                thermalColor = lerp(thermalColor, _RimColor.rgb, rim * _RimBoost * _Intensity);

                // 8. Orijinal renk ile blend (Intensity = 0 → orijinal, 1 → tam termal)
                float3 result = lerp(src.rgb, thermalColor, _Intensity);

                fixed4 col = fixed4(result, src.a);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    // Built-in pipeline fallback
    FallBack "Diffuse"
}

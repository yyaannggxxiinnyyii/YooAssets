Shader "Game/Sprite Outline Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0,8)) = 1
        _GlowIntensity ("Glow Intensity", Range(0,4)) = 0.8
        _PulseStrength ("Pulse Strength", Range(0,1)) = 0
        _PulseSpeed ("Pulse Speed", Range(0,8)) = 2
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _GlowIntensity;
            float _PulseStrength;
            float _PulseSpeed;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                #ifdef PIXELSNAP_ON
                output.vertex = UnityPixelSnap(output.vertex);
                #endif
                return output;
            }

            fixed4 SampleSprite(float2 uv)
            {
                fixed4 color = tex2D(_MainTex, uv);
                color.rgb *= color.a;
                return color;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 spriteColor = SampleSprite(input.texcoord) * input.color;
                float2 offset = _MainTex_TexelSize.xy * max(0.0, _OutlineWidth);
                float outlineAlpha = 0.0;
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(offset.x, 0)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(-offset.x, 0)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(0, offset.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(0, -offset.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(offset.x, offset.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(-offset.x, offset.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(offset.x, -offset.y)).a);
                outlineAlpha = max(outlineAlpha, tex2D(_MainTex, input.texcoord + float2(-offset.x, -offset.y)).a);

                float outsideAlpha = saturate(outlineAlpha - spriteColor.a);
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;
                fixed4 outlineColor = _OutlineColor;
                outlineColor.a *= outsideAlpha;
                outlineColor.rgb *= outlineColor.a * max(0.0, _GlowIntensity) * pulse;

                fixed4 output = spriteColor + outlineColor * (1.0 - spriteColor.a);
                return output;
            }
            ENDCG
        }
    }
}

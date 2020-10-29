Shader "Unlit/SpriteShader"
{
    Properties
    {
        _MainTex("Screen Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            uniform float4 _MainTex_TexelSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 texCoord = i.uv;
                texCoord.y *= _MainTex_TexelSize.w;
                texCoord.y = floor(texCoord.y);
                texCoord.y *= _MainTex_TexelSize.y;
                texCoord.y += 0.5 * _MainTex_TexelSize.y;

                fixed4 col = tex2D(_MainTex, texCoord);

                float scanLine = clamp(sin((i.uv.y * 200.0 - 0.25) * 2 * 3.14) * 0.66 + 0.5, 0.5, 1.0);
                return col * scanLine;
            }
            ENDCG
        }
    }
}
﻿Shader "Unlit/GreyScaleShader" {
    Properties{
        _MainTex("Texture", 2D) = "white" { }
    }
        SubShader{
            Pass {

                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"

                sampler2D _MainTex;

                struct v2f {
                    float4  pos : SV_POSITION;
                    float2  uv : TEXCOORD0;
                };

                float4 _MainTex_ST;

                v2f vert(appdata_base v)
                {
                    v2f o;
                    o.pos = UnityObjectToClipPos(v.vertex);
                    o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                    return o;
                }

                fixed4 frag(v2f i) : COLOR
                {
                    float texcol_a = tex2D(_MainTex, i.uv).a;
                    return fixed4(texcol_a, texcol_a, texcol_a, 1.0f);
                }

                ENDCG
            }
    }
        Fallback "VertexLit"
}
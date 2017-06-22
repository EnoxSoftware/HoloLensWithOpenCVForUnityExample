// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

    Shader "Custom/WireFrame"
    {
    Properties
    {
    _LineColor ("Line Color", Color) = (1,1,1,1)
    _GridColor ("Grid Color", Color) = (1,1,1,0)
    _LineWidth ("Line Width", float) = 0.01
    }
    SubShader
    {

			Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Transparent"
			"PreviewType"="Plane"
		}
		Lighting Off
		ZWrite Off
		Blend SrcAlpha OneMinusSrcAlpha

    Pass
    {
     
    CGPROGRAM
    #pragma vertex vert
    #pragma fragment frag
     
    uniform float4 _LineColor;
    uniform float4 _GridColor;
    uniform float _LineWidth;
     
    // vertex input: position, uv1, uv2
    struct appdata
    {
    float4 vertex : POSITION;
    float4 texcoord1 : TEXCOORD0;
    float4 color : COLOR;
    };
     
    struct v2f
    {
    float4 pos : POSITION;
    float4 texcoord1 : TEXCOORD0;
    float4 color : COLOR;
    };
     
    v2f vert (appdata v)
    {
    v2f o;
    o.pos = UnityObjectToClipPos( v.vertex);
    o.texcoord1 = v.texcoord1;
    o.color = v.color;
    return o;
    }
     
    fixed4 frag(v2f i) : COLOR
    {
    fixed4 answer;
     
    float lx = step(_LineWidth, i.texcoord1.x);
    float ly = step(_LineWidth, i.texcoord1.y);
    float hx = step(i.texcoord1.x, 1 - _LineWidth);
    float hy = step(i.texcoord1.y, 1 - _LineWidth);
     
    answer = lerp(_LineColor, _GridColor, lx*ly*hx*hy);
     
    return answer;
    }
    ENDCG
    }
    }
    Fallback "Vertex Colored", 1
    }
    
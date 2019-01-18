// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "AR/HolographicImageBlendShader"
{
    // Referring to https://forum.unity3d.com/threads/holographic-photo-blending-with-photocapture.416023/.

    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _VignetteScale ("Vignette Scale", RANGE(0,2)) = 0
        _VignetteOffset ("Vignette Offset" , Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
 
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
 
            #include "UnityCG.cginc"
 
            struct appdata
            {
                float4 vertex : POSITION;
            };
 
            struct v2f
            {
                float4 vertexPositionInProjectionSpace : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 vertexInProjectionSpace : TEXCOORD1;
            };
 
            sampler2D _MainTex;
            float4x4 _WorldToCameraMatrix;
            float4x4 _CameraProjectionMatrix;
            float _VignetteScale;
            float4 _VignetteOffset;
 
            v2f vert (appdata v)
            {
                v2f o;
                o.vertexPositionInProjectionSpace = UnityObjectToClipPos(v.vertex);
 
                // Calculate the vertex position in world space.
                float4 vertexPositionInWorldSpace = mul(unity_ObjectToWorld, float4(v.vertex.xyz,1));
                // Now take the world space vertex position and transform it so that
                // it is relative to the physical web camera on the HoloLens.
                float4 vertexPositionInCameraSpace = mul(_WorldToCameraMatrix, float4(vertexPositionInWorldSpace.xyz,1));
 
                // Convert our camera relative vertex into clip space.
                o.vertexInProjectionSpace = mul(_CameraProjectionMatrix, float4(vertexPositionInCameraSpace.xyz, 1.0));
 
                return o;
            }
 
            fixed4 frag (v2f i) : SV_Target
            {
                // Transform the vertex into normalized coordinate space.  Basically
                // we want to map where our vertex should be on the screen into the -1 to 1 range
                // for both the x and y axes.
                float2 signedUV = i.vertexInProjectionSpace.xy / i.vertexInProjectionSpace.w;
 
                // The HoloLens uses an additive display so the color black will
                // be transparent.  If the texture is smaller than the canvas, color the extra
                // area on the canvas black so it will be transparent on the HoloLens.
                if(abs(signedUV.x) > 1.0 || abs(signedUV.y) > 1.0)
                {
                    return fixed4( 0.0, 0.0, 0.0, 0.0);
                }
 
                // Currently our signedUV's x and y coordinates will fall between -1 and 1.
                // We need to map this range from 0 to 1 so that we can sample our texture.
                float2 uv = signedUV * 0.5 + float2(0.5, 0.5);
                fixed4 finalColor = tex2D(_MainTex, uv);
 
                // Finally add a circular vignette effect starting from the center
                // of the image.
                signedUV.x = signedUV.x + _VignetteOffset.x*2;
                signedUV.y = signedUV.y + _VignetteOffset.y*2;
                finalColor *= 1.0 -(length(signedUV) * _VignetteScale);
 
                return finalColor;
            }
            ENDCG
        }
    }
}
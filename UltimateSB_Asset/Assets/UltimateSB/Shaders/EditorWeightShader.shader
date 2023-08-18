Shader "Unlit/EditorWeightShader"
{
    Properties
    {
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
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float weight : TEXCOORD1;
            };


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.weight = v.color.a;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half4 col = lerp(fixed4(1, 0, 0, 1), fixed4(0, 0, 1, 1), i.weight);
                return col;
            }
            ENDCG
        }
    }
}

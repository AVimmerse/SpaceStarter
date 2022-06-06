Shader "Hidden/SimpleScattering/LightScatteringDownscaleDepthShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

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
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;

			sampler2D _CameraDepthTexture;
			float4 _CameraDepthTexture_TexelSize;

            fixed4 frag (v2f i) : SV_Target
            {
				float2 texelSize = 0.5 * _CameraDepthTexture_TexelSize.xy;
				//take samples in cross pattern 
				float2 taps[4] = {  float2(i.uv + float2(-1,-1) * texelSize), float2(i.uv + float2(-1,1) * texelSize),
									float2(i.uv + float2(1,-1) * texelSize), float2(i.uv + float2(1,1) * texelSize) };

				float d1 = tex2D(_CameraDepthTexture, taps[0]);
				float d2 = tex2D(_CameraDepthTexture, taps[1]);
				float d3 = tex2D(_CameraDepthTexture, taps[2]);
				float d4 = tex2D(_CameraDepthTexture, taps[3]);

				float result = min(d1, min(d2, min(d3, d4))); //pick min value from deph samples

				return result;
            }
            ENDCG
        }
    }
}

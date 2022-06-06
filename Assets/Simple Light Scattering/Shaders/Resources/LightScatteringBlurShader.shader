Shader "Hidden/SimpleScattering/LightScatteringBlurShader"
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
			float4 _MainTex_TexelSize;

			sampler2D LowResolutionDepth;
			float2 BlurDir;

			float BlurDepthFalloff;
			  
            fixed4 frag (v2f input) : SV_Target
            {			
				const float offset[4] = { 0, 1, 2, 3 };
				const float weight[4] = { 0.266, 0.213, 0.1, 0.036 };

				fixed4 result = tex2D(_MainTex, input.uv) * weight[0];
				float totalWeight = weight[0];

				float centerDepth = Linear01Depth(tex2D(LowResolutionDepth, input.uv));

				[unroll]
				for (int i = 1; i < 4; i++)
				{
					 float depth = Linear01Depth(tex2D(LowResolutionDepth, (input.uv + BlurDir * offset[i] * _MainTex_TexelSize.xy)));

					 float w = abs(depth - centerDepth) * BlurDepthFalloff;
					 w = exp(-w * w);

					 result += tex2D(_MainTex, (input.uv + BlurDir * offset[i] * _MainTex_TexelSize.xy)) * w * weight[i];

					 totalWeight += w * weight[i];

					 depth = Linear01Depth(tex2D(LowResolutionDepth, (input.uv - BlurDir * offset[i] * _MainTex_TexelSize.xy)));

					 w = abs(depth - centerDepth) * BlurDepthFalloff;
					 w = exp(-w * w);

					 result += tex2D(_MainTex, (input.uv - BlurDir * offset[i] * _MainTex_TexelSize.xy)) * w * weight[i];

					 totalWeight += w * weight[i];
				}

				return result / totalWeight;
            }
            ENDCG
        }
    }
}

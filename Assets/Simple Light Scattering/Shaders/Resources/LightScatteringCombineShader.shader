Shader "Hidden/SimpleScattering/LightScatteringCombineShader"
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

			sampler2D _MainTex;
			UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
			sampler2D LightScatteringTexturePoint;
			sampler2D LightScatteringTextureLinear;
			sampler2D LowResolutionDepth;

			float4 _MainTex_TexelSize; 
			float4 _CameraDepthTexture_TexelSize;
			float4 LowResolutionDepth_TexelSize;

			float DepthThreshold;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

			void UpdateSample(inout float minDist, inout float2 nearestUV, float z, float2 uv, float zFull) {
				float distance = abs(z - zFull);
				if (minDist > distance) {
					minDist = distance;
					nearestUV = uv;
				}
			}

			float4 GetNearestDepthSample(float2 uv) {
				//read full resolution depth
				float zFull = Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
				  
				float minDistance = 1.e8f;

				float2 UV00 = uv - 0.5 * LowResolutionDepth_TexelSize.xy;
				float2 nearestUV = UV00;
				float Z00 = Linear01Depth(SAMPLE_DEPTH_TEXTURE(LowResolutionDepth, UV00));
				UpdateSample(minDistance, nearestUV, Z00, UV00, zFull);

				float2 UV10 = float2(UV00.x + LowResolutionDepth_TexelSize.x, UV00.y);
				float Z10 = Linear01Depth(SAMPLE_DEPTH_TEXTURE(LowResolutionDepth, UV10));
				UpdateSample(minDistance, nearestUV, Z10, UV10, zFull);

				float2 UV01 = float2(UV00.x, UV00.y + LowResolutionDepth_TexelSize.y);
				float Z01 = Linear01Depth(SAMPLE_DEPTH_TEXTURE(LowResolutionDepth, UV01));
				UpdateSample(minDistance, nearestUV, Z01, UV01, zFull);

				float2 UV11 = UV00 + LowResolutionDepth_TexelSize.xy;
				float Z11 = Linear01Depth(SAMPLE_DEPTH_TEXTURE(LowResolutionDepth, UV11));
				UpdateSample(minDistance, nearestUV, Z11, UV11, zFull);

				float4 fogSample = float4(0, 0, 0, 0);

				[branch]
				if (abs(Z00 - zFull) < DepthThreshold &&
					abs(Z10 - zFull) < DepthThreshold &&
					abs(Z01 - zFull) < DepthThreshold &&
					abs(Z11 - zFull) < DepthThreshold)
				{
					fogSample = tex2Dlod(LightScatteringTextureLinear, float4(uv, 0, 0));
				}
				else
				{
					fogSample = tex2Dlod(LightScatteringTexturePoint, float4(nearestUV, 0, 0));
				}

				return fogSample;
			}

            fixed4 frag (v2f i) : SV_Target
            {
				fixed4 shaftsColor = GetNearestDepthSample(i.uv); 
                fixed4 sceneColor = tex2D(_MainTex, i.uv);  
				return half4((sceneColor.rgb * shaftsColor.a) + shaftsColor.rgb, 1.0);
            }
            ENDCG
        }
    }
}

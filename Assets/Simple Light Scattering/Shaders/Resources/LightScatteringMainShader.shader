Shader "Hidden/SimpleScattering/LightScatteringMainShader"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}

		[Header(Sunshafts basic settings)]

		InterleavedPosGridSize("InterleavedPosGridSize", Range(1, 16)) = 4

		[PowerSlider(3.0)]
		ScatteringCoefficient("ScatteringCoefficient", Range(0, 0.5)) = 0.1
		[PowerSlider(3.0)]
		ExtinctionCoeff("ExtinctionCoeff", Range(0, 0.5)) = 0.1
		SkyboxExtinctionCoeff("SkyboxExtinctionCoeff", Float) = 0.001
		
		FogDensity("GlobalFogDensity", Float) = 1.0

		MaxRayDistance("Max ray distance", Float) = 1000

		[IntRange]
		StepsPerRay("Steps Per Ray", Range(1, 100)) = 30

		[Header(Mie Scattering)]

		[Toggle(MIE_SCATTERING)]
		MieScatteringEnabled("Mie scattering", Float) = 0

		[Slider]
		MieScatteringBase("MieScatteringBase", Range(0, 1)) = 0.2

		[Slider]
		MieG("MieG", Range(0, 1)) = 0.2

		[Header(Height Fog)]

		[Toggle(HEIGHT_FOG)]
		HeightFogEnabled("HeightFogEnabled", Float) = 0

		HeightFogGroundLevel("HeightFogGroundLevel", Float) = 0
		HeightFogScale("HeightFogScale", Float) = 1

		[Header(Volumetric Fog)]

		[Toggle(VOLUMETRIC_FOG)]
		VolumetricFogEnabled("VolumetricFogEnabled", Float) = 0

		[Header(Global Animated Fog)]

		[Toggle(VOLUMETRIC_GLOBAL_FOG)]
		GlobalVolumetricFogEnabled("AnimatedGlobalFogEnabled", Float) = 0

		VolumetricFogDensityMultipler("VolumetricFogDensityMultipler", Float) = 2
		VolumetricFogNoiseScale("VolumetricFogNoiseScale", Float) = 100
		VolumetricFogMoveDir("VolumetricFogMoveDir", Vector) = (1,0,1,0)
		
		[Header(Dense Height Dependent Volumetric Fog)]

		[Toggle(VOLUMETRIC_DENSE_FOG)]
		VolumetricDenseFogEnabled("VolumetricDenseFogEnabled", Float) = 0

		VolumetricDenseFogNoiseTex("VolumetricDenseFogNoiseTex", 2D) = "white" {}
		VolumetricDenseFogMoveDir("VolumetricDenseFogMoveDir", Vector) = (1, 0, 0, 0)
		VolumetricDenseFogNoiseScale("VolumetricDenseFogNoiseScale", Float) = 1
		VolumetricDenseFogDensityMultipler("VolumetricDenseFogDensityMultipler", Float) = 10

		[Space(15)]
		VolumetricDenseHeightFogColor("VolumetricDenseHeightFogColor", Color) = (1,1,1,1)
		VolumetricDenseHeightShadowedFogColor("VolumetricDenseHeightShadowedFogColor", Color) = (1,1,1,1)
		VolumetricDenseHeightFogStart("VolumetricDenseHeightFogStart", Float) = 30
		VolumetricDenseHeightFogHeight("VolumetricDenseHeightFogHeight", Float) = 20
		VolumetricDenseHeightFogNoiseMaxHeight("VolumetricDenseHeightFogNoiseMaxHeight", Float) = 10

		[Header(Colors)]

		LightColor("LightColor", Color) = (1,1,1,1)
		ShadowedFogColor("ShadowedFogColor", Color) = (0,0,0,0)
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
				#include "Lighting.cginc"
				#include "UnityShadowLibrary.cginc"

				#pragma shader_feature HEIGHT_FOG
				#pragma shader_feature MIE_SCATTERING
				#pragma shader_feature VOLUMETRIC_FOG
				#pragma shader_feature VOLUMETRIC_GLOBAL_FOG
				#pragma shader_feature VOLUMETRIC_DENSE_FOG

				#define PI 3.1415926

				sampler2D _MainTex, VolumetricDenseFogNoiseTex;
				float4 _MainTex_TexelSize;

				UNITY_DECLARE_SHADOWMAP(ShadowMap);
				float4 ShadowMap_TexelSize;

				UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

				sampler2D LowResolutionDepth;
				float4 LowResolutionDepth_TexelSize;

				float4x4 InverseViewMatrix;

				int InterleavedPosGridSize;
				
				float ScatteringCoefficient;
				float ExtinctionCoefficient;

				float StepsPerRay;

				float SkyboxExtinctionCoefficient;

				float MieG;
				float MieScatteringBase;

				float3 LightColor, ShadowedFogColor;
				float FogDensity;

				float MaxRayDistance;

				float HeightFogGroundLevel;
				float HeightFogScale;

				float VolumetricFogDensityMultipler;
				float VolumetricFogNoiseScale;
				float3 VolumetricFogMoveDir;

				float VolumetricDenseFogNoiseScale;
				float VolumetricDenseFogDensityMultipler;
				float4 VolumetricDenseHeightFogColor;
				float4 VolumetricDenseHeightShadowedFogColor;
				float VolumetricDenseHeightFogStart;
				float VolumetricDenseHeightFogHeight;
				float VolumetricDenseHeightFogNoiseMaxHeight;
				float2 VolumetricDenseFogMoveDir;
				 
				struct appdata
				{
					float4 vertex : POSITION;
					float2 uv : TEXCOORD0;
				};

				struct v2f
				{
					float2 uv : TEXCOORD0;
					float4 vertex : SV_POSITION;
					float4 cameraRay : TEXCOORD1;
				};

				v2f vert(appdata v)
				{
					v2f o;
					o.vertex = UnityObjectToClipPos(v.vertex);
					o.uv = v.uv;

					//transform clip pos to view space
					float4 clipPos = float4(v.uv * 2.0 - 1.0, 1.0, 1.0);
					float4 cameraRay = mul(unity_CameraInvProjection, clipPos);
					o.cameraRay = cameraRay / cameraRay.w;

					return o;
				}
  
				float noise3dHash(float3 p)
				{
					p = frac(p * 0.3183099 + .1);
					p *= 15.0; //init scale for noise
					return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
				}

				float noise3d(in float3 x)
				{
					float3 i = floor(x);
					float3 f = frac(x);
					f = f * f * (3.0 - 2.0 * f);

					float n = lerp(lerp(lerp(noise3dHash(i + float3(0, 0, 0)),
						noise3dHash(i + float3(1, 0, 0)), f.x),
						lerp(noise3dHash(i + float3(0, 1, 0)),
							noise3dHash(i + float3(1, 1, 0)), f.x), f.y),
						lerp(lerp(noise3dHash(i + float3(0, 0, 1)),
							noise3dHash(i + float3(1, 0, 1)), f.x),
							lerp(noise3dHash(i + float3(0, 1, 1)),
								noise3dHash(i + float3(1, 1, 1)), f.x), f.y), f.z);

					return n;
				}

				fixed4 GetCascadeWeights(float3 wpos)
				{
					float3 fromCenter0 = wpos.xyz - unity_ShadowSplitSpheres[0].xyz;
					float3 fromCenter1 = wpos.xyz - unity_ShadowSplitSpheres[1].xyz;
					float3 fromCenter2 = wpos.xyz - unity_ShadowSplitSpheres[2].xyz;
					float3 fromCenter3 = wpos.xyz - unity_ShadowSplitSpheres[3].xyz;
					float4 dotdistances = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));
					fixed4 weights = float4(dotdistances < unity_ShadowSplitSqRadii);
					weights.yzw = saturate(weights.yzw - weights.xyz);
					return weights;
				}

				float4 GetShadowCoord(float4 wpos, fixed4 cascadeWeights)
				{
					float3 sc0 = mul(unity_WorldToShadow[0], wpos).xyz;
					float3 sc1 = mul(unity_WorldToShadow[1], wpos).xyz;
					float3 sc2 = mul(unity_WorldToShadow[2], wpos).xyz;
					float3 sc3 = mul(unity_WorldToShadow[3], wpos).xyz;

					float4 shadowMapCoord = float4(sc0 * cascadeWeights[0] + sc1 * cascadeWeights[1] + sc2 * cascadeWeights[2] + sc3 * cascadeWeights[3], 1);
					#if defined(UNITY_REVERSED_Z)
					float noCascadeWeights = 1 - dot(cascadeWeights, float4(1, 1, 1, 1));
					shadowMapCoord.z += noCascadeWeights;
					#endif

					return shadowMapCoord;
				}

				half IsShadow(float3 currentWPos, float4 cascadeWeights) {
					float4 shadowCoord = GetShadowCoord(float4(currentWPos, 1.0), cascadeWeights);
					half shadow = UNITY_SAMPLE_SHADOW(ShadowMap, shadowCoord);
					return shadow;
				}

				float GetLinearDepth(float2 uv) {
					float depth = SAMPLE_DEPTH_TEXTURE(LowResolutionDepth, uv);
					float lindepth = Linear01Depth(depth);
					return lindepth;
				}

				float MieScattering(float LRdot) {
					float MieG2 = MieG * MieG;
					return (1.0 / (4.0 * PI)) * ((1.0 - MieG2) / (pow((1.0 + MieG2) - (2.0 * MieG) * LRdot, 1.5)));
				}
				 
				void ApplyHeightFog(float3 wpos, inout float density) {
					density *= exp(-(wpos.y + HeightFogGroundLevel) * HeightFogScale);
				}

				void ApplyFogs(float3 currentPosition, float shadow, inout float density, inout float3 fogColor) {
					//apply different variations of fogs
					#ifdef VOLUMETRIC_FOG

					//Global fog based on generated on fly 3d noise values
					#ifdef VOLUMETRIC_GLOBAL_FOG
					float3 fogCoords = (float3(currentPosition.x, currentPosition.y, currentPosition.z) / VolumetricFogNoiseScale) + (VolumetricFogMoveDir * _Time.xxx);
					float fogValue = saturate(VolumetricFogDensityMultipler * noise3d(fogCoords));
					density *= fogValue;
					#endif

					//Dense fog at specific height
					#ifdef VOLUMETRIC_DENSE_FOG
					float2 noiseUV1 = currentPosition.xz / VolumetricDenseFogNoiseScale + (VolumetricDenseFogMoveDir * _Time.xx);
					float2 noiseUV2 = currentPosition.xz / (VolumetricDenseFogNoiseScale * 2) + (-1 * VolumetricDenseFogMoveDir * _Time.xx);

					float denseFogNoiseValue1 = saturate(tex2Dlod(VolumetricDenseFogNoiseTex, float4(noiseUV1, 0, 0)));
					float denseFogNoiseValue2 = saturate(tex2Dlod(VolumetricDenseFogNoiseTex, float4(noiseUV2, 0, 0)));

					float denseFogNoiseValue = denseFogNoiseValue1 * denseFogNoiseValue2;

					float denseHeightFogEnd = VolumetricDenseHeightFogStart + VolumetricDenseHeightFogHeight + (VolumetricDenseHeightFogNoiseMaxHeight * denseFogNoiseValue);

					//apply dense height dependent fog
					if (currentPosition.y < denseHeightFogEnd && currentPosition.y > VolumetricDenseHeightFogStart) {
						//adjust current density to apply fog
						density = (0.5 + density) * VolumetricDenseFogDensityMultipler;
						//color fog
						fogColor = lerp(VolumetricDenseHeightShadowedFogColor, VolumetricDenseHeightFogColor, shadow);
					}
					#endif

					#endif	

					//to activate in script use Shader.EnableKeyword(keyword);
					#ifdef HEIGHT_FOG
					ApplyHeightFog(currentPosition, density);
					#endif
				}

				fixed4 frag(v2f i) : SV_Target
				{
					//get linear depth and view, world position
					float linearDepth = GetLinearDepth(i.uv);
					float4 viewPos = float4(i.cameraRay.xyz * linearDepth, 1);
					float3 worldPos = mul(InverseViewMatrix, viewPos).xyz;

					//calculate weights for cascade shadow map split 
					float4 weights = GetCascadeWeights(worldPos);

					//get the ray direction in world space, raymarching is towards the camera
					float3 startPosition = _WorldSpaceCameraPos.xyz;
					float3 rayVector = worldPos - startPosition;
					float rayLength = length(rayVector);
					float3 rayDirection = normalize(rayVector);

					//clamp ray distance
					rayLength = min(rayLength, MaxRayDistance); 

					float stepLength = rayLength / StepsPerRay;
					float3 step = rayDirection * stepLength;
					float3 currentPosition = startPosition;

					//interleaved sampling pattern
					float2 interleavedPos = fmod(float2(i.vertex.x, LowResolutionDepth_TexelSize.w - i.vertex.y), InterleavedPosGridSize);
					float rayOffset = (interleavedPos.y * InterleavedPosGridSize + interleavedPos.x) * (stepLength * (1.0 / (InterleavedPosGridSize * InterleavedPosGridSize)));
					currentPosition += rayDirection.xyz * rayOffset;

					//accumulated ray parameters
					float4 lightsum = 0;
					float extinction = 0;
					float3 currentFogColor = LightColor;

					//ray march
					for (int i = 0; i < StepsPerRay; i++)
					{	
						float shadow = IsShadow(currentPosition, weights);
						
						currentFogColor = lerp(ShadowedFogColor, LightColor, shadow);

						float density = FogDensity;
						
						//apply different fog effects 
						ApplyFogs(currentPosition, shadow, density, currentFogColor);
						
						//calculate this pixel scattering 
						float scattering = ScatteringCoefficient * stepLength * density;
						extinction += ExtinctionCoefficient * stepLength * density;

						float4 light = float4(currentFogColor, 1.0) * scattering * exp(-extinction);

						//accumulate light 
						lightsum += light;

						//make step
						currentPosition += step;
					}

					//apply mie scatttering
					#ifdef MIE_SCATTERING 
					float LRdot = dot(_WorldSpaceLightPos0.xyz, rayDirection);
					lightsum *= MieScatteringBase + MieScattering(LRdot);
					#endif

					//avoid negative values
					lightsum = max(0, lightsum);

					//change light transmittance accoring to this pixel previously calculated extintion value
					lightsum.w = exp(-extinction);

					//hide/show unity normal skybox using separate parameter 
					if (linearDepth > 0.999999) //basically check if we are at the end of visible world coords/where skybox occurs
						lightsum.w = lerp(lightsum.w, 1, 1.0 - SkyboxExtinctionCoefficient);
					
					return lightsum;
				}
				ENDCG
			}
		}
}


Shader "TerrainShader/Main"
{
	Properties
	{
		[Header(Flat Terrain)]
		_ShoreLow("Shore Low", Color) = (0,0,0,1)
		_ShoreHigh("Shore High", Color) = (0,0,0,1)
		_FlatLowA("Flat Low A", Color) = (0,0,0,1)
		_FlatHighA("Flat High A", Color) = (0,0,0,1)

		_FlatColBlend("Colour Blend", Range(0,3)) = 1.5
		_FlatColBlendNoise("Blend Noise", Range(0,1)) = 0.3
		_ShoreHeight("Shore Height", Range(0,0.2)) = 0.05
		_ShoreBlend("Shore Blend", Range(0,0.2)) = 0.03
		_MaxFlatHeight("Max Flat Height", Range(0,1)) = 0.5

		[Header(Steep Terrain)]
		_SteepLow("Steep Colour Low", Color) = (0,0,0,1)
		_SteepHigh("Steep Colour High", Color) = (0,0,0,1)
		_SteepBands("Steep Bands", Range(1, 20)) = 8
		_SteepBandStrength("Band Strength", Range(-1,1)) = 0.5

		[Header(Flat to Steep Transition)]
		_SteepnessThreshold("Steep Threshold", Range(0,1)) = 0.5
		_FlatToSteepBlend("Flat to Steep Blend", Range(0,0.3)) = 0.1
		_FlatToSteepNoise("Flat to Steep Noise", Range(0,0.2)) = 0.1

		[Header(Noise)]
		[NoScaleOffset] _NoiseTex("Noise Texture", 2D) = "white" {}
		_NoiseScale("Noise Scale", Float) = 1
		_NoiseScale2("Noise Scale2", Float) = 1

		[Header(Other)]
		_FresnelCol("Fresnel Colour", Color) = (1,1,1,1)
		_FresnelStrengthNear("Fresnel Strength Min", float) = 2
		_FresnelStrengthFar("Fresnel Strength Max", float) = 5
		_FresnelPow("Fresnel Power", float) = 2
		_Glossiness("Smoothness", Range(0,1)) = 0.5
		_Metallic("Metallic", Range(0,1)) = 0.0
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 200
		CGPROGRAM

		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vert
		#pragma target 3.5
		//Include libraries for triplanar UV projections
		#include "Includes/Triplanar.cginc"
		#include "Includes/Math.cginc"
		
		//Fresnel Data
		float4 _FresnelCol;
		float _FresnelStrengthNear, _FresnelStrengthFar, _FresnelPow;
		float bodyScale;

		struct Input
		{
			float2 uv_MainTex;
			float3 worldPos;
			float4 terrainData;
			float3 vertPos;
			float3 normal;
			float4 tangent;
			float fresnel;
		};

		void vert(inout appdata_full v, out Input o)
		{
			UNITY_INITIALIZE_OUTPUT(Input, o);
			o.vertPos = v.vertex;
			o.normal = v.normal;
			o.terrainData = v.texcoord;
			o.tangent = v.tangent;

			// Fresnel (fade out when close to body)
			float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
			float3 bodyWorldCentre = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
			float camRadiiFromSurface = (length(bodyWorldCentre - _WorldSpaceCameraPos.xyz) - bodyScale) / bodyScale;
			float fresnelT = smoothstep(0,1,camRadiiFromSurface);
			float3 viewDir = normalize(worldPos - _WorldSpaceCameraPos.xyz);
			float3 normWorld = normalize(mul(unity_ObjectToWorld, float4(v.normal,0)));
			float fresStrength = lerp(_FresnelStrengthNear, _FresnelStrengthFar, fresnelT);
			o.fresnel = saturate(fresStrength * pow(1 + dot(viewDir, normWorld), _FresnelPow));
		}

		// Flat terrain
		float4 _ShoreLow, _ShoreHigh;
		float4 _FlatLowA, _FlatHighA;
		float _FlatColBlend, _FlatColBlendNoise;
		float _ShoreHeight, _ShoreBlend;
		float _MaxFlatHeight;

		// Steep terrain
		float4 _SteepLow, _SteepHigh;
		float _SteepBands, _SteepBandStrength;

		// Flat to steep transition
		float _SteepnessThreshold, _FlatToSteepBlend, _FlatToSteepNoise;

		// Other
		float _Glossiness, _Metallic;
		sampler2D _NoiseTex;
		float _NoiseScale, _NoiseScale2;

		// Height data
		float2 heightMinMax;
		float oceanLevel;

		fixed4 LightingNoLighting(SurfaceOutput s, fixed3 lightDir, fixed atten)
		{
			fixed4 c;
			c.rgb = s.Albedo * 0.8;
			c.a = s.Alpha;
			return c;
		}

		void surf(Input IN, inout SurfaceOutputStandard o)
		{

			// Calculate steepness: 0 where totally flat, 1 at max steepness
			float3 sphereNormal = normalize(IN.vertPos);
			float steepness = 1 - dot(sphereNormal, IN.normal);
			steepness = remap01(steepness, 0, 0.65);

			// Calculate heights
			float terrainHeight = length(IN.vertPos);
			float shoreHeight = lerp(heightMinMax.x, 1, oceanLevel);
			float aboveShoreHeight01 = remap01(terrainHeight, shoreHeight, heightMinMax.y);
			float flatHeight01 = remap01(aboveShoreHeight01, 0, _MaxFlatHeight);

			// Sample noise texture at two different scales
			float4 texNoise = triplanar(IN.vertPos, IN.normal, _NoiseScale, _NoiseTex);
			float4 texNoise2 = triplanar(IN.vertPos, IN.normal, _NoiseScale2, _NoiseTex);

			// Flat terrain colour
			float flatColBlendWeight = Blend(0, _FlatColBlend, (flatHeight01 - .5) + (texNoise.b - 0.5) * _FlatColBlendNoise);
			float3 flatTerrainCol = lerp(_FlatLowA, _FlatHighA, flatColBlendWeight);
			flatTerrainCol = lerp(flatTerrainCol, (_FlatLowA + _FlatHighA) / 2, texNoise.a);

			// Shore
			float shoreBlendWeight = 1 - Blend(_ShoreHeight, _ShoreBlend, flatHeight01);
			float4 shoreCol = lerp(_ShoreLow, _ShoreHigh, remap01(aboveShoreHeight01, 0, _ShoreHeight));
			shoreCol = lerp(shoreCol, (_ShoreLow + _ShoreHigh) / 2, texNoise.g);
			flatTerrainCol = lerp(flatTerrainCol, shoreCol, shoreBlendWeight);

			// Steep terrain colour
			float3 sphereTangent = normalize(float3(-sphereNormal.z, 0, sphereNormal.x));
			float3 normalTangent = normalize(IN.normal - sphereNormal * dot(IN.normal, sphereNormal));
			float banding = dot(sphereTangent, normalTangent) * .5 + .5;
			banding = (int)(banding * (_SteepBands + 1)) / _SteepBands;
			banding = (abs(banding - 0.5) * 2 - 0.5) * _SteepBandStrength;
			float3 steepTerrainCol = lerp(_SteepLow, _SteepHigh, aboveShoreHeight01 + banding);

			// Flat to steep colour transition
			float flatBlendNoise = (texNoise2.r - 0.5) * _FlatToSteepNoise;
			float flatStrength = 1 - Blend(_SteepnessThreshold + flatBlendNoise, _FlatToSteepBlend, steepness);
			float flatHeightFalloff = 1 - Blend(_MaxFlatHeight + flatBlendNoise, _FlatToSteepBlend, aboveShoreHeight01);
			flatStrength *= flatHeightFalloff;

			// Set surface colour
			float3 compositeCol = lerp(steepTerrainCol, flatTerrainCol, flatStrength);
			compositeCol = lerp(compositeCol, _FresnelCol, IN.fresnel);
			o.Albedo = compositeCol;

			// Glossiness
			float glossiness = dot(o.Albedo, 1) / 3 * _Glossiness;
			o.Smoothness = glossiness;
			o.Metallic = _Metallic;
		}
		ENDCG
	}
	FallBack "Diffuse"
}

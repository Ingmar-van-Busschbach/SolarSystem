using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ShaderSettings
{
	public TerrainColours terrainColors;
	public FresnelSettings fresnelSettings;
	public int seed;
	public ComputeShader shadingDataCompute;
	protected Vector4[] cachedShadingData;
	private ComputeBuffer shadingBuffer;
	public Material terrainMaterial = null;
	[Range(0, 1)]
	public float oceanLevel;
	[Header("Shading Data")]
	public SimpleNoiseSettings detailWarpNoise;
	public SimpleNoiseSettings detailNoise;
	public SimpleNoiseSettings largeNoise;
	public SimpleNoiseSettings smallNoise;
	public Vector4[] GenerateShadingData(ComputeBuffer vertexBuffer)
	{
		int numVertices = vertexBuffer.count;
		Vector4[] shadingData = new Vector4[numVertices];

		if (shadingDataCompute)
		{
			// Set data
			SetShadingDataComputeProperties();

			shadingDataCompute.SetInt("numVertices", numVertices);
			shadingDataCompute.SetBuffer(0, "vertices", vertexBuffer);
			ComputeHelper.CreateAndSetBuffer<Vector4>(ref shadingBuffer, numVertices, shadingDataCompute, "shadingData");

			// Run
			ComputeHelper.Run(shadingDataCompute, numVertices);

			// Get data
			shadingBuffer.GetData(shadingData);
		}

		cachedShadingData = shadingData;
		return shadingData;
	}
	public void SetShadingDataComputeProperties()
	{
		PRNG random = new PRNG(seed);
		detailNoise.SetComputeValues(shadingDataCompute, random, "_detail");
		detailWarpNoise.SetComputeValues(shadingDataCompute, random, "_detailWarp");
		largeNoise.SetComputeValues(shadingDataCompute, random, "_large");
		smallNoise.SetComputeValues(shadingDataCompute, random, "_small");
	}
	public void SetTerrainProperties(Material material, Vector2 heightMinMax, float bodyScale)
	{

		material.SetVector("heightMinMax", heightMinMax);
		material.SetFloat("oceanLevel", oceanLevel);
		material.SetFloat("bodyScale", bodyScale);
		ApplyColours(material, terrainColors, fresnelSettings);
	}
	void ApplyColours(Material material, TerrainColours colours, FresnelSettings fresnel)
	{
		material.SetColor("_ShoreLow", colours.shoreColLow);
		material.SetColor("_ShoreHigh", colours.shoreColHigh);

		material.SetColor("_FlatLowA", colours.flatColLow);
		material.SetColor("_FlatHighA", colours.flatColHigh);

		material.SetColor("_SteepLow", colours.steepLow);
		material.SetColor("_SteepHigh", colours.steepHigh);

		material.SetColor("_FresnelCol", fresnel.fresnelCol);
		material.SetFloat("_FresnelStrengthNear", fresnel.strengthNear);
		material.SetFloat("_FresnelStrengthFar", fresnel.strengthFar);
		material.SetFloat("_FresnelPow", fresnel.power);
	}
	public void ReleaseBuffers()
	{
		ComputeHelper.Release(shadingBuffer);
	}
	[System.Serializable]
	public struct TerrainColours
	{
		[Tooltip("Color of the shores")]
		public Color shoreColLow;
		[Tooltip("Color of the shores")]
		public Color shoreColHigh;
		[Tooltip("Color of the lowlands")]
		public Color flatColLow;
		[Tooltip("Color of the lowlands")]
		public Color flatColHigh;
		[Tooltip("Color of the mountains")]
		public Color steepLow;
		[Tooltip("Color of the mountains")]
		public Color steepHigh;
	}
	[System.Serializable]
	public struct FresnelSettings
	{
		[Tooltip("Color of the fog effect")]
		public Color fresnelCol;
		public float strengthNear;
		public float strengthFar;
		public float power;
	}
}

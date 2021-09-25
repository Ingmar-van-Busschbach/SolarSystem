using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CelestialBodyGenerator : MonoBehaviour
{
    [Header("Core settings")]
    public ComputeShader heightMapCompute;
    [Tooltip("Simplex noise seed")]
    public int seed;
    [Tooltip("The vertex count per axis of a face. There are 6 faces and 2 axis per face. As such, the total vertex count after triangulation is [12*resolution^2]")]
    [SerializeField] private int resolution = 10;
    [Tooltip("Whether to log generation time")]
    public bool logTimers;
    
    

    [Header("Continent settings")]
    public float oceanDepthMultiplier = 5;
    public float oceanFloorDepth = 1.5f;
    public float oceanFloorSmoothing = 0.5f;
    public float mountainBlend = 1.2f; // Determines how smoothly the base of mountains blends into the terrain

    [Header("Noise settings")]
    public SimpleNoiseSettings continentNoise;
    public SimpleNoiseSettings maskNoise;
    public RidgeNoiseSettings ridgeNoise;

    [Header("Shader settings")]
    public ShaderSettings shaderSettings;

    private MeshFilter terrainMeshFilter;
    private MeshRenderer terrainRenderer;
    private Material terrainMatInstance;
    private Mesh mesh;
    private ComputeBuffer vertexBuffer;
    private Vector2 heightMinMax;
    private ComputeBuffer heightBuffer;
    static Dictionary<int, SphereMesh> sphereGenerators;

    void Start()
    {
        terrainMeshFilter = this.GetComponent<MeshFilter>();
        terrainRenderer = this.GetComponent<MeshRenderer>();
        var terrainMeshTimer = System.Diagnostics.Stopwatch.StartNew();
        heightMinMax = GenerateTerrainMesh(ref mesh, resolution);

        LogTimer(terrainMeshTimer, "Generate terrain mesh");
        terrainMatInstance = new Material(shaderSettings.terrainMaterial);
        shaderSettings.SetTerrainProperties(terrainMatInstance, heightMinMax, BodyScale);
        terrainMeshFilter.mesh = mesh;
        terrainRenderer.sharedMaterial = terrainMatInstance;
        ComputeHelper.Release(vertexBuffer);
        ComputeHelper.Release(heightBuffer);
        shaderSettings.ReleaseBuffers();
    }
    protected virtual void SetShapeData()
    {
        var prng = new PRNG(seed);
        continentNoise.SetComputeValues(heightMapCompute, prng, "_continents");
        ridgeNoise.SetComputeValues(heightMapCompute, prng, "_mountains");
        maskNoise.SetComputeValues(heightMapCompute, prng, "_mask");

        heightMapCompute.SetFloat("oceanDepthMultiplier", oceanDepthMultiplier);
        heightMapCompute.SetFloat("oceanFloorDepth", oceanFloorDepth);
        heightMapCompute.SetFloat("oceanFloorSmoothing", oceanFloorSmoothing);
        heightMapCompute.SetFloat("mountainBlend", mountainBlend);
    }

    (Vector3[] vertices, int[] triangles) CreateSphereVertsAndTris(int resolution)
    {
        if (sphereGenerators == null)
        {
            sphereGenerators = new Dictionary<int, SphereMesh>();
        }

        if (!sphereGenerators.ContainsKey(resolution))
        {
            sphereGenerators.Add(resolution, new SphereMesh(resolution));
        }

        var generator = sphereGenerators[resolution];

        var vertices = new Vector3[generator.Vertices.Length];
        var triangles = new int[generator.Triangles.Length];
        System.Array.Copy(generator.Vertices, vertices, vertices.Length);
        System.Array.Copy(generator.Triangles, triangles, triangles.Length);
        return (vertices, triangles);
    }

    public virtual float[] CalculateHeights(ComputeBuffer vertexBuffer)
    {
        SetShapeData();

        heightMapCompute.SetInt("numVertices", vertexBuffer.count);
        heightMapCompute.SetBuffer(0, "vertices", vertexBuffer);
        ComputeHelper.CreateAndSetBuffer<float>(ref heightBuffer, vertexBuffer.count, heightMapCompute, "heights");

        // Run
        ComputeHelper.Run(heightMapCompute, vertexBuffer.count);

        // Get heights
        var heights = new float[vertexBuffer.count];
        heightBuffer.GetData(heights);
        return heights;
    }

    void CreateMesh(ref Mesh mesh, int numVertices)
    {
        const int vertexLimit16Bit = 1 << 16 - 1; // 65535
        if (mesh == null)
        {
            mesh = new Mesh();
        }
        else
        {
            mesh.Clear();
        }
        mesh.indexFormat = (numVertices < vertexLimit16Bit) ? UnityEngine.Rendering.IndexFormat.UInt16 : UnityEngine.Rendering.IndexFormat.UInt32;
    }

    void LogTimer(System.Diagnostics.Stopwatch sw, string text)
    {
        if (logTimers)
        {
            Debug.Log(text + " " + sw.ElapsedMilliseconds + " ms.");
        }
    }

    Vector2 GenerateTerrainMesh(ref Mesh mesh, int resolution)
    {
        var (vertices, triangles) = CreateSphereVertsAndTris(resolution);
        ComputeHelper.CreateStructuredBuffer<Vector3>(ref vertexBuffer, vertices);

        float edgeLength = (vertices[triangles[0]] - vertices[triangles[1]]).magnitude;

        // Set heights
        float[] heights = CalculateHeights(vertexBuffer);
        // Calculate terrain min/max height and set heights of vertices
        float minHeight = float.PositiveInfinity;
        float maxHeight = float.NegativeInfinity;
        for (int i = 0; i < heights.Length; i++)
        {
            float height = heights[i];
            vertices[i] *= height;
            minHeight = Mathf.Min(minHeight, height);
            maxHeight = Mathf.Max(maxHeight, height);
        }

        // Create mesh
        CreateMesh(ref mesh, vertices.Length);
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals(); //

        // Shading noise data
        Vector4[] shadingData = shaderSettings.GenerateShadingData(vertexBuffer);
        mesh.SetUVs(0, shadingData);
        // Create crude tangents (vectors perpendicular to surface normal)
        // This is needed (even though normal mapping is being done with triplanar)
        // because surfaceshader wants normals in tangent space
        var normals = mesh.normals;
        var crudeTangents = new Vector4[mesh.vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 normal = normals[i];
            crudeTangents[i] = new Vector4(-normal.z, 0, normal.x, 1);
        }
        mesh.SetTangents(crudeTangents);
        return new Vector2(minHeight, maxHeight);
    }
    public float BodyScale
    {
        get
        {
            // Body radius is determined by the celestial body class,
            // which sets the local scale of the generator object (this object)
            return transform.localScale.x;
        }
    }
}

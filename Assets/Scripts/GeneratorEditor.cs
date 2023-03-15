using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Generator))]
public class GeneratorEditor : Editor
{
    Generator generator;
    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Generate new planet..."))
        { //Generate new planet
            generator.Generate();
        }
        using (var check = new EditorGUI.ChangeCheckScope())
        { //Draw the default settings available within the inspector
            DrawDefaultInspector();
        }
        if (GUILayout.Button(new GUIContent("Save mesh...", "Save mesh as reference. If you generate a new body afterwards, it will update every instance of this mesh automatically")))
        { //Save generated planet as object file
            SaveMesh(false);
        }
        if (GUILayout.Button(new GUIContent("Save mesh as new instance...", "Save mesh as new instance, meaning instances of the mesh will not be updated if the original changes")))
        { //Save generated planet as instanced object file
            SaveMesh(true);
        }
        if (GUILayout.Button(new GUIContent("Save material...", "Save material as reference. If you generate a new shading pattern afterwards, it will update every instance of this material automatically")))
        { //Save generated shader as material file
            SaveMaterial();
        }
    }
    public void SaveMesh(bool instance)
    { //Get mesh filter of the generator as that contains the mesh that has to be saved
        MeshFilter mf = generator.MeshFilter;
        if (mf != null)
        { //Only save if there is a generated planet to save
            Mesh m = mf.sharedMesh;
            WriteMeshFile(m, m.name, instance, true);
        }
        else { Debug.Log("No generated planet available to save!");}
    }
    public static void WriteMeshFile(Mesh mesh, string name, bool makeNewInstance, bool optimizeMesh)
    { //Open up a file explorer window to select the file path to save to, as well as the file name
        string folder = (makeNewInstance) ? "Assets/GeneratedData/" : "Assets/GeneratedData/ReferenceMeshes/";
        string path = EditorUtility.SaveFilePanel("Save new mesh asset", folder, name, "asset");
        if (string.IsNullOrEmpty(path)) return;
        path = FileUtil.GetProjectRelativePath(path);

        //Check for file overwriting
        var asset = AssetDatabase.LoadMainAssetAtPath(path);
        if (asset != null && makeNewInstance) { AssetDatabase.DeleteAsset(path); }

        Mesh meshToSave = (makeNewInstance) ? Object.Instantiate(mesh) as Mesh : mesh;

        if (optimizeMesh)
            MeshUtility.Optimize(meshToSave);
        //Save the mesh
        AssetDatabase.CreateAsset(meshToSave, path);
        AssetDatabase.SaveAssets();
    }
    public void SaveMaterial()
    { //Get mesh filter of the generator as that contains the mesh that has to be saved
        MeshRenderer mr = generator.MeshRenderer;
        if (mr != null)
        { //Only save if there is a generated planet to save
            Material m = mr.sharedMaterial;
            Vector2 minMax = generator.MinMax;
            float oceanLevel = generator.OceanLevel;
            m.SetFloat("_HeightMin", minMax.x);
            m.SetFloat("_HeightMax", minMax.y);
            m.SetFloat("oceanLevel", oceanLevel);
            WriteMaterialFile(m, m.name);
        }
        else { Debug.Log("No generated planet available to save!"); }
    }
    public static void WriteMaterialFile(Material material, string name)
    { //Open up a file explorer window to select the file path to save to, as well as the file name
        string path = EditorUtility.SaveFilePanel("Save new mesh asset", "Assets/GeneratedData/", name, "asset");
        if (string.IsNullOrEmpty(path)) return;
        path = FileUtil.GetProjectRelativePath(path);

        //Check for file overwriting, rename the file to prevent file overwriting
        var asset = AssetDatabase.LoadMainAssetAtPath(path);
        if (asset != null) { string[] pathName = path.Split(char.Parse(".")); pathName[0] += "_1"; path = pathName[0] + ".asset"; }

        AssetDatabase.CreateAsset(material, path);
        AssetDatabase.SaveAssets();
    }
    private void OnEnable()
    {
        generator = (Generator)target;
    }
}

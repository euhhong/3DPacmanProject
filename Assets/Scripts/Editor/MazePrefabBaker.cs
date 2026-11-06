using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 전용: 씬의 미로(MazeBuilder)를 프리팹 에셋으로 저장한다.
// - 메뉴 Tools > Pacman > Save Maze Prefab 으로 실행 (미로를 바꾼 뒤 다시 실행하면 덮어씀)
// - 프리팹이 아직 없으면 컴파일 후 자동으로 한 번 실행
// 생성된 머티리얼은 Assets/Prefabs/Materials 에 .mat 에셋으로 함께 저장한다.
[InitializeOnLoad]
public static class MazePrefabBaker
{
    private const string PrefabFolder = "Assets/Prefabs";
    private const string MaterialFolder = "Assets/Prefabs/Materials";
    private const string PrefabPath = "Assets/Prefabs/Maze.prefab";

    static MazePrefabBaker()
    {
        EditorApplication.update += WaitForEditMode;
    }

    // Play 모드가 아닐 때(편집 상태) 첫 프레임에 한 번 검사한다. Play 중이면 끝날 때까지 기다린다.
    private static void WaitForEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= WaitForEditMode;
        AutoBake();
    }

    private static void AutoBake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        MazeBuilder maze = Object.FindAnyObjectByType<MazeBuilder>();
        if (maze == null) return;

        // 프리팹이 이미 있고 배치도/설정도 최신이면 건너뛴다. (배치도를 바꾸면 자동으로 다시 저장)
        bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        if (prefabExists && maze.IsUpToDate) return;

        SaveMazePrefab();
    }

    [MenuItem("Tools/Pacman/Save Maze Prefab")]
    public static void SaveMazePrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("MazePrefabBaker: Play 모드를 끈 뒤 실행하세요.");
            return;
        }

        MazeBuilder maze = Object.FindAnyObjectByType<MazeBuilder>();
        if (maze == null)
        {
            Debug.LogWarning("MazePrefabBaker: 씬에 MazeBuilder가 없습니다.");
            return;
        }

        GameObject root = maze.gameObject;

        // 이미 프리팹 인스턴스면 풀어서 최신 설정으로 다시 만든 뒤 덮어쓴다.
        if (PrefabUtility.IsPartOfPrefabInstance(root))
        {
            GameObject outer = PrefabUtility.GetOutermostPrefabInstanceRoot(root);
            PrefabUtility.UnpackPrefabInstance(outer, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        maze.BuildMaze();

        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);
        SaveMaterialsAsAssets(root);

        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
        AssetDatabase.SaveAssets();

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);

        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"MazePrefabBaker: 프리팹 저장 완료 → {PrefabPath}");
    }

    // 실행 중에 만든 머티리얼은 에셋이 아니라서 프리팹에 저장되지 않으므로 .mat 파일로 만든다.
    private static void SaveMaterialsAsAssets(GameObject root)
    {
        var savedAssets = new Dictionary<Material, Material>();

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material material = renderer.sharedMaterial;
            if (material == null) continue;

            if (!savedAssets.TryGetValue(material, out Material asset))
            {
                asset = SaveMaterialAsset(material);
                savedAssets[material] = asset;
            }

            renderer.sharedMaterial = asset;
        }
    }

    private static Material SaveMaterialAsset(Material source)
    {
        if (AssetDatabase.Contains(source)) return source;

        string path = $"{MaterialFolder}/{source.name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            // 같은 이름의 에셋이 있으면 값만 갱신해 GUID(참조)를 유지한다.
            existing.shader = source.shader;
            existing.CopyPropertiesFromMaterial(source);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        AssetDatabase.CreateAsset(source, path);
        return source;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

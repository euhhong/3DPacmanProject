using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 전용: 플레이어(PlayerMove가 붙은 Cube)의 겉모습을 FBX 모델로 교체한다.
// 1. FBX에서 꺼낸 텍스처(Assets/Models/Textures)로 URP 머티리얼을 만들고 모델에 연결
// 2. Cube의 MeshRenderer만 끄고, 자식 "PlayerModel" 아래에 모델을 넣어 크기/위치를 맞춤
// 3. 텍스처에서 빨간색(입 안쪽)인 부분이 있는 방향을 찾아, 입이 정면(+Z)을 보도록 회전
// - 아직 적용되지 않았다면 편집 상태가 되었을 때 자동으로 한 번 실행
// - 메뉴 Tools > Pacman > Apply Player Model 로 다시 실행 가능
[InitializeOnLoad]
public static class PlayerModelSetup
{
    private const string ModelPath = "Assets/Models/3D Pac-Man Icon.fbx";
    private const string BaseColorPath = "Assets/Models/Textures/PacmanIcon_BaseColor.png";
    private const string NormalPath = "Assets/Models/Textures/PacmanIcon_Normal.png";
    private const string MaterialFolder = "Assets/Models/Materials";
    private const string MaterialPath = "Assets/Models/Materials/PacmanIcon.mat";
    private const string SourceMaterialName = "material_0"; // FBX 안의 머티리얼 이름

    private const string WrapperName = "PlayerModel";
    private const float TargetSize = 1.3f; // 모델의 가장 긴 변 길이 (한 칸 = 2)

    static PlayerModelSetup()
    {
        EditorApplication.update += WaitForEditMode;
    }

    // Play 모드가 아닐 때(편집 상태) 첫 프레임에 한 번 검사한다. Play 중이면 끝날 때까지 기다린다.
    private static void WaitForEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= WaitForEditMode;
        AutoApply();
    }

    private static void AutoApply()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null) return;
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath) == null) return;

        PlayerMove player = Object.FindAnyObjectByType<PlayerMove>();
        if (player == null) return;

        bool modelPlaced = player.transform.Find(WrapperName) != null;
        bool materialLinked = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null;
        if (modelPlaced && materialLinked) return;

        ApplyPlayerModel();
    }

    [MenuItem("Tools/Pacman/Apply Player Model")]
    public static void ApplyPlayerModel()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("PlayerModelSetup: Play 모드를 끈 뒤 실행하세요.");
            return;
        }

        PlayerMove player = Object.FindAnyObjectByType<PlayerMove>();
        if (player == null || AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
        {
            Debug.LogWarning($"PlayerModelSetup: 모델({ModelPath}) 또는 PlayerMove 오브젝트를 찾을 수 없습니다.");
            return;
        }

        Material material = CreateOrUpdateMaterial();
        LinkMaterialToModel(material);
        PlaceModel(player.transform);
    }

    // ---------- 텍스처 / 머티리얼 ----------

    private static Material CreateOrUpdateMaterial()
    {
        // 기본 색 텍스처: 입 방향 계산을 위해 픽셀을 읽을 수 있게 한다.
        var baseImporter = (TextureImporter)AssetImporter.GetAtPath(BaseColorPath);
        if (!baseImporter.isReadable || !baseImporter.sRGBTexture)
        {
            baseImporter.isReadable = true;
            baseImporter.sRGBTexture = true;
            baseImporter.SaveAndReimport();
        }

        var normalImporter = (TextureImporter)AssetImporter.GetAtPath(NormalPath);
        if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
        }

        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Models", "Materials");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath));
        material.SetColor("_BaseColor", Color.white);

        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }

        material.SetFloat("_Smoothness", 0.45f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    // FBX 안의 머티리얼 대신 새 머티리얼을 쓰도록 연결한다. (모든 사용처에 적용)
    private static void LinkMaterialToModel(Material material)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), SourceMaterialName), material);
        importer.isReadable = true; // 입 방향 계산에 메시 정점이 필요
        importer.SaveAndReimport();
    }

    // ---------- 배치 ----------

    private static void PlaceModel(Transform playerRoot)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

        // 기존 모델이 있으면 지우고 새로 넣는다.
        Transform old = playerRoot.Find(WrapperName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // 회전 중심이 될 빈 오브젝트
        var wrapper = new GameObject(WrapperName);
        wrapper.transform.SetParent(playerRoot, false);
        wrapper.AddComponent<PlayerFacing>();
        Vector3 pivot = wrapper.transform.position;

        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, wrapper.transform);
        model.transform.localPosition = Vector3.zero;

        // 가장 긴 변이 TargetSize가 되도록 크기 조정 후 중심 맞추기
        Bounds bounds = GetRendererBounds(model);
        float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (longest > 0f) model.transform.localScale *= TargetSize / longest;
        model.transform.position -= GetRendererBounds(model).center - pivot;

        // 입이 정면(+Z)을 보도록 Y축으로 회전
        if (TryFindMouthDirection(model, pivot, out Vector3 mouth))
        {
            float angle = Vector3.SignedAngle(mouth, Vector3.forward, Vector3.up);
            model.transform.RotateAround(pivot, Vector3.up, angle);
            model.transform.position -= GetRendererBounds(model).center - pivot;
            Debug.Log($"PlayerModelSetup: 입 방향을 찾아 {angle:0}도 회전했습니다.");
        }
        else
        {
            Debug.LogWarning("PlayerModelSetup: 입 방향을 찾지 못했습니다. PlayerModel의 PlayerFacing > Yaw Offset 으로 직접 맞춰 주세요.");
        }

        // 바닥(Cube 밑면)에 닿도록 wrapper를 올린다.
        bounds = GetRendererBounds(model);
        float halfCube = playerRoot.TryGetComponent(out Collider col) ? col.bounds.extents.y : 0.5f;
        wrapper.transform.localPosition = new Vector3(0f, bounds.extents.y - halfCube, 0f);

        // Cube의 겉모습만 숨긴다. (콜라이더/PlayerMove는 유지)
        if (playerRoot.TryGetComponent(out MeshRenderer cubeRenderer)) cubeRenderer.enabled = false;

        EditorSceneManager.MarkSceneDirty(playerRoot.gameObject.scene);
        EditorSceneManager.SaveScene(playerRoot.gameObject.scene);
        Debug.Log("PlayerModelSetup: 플레이어 모델/텍스처 적용 완료, 씬 저장됨.");
    }

    // 텍스처가 빨간색(입 안쪽)인 정점들의 평균 위치가 중심에서 어느 쪽에 있는지로 입 방향을 구한다.
    private static bool TryFindMouthDirection(GameObject model, Vector3 center, out Vector3 direction)
    {
        direction = Vector3.zero;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
        if (texture == null || !texture.isReadable) return false;

        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable) continue;

            Vector3[] vertices = mesh.vertices;
            Vector2[] uvs = mesh.uv;
            if (uvs.Length != vertices.Length) continue;

            int step = Mathf.Max(1, vertices.Length / 50000); // 정점이 많으면 일부만 검사
            for (int i = 0; i < vertices.Length; i += step)
            {
                Color c = texture.GetPixelBilinear(uvs[i].x, uvs[i].y);
                bool isRed = c.r > 0.7f && c.g < 0.45f && c.b < 0.45f;
                if (!isRed) continue;

                sum += filter.transform.TransformPoint(vertices[i]);
                count++;
            }
        }

        if (count < 10) return false;

        direction = sum / count - center;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return false;

        direction.Normalize();
        return true;
    }

    private static Bounds GetRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }
}

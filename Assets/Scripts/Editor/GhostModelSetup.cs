using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 전용: 방해꾼(EnemyMove가 붙은 큐브)의 겉모습을 고스트 FBX 모델로 교체한다.
// 1. FBX에서 꺼낸 빨간 기본 텍스처의 빨간 부분만 색조를 바꿔 주황/민트/분홍 텍스처를 만든다. (눈의 흰색·파란색은 유지)
// 2. 색마다 URP 머티리얼을 만들고, 방해꾼마다 지정된 색을 입힌다.
// 3. 큐브의 MeshRenderer만 끄고 자식 "GhostModel" 아래에 모델을 넣어 크기/위치를 맞춘다.
// 4. 텍스처에서 파란색(눈동자)인 부분이 있는 방향을 찾아, 눈이 정면(+Z)을 보도록 회전한다.
// - 아직 교체되지 않은 방해꾼이 있으면 편집 상태가 되었을 때 자동으로 한 번 실행
// - 메뉴 Tools > Pacman > Apply Ghost Models 로 다시 실행 가능
[InitializeOnLoad]
public static class GhostModelSetup
{
    private const string ModelPath = "Assets/Models/Pac-Man Blinky Plush.fbx";
    private const string TextureFolder = "Assets/Models/Textures";
    private const string BaseColorPath = TextureFolder + "/Ghost_BaseColor.png";
    private const string NormalPath = TextureFolder + "/Ghost_Normal.png";
    private const string MaterialFolder = "Assets/Models/Materials";

    private const string WrapperName = "GhostModel";
    private const float TargetSize = 1.4f; // 모델의 가장 긴 변 길이 (한 칸 = 2)

    private struct GhostColor
    {
        public string Name;
        public bool KeepOriginal;     // 원본 빨간색 그대로
        public float Hue;             // 0~1 색조
        public float SaturationScale; // 채도 배율
        public float ValueScale;      // 밝기 배율
    }

    private static readonly GhostColor Red = new GhostColor { Name = "Red", KeepOriginal = true };
    private static readonly GhostColor Orange = new GhostColor { Name = "Orange", Hue = 0.075f, SaturationScale = 1f, ValueScale = 1.05f };
    private static readonly GhostColor Mint = new GhostColor { Name = "Mint", Hue = 0.45f, SaturationScale = 0.6f, ValueScale = 1.15f };
    private static readonly GhostColor Pink = new GhostColor { Name = "Pink", Hue = 0.92f, SaturationScale = 0.55f, ValueScale = 1.2f };

    static GhostModelSetup()
    {
        EditorApplication.update += WaitForEditMode;
    }

    private static void WaitForEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= WaitForEditMode;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null) return;
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath) == null) return;

        // 여백을 메운 텍스처가 아직 없으면 (이전 버전으로 만든 경우 포함) 다시 적용
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(GeneratedTexturePath(Red)) == null)
        {
            ApplyGhostModels();
            return;
        }

        foreach (EnemyMove enemy in Object.FindObjectsByType<EnemyMove>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (enemy.transform.Find(WrapperName) == null)
            {
                ApplyGhostModels();
                return;
            }
        }
    }

    [MenuItem("Tools/Pacman/Apply Ghost Models")]
    public static void ApplyGhostModels()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("GhostModelSetup: Play 모드를 끈 뒤 실행하세요.");
            return;
        }

        EnemyMove[] enemies = Object.FindObjectsByType<EnemyMove>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (enemies.Length == 0 || AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
        {
            Debug.LogWarning($"GhostModelSetup: 모델({ModelPath}) 또는 방해꾼(EnemyMove)을 찾을 수 없습니다.");
            return;
        }

        PrepareImporters();

        foreach (EnemyMove enemy in enemies)
        {
            GhostColor color = ColorFor(enemy);
            PlaceModel(enemy.transform, GetOrCreateMaterial(color));
        }

        var scene = enemies[0].gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"GhostModelSetup: 방해꾼 {enemies.Length}마리 고스트 모델/색상 적용 완료, 씬 저장됨.");
    }

    // 방해꾼 성격별 색: Chaser 빨강, Ambusher 분홍, Wanderer 민트, Shy 주황
    private static GhostColor ColorFor(EnemyMove enemy)
    {
        string enemyName = enemy.name;
        if (enemyName.Contains("Ambusher")) return Pink;
        if (enemyName.Contains("Wanderer")) return Mint;
        if (enemyName.Contains("Shy")) return Orange;
        return Red;
    }

    // ---------- 임포트 설정 ----------

    private static void PrepareImporters()
    {
        var baseImporter = (TextureImporter)AssetImporter.GetAtPath(BaseColorPath);
        if (!baseImporter.isReadable)
        {
            baseImporter.isReadable = true; // 색 바꾸기와 눈 방향 계산에 픽셀이 필요
            baseImporter.SaveAndReimport();
        }

        var normalImporter = (TextureImporter)AssetImporter.GetAtPath(NormalPath);
        if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap)
        {
            normalImporter.textureType = TextureImporterType.NormalMap;
            normalImporter.SaveAndReimport();
        }

        var modelImporter = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        if (!modelImporter.isReadable)
        {
            modelImporter.isReadable = true; // 눈 방향 계산에 메시 정점이 필요
            modelImporter.SaveAndReimport();
        }
    }

    // ---------- 텍스처 / 머티리얼 ----------

    private static Material GetOrCreateMaterial(GhostColor color)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Models", "Materials");

        string materialPath = $"{MaterialFolder}/Ghost_{color.Name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.SetTexture("_BaseMap", GetOrCreateTexture(color));
        material.SetColor("_BaseColor", Color.white);

        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }

        material.SetFloat("_Smoothness", 0.25f); // 봉제 인형 느낌으로 덜 반짝이게
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    private static string GeneratedTexturePath(GhostColor color) => $"{TextureFolder}/Ghost_BaseColor_{color.Name}.png";

    // 여백을 메운 원본 픽셀 (한 번 실행하는 동안 재사용)
    private static Color32[] paddedPixels;
    private static int paddedWidth;
    private static int paddedHeight;

    private static Texture2D GetOrCreateTexture(GhostColor color)
    {
        string path = GeneratedTexturePath(color);
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        Color32[] pixels = (Color32[])LoadPaddedPixels().Clone();

        for (int i = 0; !color.KeepOriginal && i < pixels.Length; i++)
        {
            Color.RGBToHSV(pixels[i], out float h, out float s, out float v);

            // 빨간 계열(몸통, 어두운 밑면)만 바꾸고 흰색·파란색(눈)은 그대로 둔다.
            bool isRed = s > 0.3f && (h < 0.1f || h > 0.9f);
            if (!isRed) continue;

            float offset = h > 0.5f ? h - 1f : h; // 원래 빨강에서 살짝 어긋난 정도는 유지
            float newHue = Mathf.Repeat(color.Hue + offset, 1f);
            Color shifted = Color.HSVToRGB(newHue, Mathf.Clamp01(s * color.SaturationScale), Mathf.Clamp01(v * color.ValueScale));
            shifted.a = pixels[i].a / 255f;
            pixels[i] = shifted;
        }

        var result = new Texture2D(paddedWidth, paddedHeight, TextureFormat.RGBA32, false);
        result.SetPixels32(pixels);
        result.Apply();
        File.WriteAllBytes(path, result.EncodeToPNG());
        Object.DestroyImmediate(result);

        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 원본 텍스처에서 모델이 실제로 쓰는 영역(UV 조각) 바깥의 여백을, 가장 가까운 조각의 색으로 채운다.
    // 여백에 남아 있는 회색/흰색 찌꺼기가 멀리서 볼 때(밉맵) 경계로 번져 "칠이 벗겨진 것처럼" 보이는 것을 막는다.
    private static Color32[] LoadPaddedPixels()
    {
        if (paddedPixels != null) return paddedPixels;

        EditorUtility.DisplayProgressBar("Ghost Texture", "원본 텍스처 읽는 중...", 0.1f);
        try
        {
            // 압축되지 않은 원본 해상도로 읽기 위해 PNG 파일을 직접 불러온다.
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(BaseColorPath));
            paddedWidth = source.width;
            paddedHeight = source.height;
            Color32[] pixels = source.GetPixels32();
            Object.DestroyImmediate(source);

            EditorUtility.DisplayProgressBar("Ghost Texture", "모델이 쓰는 영역 계산 중...", 0.4f);
            bool[] used = BuildUvMask(paddedWidth, paddedHeight);

            EditorUtility.DisplayProgressBar("Ghost Texture", "여백 채우는 중...", 0.7f);
            FillUnusedFromNearest(pixels, used, paddedWidth, paddedHeight);

            paddedPixels = pixels;
            return paddedPixels;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // 모델 메시의 UV 삼각형이 덮는 텍스처 픽셀을 true로 표시한다.
    private static bool[] BuildUvMask(int width, int height)
    {
        var used = new bool[width * height];

        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
        {
            if (asset is not Mesh mesh || !mesh.isReadable) continue;

            Vector2[] uvs = mesh.uv;
            int[] triangles = mesh.triangles;
            if (uvs.Length == 0) continue;

            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector2 a = Vector2.Scale(uvs[triangles[t]], new Vector2(width, height));
                Vector2 b = Vector2.Scale(uvs[triangles[t + 1]], new Vector2(width, height));
                Vector2 c = Vector2.Scale(uvs[triangles[t + 2]], new Vector2(width, height));
                RasterizeTriangle(used, width, height, a, b, c);
            }
        }

        return used;
    }

    private static void RasterizeTriangle(bool[] used, int width, int height, Vector2 a, Vector2 b, Vector2 c)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))) - 1, 0, width - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))) + 1, 0, width - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))) - 1, 0, height - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))) + 1, 0, height - 1);

        float area = Edge(a, b, c);
        if (Mathf.Abs(area) < 1e-6f)
        {
            // 넓이가 없는 삼각형은 꼭짓점 픽셀만 표시
            MarkPixel(used, width, height, a);
            return;
        }

        const float margin = 0.75f; // 경계에 걸친 픽셀도 포함 (픽셀 단위)
        float lenAB = (b - a).magnitude, lenBC = (c - b).magnitude, lenCA = (a - c).magnitude;
        float sign = Mathf.Sign(area);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float w0 = Edge(b, c, p) * sign, w1 = Edge(c, a, p) * sign, w2 = Edge(a, b, p) * sign;
                if (w0 >= -margin * lenBC && w1 >= -margin * lenCA && w2 >= -margin * lenAB)
                    used[y * width + x] = true;
            }
        }
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

    private static void MarkPixel(bool[] used, int width, int height, Vector2 p)
    {
        int x = Mathf.Clamp(Mathf.FloorToInt(p.x), 0, width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(p.y), 0, height - 1);
        used[y * width + x] = true;
    }

    // 쓰이는 픽셀에서 바깥으로 한 칸씩 넓혀 가며(BFS), 쓰이지 않는 픽셀을 가장 가까운 쓰이는 픽셀 색으로 칠한다.
    private static void FillUnusedFromNearest(Color32[] pixels, bool[] used, int width, int height)
    {
        int count = pixels.Length;
        var filled = (bool[])used.Clone();
        var queue = new int[count];
        int head = 0, tail = 0;

        for (int i = 0; i < count; i++)
            if (filled[i]) queue[tail++] = i;

        if (tail == 0) return; // UV 정보를 못 읽은 경우 원본 유지

        while (head < tail)
        {
            int index = queue[head++];
            int x = index % width;
            int y = index / width;

            if (x > 0) Spread(index, index - 1);
            if (x < width - 1) Spread(index, index + 1);
            if (y > 0) Spread(index, index - width);
            if (y < height - 1) Spread(index, index + width);
        }

        void Spread(int from, int to)
        {
            if (filled[to]) return;
            filled[to] = true;
            pixels[to] = pixels[from];
            queue[tail++] = to;
        }
    }

    // ---------- 배치 ----------

    private static void PlaceModel(Transform enemyRoot, Material material)
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

        Transform old = enemyRoot.Find(WrapperName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // 회전 중심이 될 빈 오브젝트 (이동 방향을 바라보게 하는 PlayerFacing을 재사용)
        var wrapper = new GameObject(WrapperName);
        wrapper.transform.SetParent(enemyRoot, false);
        wrapper.AddComponent<PlayerFacing>();
        Vector3 pivot = wrapper.transform.position;

        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, wrapper.transform);
        model.transform.localPosition = Vector3.zero;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;

        // 가장 긴 변이 TargetSize가 되도록 크기 조정 후 중심 맞추기
        Bounds bounds = GetRendererBounds(model);
        float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (longest > 0f) model.transform.localScale *= TargetSize / longest;
        model.transform.position -= GetRendererBounds(model).center - pivot;

        // 눈이 정면(+Z)을 보도록 Y축으로 회전
        if (TryFindEyeDirection(model, pivot, out Vector3 eyes))
        {
            float angle = Vector3.SignedAngle(eyes, Vector3.forward, Vector3.up);
            model.transform.RotateAround(pivot, Vector3.up, angle);
            model.transform.position -= GetRendererBounds(model).center - pivot;
        }
        else
        {
            Debug.LogWarning($"GhostModelSetup: {enemyRoot.name}의 눈 방향을 찾지 못했습니다. GhostModel의 PlayerFacing > Yaw Offset 으로 맞춰 주세요.");
        }

        // 밑면이 바닥(큐브 밑면)에 닿도록 올린다.
        bounds = GetRendererBounds(model);
        float halfCube = enemyRoot.lossyScale.y * 0.5f;
        wrapper.transform.position = enemyRoot.position + Vector3.up * (bounds.extents.y - halfCube);

        // 큐브의 겉모습만 숨긴다. (Trigger 콜라이더와 스크립트는 유지)
        if (enemyRoot.TryGetComponent(out MeshRenderer cubeRenderer)) cubeRenderer.enabled = false;
    }

    // 텍스처가 파란색(눈동자)인 정점들의 평균 위치가 중심에서 어느 쪽에 있는지로 정면을 구한다.
    private static bool TryFindEyeDirection(GameObject model, Vector3 center, out Vector3 direction)
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

            int step = Mathf.Max(1, vertices.Length / 50000);
            for (int i = 0; i < vertices.Length; i += step)
            {
                Color c = texture.GetPixelBilinear(uvs[i].x, uvs[i].y);
                bool isBlue = c.b > 0.45f && c.r < 0.3f && c.g < 0.45f;
                if (!isBlue) continue;

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

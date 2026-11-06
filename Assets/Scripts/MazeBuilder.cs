using UnityEngine;

// 문자열 배치도를 읽어 네온 스타일 미로를 생성한다.
// '#' = 벽, '.' = 통로. 배치도의 정중앙 칸이 이 오브젝트의 위치이며 플레이어 시작 칸이다.
// - 바닥/벽 몸체: 검은색 (벽 몸체가 충돌 담당)
// - 네온 라인: 통로와 맞닿은 벽 가장자리를 따라 은은하게 빛나는 파란 선 (충돌 없음)
// - 에디터: Play 전에도 Scene 창에 미로가 보이도록 자동 생성/갱신한다.
[ExecuteAlways]
public class MazeBuilder : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private float gridCellSize = 2f; // 한 칸(통로 폭)의 크기
    [SerializeField] private float wallHeight = 0.6f;

    [Header("Neon")]
    [SerializeField] private Color glowColor = new Color(0.15f, 0.4f, 1f);
    [SerializeField] private float glowIntensity = 1.6f; // 1보다 클수록 Bloom으로 더 많이 번짐
    [SerializeField] private float glowLineWidth = 0.12f;
    [SerializeField] private float glowLineInset = 0.3f;   // 벽 가장자리에서 안쪽으로 들어간 거리

    [Header("Neon Tube Core (레트로 네온관의 밝은 심지)")]
    [SerializeField, Range(0f, 1f)] private float tubeCoreWhiteness = 0.5f; // 심지 색이 흰색에 가까운 정도
    [SerializeField] private float tubeCoreIntensity = 3f;
    [SerializeField, Range(0.1f, 1f)] private float tubeCoreWidthRatio = 0.4f; // 네온 선 굵기 대비 심지 굵기

    [Header("Base")]
    [SerializeField] private Color wallBlackColor = new Color(0.006f, 0.006f, 0.012f);
    [SerializeField] private Color floorBlackColor = new Color(0.002f, 0.002f, 0.004f);
    [SerializeField] private int floorMarginCells = 6;

    [SerializeField, HideInInspector] private string builtSignature;

    private static readonly string[] Layout =
    {
        "###############################",
        "#.......#.............#.......#",
        "#.###.#.#.#.###.###.#.#.#.###.#",
        "#.#...#...#.........#...#...#.#",
        "#.#.###.#####.#.#.#####.###.#.#",
        "#...#...#...#.#.#.#...#...#...#",
        "#.###.###.#.#.#.#.#.#.###.###.#",
        "#.........#...#.#...#.........#",
        "#.###.###.#.#.#.#.#.#.###.###.#",
        "#...#...#...#.#.#.#...#...#...#",
        "#.#.###.#####.#.#.#####.###.#.#",
        "#.#...#...#.........#...#...#.#",
        "#.###.#.#.#.###.###.#.#.#.###.#",
        "#.......#.............#.......#",
        "###############################",
    };

    private const float NeonHeight = 0.05f;

    private Material neonMaterial;
    private Material coreMaterial;

    public float CellSize => gridCellSize;

    // 플레이어 시작 칸(배치도 정중앙)의 바닥 위치
    public Vector3 StartCellPosition => transform.position;

    private int Rows => Layout.Length;
    private int Cols => Layout[0].Length;

    private void Awake()
    {
        if (!Application.isPlaying) return;

        // 미리 만들어 둔(또는 프리팹에 저장된) 미로가 최신이면 그대로 쓴다.
        if (NeedsRebuild()) BuildMaze();
        Physics.SyncTransforms(); // 같은 프레임의 이동 판정이 벽을 바로 인식하도록
    }

    private bool NeedsRebuild() => transform.childCount == 0 || builtSignature != ComputeSignature();

#if UNITY_EDITOR
    private void OnEnable() => ScheduleEditorRebuild();
    private void OnValidate() => ScheduleEditorRebuild();

    // 에디터에서는 미로가 없거나 배치도/설정이 바뀌었을 때만 다시 만든다.
    // 프리팹(인스턴스/에셋/프리팹 편집 화면)은 저장된 결과를 유지하고 건드리지 않는다.
    private void ScheduleEditorRebuild()
    {
        if (Application.isPlaying) return;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (IsPrefabRelated() || !NeedsRebuild()) return;
            BuildMaze();
        };
    }

    private bool IsPrefabRelated()
    {
        return UnityEditor.PrefabUtility.IsPartOfAnyPrefab(gameObject)
               || UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject) != null;
    }
#endif

    [ContextMenu("Build Maze")]
    public void BuildMaze()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && IsPrefabRelated())
        {
            Debug.LogWarning("MazeBuilder: 프리팹은 직접 다시 만들 수 없습니다. Tools > Pacman > Save Maze Prefab 을 실행하세요.");
            return;
        }
#endif
        ClearChildren();
        neonMaterial = CreateNeonMaterial("Maze_Neon", glowColor, glowIntensity);
        coreMaterial = CreateNeonMaterial("Maze_NeonCore", Color.Lerp(glowColor, Color.white, tubeCoreWhiteness), tubeCoreIntensity);

        BuildFloor();
        BuildWallBodies(CreateChild("Walls"));
        Transform neonRoot = CreateChild("Neon");
        BuildHorizontalNeon(neonRoot);
        BuildVerticalNeon(neonRoot);

        builtSignature = ComputeSignature();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    private string ComputeSignature()
    {
        return string.Join("|", Layout) + gridCellSize + wallHeight + glowColor + glowIntensity
               + glowLineWidth + glowLineInset + tubeCoreWhiteness + tubeCoreIntensity + tubeCoreWidthRatio
               + wallBlackColor + floorBlackColor + floorMarginCells;
    }

    // ---------- 생성 ----------

    private void BuildFloor()
    {
        GameObject floor = CreateBlock("Floor", transform, CreateLitMaterial("Maze_Floor", floorBlackColor, 0.85f), true);
        floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        floor.transform.localScale = new Vector3(
            (Cols + floorMarginCells * 2) * gridCellSize, 0.1f, (Rows + floorMarginCells * 2) * gridCellSize);
    }

    // 가로로 이어진 벽 칸은 하나의 블록으로 합친다.
    private void BuildWallBodies(Transform parent)
    {
        Material bodyMaterial = CreateLitMaterial("Maze_Wall", wallBlackColor, 0.3f);

        for (int row = 0; row < Rows; row++)
        {
            int col = 0;
            while (col < Cols)
            {
                if (!IsWall(row, col)) { col++; continue; }

                int start = col;
                while (col < Cols && IsWall(row, col)) col++;
                int length = col - start;

                GameObject wall = CreateBlock($"Wall_{row}_{start}", parent, bodyMaterial, true);
                Vector3 center = CellCenter(row, start + (length - 1) * 0.5f);
                wall.transform.localPosition = center + Vector3.up * (wallHeight * 0.5f);
                wall.transform.localScale = new Vector3(length * gridCellSize, wallHeight, gridCellSize);
            }
        }
    }

    // 위(북)/아래(남)쪽이 통로인 벽 가장자리를 가로선으로 잇는다.
    private void BuildHorizontalNeon(Transform parent)
    {
        float half = gridCellSize * 0.5f;

        foreach (int dr in new[] { -1, 1 }) // -1 = 북쪽 면, +1 = 남쪽 면
        {
            for (int row = 0; row < Rows; row++)
            {
                int col = 0;
                while (col < Cols)
                {
                    if (!HasOpenFace(row, col, dr, 0)) { col++; continue; }

                    int start = col;
                    while (col < Cols && HasOpenFace(row, col, dr, 0)) col++;
                    int end = col - 1;

                    float left = CellCenter(row, start).x - half + EndOffset(row, start - 1);
                    float right = CellCenter(row, end).x + half - EndOffset(row, end + 1);
                    float z = CellCenter(row, start).z - dr * (half - glowLineInset);

                    CreateNeon(parent,
                        new Vector3((left + right) * 0.5f, 0f, z),
                        new Vector3(right - left, NeonHeight, glowLineWidth));
                }
            }
        }
    }

    // 왼쪽(서)/오른쪽(동)쪽이 통로인 벽 가장자리를 세로선으로 잇는다.
    private void BuildVerticalNeon(Transform parent)
    {
        float half = gridCellSize * 0.5f;

        foreach (int dc in new[] { -1, 1 }) // -1 = 서쪽 면, +1 = 동쪽 면
        {
            for (int col = 0; col < Cols; col++)
            {
                int row = 0;
                while (row < Rows)
                {
                    if (!HasOpenFace(row, col, 0, dc)) { row++; continue; }

                    int start = row;
                    while (row < Rows && HasOpenFace(row, col, 0, dc)) row++;
                    int end = row - 1;

                    float top = CellCenter(start, col).z + half - EndOffset(start - 1, col);
                    float bottom = CellCenter(end, col).z - half + EndOffset(end + 1, col);
                    float x = CellCenter(start, col).x + dc * (half - glowLineInset);

                    CreateNeon(parent,
                        new Vector3(x, 0f, (top + bottom) * 0.5f),
                        new Vector3(glowLineWidth, NeonHeight, top - bottom));
                }
            }
        }
    }

    // 선의 끝이 닿는 옆 칸이 통로면 바깥 모서리 → 안쪽으로 줄이고,
    // 벽이면 안쪽 모서리 → 꺾이는 다른 선과 만나도록 늘린다.
    private float EndOffset(int neighborRow, int neighborCol)
    {
        return IsWall(neighborRow, neighborCol) ? -glowLineInset : glowLineInset;
    }

    private void CreateNeon(Transform parent, Vector3 localCenter, Vector3 size)
    {
        GameObject neon = CreateBlock("NeonLine", parent, neonMaterial, false);
        neon.transform.localPosition = localCenter + Vector3.up * (wallHeight + NeonHeight * 0.5f);
        neon.transform.localScale = size;

        // 선 가운데에 더 가늘고 밝은 심지를 얹어 네온관처럼 보이게 한다.
        float width = Mathf.Min(size.x, size.z);
        float coreWidth = width * tubeCoreWidthRatio;
        float shorten = width - coreWidth;
        Vector3 coreSize = size.x < size.z
            ? new Vector3(coreWidth, NeonHeight, size.z - shorten)
            : new Vector3(size.x - shorten, NeonHeight, coreWidth);

        GameObject core = CreateBlock("NeonCore", neon.transform.parent, coreMaterial, false);
        core.transform.localPosition = localCenter + Vector3.up * (wallHeight + NeonHeight * 0.5f + 0.01f);
        core.transform.localScale = coreSize;
    }

    // ---------- 배치도 판정 ----------

    private bool IsInside(int row, int col) => row >= 0 && col >= 0 && row < Rows && col < Cols;

    // 배치도 바깥은 벽으로 취급한다.
    private bool IsWall(int row, int col) => !IsInside(row, col) || Layout[row][col] == '#';

    private bool IsOpen(int row, int col) => IsInside(row, col) && Layout[row][col] == '.';

    private bool HasOpenFace(int row, int col, int dr, int dc)
    {
        return IsInside(row, col) && Layout[row][col] == '#' && IsOpen(row + dr, col + dc);
    }

    private Vector3 CellCenter(int row, float col)
    {
        float x = (col - (Cols - 1) * 0.5f) * gridCellSize;
        float z = ((Rows - 1) * 0.5f - row) * gridCellSize;
        return new Vector3(x, 0f, z);
    }

    // ---------- 오브젝트 / 머티리얼 ----------

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                child.SetActive(false); // 콜라이더를 즉시 제거하고 오브젝트는 프레임 끝에 삭제
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    private Transform CreateChild(string childName)
    {
        Transform child = new GameObject(childName).transform;
        child.SetParent(transform, false);
        return child;
    }

    private static GameObject CreateBlock(string objectName, Transform parent, Material material, bool withCollider)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = objectName;
        block.transform.SetParent(parent, false);

        Renderer renderer = block.GetComponent<Renderer>();
        renderer.sharedMaterial = material;

        if (!withCollider)
        {
            DestroyImmediate(block.GetComponent<Collider>());
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        return block;
    }

    // 조명의 영향을 받지 않고 HDR 밝기로 빛나는 머티리얼 (Bloom 대상)
    private static Material CreateNeonMaterial(string materialName, Color color, float intensity)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Color hdr = color * intensity;
        hdr.a = 1f;

        Material material = new Material(shader) { name = materialName };
        material.SetColor("_BaseColor", hdr);
        material.color = hdr;
        return material;
    }

    private static Material CreateLitMaterial(string materialName, Color color, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = new Material(shader) { name = materialName };
        material.SetColor("_BaseColor", color);
        material.color = color;
        material.SetFloat("_Smoothness", smoothness);
        return material;
    }
}

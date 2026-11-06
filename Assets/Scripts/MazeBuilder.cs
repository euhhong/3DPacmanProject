using System.Collections.Generic;
using UnityEngine;

// 문자열 배치도를 읽어 미로(바닥 + 벽)를 생성한다.
// '#' = 벽, '.' = 통로. 배치도의 정중앙 칸이 이 오브젝트의 위치이며 플레이어 시작 칸이다.
// 바깥 테두리와 이어진 벽은 파란색, 안쪽의 떨어진 벽 덩어리(섬)는 색을 돌려 가며 칠한다.
// - 에디터: Play 전에도 Scene 창에 미로가 보이도록 자동 생성/갱신한다.
// - 실행 시: 배치도대로 새로 생성한다.
[ExecuteAlways]
public class MazeBuilder : MonoBehaviour
{
    [SerializeField] private float gridCellSize = 2f; // 한 칸(통로 폭)의 크기
    [SerializeField] private float wallHeight = 1f;
    [SerializeField] private Color borderColor = new Color(0.1f, 0.2f, 1f);
    [SerializeField] private Color[] islandColors =
    {
        new Color(0.9f, 0.1f, 0.1f),   // 빨강
        new Color(1f, 0.85f, 0.1f),    // 노랑
        new Color(0.1f, 0.8f, 0.2f),   // 초록
        new Color(1f, 0.45f, 0.75f),   // 분홍
    };
    [SerializeField] private Color floorColor = new Color(0.02f, 0.02f, 0.03f);

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

    public float CellSize => gridCellSize;

    // 플레이어 시작 칸(배치도 정중앙)의 바닥 위치
    public Vector3 StartCellPosition => transform.position;

    private int Rows => Layout.Length;
    private int Cols => Layout[0].Length;

    private void Awake()
    {
        if (!Application.isPlaying) return;

        BuildMaze();
        Physics.SyncTransforms(); // 같은 프레임의 이동 판정이 벽을 바로 인식하도록
    }

#if UNITY_EDITOR
    private void OnEnable() => ScheduleEditorRebuild();
    private void OnValidate() => ScheduleEditorRebuild();

    // 에디터에서는 미로가 없거나 배치도/설정이 바뀌었을 때만 다시 만든다.
    private void ScheduleEditorRebuild()
    {
        if (Application.isPlaying) return;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (transform.childCount > 0 && builtSignature == ComputeSignature()) return;
            BuildMaze();
        };
    }
#endif

    [ContextMenu("Build Maze")]
    public void BuildMaze()
    {
        ClearChildren();
        BuildFloor();
        BuildWalls();
        builtSignature = ComputeSignature();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    private string ComputeSignature()
    {
        return string.Join("|", Layout) + gridCellSize + wallHeight + borderColor + floorColor
               + string.Join(",", islandColors);
    }

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

    private void BuildFloor()
    {
        GameObject floor = CreateBlock("Floor", CreateMaterial(floorColor));
        floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        floor.transform.localScale = new Vector3(Cols * gridCellSize, 0.1f, Rows * gridCellSize);
    }

    // 가로로 이어진 벽 칸은 하나의 블록으로 합친다. (가로로 이어지면 항상 같은 덩어리)
    private void BuildWalls()
    {
        int[,] groupOf = LabelWallGroups(out List<bool> touchesBorder, out List<int> groupKey);

        Material borderMaterial = CreateMaterial(borderColor);
        var islandMaterials = new Material[Mathf.Max(1, islandColors.Length)];
        for (int i = 0; i < islandMaterials.Length; i++)
            islandMaterials[i] = CreateMaterial(islandColors.Length > 0 ? islandColors[i] : borderColor);

        for (int row = 0; row < Rows; row++)
        {
            int col = 0;
            while (col < Cols)
            {
                if (Layout[row][col] != '#') { col++; continue; }

                int start = col;
                while (col < Cols && Layout[row][col] == '#') col++;
                int length = col - start;

                int group = groupOf[row, start];
                Material material = touchesBorder[group]
                    ? borderMaterial
                    : islandMaterials[groupKey[group] % islandMaterials.Length];

                float centerCol = start + (length - 1) * 0.5f;
                GameObject wall = CreateBlock($"Wall_{row}_{start}", material);
                wall.transform.localPosition = CellToLocal(row, centerCol) + Vector3.up * (wallHeight * 0.5f);
                wall.transform.localScale = new Vector3(length * gridCellSize, wallHeight, gridCellSize);
            }
        }
    }

    // 상하좌우로 이어진 벽 칸끼리 같은 번호를 매긴다.
    // groupKey는 좌우/상하 대칭 위치의 섬이 같은 색이 되도록 대칭 기준 위치로 만든 값이다.
    private int[,] LabelWallGroups(out List<bool> touchesBorder, out List<int> groupKey)
    {
        var groupOf = new int[Rows, Cols];
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                groupOf[r, c] = -1;

        touchesBorder = new List<bool>();
        groupKey = new List<int>();
        var stack = new Stack<Vector2Int>();
        Vector2Int[] steps = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                if (Layout[r][c] != '#' || groupOf[r, c] != -1) continue;

                int id = touchesBorder.Count;
                bool border = false;
                int key = int.MaxValue;
                groupOf[r, c] = id;
                stack.Push(new Vector2Int(c, r));

                while (stack.Count > 0)
                {
                    Vector2Int cell = stack.Pop();
                    if (cell.x == 0 || cell.y == 0 || cell.x == Cols - 1 || cell.y == Rows - 1) border = true;

                    int mirroredRow = Mathf.Min(cell.y, Rows - 1 - cell.y);
                    int mirroredCol = Mathf.Min(cell.x, Cols - 1 - cell.x);
                    key = Mathf.Min(key, mirroredRow * Cols + mirroredCol);

                    foreach (Vector2Int step in steps)
                    {
                        Vector2Int next = cell + step;
                        if (next.x < 0 || next.y < 0 || next.x >= Cols || next.y >= Rows) continue;
                        if (Layout[next.y][next.x] != '#' || groupOf[next.y, next.x] != -1) continue;
                        groupOf[next.y, next.x] = id;
                        stack.Push(next);
                    }
                }

                touchesBorder.Add(border);
                groupKey.Add(key);
            }
        }

        // 대칭 기준 값을 0, 1, 2... 순번으로 바꿔 색이 고르게 돌아가게 한다.
        var order = new List<int>(new SortedSet<int>(groupKey));
        for (int i = 0; i < groupKey.Count; i++) groupKey[i] = order.IndexOf(groupKey[i]);

        return groupOf;
    }

    private Vector3 CellToLocal(int row, float col)
    {
        float x = (col - (Cols - 1) * 0.5f) * gridCellSize;
        float z = ((Rows - 1) * 0.5f - row) * gridCellSize;
        return new Vector3(x, 0f, z);
    }

    private GameObject CreateBlock(string objectName, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube); // BoxCollider 포함
        block.name = objectName;
        block.transform.SetParent(transform, false);
        block.GetComponent<Renderer>().sharedMaterial = material;
        return block;
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.SetColor("_BaseColor", color);
        material.color = color;
        return material;
    }
}

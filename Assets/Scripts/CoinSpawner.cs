using UnityEngine;

// 미로(MazeBuilder)의 모든 통로 칸에 코인을 배치한다. (시작 칸 제외, 네 모서리는 큰 파워 코인)
// - 에디터: Play 전에도 Scene 창에 코인이 보이도록 자동 생성/갱신한다.
// - 실행 시: 미리 만들어 둔 코인이 최신이면 그대로 쓰고, 아니면 새로 만든다.
[ExecuteAlways]
public class CoinSpawner : MonoBehaviour
{
    [Header("Value")]
    [SerializeField] private int coinValue = 10;
    [SerializeField] private int powerCoinValue = 50;

    [Header("Look")]
    [SerializeField] private float coinSize = 0.3f;
    [SerializeField] private float powerCoinSize = 0.7f;
    [SerializeField] private float coinHeight = 0.5f; // 바닥에서 코인 중심까지 높이
    [SerializeField] private Color coinColor = new Color(1f, 0.82f, 0.55f);
    [SerializeField] private float coinGlow = 2f;     // 1보다 크면 Bloom으로 은은하게 빛남
    [SerializeField] private float powerCoinGlow = 3f;

    [SerializeField, HideInInspector] private string builtSignature;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        if (NeedsRebuild()) BuildCoins();
    }

    private bool NeedsRebuild() => transform.childCount == 0 || builtSignature != ComputeSignature();

    private string ComputeSignature()
    {
        MazeBuilder maze = FindAnyObjectByType<MazeBuilder>();
        string mazeSignature = maze != null ? maze.LayoutSignature + maze.transform.position : "none";
        return mazeSignature + coinValue + powerCoinValue + coinSize + powerCoinSize + coinHeight
               + coinColor + coinGlow + powerCoinGlow;
    }

#if UNITY_EDITOR
    private void OnEnable() => ScheduleEditorRebuild();
    private void OnValidate() => ScheduleEditorRebuild();

    private void ScheduleEditorRebuild()
    {
        if (Application.isPlaying) return;

        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || Application.isPlaying) return;
            if (UnityEditor.PrefabUtility.IsPartOfAnyPrefab(gameObject)) return;
            if (NeedsRebuild()) BuildCoins();
        };
    }
#endif

    [ContextMenu("Build Coins")]
    public void BuildCoins()
    {
        MazeBuilder maze = FindAnyObjectByType<MazeBuilder>();
        if (maze == null)
        {
            Debug.LogWarning("CoinSpawner: 씬에서 MazeBuilder를 찾을 수 없습니다.");
            return;
        }

        ClearChildren();

        Material coinMaterial = CreateGlowMaterial("Coin", coinColor, coinGlow);
        Material powerMaterial = CreateGlowMaterial("PowerCoin", coinColor, powerCoinGlow);

        int lastRow = maze.RowCount - 1;
        int lastCol = maze.ColumnCount - 1;
        Vector2Int start = maze.StartCell;

        for (int row = 0; row <= lastRow; row++)
        {
            for (int col = 0; col <= lastCol; col++)
            {
                if (!maze.IsOpenCell(row, col)) continue;
                if (row == start.y && col == start.x) continue;

                // 바깥 테두리 바로 안쪽의 네 모서리 칸은 파워 코인
                bool isPower = (row == 1 || row == lastRow - 1) && (col == 1 || col == lastCol - 1);

                CreateCoin(maze.CellWorldPosition(row, col), isPower, isPower ? powerMaterial : coinMaterial);
            }
        }

        builtSignature = ComputeSignature();

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    private void CreateCoin(Vector3 floorPosition, bool isPower, Material material)
    {
        GameObject coin = GameObject.CreatePrimitive(PrimitiveType.Sphere); // SphereCollider 포함
        coin.name = isPower ? "PowerCoin" : "Coin";
        coin.transform.SetParent(transform, false);
        coin.transform.position = floorPosition + Vector3.up * coinHeight;
        coin.transform.localScale = Vector3.one * (isPower ? powerCoinSize : coinSize);

        coin.GetComponent<Collider>().isTrigger = true;

        Renderer renderer = coin.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        coin.AddComponent<Coin>().Setup(isPower ? powerCoinValue : coinValue, isPower);
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                child.SetActive(false);
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }
    }

    // 조명의 영향을 받지 않고 HDR 밝기로 빛나는 머티리얼 (Bloom 대상)
    private static Material CreateGlowMaterial(string materialName, Color color, float intensity)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Color hdr = color * intensity;
        hdr.a = 1f;

        var material = new Material(shader) { name = materialName };
        material.SetColor("_BaseColor", hdr);
        material.color = hdr;
        return material;
    }
}

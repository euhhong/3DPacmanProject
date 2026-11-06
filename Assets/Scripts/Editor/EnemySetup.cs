using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 전용: 방해꾼 4마리(임시 큐브)와 게임 종료 관리(GameState)를 씬에 배치하고 저장한다.
// - 아직 배치되지 않았다면 편집 상태가 되었을 때 자동으로 한 번 실행
// - 메뉴 Tools > Pacman > Setup Enemies 로 다시 실행 가능 (기존 방해꾼은 지우고 새로 만듦)
[InitializeOnLoad]
public static class EnemySetup
{
    private const string RootName = "Enemies";
    private const float CubeSize = 1.1f;
    private const float Speed = 5f;      // 플레이어(6)보다 약간 느리게
    private const float GlowIntensity = 1.5f;

    private struct EnemyInfo
    {
        public string Name;
        public EnemyTargeting.Personality Personality;
        public Color Color;
        public Vector2Int Spawn;  // (x = 열, y = 행)
        public Vector2Int Home;   // Shy가 물러나는 구석
        public float Delay;       // 출발 대기 시간(초)
    }

    private static readonly EnemyInfo[] Enemies =
    {
        new EnemyInfo { Name = "Enemy_Chaser",   Personality = EnemyTargeting.Personality.Chaser,
            Color = new Color(1f, 0.15f, 0.35f), Spawn = new Vector2Int(15, 1),  Home = new Vector2Int(29, 1),  Delay = 1f },
        new EnemyInfo { Name = "Enemy_Ambusher", Personality = EnemyTargeting.Personality.Ambusher,
            Color = new Color(0.75f, 0.3f, 1f),  Spawn = new Vector2Int(15, 13), Home = new Vector2Int(1, 1),   Delay = 3f },
        new EnemyInfo { Name = "Enemy_Wanderer", Personality = EnemyTargeting.Personality.Wanderer,
            Color = new Color(0.3f, 1f, 0.45f),  Spawn = new Vector2Int(1, 3),   Home = new Vector2Int(1, 13),  Delay = 5f },
        new EnemyInfo { Name = "Enemy_Shy",      Personality = EnemyTargeting.Personality.Shy,
            Color = new Color(1f, 0.55f, 0.1f),  Spawn = new Vector2Int(29, 11), Home = new Vector2Int(29, 13), Delay = 7f },
    };

    static EnemySetup()
    {
        EditorApplication.update += WaitForEditMode;
    }

    private static void WaitForEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= WaitForEditMode;

        if (Object.FindAnyObjectByType<MazeBuilder>() == null) return;  // 대상 씬이 아님
        if (Object.FindAnyObjectByType<EnemyMove>() != null && Object.FindAnyObjectByType<GameState>() != null) return;
        Setup();
    }

    [MenuItem("Tools/Pacman/Setup Enemies")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("EnemySetup: Play 모드를 끈 뒤 실행하세요.");
            return;
        }

        MazeBuilder maze = Object.FindAnyObjectByType<MazeBuilder>();
        if (maze == null)
        {
            Debug.LogWarning("EnemySetup: 씬에 MazeBuilder가 없습니다.");
            return;
        }

        GameObject oldRoot = GameObject.Find(RootName);
        if (oldRoot != null) Object.DestroyImmediate(oldRoot);

        var root = new GameObject(RootName);
        foreach (EnemyInfo info in Enemies) CreateEnemy(root.transform, maze, info);

        EnsureGameState();

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveScene(root.scene);
        Debug.Log($"EnemySetup: 방해꾼 {Enemies.Length}마리와 GameState 배치 완료, 씬 저장됨.");
    }

    private static void CreateEnemy(Transform parent, MazeBuilder maze, EnemyInfo info)
    {
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        enemy.name = info.Name;
        enemy.transform.SetParent(parent, false);
        enemy.transform.localScale = Vector3.one * CubeSize;

        // Scene 창에서도 시작 위치에 보이도록 배치 (실행 시 EnemyMove가 다시 맞춤)
        enemy.transform.position = maze.CellWorldPosition(info.Spawn.y, info.Spawn.x) + Vector3.up * (CubeSize * 0.5f);

        // 잡기 판정: 몸보다 약간 작은 Trigger (스치기만 해도 잡히는 것 방지)
        var box = enemy.GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = Vector3.one * 0.8f;

        var renderer = enemy.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateGlowMaterial(info.Name, info.Color);

        enemy.AddComponent<EnemyTargeting>().Setup(info.Personality, info.Home);
        enemy.AddComponent<EnemyMove>().Setup(info.Spawn, info.Delay, Speed);
        enemy.AddComponent<EnemyCatch>();
    }

    private static void EnsureGameState()
    {
        if (Object.FindAnyObjectByType<GameState>() != null) return;

        ScoreManager scoreManager = Object.FindAnyObjectByType<ScoreManager>();
        GameObject host = scoreManager != null ? scoreManager.gameObject : new GameObject("GameManager");
        host.AddComponent<GameState>();
    }

    private static Material CreateGlowMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Color hdr = color * GlowIntensity;
        hdr.a = 1f;

        var material = new Material(shader) { name = materialName };
        material.SetColor("_BaseColor", hdr);
        material.color = hdr;
        return material;
    }
}

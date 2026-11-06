using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 전용: 코인 게임에 필요한 오브젝트를 씬에 배치하고 저장한다.
// - "Coins" (CoinSpawner): 미로의 모든 통로에 코인 생성
// - "GameManager" (ScoreManager + ScoreUI): 점수 계산과 화면 표시
// - 플레이어에 Kinematic Rigidbody 보장 (코인 Trigger 감지에 필요)
// - 아직 배치되지 않았다면 편집 상태가 되었을 때 자동으로 한 번 실행
// - 메뉴 Tools > Pacman > Setup Coins And Score 로 다시 실행 가능
[InitializeOnLoad]
public static class CoinGameSetup
{
    static CoinGameSetup()
    {
        EditorApplication.update += WaitForEditMode;
    }

    private static void WaitForEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= WaitForEditMode;
        AutoSetup();
    }

    private static void AutoSetup()
    {
        if (Object.FindAnyObjectByType<MazeBuilder>() == null) return;   // 대상 씬이 아님
        if (Object.FindAnyObjectByType<CoinSpawner>() != null &&
            Object.FindAnyObjectByType<ScoreManager>() != null) return;  // 이미 배치됨

        Setup();
    }

    [MenuItem("Tools/Pacman/Setup Coins And Score")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("CoinGameSetup: Play 모드를 끈 뒤 실행하세요.");
            return;
        }

        CoinSpawner spawner = Object.FindAnyObjectByType<CoinSpawner>();
        if (spawner == null) spawner = new GameObject("Coins").AddComponent<CoinSpawner>();
        spawner.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        spawner.BuildCoins();

        ScoreManager scoreManager = Object.FindAnyObjectByType<ScoreManager>();
        if (scoreManager == null) scoreManager = new GameObject("GameManager").AddComponent<ScoreManager>();
        if (!scoreManager.TryGetComponent(out ScoreUI _)) scoreManager.gameObject.AddComponent<ScoreUI>();

        PlayerMove player = Object.FindAnyObjectByType<PlayerMove>();
        if (player != null)
        {
            if (!player.TryGetComponent(out Rigidbody body)) body = player.gameObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
        }

        var scene = spawner.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"CoinGameSetup: 코인 {spawner.transform.childCount}개와 GameManager 배치 완료, 씬 저장됨.");
    }
}

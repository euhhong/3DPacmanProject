using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 전용 메뉴: Tools > Pacman > Setup Scene
// 미로를 원점에 두고, 플레이어와 카메라를 미로 전체가 보기 좋은 위치에 배치한 뒤 씬을 저장한다.
public static class PacmanSceneSetup
{
    private static readonly Vector3 PlayerStart = new Vector3(0f, 0.5f, 0f);
    private static readonly Vector3 CameraPosition = new Vector3(0f, 20f, -7f);
    private static readonly Vector3 CameraEuler = new Vector3(70f, 0f, 0f);

    [MenuItem("Tools/Pacman/Setup Scene")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("PacmanSceneSetup: Play 모드를 끈 뒤 실행하세요.");
            return;
        }

        MazeBuilder maze = Object.FindAnyObjectByType<MazeBuilder>();
        if (maze == null) maze = new GameObject("Maze").AddComponent<MazeBuilder>();

        maze.gameObject.name = "Maze";
        maze.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        maze.BuildMaze();

        PlayerMove player = Object.FindAnyObjectByType<PlayerMove>();
        if (player != null)
        {
            player.transform.SetPositionAndRotation(PlayerStart, Quaternion.identity);
            if (player.TryGetComponent(out Rigidbody body))
            {
                body.useGravity = false;
                body.isKinematic = true;
            }
        }

        CameraMove cameraMove = Object.FindAnyObjectByType<CameraMove>();
        if (cameraMove != null)
        {
            cameraMove.transform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(CameraEuler));
        }

        EditorSceneManager.MarkSceneDirty(maze.gameObject.scene);
        EditorSceneManager.SaveScene(maze.gameObject.scene);
        Debug.Log("PacmanSceneSetup: 미로 생성 및 플레이어/카메라 배치 완료, 씬 저장됨.");
    }
}

using UnityEngine;

// 방해꾼이 "어느 칸을 향해 갈지"를 성격별로 정한다. 실제 이동은 EnemyMove가 맡는다.
public class EnemyTargeting : MonoBehaviour
{
    public enum Personality
    {
        Chaser,   // 플레이어가 있는 칸을 곧장 쫓음
        Ambusher, // 플레이어가 가는 방향의 몇 칸 앞을 노림
        Wanderer, // 몇 초마다 아무 통로 칸이나 골라 돌아다님
        Shy,      // 멀면 쫓아오고, 가까워지면 자기 구석으로 물러남
    }

    [SerializeField] private Personality personality = Personality.Chaser;
    [SerializeField] private Vector2Int homeCorner = new Vector2Int(1, 1); // (x = 열, y = 행)
    [SerializeField] private int ambushCells = 4;
    [SerializeField] private float shyDistance = 8f;
    [SerializeField] private float wanderInterval = 4f;

    private MazeBuilder maze;
    private PlayerMove player;
    private Vector2Int wanderTarget;
    private float wanderTimer;

    public void Setup(Personality newPersonality, Vector2Int corner)
    {
        personality = newPersonality;
        homeCorner = corner;
    }

    private void Start()
    {
        maze = FindAnyObjectByType<MazeBuilder>();
        player = FindAnyObjectByType<PlayerMove>();
        wanderTarget = homeCorner;
    }

    private void Update()
    {
        if (personality != Personality.Wanderer || maze == null) return;

        wanderTimer -= Time.deltaTime;
        if (wanderTimer > 0f) return;

        wanderTimer = wanderInterval;
        wanderTarget = RandomOpenCell();
    }

    public Vector2Int GetTargetCell(Vector2Int myCell)
    {
        if (maze == null || player == null) return homeCorner;

        Vector2Int playerCell = maze.WorldToCell(player.transform.position);

        switch (personality)
        {
            case Personality.Ambusher:
                return playerCell + DirectionToCell(player.MoveDirection) * ambushCells;

            case Personality.Wanderer:
                return wanderTarget;

            case Personality.Shy:
                return Vector2Int.Distance(myCell, playerCell) > shyDistance ? playerCell : homeCorner;

            default:
                return playerCell;
        }
    }

    // 월드 방향 → 칸 방향 (열은 +X, 행은 -Z 방향으로 증가)
    private static Vector2Int DirectionToCell(Vector3 direction)
    {
        return new Vector2Int(Mathf.RoundToInt(direction.x), -Mathf.RoundToInt(direction.z));
    }

    private Vector2Int RandomOpenCell()
    {
        for (int i = 0; i < 100; i++)
        {
            int row = Random.Range(0, maze.RowCount);
            int col = Random.Range(0, maze.ColumnCount);
            if (maze.IsOpenCell(row, col)) return new Vector2Int(col, row);
        }
        return homeCorner;
    }
}

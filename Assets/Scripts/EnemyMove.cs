using UnityEngine;

// 방해꾼의 칸 단위 이동.
// - 시작 칸에서 releaseDelay초 기다린 뒤 출발
// - 칸 중앙마다 갈 수 있는 방향 중, 목표 칸(EnemyTargeting)에 가장 가까워지는 방향을 고름
// - 막다른 길이 아니면 뒤로 돌지 않음 (고전 미로 게임 방식)
// - 좌우 터널을 플레이어와 같은 방식으로 통과
[RequireComponent(typeof(EnemyTargeting))]
public class EnemyMove : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private Vector2Int spawnCell = new Vector2Int(15, 1); // (x = 열, y = 행)
    [SerializeField] private float releaseDelay = 2f;

    // 위, 왼쪽, 아래, 오른쪽 순서 (거리가 같으면 앞쪽 방향을 고름)
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, -1), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(1, 0),
    };

    private MazeBuilder maze;
    private EnemyTargeting targeting;
    private float height;
    private float releaseTimer;
    private Vector2Int currentCell;
    private Vector2Int nextCell;
    private Vector2Int direction;
    private bool moving;

    public void Setup(Vector2Int cell, float delay, float speed)
    {
        spawnCell = cell;
        releaseDelay = delay;
        moveSpeed = speed;
    }

    private void Start()
    {
        maze = FindAnyObjectByType<MazeBuilder>();
        targeting = GetComponent<EnemyTargeting>();
        if (maze == null)
        {
            Debug.LogWarning($"{name}: 씬에서 MazeBuilder를 찾을 수 없습니다.");
            enabled = false;
            return;
        }

        height = transform.lossyScale.y * 0.5f; // 큐브 밑면이 바닥에 닿는 높이
        currentCell = spawnCell;
        transform.position = CellToWorld(currentCell);
        releaseTimer = releaseDelay;
    }

    private void Update()
    {
        if (releaseTimer > 0f)
        {
            releaseTimer -= Time.deltaTime;
            return;
        }

        if (!moving)
        {
            ChooseNextCell();
            if (!moving) return;
        }

        float remaining = moveSpeed * Time.deltaTime;
        while (remaining > 0f && moving)
        {
            Vector3 target = CellToWorld(nextCell);
            float distance = Vector3.Distance(transform.position, target);
            if (distance > remaining)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, remaining);
                break;
            }

            // 칸 중앙 도착
            transform.position = target;
            remaining -= distance;
            currentCell = nextCell;
            WrapThroughTunnel();
            ChooseNextCell();
        }
    }

    // 터널 바깥 한 칸에 도착하면 반대편 바깥 한 칸으로 순간이동
    private void WrapThroughTunnel()
    {
        if (currentCell.x < 0) currentCell.x = maze.ColumnCount;
        else if (currentCell.x >= maze.ColumnCount) currentCell.x = -1;
        else return;

        transform.position = CellToWorld(currentCell);
    }

    private void ChooseNextCell()
    {
        bool insideMaze = currentCell.x >= 0 && currentCell.x < maze.ColumnCount;

        // 터널 안에서는 같은 방향으로 계속 간다.
        if (!insideMaze && direction != Vector2Int.zero)
        {
            SetNext(direction);
            return;
        }

        Vector2Int goal = targeting.GetTargetCell(currentCell);
        Vector2Int best = Vector2Int.zero;
        float bestDistance = float.MaxValue;

        foreach (Vector2Int candidate in Directions)
        {
            if (candidate == -direction && direction != Vector2Int.zero) continue; // 뒤로 돌지 않음
            if (!IsWalkable(currentCell + candidate)) continue;

            float distance = (currentCell + candidate - goal).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        // 막다른 길이면 되돌아간다.
        if (best == Vector2Int.zero && direction != Vector2Int.zero && IsWalkable(currentCell - direction))
            best = -direction;

        if (best == Vector2Int.zero)
        {
            moving = false;
            return;
        }

        SetNext(best);
    }

    private void SetNext(Vector2Int newDirection)
    {
        direction = newDirection;
        nextCell = currentCell + direction;
        moving = true;
    }

    private bool IsWalkable(Vector2Int cell) => maze.IsWalkableCell(cell.y, cell.x);

    private Vector3 CellToWorld(Vector2Int cell) => maze.CellWorldPosition(cell.y, cell.x) + Vector3.up * height;
}

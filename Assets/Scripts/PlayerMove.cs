using UnityEngine;
using UnityEngine.InputSystem;

// 칸(그리드) 단위로 이동한다. 칸 크기와 시작 위치는 씬의 MazeBuilder에서 가져온다.
// - 정지 상태로 시작, WASD로 방향을 정하면 그 방향으로 계속 이동
// - 입력은 저장해 두었다가 칸 중앙에 도착했을 때 그 방향이 열려 있으면 꺾음
// - 막힌 방향 입력은 보류하고 원래 방향으로 계속 진행
// - 정면 칸이 벽이면 칸 중앙에서 정지
// - 반대 방향 입력은 즉시 되돌아감
// - relativeControls가 켜져 있으면 WASD는 플레이어가 바라보는 방향 기준 (W 앞, S 뒤, A 왼쪽, D 오른쪽)
//   → 카메라가 항상 뒤에서 따라가므로 화면 기준 방향과 같아진다.
public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private bool relativeControls = true;

    // 현재 진행 방향 (정지 시 zero). 방해꾼의 예측 추적 등에 쓰인다.
    public Vector3 MoveDirection => moveDirection;

    // 마지막으로 바라본 방향 (정지해도 유지). 카메라가 이 방향 뒤에 선다.
    public Vector3 Heading => heading;

    private MazeBuilder maze;
    private float cellSize = 1f;
    private Vector3 heading = Vector3.forward;
    private Vector3 moveDirection = Vector3.zero;
    private Vector3 bufferedDirection = Vector3.zero;
    private Vector3 targetPosition;

    private void Awake()
    {
        // 이동은 이 스크립트가 직접 하므로 물리(중력/충돌 반응)는 끈다.
        if (TryGetComponent(out Rigidbody body))
        {
            body.useGravity = false;
            body.isKinematic = true;
        }
    }

    private void Start()
    {
        float halfHeight = TryGetComponent(out Collider col) ? col.bounds.extents.y : 0.5f;

        maze = FindAnyObjectByType<MazeBuilder>();
        if (maze != null)
        {
            cellSize = maze.CellSize;
            transform.position = maze.StartCellPosition + Vector3.up * halfHeight;
        }
        else
        {
            Debug.LogWarning("PlayerMove: 씬에서 MazeBuilder를 찾을 수 없어 현재 위치에서 시작합니다.");
        }

        transform.rotation = Quaternion.identity;
        targetPosition = transform.position;
    }

    private void Update()
    {
        ReadInput();

        if (moveDirection == Vector3.zero)
        {
            TryStartMove();
            return;
        }

        float remaining = moveSpeed * Time.deltaTime;
        while (remaining > 0f && moveDirection != Vector3.zero)
        {
            float distance = Vector3.Distance(transform.position, targetPosition);
            if (distance > remaining)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, remaining);
                break;
            }

            // 칸 중앙 도착: 남은 거리는 다음 칸 이동에 이어서 사용
            transform.position = targetPosition;
            remaining -= distance;
            ChooseNextDirection();
        }
    }

    private void LateUpdate()
    {
        if (moveDirection != Vector3.zero) heading = moveDirection;
    }

    private void ReadInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector3 forward = relativeControls ? heading : Vector3.forward;
        Vector3 right = new Vector3(forward.z, 0f, -forward.x); // forward를 오른쪽으로 90도 돌린 방향

        Vector3 input = Vector3.zero;
        if (keyboard.wKey.wasPressedThisFrame) input = forward;
        else if (keyboard.sKey.wasPressedThisFrame) input = -forward;
        else if (keyboard.aKey.wasPressedThisFrame) input = -right;
        else if (keyboard.dKey.wasPressedThisFrame) input = right;
        if (input == Vector3.zero) return;

        input = new Vector3(Mathf.Round(input.x), 0f, Mathf.Round(input.z)); // 부동소수 오차 제거

        if (moveDirection != Vector3.zero && input == -moveDirection)
        {
            // 반대 방향은 칸 중앙을 기다리지 않고 바로 되돌아간다.
            targetPosition -= moveDirection * cellSize;
            moveDirection = input;
            bufferedDirection = Vector3.zero;
            return;
        }

        bufferedDirection = input == moveDirection ? Vector3.zero : input;
    }

    private void TryStartMove()
    {
        if (bufferedDirection == Vector3.zero || IsWall(bufferedDirection)) return;

        moveDirection = bufferedDirection;
        bufferedDirection = Vector3.zero;
        targetPosition = transform.position + moveDirection * cellSize;
    }

    private void ChooseNextDirection()
    {
        // 터널: 미로 바깥 한 칸까지 나가면 반대편 바깥 한 칸으로 순간이동한 뒤 같은 방향으로 계속 들어간다.
        if (maze != null && maze.TryWrapTunnel(transform.position, out Vector3 wrapped))
        {
            transform.position = wrapped;
        }

        // 미로 바깥(터널 안)에서는 방향을 꺾거나 멈추지 않는다.
        if (maze != null && !maze.IsInsideGrid(transform.position))
        {
            targetPosition = transform.position + moveDirection * cellSize;
            return;
        }

        if (bufferedDirection != Vector3.zero && !IsWall(bufferedDirection))
        {
            moveDirection = bufferedDirection;
            bufferedDirection = Vector3.zero;
        }
        else if (IsWall(moveDirection))
        {
            moveDirection = Vector3.zero; // 정면이 막혀 정지 (보류된 입력은 유지)
            return;
        }

        targetPosition = transform.position + moveDirection * cellSize;
    }

    // 현재 칸에서 direction 쪽 옆 칸에 벽(Trigger가 아닌 Collider)이 있는지 검사한다.
    // 높이는 얇게 잡아 바닥은 벽으로 치지 않는다.
    private bool IsWall(Vector3 direction)
    {
        Vector3 nextCell = transform.position + direction * cellSize;
        Vector3 halfExtents = new Vector3(cellSize * 0.4f, 0.1f, cellSize * 0.4f);
        return Physics.CheckBox(nextCell, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
    }
}

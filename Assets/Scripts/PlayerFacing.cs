using UnityEngine;

// 플레이어 모델(자식 오브젝트)을 실제로 움직인 방향으로 부드럽게 돌린다.
// 이동 로직(PlayerMove)과 분리되어 있어, 이 오브젝트의 위치 변화만 보고 방향을 정한다.
public class PlayerFacing : MonoBehaviour
{
    [SerializeField] private float yawOffset = 0f;    // 모델의 정면이 이동 방향과 다르면 90/180/-90 등으로 보정
    [SerializeField] private float turnSpeed = 900f;  // 초당 회전 각도

    private Vector3 lastPosition;
    private Quaternion targetRotation;

    private void Start()
    {
        lastPosition = transform.position;
        targetRotation = Quaternion.Euler(0f, yawOffset, 0f); // 처음엔 앞(+Z)을 바라봄
        transform.rotation = targetRotation;
    }

    private void LateUpdate()
    {
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;

        // 아주 작은 떨림과 터널 순간이동(한 프레임에 크게 점프)은 방향 계산에서 뺀다.
        if (delta.sqrMagnitude > 0.000001f && delta.sqrMagnitude < 4f)
        {
            targetRotation = Quaternion.LookRotation(delta.normalized) * Quaternion.Euler(0f, yawOffset, 0f);
        }

        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }
}

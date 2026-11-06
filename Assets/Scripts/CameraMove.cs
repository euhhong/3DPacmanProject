using UnityEngine;

// 플레이어 뒤쪽 위에서 따라가며, 플레이어가 방향을 바꾸면 함께 돌아 항상 뒤통수를 비춘다.
public class CameraMove : MonoBehaviour
{
    [SerializeField] private float followDistance = 6f;   // 플레이어 뒤로 떨어진 거리
    [SerializeField] private float followHeight = 5f;     // 바닥 위 높이
    [SerializeField] private float lookAheadDistance = 2f; // 플레이어보다 이만큼 앞을 바라봄
    [SerializeField] private float positionSmooth = 10f;  // 클수록 위치를 바짝 따라감
    [SerializeField] private float turnSmooth = 6f;       // 클수록 회전을 빨리 따라감
    [SerializeField] private float teleportDistance = 3f; // 한 프레임에 이만큼 넘게 움직이면 순간이동으로 봄

    private PlayerMove player;
    private Vector3 smoothedTarget;
    private Vector3 lastTargetPosition;
    private float currentYaw;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerMove>();
        if (player == null)
        {
            Debug.LogWarning("CameraMove: 씬에서 PlayerMove를 찾을 수 없습니다.");
            enabled = false;
            return;
        }

        smoothedTarget = player.transform.position;
        lastTargetPosition = smoothedTarget;
        currentYaw = YawOf(player.Heading);
        ApplyTransform();
    }

    // 플레이어 이동(Update)이 끝난 뒤 카메라를 옮겨 떨림을 막는다.
    private void LateUpdate()
    {
        Vector3 targetPosition = player.transform.position;

        // 터널 순간이동: 맵을 가로질러 미끄러지지 않도록 바로 따라간다.
        if ((targetPosition - lastTargetPosition).magnitude > teleportDistance) smoothedTarget = targetPosition;
        lastTargetPosition = targetPosition;

        smoothedTarget = Vector3.Lerp(smoothedTarget, targetPosition, positionSmooth * Time.deltaTime);
        currentYaw = Mathf.LerpAngle(currentYaw, YawOf(player.Heading), turnSmooth * Time.deltaTime);

        ApplyTransform();
    }

    private void ApplyTransform()
    {
        Quaternion yaw = Quaternion.Euler(0f, currentYaw, 0f);
        transform.position = smoothedTarget + yaw * new Vector3(0f, followHeight, -followDistance);
        transform.LookAt(smoothedTarget + yaw * (Vector3.forward * lookAheadDistance));
    }

    private static float YawOf(Vector3 direction)
    {
        return direction.sqrMagnitude > 0.0001f ? Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg : 0f;
    }
}

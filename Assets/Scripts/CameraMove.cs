using UnityEngine;

// 플레이어(PlayerMove가 붙은 오브젝트)를 처음 배치된 거리만큼 유지하며 따라간다.
public class CameraMove : MonoBehaviour
{
    [SerializeField] private float followSpeed = 10f;

    [SerializeField] private float teleportDistance = 3f; // 한 프레임에 이만큼 넘게 움직이면 순간이동으로 봄

    private Transform target;
    private Vector3 offset;
    private Vector3 lastTargetPosition;

    private void Start()
    {
        PlayerMove player = FindAnyObjectByType<PlayerMove>();
        if (player == null)
        {
            Debug.LogWarning("CameraMove: 씬에서 PlayerMove를 찾을 수 없습니다.");
            enabled = false;
            return;
        }

        target = player.transform;
        offset = transform.position - target.position;
        lastTargetPosition = target.position;
    }

    // 플레이어 이동(Update)이 끝난 뒤 카메라를 옮겨 떨림을 막는다.
    private void LateUpdate()
    {
        // 터널 순간이동: 카메라가 맵을 가로질러 미끄러지지 않도록 같은 거리만큼 바로 옮긴다.
        Vector3 jump = target.position - lastTargetPosition;
        if (jump.magnitude > teleportDistance) transform.position += jump;
        lastTargetPosition = target.position;

        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, followSpeed * Time.deltaTime);
    }
}

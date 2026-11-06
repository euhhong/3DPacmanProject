using UnityEngine;

// 플레이어(PlayerMove가 붙은 오브젝트)를 처음 배치된 거리만큼 유지하며 따라간다.
public class CameraMove : MonoBehaviour
{
    [SerializeField] private float followSpeed = 10f;

    private Transform target;
    private Vector3 offset;

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
    }

    // 플레이어 이동(Update)이 끝난 뒤 카메라를 옮겨 떨림을 막는다.
    private void LateUpdate()
    {
        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, followSpeed * Time.deltaTime);
    }
}

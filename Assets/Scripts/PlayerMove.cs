using UnityEngine;
using UnityEngine.InputSystem;

// 오브젝트가 바라보는 방향(로컬 Z축)으로 계속 전진하고, A/D 키로 90도씩 방향을 바꾼다.
public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // 누른 순간 한 번만 회전
            if (keyboard.aKey.wasPressedThisFrame) transform.Rotate(0f, -90f, 0f);
            if (keyboard.dKey.wasPressedThisFrame) transform.Rotate(0f, 90f, 0f);
        }

        transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime, Space.Self);
    }
}

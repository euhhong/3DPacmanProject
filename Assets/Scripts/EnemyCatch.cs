using System;
using UnityEngine;

// 방해꾼이 플레이어에 닿으면 PlayerCaught 이벤트를 알린다. (게임 오버 처리는 GameState가 맡음)
[RequireComponent(typeof(Collider))]
public class EnemyCatch : MonoBehaviour
{
    public static event Action PlayerCaught;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerMove>() == null) return;
        PlayerCaught?.Invoke();
    }
}

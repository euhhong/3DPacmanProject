using System;
using UnityEngine;

// 플레이어가 닿으면 먹히는 코인. 점수 처리는 Collected 이벤트를 받는 쪽(ScoreManager)이 맡는다.
[RequireComponent(typeof(Collider))]
public class Coin : MonoBehaviour
{
    public static event Action<Coin> Collected;

    [SerializeField] private int value = 10;
    [SerializeField] private bool isPowerCoin;

    public int Value => value;
    public bool IsPowerCoin => isPowerCoin;

    public void Setup(int coinValue, bool powerCoin)
    {
        value = coinValue;
        isPowerCoin = powerCoin;
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerMove>() == null) return;

        Collected?.Invoke(this);
        gameObject.SetActive(false);
    }
}

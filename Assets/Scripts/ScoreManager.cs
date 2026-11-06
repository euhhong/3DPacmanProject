using System;
using UnityEngine;

// 점수와 남은 코인 수를 관리하고, 코인을 모두 먹으면 StageCleared를 알린다.
public class ScoreManager : MonoBehaviour
{
    public event Action<int, int> ScoreChanged; // (점수, 남은 코인 수)
    public event Action StageCleared;

    public int Score { get; private set; }
    public int RemainingCoins { get; private set; }

    private void OnEnable() => Coin.Collected += HandleCoinCollected;
    private void OnDisable() => Coin.Collected -= HandleCoinCollected;

    private void Start()
    {
        // 코인은 CoinSpawner의 Awake에서 만들어지므로 Start에서 센다.
        RemainingCoins = FindObjectsByType<Coin>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        ScoreChanged?.Invoke(Score, RemainingCoins);
    }

    private void HandleCoinCollected(Coin coin)
    {
        Score += coin.Value;
        RemainingCoins = Mathf.Max(0, RemainingCoins - 1);
        ScoreChanged?.Invoke(Score, RemainingCoins);

        if (RemainingCoins == 0) StageCleared?.Invoke(); // 게임 정지는 GameState가 처리
    }
}

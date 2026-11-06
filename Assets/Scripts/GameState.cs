using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// 게임의 끝을 관리한다.
// - 코인을 모두 먹으면 클리어, 방해꾼에게 잡히면 게임 오버 → 게임 정지
// - 끝난 뒤 R 키를 누르면 처음부터 다시 시작
public class GameState : MonoBehaviour
{
    public event Action<bool> GameEnded; // true = 클리어, false = 게임 오버

    public bool IsEnded { get; private set; }

    private ScoreManager scoreManager;

    private void OnEnable()
    {
        scoreManager = FindAnyObjectByType<ScoreManager>();
        if (scoreManager != null) scoreManager.StageCleared += HandleStageCleared;
        EnemyCatch.PlayerCaught += HandlePlayerCaught;
    }

    private void OnDisable()
    {
        if (scoreManager != null) scoreManager.StageCleared -= HandleStageCleared;
        EnemyCatch.PlayerCaught -= HandlePlayerCaught;
    }

    private void Update()
    {
        if (!IsEnded) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void HandleStageCleared() => EndGame(true);
    private void HandlePlayerCaught() => EndGame(false);

    private void EndGame(bool cleared)
    {
        if (IsEnded) return;

        IsEnded = true;
        Time.timeScale = 0f;
        GameEnded?.Invoke(cleared);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f; // Play를 멈추거나 씬이 바뀔 때 원래대로
    }
}

using UnityEngine;
using UnityEngine.UI;

// 화면 위에 점수 / 남은 코인 수 / 결과(클리어·게임 오버) 문구를 네온 느낌으로 표시한다.
// 캔버스와 텍스트는 실행 시 코드로 만들어 인스펙터 연결이 필요 없다.
public class ScoreUI : MonoBehaviour
{
    [SerializeField] private Color scoreColor = new Color(1f, 0.85f, 0.4f);
    [SerializeField] private Color infoColor = new Color(0.45f, 0.75f, 1f);
    [SerializeField] private Color gameOverColor = new Color(1f, 0.25f, 0.35f);
    [SerializeField] private int fontSize = 36;

    private ScoreManager scoreManager;
    private GameState gameState;
    private Text scoreText;
    private Text coinsText;
    private Text resultText;
    private Text hintText;

    private void Awake()
    {
        BuildCanvas();
    }

    private void OnEnable()
    {
        scoreManager = FindAnyObjectByType<ScoreManager>();
        gameState = FindAnyObjectByType<GameState>();

        if (scoreManager != null) scoreManager.ScoreChanged += HandleScoreChanged;
        if (gameState != null) gameState.GameEnded += HandleGameEnded;
    }

    private void OnDisable()
    {
        if (scoreManager != null) scoreManager.ScoreChanged -= HandleScoreChanged;
        if (gameState != null) gameState.GameEnded -= HandleGameEnded;
    }

    private void HandleScoreChanged(int score, int remaining)
    {
        scoreText.text = $"SCORE  {score:000000}";
        coinsText.text = $"COINS  {remaining}";
    }

    private void HandleGameEnded(bool cleared)
    {
        resultText.text = cleared ? "STAGE CLEAR!" : "GAME OVER";
        resultText.color = cleared ? scoreColor : gameOverColor;
        resultText.GetComponent<Outline>().effectColor = WithAlpha(resultText.color, 0.35f);

        resultText.gameObject.SetActive(true);
        hintText.gameObject.SetActive(true);
    }

    // ---------- UI 생성 ----------

    private void BuildCanvas()
    {
        var canvasObject = new GameObject("ScoreCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Transform root = canvasObject.transform;
        scoreText = CreateText(root, "Score", scoreColor, fontSize,
            new Vector2(0f, 1f), new Vector2(40f, -30f), TextAnchor.UpperLeft);
        coinsText = CreateText(root, "Coins", infoColor, fontSize,
            new Vector2(1f, 1f), new Vector2(-40f, -30f), TextAnchor.UpperRight);
        resultText = CreateText(root, "Result", scoreColor, fontSize * 3,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), TextAnchor.MiddleCenter);
        hintText = CreateText(root, "Hint", infoColor, fontSize,
            new Vector2(0.5f, 0.5f), new Vector2(0f, -70f), TextAnchor.MiddleCenter);

        scoreText.text = "SCORE  000000";
        coinsText.text = "COINS  -";
        hintText.text = "PRESS  R  TO  RESTART";
        resultText.gameObject.SetActive(false);
        hintText.gameObject.SetActive(false);
    }

    private static Text CreateText(Transform parent, string objectName, Color color, int size,
        Vector2 anchor, Vector2 offset, TextAnchor alignment)
    {
        var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(parent, false);

        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = offset;
        rect.sizeDelta = new Vector2(1200f, size * 1.6f);

        var text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.raycastTarget = false;

        // 글자 주변에 같은 색의 옅은 테두리를 둘러 네온처럼 보이게 한다.
        var outline = textObject.GetComponent<Outline>();
        outline.effectColor = WithAlpha(color, 0.35f);
        outline.effectDistance = new Vector2(2f, -2f);

        return text;
    }

    private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
}

using UnityEngine;
using UnityEngine.UI;

// 화면 위에 점수 / 남은 코인 수 / 클리어 문구를 네온 느낌으로 표시한다.
// 캔버스와 텍스트는 실행 시 코드로 만들어 인스펙터 연결이 필요 없다.
public class ScoreUI : MonoBehaviour
{
    [SerializeField] private Color scoreColor = new Color(1f, 0.85f, 0.4f);
    [SerializeField] private Color infoColor = new Color(0.45f, 0.75f, 1f);
    [SerializeField] private int fontSize = 36;

    private ScoreManager scoreManager;
    private Text scoreText;
    private Text coinsText;
    private Text clearText;

    private void Awake()
    {
        scoreManager = FindAnyObjectByType<ScoreManager>();
        BuildCanvas();
    }

    private void OnEnable()
    {
        if (scoreManager == null) scoreManager = FindAnyObjectByType<ScoreManager>();
        if (scoreManager == null) return;

        scoreManager.ScoreChanged += HandleScoreChanged;
        scoreManager.StageCleared += HandleStageCleared;
    }

    private void OnDisable()
    {
        if (scoreManager == null) return;

        scoreManager.ScoreChanged -= HandleScoreChanged;
        scoreManager.StageCleared -= HandleStageCleared;
    }

    private void HandleScoreChanged(int score, int remaining)
    {
        scoreText.text = $"SCORE  {score:000000}";
        coinsText.text = $"COINS  {remaining}";
    }

    private void HandleStageCleared()
    {
        clearText.gameObject.SetActive(true);
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

        scoreText = CreateText(canvasObject.transform, "Score", scoreColor, fontSize,
            new Vector2(0f, 1f), new Vector2(40f, -30f), TextAnchor.UpperLeft);
        coinsText = CreateText(canvasObject.transform, "Coins", infoColor, fontSize,
            new Vector2(1f, 1f), new Vector2(-40f, -30f), TextAnchor.UpperRight);
        clearText = CreateText(canvasObject.transform, "StageClear", scoreColor, fontSize * 3,
            new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter);

        scoreText.text = "SCORE  000000";
        coinsText.text = "COINS  -";
        clearText.text = "STAGE CLEAR!";
        clearText.gameObject.SetActive(false);
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
        rect.sizeDelta = new Vector2(900f, size * 1.6f);

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
        outline.effectColor = new Color(color.r, color.g, color.b, 0.35f);
        outline.effectDistance = new Vector2(2f, -2f);

        return text;
    }
}

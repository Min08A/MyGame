using UnityEngine;
using UnityEngine.UI;

namespace LabEscape
{
    /// <summary>
    /// Весь интерфейс: стартовый экран, игровой HUD (здоровье, сытость, счёт, время, бусты, цель),
    /// всплывающие сообщения, пауза, экраны победы и поражения.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("Панели")]
        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject gameplayPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject losePanel;

        [Header("Полоски")]
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private RectTransform foodFill;
        [SerializeField] private Text healthText;
        [SerializeField] private Text foodText;

        [Header("Тексты HUD")]
        [SerializeField] private Text cheeseText;
        [SerializeField] private Text catsText;
        [SerializeField] private Text timeText;
        [SerializeField] private Text keycardText;
        [SerializeField] private Text effectsText;
        [SerializeField] private Text zoneText;
        [SerializeField] private Text objectiveText;
        [SerializeField] private Text messageText;

        [Header("Экраны итогов")]
        [SerializeField] private Text winStatsText;
        [SerializeField] private Text loseReasonText;
        [SerializeField] private Text loseStatsText;

        [Header("Кнопки")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseRestartButton;
        [SerializeField] private Button winRestartButton;
        [SerializeField] private Button loseRestartButton;

        private float messageTimer;
        private float messageDuration = 1f;

        private void Awake()
        {
            if (startButton != null) startButton.onClick.AddListener(() => GameManager.Instance.StartGame());
            if (resumeButton != null) resumeButton.onClick.AddListener(() => GameManager.Instance.SetPaused(false));
            if (pauseRestartButton != null) pauseRestartButton.onClick.AddListener(() => GameManager.Instance.Restart());
            if (winRestartButton != null) winRestartButton.onClick.AddListener(() => GameManager.Instance.Restart());
            if (loseRestartButton != null) loseRestartButton.onClick.AddListener(() => GameManager.Instance.Restart());
            if (messageText != null) messageText.text = "";
        }

        private void Update()
        {
            UpdateMessage();

            GameManager gm = GameManager.Instance;
            if (gm == null || gameplayPanel == null || !gameplayPanel.activeSelf) return;

            PlayerStats p = gm.Player;
            if (p != null)
            {
                SetBar(healthFill, p.Health / p.MaxHealth);
                SetBar(foodFill, p.Food / p.MaxFood);
                if (healthText != null) healthText.text = $"Здоровье  {Mathf.CeilToInt(p.Health)} / {p.MaxHealth:0}";
                if (foodText != null)
                    foodText.text = p.Food < 20f
                        ? $"<color=#FFB0B0>Сытость  {Mathf.CeilToInt(p.Food)} — ГОЛОДНО!</color>"
                        : $"Сытость  {Mathf.CeilToInt(p.Food)} / {p.MaxFood:0}";
                if (effectsText != null) effectsText.text = p.ActiveEffectsDescription;
            }

            if (cheeseText != null) cheeseText.text = $"Сыр: {gm.CheeseCollected}";
            if (catsText != null) catsText.text = $"Обезврежено котов: {gm.CatsDefeated}";
            if (timeText != null) timeText.text = $"Время: {GameManager.FormatTime(gm.ElapsedTime)}";
            if (keycardText != null)
                keycardText.text = gm.HasKeycard
                    ? "<color=#5CE1FF>Ключ-карта: ЕСТЬ</color>"
                    : "Ключ-карта: нет";
            if (zoneText != null) zoneText.text = gm.CurrentZone;
            if (objectiveText != null) objectiveText.text = gm.Objective;
        }

        private static void SetBar(RectTransform fill, float value)
        {
            if (fill == null) return;
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        }

        // ---------- Сообщения ----------

        public void ShowMessage(string text, float duration)
        {
            if (messageText == null) return;
            messageText.text = text;
            messageDuration = Mathf.Max(0.1f, duration);
            messageTimer = messageDuration;
        }

        private void UpdateMessage()
        {
            if (messageText == null || messageTimer <= 0f) return;
            messageTimer -= Time.unscaledDeltaTime;
            Color c = messageText.color;
            c.a = Mathf.Clamp01(messageTimer / 0.5f); // плавное исчезновение в последние полсекунды
            messageText.color = c;
            if (messageTimer <= 0f) messageText.text = "";
        }

        // ---------- Экраны ----------

        public void ShowStart()
        {
            SetPanels(start: true, gameplay: false, pause: false, win: false, lose: false);
        }

        public void ShowGameplay()
        {
            SetPanels(start: false, gameplay: true, pause: false, win: false, lose: false);
        }

        public void ShowPause(bool show)
        {
            if (pausePanel != null) pausePanel.SetActive(show);
        }

        public void ShowWin(string stats)
        {
            SetPanels(start: false, gameplay: false, pause: false, win: true, lose: false);
            if (winStatsText != null) winStatsText.text = stats;
        }

        public void ShowLose(string reason, string stats)
        {
            SetPanels(start: false, gameplay: false, pause: false, win: false, lose: true);
            if (loseReasonText != null) loseReasonText.text = reason;
            if (loseStatsText != null) loseStatsText.text = stats;
        }

        private void SetPanels(bool start, bool gameplay, bool pause, bool win, bool lose)
        {
            if (startPanel != null) startPanel.SetActive(start);
            if (gameplayPanel != null) gameplayPanel.SetActive(gameplay);
            if (pausePanel != null) pausePanel.SetActive(pause);
            if (winPanel != null) winPanel.SetActive(win);
            if (losePanel != null) losePanel.SetActive(lose);
        }
    }
}

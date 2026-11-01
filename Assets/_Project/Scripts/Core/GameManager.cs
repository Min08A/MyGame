using UnityEngine;
using UnityEngine.SceneManagement;

namespace LabEscape
{
    public enum GameState { Menu, Playing, Paused, Won, Lost }

    /// <summary>
    /// Главный менеджер игры: хранит состояние (меню, игра, пауза, победа, поражение),
    /// счёт и прогресс (сыр, обезвреженные коты, ключ-карта), управляет временем и перезапуском.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>Удобная проверка «идёт ли сейчас игра» для всех остальных скриптов.</summary>
        public static bool IsPlaying => Instance != null && Instance.State == GameState.Playing;

        [Header("Ссылки")]
        [SerializeField] private PlayerStats player;
        [SerializeField] private HUD hud;

        [Header("Правила")]
        [Tooltip("Если мышь упадёт ниже этой высоты — поражение")]
        [SerializeField] private float fallLimitY = -10f;

        public GameState State { get; private set; } = GameState.Menu;
        public int CheeseCollected { get; private set; }
        public int CatsDefeated { get; private set; }
        public bool HasKeycard { get; private set; }
        public bool BossDefeated { get; private set; }
        public float ElapsedTime { get; private set; }
        public string CurrentZone { get; set; } = "Виварий";
        public PlayerStats Player => player;

        /// <summary>Текущая цель игрока — показывается в верхней части экрана.</summary>
        public string Objective
        {
            get
            {
                if (HasKeycard) return "Ключ-карта у тебя! Беги к вентиляционной двери в конце лаборатории";
                if (BossDefeated) return "Барсик повержен — подбери ключ-карту, которую он уронил";
                return "Цель: сбеги из лаборатории. Ключ-карту от выхода носит кот-охранник Барсик";
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (player == null) player = FindAnyObjectByType<PlayerStats>();
            if (hud == null) hud = FindAnyObjectByType<HUD>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            EnterMenu();
        }

        private void Update()
        {
            switch (State)
            {
                case GameState.Menu:
                    if (GameInput.ConfirmPressed) StartGame();
                    break;

                case GameState.Playing:
                    ElapsedTime += Time.deltaTime;
                    if (GameInput.PausePressed)
                        SetPaused(true);
                    else if (player != null && player.transform.position.y < fallLimitY)
                        Lose("Мышь сорвалась в бездну...");
                    break;

                case GameState.Paused:
                    if (GameInput.PausePressed) SetPaused(false);
                    else if (GameInput.RestartPressed) Restart();
                    break;

                case GameState.Won:
                case GameState.Lost:
                    if (GameInput.RestartPressed || GameInput.ConfirmPressed) Restart();
                    break;
            }
        }

        private void EnterMenu()
        {
            State = GameState.Menu;
            Time.timeScale = 0f;
            SetCursorLocked(false);
            if (hud != null) hud.ShowStart();
        }

        public void StartGame()
        {
            if (State != GameState.Menu) return;
            State = GameState.Playing;
            Time.timeScale = 1f;
            SetCursorLocked(true);
            if (hud != null) hud.ShowGameplay();
            ShowMessage("Двигайся на WASD, крути камеру мышью. Найди выход из вивария!", 5f);
        }

        public void SetPaused(bool paused)
        {
            if (paused && State == GameState.Playing)
            {
                State = GameState.Paused;
                Time.timeScale = 0f;
                SetCursorLocked(false);
                if (hud != null) hud.ShowPause(true);
            }
            else if (!paused && State == GameState.Paused)
            {
                State = GameState.Playing;
                Time.timeScale = 1f;
                SetCursorLocked(true);
                if (hud != null) hud.ShowPause(false);
            }
        }

        public void Win()
        {
            if (State != GameState.Playing) return;
            State = GameState.Won;
            Time.timeScale = 0f;
            SetCursorLocked(false);
            if (hud != null) hud.ShowWin(BuildStatsText());
        }

        public void Lose(string reason)
        {
            if (State != GameState.Playing) return;
            State = GameState.Lost;
            Time.timeScale = 0f;
            SetCursorLocked(false);
            if (hud != null) hud.ShowLose(reason, BuildStatsText());
        }

        /// <summary>Перезапуск уровня — сцена загружается заново, всё сбрасывается.</summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void AddCheese(int amount)
        {
            CheeseCollected += amount;
        }

        public void RegisterCatDefeated(bool isBoss, string catName)
        {
            CatsDefeated++;
            if (isBoss)
            {
                BossDefeated = true;
                ShowMessage($"{catName} повержен! Он уронил ключ-карту — подбери её!", 4f);
            }
            else
            {
                ShowMessage($"{catName} обезврежен!", 2f);
            }
        }

        public void GiveKeycard()
        {
            HasKeycard = true;
        }

        public void ShowMessage(string text, float duration = 2.5f)
        {
            if (hud != null) hud.ShowMessage(text, duration);
        }

        private string BuildStatsText()
        {
            return $"Время: {FormatTime(ElapsedTime)}\n" +
                   $"Собрано сыра: {CheeseCollected}\n" +
                   $"Обезврежено котов: {CatsDefeated}";
        }

        public static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:00}:{s:00}";
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}

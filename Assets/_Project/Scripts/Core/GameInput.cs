using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Единая точка чтения ввода. Все скрипты спрашивают клавиши только здесь,
    /// поэтому управление легко поменять в одном месте.
    /// Используется классический Input Manager (Project Settings → Player → Active Input Handling).
    /// </summary>
    public static class GameInput
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        /// <summary>Направление движения: WASD или стрелки. X — влево/вправо, Y — вперёд/назад.</summary>
        public static Vector2 Move
        {
            get
            {
                float x = 0f, y = 0f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public static bool Run => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        public static bool JumpPressed => Input.GetKeyDown(KeyCode.Space);
        public static bool FirePressed => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F);
        public static Vector2 Look => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        public static float Zoom => Input.mouseScrollDelta.y;
        public static bool PausePressed => Input.GetKeyDown(KeyCode.Escape);
        public static bool RestartPressed => Input.GetKeyDown(KeyCode.R);
        public static bool ConfirmPressed => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
        // Если в проекте выключен старый Input Manager, игра не упадёт,
        // но управление работать не будет. Включите "Input Manager (Old)" или "Both"
        // в Project Settings → Player → Active Input Handling.
        public static Vector2 Move => Vector2.zero;
        public static bool Run => false;
        public static bool JumpPressed => false;
        public static bool FirePressed => false;
        public static Vector2 Look => Vector2.zero;
        public static float Zoom => 0f;
        public static bool PausePressed => false;
        public static bool RestartPressed => false;
        public static bool ConfirmPressed => false;
#endif
    }
}

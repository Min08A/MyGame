using System.IO;
using UnityEditor;
using UnityEngine;

namespace LabEscape.EditorTools
{
    /// <summary>
    /// Запускается при открытии проекта:
    /// 1) включает классический Input Manager, если он выключен (нужен для управления);
    /// 2) если сцена уровня ещё не собрана — собирает её автоматически.
    /// </summary>
    [InitializeOnLoad]
    internal static class LabEscapeAutoSetup
    {
        private const string SessionKey = "LabEscape_AutoBuildTried";

        static LabEscapeAutoSetup()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            if (EnsureLegacyInputEnabled()) return; // ждём перезапуска редактора

            if (File.Exists(LabEscapeBuilder.ScenePath)) return;
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);

            Debug.Log("[Lab Escape] Сцена не найдена — собираю игру автоматически...");
            LabEscapeBuilder.BuildAll();
        }

        /// <returns>true, если настройка изменена и нужен перезапуск Unity</returns>
        private static bool EnsureLegacyInputEnabled()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return false;
#else
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return false;

            var so = new SerializedObject(assets[0]);
            SerializedProperty prop = so.FindProperty("activeInputHandler");
            if (prop == null) return false;

            prop.intValue = 2; // 0 = Input Manager (Old), 1 = Input System (New), 2 = Both
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            if (EditorUtility.DisplayDialog("Lab Escape",
                    "Для управления игрой включён классический Input Manager " +
                    "(Project Settings → Player → Active Input Handling = Both).\n\n" +
                    "Чтобы настройка применилась, Unity нужно перезапустить. Перезапустить сейчас?",
                    "Перезапустить", "Позже"))
            {
                EditorApplication.OpenProject(Directory.GetCurrentDirectory());
            }
            return true;
#endif
        }
    }
}

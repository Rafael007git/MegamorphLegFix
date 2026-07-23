using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MMorphFix
{
    [BepInPlugin("com.lorifel007.megamorphlegfix", "Megamorph Mod Fix", "1.0.0")]
    public class MMorphFixPlugin : BaseUnityPlugin
    {
        public static string DebugMonitorText = "Инициализация...";
        public static int TotalFramesCount = 0;
        public static int FixExecutionCalls = 0;
        public static string LastFoundCharacter = "Никого";
        public static string LeftLegTelemetry = "L: Ожидание суккуба...";
        public static string RightLegTelemetry = "R: Ожидание суккуба...";

        public static ManualLogSource InstanceLogger;

        private void Awake()
        {
            InstanceLogger = Logger;

            // 👇 ИНИЦИАЛИЗАЦИЯ НАСТРОЕК: Передаем встроенный объект Config в наш новый менеджер
            ModConfigManager.Initialize(Config);

            InstanceLogger.LogInfo("=== [LegFix] Запуск каскадного щита 6 суставов (v0.7.5) ===");

            var harmony = new Harmony("com.lorifel007.megamorphlegfix");

            // --- ХУК 1: Патч редактора Мегаморфа (DoUpdate) ---
            Type megaMorphType = AccessTools.TypeByName("MegaMorph.BepInExPlugin+MegaMorph")
                                 ?? AccessTools.TypeByName("MegaMorph.MegaMorph")
                                 ?? AccessTools.TypeByName("MegaMorph");
            if (megaMorphType != null)
            {
                var original = AccessTools.Method(megaMorphType, "DoUpdate");
                var postfix = AccessTools.Method(typeof(MegamorphDoUpdatePatch), "Postfix");
                if (original != null && postfix != null)
                {
                    harmony.Patch(original, null, new HarmonyMethod(postfix));
                    InstanceLogger.LogInfo("[LegFix] Хук MegaMorph.DoUpdate успешно установлен.");
                }
            }

            // --- ХУК 2: Восстановление FreePose (Патчим реальный Update из TransformGizmo) ---
            Type gizmoType = AccessTools.TypeByName("RuntimeGizmos.TransformGizmo")
                             ?? AccessTools.TypeByName("TransformGizmo");
            if (gizmoType != null)
            {
                // Точка А: Живой радар при движении мыши во FreePose
                var originalUpdate = AccessTools.Method(gizmoType, "Update");
                var postfixUpdate = AccessTools.Method(typeof(FreePoseGizmoUpdatePatch), "Postfix");
                if (originalUpdate != null && postfixUpdate != null)
                {
                    harmony.Patch(originalUpdate, null, new HarmonyMethod(postfixUpdate));
                    InstanceLogger.LogInfo("[LegFix] Хук TransformGizmo.Update (Радар) установлен.");
                }

                // Точка Б: Коррекция меша в FixedUpdate сразу ПОСЛЕ ручного разворота костей
                var originalFixed = AccessTools.Method(gizmoType, "FixedUpdate");
                var postfixFixed = AccessTools.Method(typeof(FreePoseGizmoFixedUpdatePatch), "Postfix");
                if (originalFixed != null && postfixFixed != null)
                {
                    harmony.Patch(originalFixed, null, new HarmonyMethod(postfixFixed));
                    InstanceLogger.LogInfo("[LegFix] Хук TransformGizmo.FixedUpdate (Фикс) установлен.");
                }
            }
            else
            {
                InstanceLogger.LogError("[LegFix] КРИТИЧЕСКАЯ ОШИБКА: Класс TransformGizmo не найден в игре!");
            }

            // --- ХУК 3: Сквозной автоматический контроль (LateUpdate и Смена Поз) ---
            try
            {
                harmony.PatchAll();
                DebugMonitorText = "Все матричные барьеры возведены. Ожидание фазы обсчета...";
            }
            catch (Exception ex)
            {
                InstanceLogger.LogError($"[LegFix] Ошибка PatchAll: {ex}");
                DebugMonitorText = $"Ошибка Harmony PatchAll: {ex.Message}";
            }
        }

        private void Update()
        {
            TotalFramesCount++;
        }

        // Глобальный флаг для оперативного включения/выключения защиты
        public static bool IsFixEnabled = true;

        private void OnGUI()
        {
            // Высота окна 350 пикселей идеально вмещает обе кнопки управления
            UnityEngine.GUI.Box(new UnityEngine.Rect(10, 10, 650, 350), "Leg Fix REALTIME DIAGNOSTIC (Стабильный Билд 0.8.2)");

            UnityEngine.GUIStyle labelStyle = new UnityEngine.GUIStyle(UnityEngine.GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = UnityEngine.FontStyle.Bold,
                alignment = UnityEngine.TextAnchor.UpperLeft
            };
            labelStyle.normal.textColor = UnityEngine.Color.green;

            // КНОПКА 1: Переключение состояния матричного щита (слева)
            string buttonShieldText = IsFixEnabled ? "❌ ОТКЛЮЧИТЬ МАТРИЧНЫЙ ЩИТ" : "✅ ВКЛЮЧИТЬ МАТРИЧНЫЙ ЩИТ";
            if (UnityEngine.GUI.Button(new UnityEngine.Rect(20, 35, 250, 25), buttonShieldText))
            {
                IsFixEnabled = !IsFixEnabled;
                if (InstanceLogger != null)
                {
                    InstanceLogger.LogInfo($"[LegFix] Переключено состояние щита. Активен: {IsFixEnabled}");
                }
            }

            // КНОПКА 2: Копирование телеметрии в буфер обмена Windows (справа, сдвинута на Rect X=280)
            if (UnityEngine.GUI.Button(new UnityEngine.Rect(280, 35, 180, 25), "📋 СКОПИРОВАТЬ ДАННЫЕ"))
            {
                string textToCopy =
                    $"=== [MegamorphLegFix ТЕЛЕМЕТРИЯ] ===\n" +
                    $"Кадры: {TotalFramesCount} | Инверсий суставов: {FixExecutionCalls}\n" +
                    $"Режим защиты: {(IsFixEnabled ? "АКТИВЕН" : "ВЫКЛЮЧЕН")}\n" +
                    $"Персонаж: {LastFoundCharacter}\n" +
                    $"Статус: {DebugMonitorText}\n" +
                    $"--------------------------------------------------\n" +
                    $"{LeftLegTelemetry}\n" +
                    $"--------------------------------------------------\n" +
                    $"{RightLegTelemetry}\n" +
                    $"====================================";

                // Запись собранного текста в системный буфер обмена Unity/Windows
                UnityEngine.GUIUtility.systemCopyBuffer = textToCopy;

                // Меняем статусную строку, чтобы мгновенно подтвердить клик игроку
                DebugMonitorText = "Данные радара успешно скопированы в буфер обмена!";

                if (InstanceLogger != null)
                {
                    InstanceLogger.LogInfo("[LegFix] Телеметрия скопирована через GUI-кнопку.");
                }
            }

            string systemStatus =
                $"[СИСТЕМА] Кадры: {TotalFramesCount} | Инверсий суставов: {FixExecutionCalls}\n" +
                $"[РЕЖИМ ЗАЩИТЫ] {(IsFixEnabled ? "АКТИВЕН (Меш защищен)" : "ВЫКЛЮЧЕН (Оригинальный Мегаморф)")}\n" +
                $"[ПЕРСОНАЖ] Активный ID: {LastFoundCharacter}\n" +
                $"[СТАТУС] {DebugMonitorText}\n" +
                $"------------------------------------------------------------------------------------------------------\n" +
                $"{LeftLegTelemetry}\n" +
                $"------------------------------------------------------------------------------------------------------\n" +
                $"{RightLegTelemetry}";

            // Текст радара отрисовывается чуть ниже элементов управления
            UnityEngine.GUI.Label(new UnityEngine.Rect(20, 70, 630, 270), systemStatus, labelStyle);
        }


    }

    public static class MegamorphDoUpdatePatch
    {
        public static void Postfix(MonoBehaviour __instance)
        {
            if (__instance == null) return;
            CharacterCustomization cc = AccessTools.Field(__instance.GetType(), "cc")?.GetValue(__instance) as CharacterCustomization;
            if (cc != null) LegEqualizerCore.ExecuteFix(cc, "МЕГАМОРФ");
        }
    }

    // Новый рабочий патч для FreePose-радара
    public static class FreePoseGizmoUpdatePatch
    {
        public static void Postfix(MonoBehaviour __instance)
        {
            // Проверяем флаг работы гизмо из исходника (runTransformGizmo)
            bool isRunning = (bool)AccessTools.Field(__instance.GetType(), "runTransformGizmo")?.GetValue(__instance);

            if (isRunning && Global.code != null && Global.code.uiFreePose != null && Global.code.uiFreePose.selectedCharacter)
            {
                CharacterCustomization cc = Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>();
                if (cc != null) LegEqualizerCore.ExecuteFix(cc, "ГЛИФ_РАДАР");
            }
        }
    }

    // Фикс деформации сразу после применения ручной позы в FixedUpdate
    public static class FreePoseGizmoFixedUpdatePatch
    {
        public static void Postfix(MonoBehaviour __instance)
        {
            bool isRunning = (bool)AccessTools.Field(__instance.GetType(), "runTransformGizmo")?.GetValue(__instance);

            if (isRunning && Global.code != null && Global.code.uiFreePose != null && Global.code.uiFreePose.selectedCharacter)
            {
                CharacterCustomization cc = Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>();
                if (cc != null) LegEqualizerCore.ExecuteFix(cc, "ГЛИФ_ФИКС");
            }
        }
    }

    [HarmonyPatch(typeof(CharacterCustomization), "LateUpdate")]
    public static class GameLateUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(CharacterCustomization __instance)
        {
            if (__instance == null) return;

            bool isFreePoseActive = Global.code != null && Global.code.uiFreePose != null && Global.code.uiFreePose.isActiveAndEnabled;
            if (!isFreePoseActive)
            {
                // Вместо мгновенного вызова запускаем корутину ожидания конца кадра,
                // чтобы намертво перебить запеченные масштабы из анимации мебели (Furniture)
                __instance.StartCoroutine(RunFixAtEndOfFrame(__instance));
            }
        }

        private static System.Collections.IEnumerator RunFixAtEndOfFrame(CharacterCustomization cc)
        {
            // Ждем, пока отработают ВСЕ аниматоры, физика мебели и скиннинг Unity в этом кадре
            yield return new UnityEngine.WaitForEndOfFrame();

            if (cc != null)
            {
                // Наносим финальный матричный удар, когда кости уже никто не сможет перезаписать
                LegEqualizerCore.ExecuteFix(cc, "КОНЕЦ_КАДРА_МЕБЕЛЬ");

                // 👇 ВНЕДРЯЕМ СЮДА
                // Передаем корневой GameObject персонажа для независимого и легкого контроля огоньков
                if (cc.gameObject != null)
                {
                    AnatomicalLightFix.MonitorAndExecute(cc.gameObject);
                }
            }
        }
    }


    // ХУК: Перехватываем клик по любой встроенной позе из коллекции игры
    [HarmonyPatch(typeof(UIFreePose), "PoseButtonClicked")]
    public static class GamePoseChangeTriggerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(global::Pose code, UIFreePose __instance)
        {
            // Вытаскиваем текущего выбранного персонажа из интерфейса
            if (__instance != null && __instance.selectedCharacter != null)
            {
                CharacterCustomization cc = __instance.selectedCharacter.GetComponent<CharacterCustomization>();
                if (cc != null)
                {
                    // Принудительно встряхиваем и выпрямляем каскад ног в момент смены позы
                    LegEqualizerCore.ExecuteFix(cc, "КОЛЛЕКЦИЯ_ПОЗ");
                }
            }
        }
    }

    // ХУК: Перехватываем применение сохраненных пользователем позо-файлов
    [HarmonyPatch(typeof(LoadPoseIcon), "Click")] // Или альтернативный метод клика по кастомной позе
    public static class CustomPoseLoadTriggerPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            // Если открыто меню свободных поз, находим персонажа и мгновенно лечим его скелет
            if (Global.code != null && Global.code.uiFreePose != null && Global.code.uiFreePose.selectedCharacter)
            {
                CharacterCustomization cc = Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>();
                if (cc != null)
                {
                    LegEqualizerCore.ExecuteFix(cc, "КАСТОМ_ПОЗА");
                }
            }
        }
    }
}

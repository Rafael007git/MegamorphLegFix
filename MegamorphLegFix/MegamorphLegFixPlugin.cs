extern alias MM;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using static MM::MegaMorph.BepInExPlugin;

namespace MMorphFix
{
    [BepInPlugin("com.lorifel007.megamorphlegfix", "Megamorph Mod Fix", "1.1.0")]
    public class MMorphFixPlugin : BaseUnityPlugin
    {
        public static string DebugMonitorText = "Инициализация...";
        public static int TotalFramesCount = 0;
        public static int FixExecutionCalls = 0;
        public static string LastFoundCharacter = "Никого";
        public static string LeftLegTelemetry = "L: Ожидание суккуба...";
        public static string RightLegTelemetry = "R: Ожидание суккуба...";
        // ПЕРЕМЕННАЯ ДЛЯ ОТСЛЕЖИВАНИЯ СОСТОЯНИЯ ГИЗМО ВО FREEPOSE
        public static bool WasTransformingLastFrame = false;

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
            // Если радар выключен в меню F1 — ничего не рисуем!
            if (!ModConfigManager.ShowRadar) return;

            // Увеличиваем высоту окна до 450, чтобы влез диагностический блок света
            UnityEngine.GUI.Box(new UnityEngine.Rect(10, 10, 650, 450), "Leg and Light Fix REALTIME DIAGNOSTIC (v 1.1.0)");

            UnityEngine.GUIStyle labelStyle = new UnityEngine.GUIStyle(UnityEngine.GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = UnityEngine.FontStyle.Bold,
                alignment = UnityEngine.TextAnchor.UpperLeft
            };
            labelStyle.normal.textColor = UnityEngine.Color.green;

            // КНОПКА 1: Переключение состояния матричного щита (слева)
            string buttonShieldText = IsFixEnabled ? "❌ DISABLE MATRIX FIX" : "✅ ENABLE MATRIX FIX";
            if (UnityEngine.GUI.Button(new UnityEngine.Rect(20, 35, 250, 25), buttonShieldText))
            {
                IsFixEnabled = !IsFixEnabled;
                if (InstanceLogger != null)
                {
                    InstanceLogger.LogInfo($"[LegFix] State toggled. Currently: {IsFixEnabled}");
                }
            }

            // КНОПКА 2: Копирование телеметрии в буфер обмена Windows
            if (UnityEngine.GUI.Button(new UnityEngine.Rect(280, 35, 180, 25), "📋 COPY TELEMETRY DATA"))
            {
                string textToCopy =
                    $"=== [MegamorphLegFix TELEMETRY] ===\n" +
                    $"Frames: {TotalFramesCount} | Joint Inversions: {FixExecutionCalls}\n" +
                    $"Protection mode: {(IsFixEnabled ? "Active" : "Disabled")}\n" +
                    $"Character: {LastFoundCharacter}\n" +
                    $"Status: {DebugMonitorText}\n" +
                    $"--------------------------------------------------\n" +
                    $"{LeftLegTelemetry}\n" +
                    $"--------------------------------------------------\n" +
                    $"{RightLegTelemetry}\n" +
                    $"=== [AnatomicalLightFix Telemetry] ===\n" +
                    $"Lights Status: {AnatomicalLightFix.Radar_Status}\n" +
                    $"Distance scale: {AnatomicalLightFix.Radar_HipScaleY:F4}\n" +
                    $"Distance scale: {AnatomicalLightFix.Radar_HipLossyScaleY:F4}\n" +
                    $"Light 1 Intensity: {AnatomicalLightFix.Radar_Light1_Intensity:F2}\n" +
                    $"Light 5 Intensity: {AnatomicalLightFix.Radar_Light5_Intensity:F2}\n" +
                    $"Light 9 Intensity: {AnatomicalLightFix.Radar_Light9_Intensity:F2}\n" +
                    $"====================================";

                UnityEngine.GUIUtility.systemCopyBuffer = textToCopy;
                DebugMonitorText = "Radar telemetry successfully copied to clipboard!";
            }

            // Базовый статус ног
            string systemStatus =
                $"[System] FPS: {TotalFramesCount} | Joint Inversions: {FixExecutionCalls}\n" +
                $"[Protection Mode] {(IsFixEnabled ? "ACTIVE (Mesh protected)" : "DISABLED (Original MegaMorph)")}\n" +
                $"[ПЕРСОНАЖ] Активный ID: {LastFoundCharacter}\n" +
                $"[СТАТУС] {DebugMonitorText}\n" +
                $"------------------------------------------------------------------------------------------------------\n" +
                $"{LeftLegTelemetry}\n" +
                $"------------------------------------------------------------------------------------------------------\n" +
                $"{RightLegTelemetry}";

            // Отрисовка статуса ног
            UnityEngine.GUI.Label(new UnityEngine.Rect(20, 70, 630, 240), systemStatus, labelStyle);

            // СВЕЖИЙ БЛОК: Выводим данные по огонькам в самый низ радара (Rect Y = 320)
            string lightStatusText =
                $"----------------------------------------------- SKELETAL LIGHTS (FreePose) -----------------------------------------------\n" +
                $"[LIGHTS STATUS] {AnatomicalLightFix.Radar_Status}\n" +
                $"[HIP BONE SCALE] Local Y: {AnatomicalLightFix.Radar_HipScaleY:F4}  |  Global (World) Y: {AnatomicalLightFix.Radar_HipLossyScaleY:F4}\n" +
                $"[CURRENT INTENSITY] Light(1): {AnatomicalLightFix.Radar_Light1_Intensity:F2}  |  Light(5): {AnatomicalLightFix.Radar_Light5_Intensity:F2}  |  Light(9): {AnatomicalLightFix.Radar_Light9_Intensity:F2}";

            // Создаем отдельный стиль для огней (желтый цвет, чтобы визуально отделить блок в интерфейсе)
            UnityEngine.GUIStyle lightLabelStyle = new UnityEngine.GUIStyle(labelStyle);
            lightLabelStyle.normal.textColor = UnityEngine.Color.yellow;

            UnityEngine.GUI.Label(new UnityEngine.Rect(20, 320, 630, 120), lightStatusText, lightLabelStyle);
        }

    }

    // --- ОБЪЕДИНЕННЫЕ КЛАССЫ ПАТЧЕЙ (БЕЗ ДУБЛИКАТОВ ИМЕН) ---

    public static class MegamorphDoUpdatePatch
    {
        public static void Postfix(MonoBehaviour __instance)
        {
            if (__instance == null) return;

            CharacterCustomization cc = AccessTools.Field(__instance.GetType(), "cc")?.GetValue(__instance) as CharacterCustomization;
            if (cc != null)
            {
                // 1. Стабильный фикс деформации ног Мегаморфа
                LegEqualizerCore.ExecuteFix(cc, "МЕГАМОРФ");

                // 2. Реактивный фикс огоньков при смене слайдеров / пресетов
                AnatomicalLightFix.ExecuteFix(cc.gameObject);
            }
        }
    }

    public static class FreePoseGizmoUpdatePatch
    {
        public static void Postfix(MonoBehaviour __instance)
        {
            if (__instance == null) return;

            // Проверяем флаг работы гизмо из исходника игры
            bool isRunning = (bool)AccessTools.Field(__instance.GetType(), "runTransformGizmo")?.GetValue(__instance);

            // Считываем флаг трансформации (тянет ли игрок мышку прямо сейчас)
            // Мы безопасно вытаскиваем свойство isTransforming через рефлексию
            bool currentlyTransforming = false;
            var prop = __instance.GetType().GetProperty("isTransforming", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop != null)
            {
                currentlyTransforming = (bool)prop.GetValue(__instance, null);
            }

            if (isRunning && Global.code != null && Global.code.uiFreePose != null && Global.code.uiFreePose.selectedCharacter)
            {
                CharacterCustomization cc = Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>();
                if (cc != null)
                {
                    // ТОЧКА А: Живой радар ног продолжает работать при движении мыши
                    LegEqualizerCore.ExecuteFix(cc, "ГЛИФ_РАДАР");

                    // ТОЧКА Б: Если в прошлом кадре крутили гизмо, а в этом ОТПУСТИЛИ мышку
                    if (MMorphFixPlugin.WasTransformingLastFrame && !currentlyTransforming && cc.gameObject != null)
                    {
                        // Обновляем свет ровно 1 раз по окончании движения гизмо!
                        AnatomicalLightFix.ExecuteFix(cc.gameObject);
                    }
                }
            }

            // Запоминаем состояние флага для следующего кадра
            MMorphFixPlugin.WasTransformingLastFrame = currentlyTransforming;
        }
    }

    public static class FreePoseGizmoFixedUpdatePatch
    {
        public static void Postfix(MonoBehaviour __instance)
        {
            if (__instance == null) return;

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
                // Стабильный фоновый фикс ног в постели / на мебели
                __instance.StartCoroutine(RunFixAtEndOfFrame(__instance));
            }
        }

        private static System.Collections.IEnumerator RunFixAtEndOfFrame(CharacterCustomization cc)
        {
            yield return new UnityEngine.WaitForEndOfFrame();
            if (cc != null)
            {
                LegEqualizerCore.ExecuteFix(cc, "КОНЕЦ_КАДРА_МЕБЕЛЬ");
            }
        }
    }

    [HarmonyPatch(typeof(UIFreePose), "PoseButtonClicked")]
    public static class GamePoseChangeTriggerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(global::Pose code, UIFreePose __instance)
        {
            if (__instance != null && __instance.selectedCharacter != null)
            {
                CharacterCustomization cc = __instance.selectedCharacter.GetComponent<CharacterCustomization>();
                if (cc != null)
                {
                    LegEqualizerCore.ExecuteFix(cc, "КОЛЛЕКЦИЯ_ПОЗ");
                    // Принудительно встряхиваем свет при выборе готовой позы из коллекции
                    AnatomicalLightFix.ExecuteFix(cc.gameObject);
                }
            }
        }
    }

    [HarmonyPatch(typeof(LoadPoseIcon), "Click")]
    public static class CustomPoseLoadTriggerPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (Global.code != null && Global.code.uiFreePose != null && Global.code.uiFreePose.selectedCharacter)
            {
                CharacterCustomization cc = Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>();
                if (cc != null)
                {
                    LegEqualizerCore.ExecuteFix(cc, "КАСТОМ_ПОЗА");
                    // Принудительно встряхиваем свет при загрузке кастомного позо-файла
                    AnatomicalLightFix.ExecuteFix(cc.gameObject);
                }
            }
        }
    }
}

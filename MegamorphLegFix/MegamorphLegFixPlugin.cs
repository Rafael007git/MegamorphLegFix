using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MMorphFix
{
    [BepInPlugin("com.lorifel007.megamorphlegfix", "Megamorph Mod Fix", "0.7.5")]
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

        private void OnGUI()
        {
            UnityEngine.GUI.Box(new UnityEngine.Rect(10, 10, 650, 320), "Leg Fix REALTIME DIAGNOSTIC (Стабильный Билд 0.7.5)");

            UnityEngine.GUIStyle labelStyle = new UnityEngine.GUIStyle(UnityEngine.GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = UnityEngine.FontStyle.Bold,
                alignment = UnityEngine.TextAnchor.UpperLeft
            };
            labelStyle.normal.textColor = UnityEngine.Color.green;

            string systemStatus =
                $"[СИСТЕМА] Кадры: {TotalFramesCount} | Инверсий суставов: {FixExecutionCalls}\n" +
                $"[ПЕРСОНАЖ] Активный ID: {LastFoundCharacter}\n" +
                $"[СТАТУС] {DebugMonitorText}\n" +
                $"------------------------------------------------------------------------------------------------------\n" +
                $"{LeftLegTelemetry}\n" +
                $"------------------------------------------------------------------------------------------------------\n" +
                $"{RightLegTelemetry}";

            UnityEngine.GUI.Label(new UnityEngine.Rect(20, 35, 630, 280), systemStatus, labelStyle);
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

            // Если активен FreePose, обычный LateUpdate игнорируем, так как для него работают выделенные патчи гизмо выше
            bool isFreePoseActive = Global.code != null && Global.code.uiFreePose != null && Global.code.uiFreePose.isActiveAndEnabled;
            if (!isFreePoseActive)
            {
                LegEqualizerCore.ExecuteFix(__instance, "ИГРОВОЙ_МИР");
            }
        }
    }

    // Дополнительный хук для отлавливания смены поз из коллекции игры (галереи анимаций)
    [HarmonyPatch(typeof(CharacterCustomization), "SetActivePose")] // Предположительное имя метода смены позы в игре
    public static class GamePoseChangeTriggerPatch
    {
        [HarmonyPostfix]
        public static void Postfix(CharacterCustomization __instance)
        {
            if (__instance != null)
            {
                // Принудительно встряхиваем каскад в момент клика по новой позе
                LegEqualizerCore.ExecuteFix(__instance, "КОЛЛЕКЦИЯ_ПОЗ");
            }
        }
    }
}

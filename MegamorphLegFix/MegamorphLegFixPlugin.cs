using System;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MMorphFix
{
    [BepInPlugin("com.lorifel007.megamorphlegfix", "Megamorph Mod Fix", "0.6.0")]
    public class MMorphFixPlugin : BaseUnityPlugin
    {
        public static string DebugMonitorText = "Инициализация...";
        public static int TotalFramesCount = 0;
        public static int FixExecutionCalls = 0;
        public static string LastFoundCharacter = "Никого";
        public static string LeftLegTelemetry = "L: Ожидание суккуба...";
        public static string RightLegTelemetry = "R: Ожидание суккуба...";

        // Глобальная ссылка на логгер для вывода в консоль BepInEx
        public static ManualLogSource InstanceLogger;

        private void Awake()
        {
            InstanceLogger = Logger;
            InstanceLogger.LogInfo("=== [LegFix] Запуск системы глубокого мониторинга скелета ===");

            var harmony = new Harmony("com.lorifel007.megamorphlegfix");

            // Хук 1: Патч редактора Мегаморфа (DoUpdate)
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
                    InstanceLogger.LogInfo("[LegFix] Хук DoUpdate Мегаморфа успешно установлен.");
                }
            }

            // Хук 2: Патч гизмо-редактора FreePose
            Type gizmoType = AccessTools.TypeByName("TransformGizmo")
                             ?? AccessTools.TypeByName("AdvancedFreePoseMode.TransformGizmo");
            if (gizmoType != null)
            {
                var originalGizmo = AccessTools.Method(gizmoType, "DoUpdate");
                var postfixGizmo = AccessTools.Method(typeof(FreePoseGizmoPatch), "Postfix");
                if (originalGizmo != null && postfixGizmo != null)
                {
                    harmony.Patch(originalGizmo, null, new HarmonyMethod(postfixGizmo));
                    InstanceLogger.LogInfo("[LegFix] Хук FreePose Gizmo успешно установлен.");
                }
            }

            // Хук 3: Сквозной контроль для обычного мира через LateUpdate
            try
            {
                harmony.PatchAll();
                DebugMonitorText = "Все хуки установлены. Ожидание фазы обсчета...";
            }
            catch (Exception ex)
            {
                InstanceLogger.LogError($"[LegFix] Критическая ошибка PatchAll: {ex}");
                DebugMonitorText = $"Ошибка Harmony PatchAll: {ex.Message}";
            }
        }

        private void Update()
        {
            TotalFramesCount++;
        }

        private void OnGUI()
        {
            UnityEngine.GUI.Box(new UnityEngine.Rect(10, 10, 650, 220), "Leg Fix REALTIME DIAGNOSTIC (Билд-5.9.1 Log+)");
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

            UnityEngine.GUI.Label(new UnityEngine.Rect(20, 35, 630, 180), systemStatus, labelStyle);
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

    public static class FreePoseGizmoPatch
    {
        public static void Postfix(MonoBehaviour __instance)
        {
            if (Global.code != null && Global.code.uiFreePose != null && Global.code.uiFreePose.selectedCharacter)
            {
                CharacterCustomization cc = Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>();
                if (cc != null) LegEqualizerCore.ExecuteFix(cc, "ГИЗМО");
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
                LegEqualizerCore.ExecuteFix(__instance, "LATEUPDATE");
            }
        }
    }
}
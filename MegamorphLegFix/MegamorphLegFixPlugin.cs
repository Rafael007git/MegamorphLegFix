using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MMorphFix
{
    [BepInPlugin("com.lorifel007.megamorphlegfix", "Megamorph Mod Fix", "0.0.9")]
    public class MMorphFixPlugin : BaseUnityPlugin
    {
        public static string DebugMonitorText = "Инициализация...";
        public static int TotalFramesCount = 0;
        public static int FixExecutionCalls = 0;
        public static string LastFoundCharacter = "Никого";
        public static string LeftLegTelemetry = "L: Ожидание суккуба...";
        public static string RightLegTelemetry = "R: Ожидание суккуба...";

        private void Awake()
        {
            var harmony = new Harmony("com.lorifel007.megamorphlegfix");
            try
            {
                // Автоматически активируем [HarmonyPatch] на LateUpdate игры
                harmony.PatchAll();
                DebugMonitorText = "Глобальный захват LateUpdate активен.";
            }
            catch (Exception ex)
            {
                DebugMonitorText = $"Ошибка Harmony: {ex.Message}";
            }
        }

        private void Update()
        {
            TotalFramesCount++;
        }

        private void OnGUI()
        {
            UnityEngine.GUI.Box(new UnityEngine.Rect(10, 10, 600, 220), "Leg Fix REALTIME DIAGNOSTIC (LateUpdate-v9)");
            UnityEngine.GUIStyle labelStyle = new UnityEngine.GUIStyle(UnityEngine.GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = UnityEngine.FontStyle.Bold,
                alignment = UnityEngine.TextAnchor.UpperLeft
            };
            labelStyle.normal.textColor = UnityEngine.Color.green;

            string systemStatus =
                $"[СИСТЕМА] Кадры плагина: {TotalFramesCount} | Коррекции суставов: {FixExecutionCalls}\n" +
                $"[ПЕРСОНАЖ] Активный ID: {LastFoundCharacter} | Статус: {DebugMonitorText}\n" +
                $"--------------------------------------------------------------------------------\n" +
                $"{LeftLegTelemetry}\n" +
                $"--------------------------------------------------------------------------------\n" +
                $"{RightLegTelemetry}";

            UnityEngine.GUI.Label(new UnityEngine.Rect(20, 35, 580, 180), systemStatus, labelStyle);
        }
    }

    // ГЛОБАЛЬНЫЙ ПЕРЕХВАТ ИГРЫ (Философия Диорамы)
    [HarmonyPatch(typeof(CharacterCustomization), "LateUpdate")]
    public static class GameLateUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(CharacterCustomization __instance)
        {
            if (__instance == null) return;

            // Как в Диораме: проверяем наличие компонента Мегаморфа на этом же объекте
            // Мы используем поиск по имени класса "MegaMorph", чтобы Unity нашла его независимо от namespace
            Component megaMorphComp = __instance.gameObject.GetComponent("MegaMorph");

            if (megaMorphComp != null)
            {
                // Если нашли — передаем управление в ядро расчетов
                LegEqualizerCore.ExecuteFix(__instance, megaMorphComp, "ИГРА_LATEUPDATE");
            }
        }
    }
}
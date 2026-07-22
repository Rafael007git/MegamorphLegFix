using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MMorphFix
{
    [BepInPlugin("com.lorifel007.megamorphlegfix", "Megamorph Mod Fix", "0.0.8")]
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
                harmony.PatchAll();
                DebugMonitorText = "Каскадный фикс 8 суккубов активен.";
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
            UnityEngine.GUI.Box(new UnityEngine.Rect(10, 10, 600, 220), "Leg Fix REALTIME DIAGNOSTIC (v0.0.8)");
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

    [HarmonyPatch(typeof(CharacterCustomization), "LateUpdate")]
    public static class GameLateUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(CharacterCustomization __instance)
        {
            if (__instance == null) return;

            // Проверяем, есть ли на персонаже рабочий компонент Мегаморфа
            Component megaMorphComp = __instance.gameObject.GetComponent("MegaMorph");
            if (megaMorphComp != null)
            {
                LegEqualizerCore.ExecuteFix(__instance, megaMorphComp, "LATEUPDATE");
            }
        }
    }
}
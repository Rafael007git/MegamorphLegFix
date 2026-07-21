using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MMorphFix
{
    [BepInPlugin("com.lorifel007.megamorphlegfix", "Megamorph Mod Fix", "0.0.2")]
    public class MMorphFixPlugin : BaseUnityPlugin
    {
        public static string DebugMonitorText = "Инициализация фикса...";
        public static int TotalFramesCount = 0;
        public static int LateUpdateCalls = 0;
        public static string LastFoundCharacter = "Никого";

        // --- НОВЫЕ ПЕРЕМЕННЫЕ ДЛЯ ТЕЛЕМЕТРИИ РАДАРА ---
        public static string LeftLegTelemetry = "L: Нет данных";
        public static string RightLegTelemetry = "R: Нет данных";

        private void Awake()
        {
            var harmony = new Harmony("com.lorifel007.megamorphlegfix");

            Type megaMorphType = AccessTools.TypeByName("MegaMorph.MegaMorph");
            if (megaMorphType != null)
            {
                var original = AccessTools.Method(megaMorphType, "DoUpdate");
                var postfix = AccessTools.Method(typeof(MegamorphDoUpdatePatch), "Postfix");
                harmony.Patch(original, null, new HarmonyMethod(postfix));
                DebugMonitorText = "Хук MegaMorph.DoUpdate успешно установлен!";
            }
            else
            {
                DebugMonitorText = "ВНИМАНИЕ: Класс MegaMorph не найден по строковому имени!";
            }

            try
            {
                harmony.PatchAll();
            }
            catch (Exception ex)
            {
                DebugMonitorText = $"Ошибка инициализации Harmony: {ex.Message}";
            }
        }

        private void Update()
        {
            TotalFramesCount++;
        }

        private void OnGUI()
        {
            // Увеличиваем окно радара по высоте (220 пикселей), чтобы влезла вся телеметрия суставов
            UnityEngine.GUI.Box(new UnityEngine.Rect(10, 10, 600, 220), "Leg Fix SYSTEM REALTIME DIAGNOSTIC");
            UnityEngine.GUIStyle labelStyle = new UnityEngine.GUIStyle(UnityEngine.GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = UnityEngine.FontStyle.Bold,
                alignment = UnityEngine.TextAnchor.UpperLeft
            };
            labelStyle.normal.textColor = UnityEngine.Color.green; // Зеленый "хакерский" цвет для лучшей читаемости

            string systemStatus =
                $"[СИСТЕМА] Кадры плагина: {TotalFramesCount} | Вызовы LateUpdate: {LateUpdateCalls}\n" +
                $"[ПЕРСОНАЖ] Активный ID: {LastFoundCharacter}\n" +
                $"[СТАТУС] {DebugMonitorText}\n" +
                $"--------------------------------------------------------------------------------\n" +
                $"{LeftLegTelemetry}\n" +
                $"--------------------------------------------------------------------------------\n" +
                $"{RightLegTelemetry}";

            UnityEngine.GUI.Label(new UnityEngine.Rect(20, 35, 580, 180), systemStatus, labelStyle);
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

    [HarmonyPatch(typeof(CharacterCustomization), "LateUpdate")]
    public static class GameLateUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(CharacterCustomization __instance)
        {
            MMorphFixPlugin.LateUpdateCalls++;
            LegEqualizerCore.ExecuteFix(__instance, "LATEUPDATE");
        }
    }
}
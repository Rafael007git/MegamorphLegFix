using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MMorphFix
{
    [BepInPlugin("com.lorifel007.megamorphlegfix", "Megamorph Mod Fix", "0.0.3")]
    public class MMorphFixPlugin : BaseUnityPlugin
    {
        public static string DebugMonitorText = "Инициализация...";
        public static int TotalFramesCount = 0;
        public static int FixExecutionCalls = 0;
        public static string LastFoundCharacter = "Никого";
        public static string LeftLegTelemetry = "L: Ожидание";
        public static string RightLegTelemetry = "R: Ожидание";

        private void Awake()
        {
            var harmony = new Harmony("com.lorifel007.megamorphlegfix");

            // ИСПРАВЛЕНО НА ОСНОВЕ ЛОГА: Используем плюс '+' для вложенного класса MegaMorph
            Type megaMorphType = AccessTools.TypeByName("MegaMorph.BepInExPlugin+MegaMorph");

            // Если вдруг автор вынес его на один уровень с BepInExPlugin, проверяем второй вариант
            if (megaMorphType == null)
            {
                megaMorphType = AccessTools.TypeByName("MegaMorph.MegaMorph");
            }

            if (megaMorphType != null)
            {
                var original = AccessTools.Method(megaMorphType, "DoUpdate");
                var postfix = AccessTools.Method(typeof(MegamorphDoUpdatePatch), "Postfix");
                harmony.Patch(original, null, new HarmonyMethod(postfix));
                DebugMonitorText = "Фикс УСПЕШНО синхронизирован с циклом MegaMorph!";
            }
            else
            {
                DebugMonitorText = "КРИТИЧЕСКАЯ ОШИБКА: Компонент MegaMorph не обнаружен в метаданных!";
            }
        }

        private void Update()
        {
            TotalFramesCount++;
        }

        private void OnGUI()
        {
            UnityEngine.GUI.Box(new UnityEngine.Rect(10, 10, 600, 220), "Leg Fix REALTIME DIAGNOSTIC (v0.0.3)");
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

    public static class MegamorphDoUpdatePatch
    {
        public static void Postfix(MonoBehaviour __instance)
        {
            if (__instance == null) return;

            // Динамически вытягиваем поле 'cc' из чужого компонента, независимо от его точного типа
            CharacterCustomization cc = AccessTools.Field(__instance.GetType(), "cc")?.GetValue(__instance) as CharacterCustomization;
            if (cc != null)
            {
                MMorphFixPlugin.FixExecutionCalls++;
                LegEqualizerCore.ExecuteFix(cc, "МЕГАМОРФ_POSTFIX");
            }
        }
    }
}
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
        public static string RightLegTelemetry = "R: Ожидание suккуба...";

        private void Awake()
        {
            var harmony = new Harmony("com.lorifel007.megamorphlegfix");

            // Захватываем точный тип Мегаморфа из логов HarmonyX
            Type megaMorphType = AccessTools.TypeByName("MegaMorph.BepInExPlugin+MegaMorph");
            if (megaMorphType == null) megaMorphType = AccessTools.TypeByName("MegaMorph.MegaMorph");

            if (megaMorphType != null)
            {
                // Цепляемся к его DoUpdate. Раз ползунки работают — этот метод вызывается 100%!
                var original = AccessTools.Method(megaMorphType, "DoUpdate");
                var postfix = AccessTools.Method(typeof(MegamorphDoUpdatePatch), "Postfix");
                harmony.Patch(original, null, new HarmonyMethod(postfix));
                DebugMonitorText = "Захват Диорамы через MegaMorph активен.";
            }
            else
            {
                DebugMonitorText = "КРИТИЧЕСКАЯ ОШИБКА: Компонент MegaMorph не найден в памяти!";
            }
        }

        private void Update()
        {
            TotalFramesCount++;
        }

        private void OnGUI()
        {
            UnityEngine.GUI.Box(new UnityEngine.Rect(10, 10, 600, 220), "Leg Fix REALTIME DIAGNOSTIC (Diorama-v9)");
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

            // Как в Диораме: вытаскиваем CharacterCustomization, который привязан к этому скрипту Мегаморфа
            CharacterCustomization cc = AccessTools.Field(__instance.GetType(), "cc")?.GetValue(__instance) as CharacterCustomization;

            if (cc != null)
            {
                // Передаем cc и сам компонент Мегаморфа в наше ядро расчетов
                LegEqualizerCore.ExecuteFix(cc, __instance, "МЕГАМОРФ_DOUPDATE");
            }
        }
    }
}
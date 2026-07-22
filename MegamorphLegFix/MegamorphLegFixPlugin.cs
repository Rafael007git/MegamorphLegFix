using System;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MMorphFix
{
    [BepInPlugin("com.lorifel007.megamorphlegfix", "Megamorph Mod Fix", "1.5.0")]
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

            // ХУК 1: Патч редактора Мегаморфа (DoUpdate)
            Type megaMorphType = AccessTools.TypeByName("MegaMorph.BepInExPlugin+MegaMorph")
                                 ?? AccessTools.TypeByName("MegaMorph.MegaMorph");
            if (megaMorphType != null)
            {
                var original = AccessTools.Method(megaMorphType, "DoUpdate");
                var postfix = AccessTools.Method(typeof(MegamorphDoUpdatePatch), "Postfix");
                harmony.Patch(original, null, new HarmonyMethod(postfix));
                DebugMonitorText = "Хук Мегаморфа успешно установлен!";
            }

            // ХУК 2: Патч гизмо-редактора FreePose (Из старого прототипа Сборки 4.0!)
            Type gizmoType = AccessTools.TypeByName("TransformGizmo")
                             ?? AccessTools.TypeByName("AdvancedFreePoseMode.TransformGizmo");
            if (gizmoType != null)
            {
                var originalGizmo = AccessTools.Method(gizmoType, "DoUpdate");
                var postfixGizmo = AccessTools.Method(typeof(FreePoseGizmoPatch), "Postfix");
                if (originalGizmo != null)
                {
                    harmony.Patch(originalGizmo, null, new HarmonyMethod(postfixGizmo));
                    DebugMonitorText += " | Хук FreePose Гизмо Активен!";
                }
            }

            // ХУК 3: Сквозной контроль для обычного мира через LateUpdate
            try
            {
                harmony.PatchAll();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка Harmony PatchAll: {ex.Message}");
            }
        }

        private void Update()
        {
            TotalFramesCount++;
        }

        private void OnGUI()
        {
            UnityEngine.GUI.Box(new UnityEngine.Rect(10, 10, 600, 220), "Leg Fix REALTIME DIAGNOSTIC (Билд-5.0)");
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
            CharacterCustomization cc = AccessTools.Field(__instance.GetType(), "cc")?.GetValue(__instance) as CharacterCustomization;
            if (cc != null) LegEqualizerCore.ExecuteFix(cc, "МЕГАМОРФ_DOUPDATE");
        }
    }

    // Захват персонажа во FreePose по глобальной ссылке из прототипа 4.0!
    public static class FreePoseGizmoPatch
    {
        public static void Postfix(MonoBehaviour __instance)
        {
            if (Global.code != null && Global.code.uiFreePose != null && Global.code.uiFreePose.selectedCharacter)
            {
                CharacterCustomization cc = Global.code.uiFreePose.selectedCharacter.GetComponent<CharacterCustomization>();
                if (cc != null) LegEqualizerCore.ExecuteFix(cc, "ГЕОМЕТРИЯ_ГИЗМО");
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

            // Во FreePose обычный LateUpdate игнорируем, чтобы не мешать анимациям
            bool isFreePoseActive = Global.code != null && Global.code.uiFreePose != null && Global.code.uiFreePose.isActiveAndEnabled;
            if (!isFreePoseActive)
            {
                LegEqualizerCore.ExecuteFix(__instance, "ИГРА_LATEUPDATE");
            }
        }
    }
}
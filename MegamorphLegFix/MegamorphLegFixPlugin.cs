using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MegamorphLegFix
{
    [BepInPlugin("com.lorifel007.megamorphlegfix", "Megamorph Mod Fix", "0.0.2")]
    public class MegamorphLegFixPlugin : BaseUnityPlugin
    {
        public static string DebugMonitorText = "Инициализация фикса...";
        public static int TotalFramesCount = 0;
        public static int LateUpdateCalls = 0;
        public static string LastFoundCharacter = "Никого";

        private void Awake()
        {
            var harmony = new Harmony("com.lorifel007.megamorphlegfix");

            // Хук на DoUpdate мода MegaMorph (если он активен)
            Type megaMorphType = AccessTools.TypeByName("MegaMorph");
            if (megaMorphType != null)
            {
                var original = AccessTools.Method(megaMorphType, "DoUpdate");
                var postfix = AccessTools.Method(typeof(MegamorphDoUpdatePatch), "Postfix");
                harmony.Patch(original, null, new HarmonyMethod(postfix));
                DebugMonitorText = "Хук MegaMorph.DoUpdate успешно установлен!";
            }
            else
            {
                DebugMonitorText = "MegaMorph не найден напрямую, работаем через глобальный LateUpdate.";
            }

            // Глобальный перехват LateUpdate игры
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
            UnityEngine.GUI.Box(new UnityEngine.Rect(10, 10, 550, 150), "Leg Fix SYSTEM DIAGNOSTIC");
            UnityEngine.GUIStyle labelStyle = new UnityEngine.GUIStyle(UnityEngine.GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = UnityEngine.FontStyle.Bold,
                alignment = UnityEngine.TextAnchor.UpperLeft
            };
            labelStyle.normal.textColor = UnityEngine.Color.white;

            string systemStatus =
                $"[СИСТЕМА] Кадры плагина: {TotalFramesCount} | Вызовы LateUpdate: {LateUpdateCalls}\n" +
                $"[ПЕРСОНАЖ] Последний активный ID: {LastFoundCharacter}\n" +
                $"-------------------------------------------------------------\n" +
                $"{DebugMonitorText}";

            UnityEngine.GUI.Label(new UnityEngine.Rect(20, 35, 530, 110), systemStatus, labelStyle);
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
            MegamorphLegFixPlugin.LateUpdateCalls++;
            LegEqualizerCore.ExecuteFix(__instance, "ИГРА_LATEUPDATE");
        }
    }

    public static class LegEqualizerCore
    {
        private static bool _isProcessing = false;

        public static void ExecuteFix(CharacterCustomization cc, string source)
        {
            if (cc == null || _isProcessing) return;
            _isProcessing = true;

            try
            {
                MegamorphLegFixPlugin.LastFoundCharacter = cc.gameObject.name;

                if (cc.bones == null || cc.bones.Length == 0)
                {
                    MegamorphLegFixPlugin.DebugMonitorText = "Ошибка: Скелет персонажа еще не инициализирован!";
                    return;
                }

                bool leftOk = false;
                bool rightOk = false;

                foreach (Transform bone in cc.bones)
                {
                    if (bone == null) continue;

                    // Левая нога
                    if (bone.name.Equals("lThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        ResetSubBonesLocalScale(bone, "lThighTwist", "lShin", "lFoot");
                        leftOk = true;
                    }
                    // Правая нога
                    else if (bone.name.Equals("rThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        ResetSubBonesLocalScale(bone, "rThighTwist", "rShin", "rFoot");
                        rightOk = true;
                    }
                }

                MegamorphLegFixPlugin.DebugMonitorText = $"[{source}] Константный фикс активен. Левая: {(leftOk ? "ОК" : "НЕТ")}, Правая: {(rightOk ? "ОК" : "НЕТ")}";
            }
            catch (Exception ex)
            {
                MegamorphLegFixPlugin.DebugMonitorText = $"Критическая ошибка математики: {ex.Message}";
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private static void ResetSubBonesLocalScale(Transform thigh, string twistName, string shinName, string footName)
        {
            // Находим все зависимые кости по иерархии вниз
            Transform twist = FindChildRecursive(thigh, twistName);
            Transform shin = FindChildRecursive(thigh, shinName);
            Transform foot = FindChildRecursive(thigh, footName);

            // РАЗРЫВ ПЕТЛИ ОБРАТНОЙ СВЯЗЫ: Жестко сбрасываем их локальный масштаб в 1.
            // Они идеально унаследуют толщину X/Z от родительского бедра (Thigh),
            // но из-за локальной единицы геометрия НИКОГДА не деформируется и не взорвется.
            if (twist != null) twist.localScale = Vector3.one;
            if (shin != null) shin.localScale = Vector3.one;
            if (foot != null) foot.localScale = Vector3.one;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindChildRecursive(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
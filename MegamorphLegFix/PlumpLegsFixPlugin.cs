using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MegamorphLegFix
{
    [BepInPlugin("com.yourname.plumplegsfix", "Megamorph Master Fix", "1.3.0")]
    public class MegamorphLegFixPlugin : BaseUnityPlugin
    {
        public static string DebugMonitorText = "Инициализация радара...";
        public static int TotalFramesCount = 0;
        public static int LateUpdateCalls = 0;
        public static string LastFoundCharacter = "Никого";

        private void Awake()
        {
            var harmony = new Harmony("com.yourname.plumplegsfix");

            Type megaMorphType = AccessTools.TypeByName("MegaMorph");
            if (megaMorphType != null)
            {
                var original = AccessTools.Method(megaMorphType, "DoUpdate");
                var postfix = AccessTools.Method(typeof(MegamorphDoUpdatePatch), "Postfix");
                harmony.Patch(original, null, new HarmonyMethod(postfix));
                DebugMonitorText = "Хук DoUpdate успешно установлен!";
            }
            else
            {
                DebugMonitorText = "ВНИМАНИЕ: Класс MegaMorph НЕ НАЙДЕН!";
            }

            try
            {
                harmony.PatchAll();
            }
            catch (Exception ex)
            {
                DebugMonitorText = $"Ошибка PatchAll: {ex.Message}";
            }
        }

        private void Update()
        {
            // Этот счетчик ДОЛЖЕН крутиться каждую секунду в игре!
            TotalFramesCount++;
        }

        private void OnGUI()
        {
            // Увеличим плашку, чтобы влезла вся техническая подноготная
            UnityEngine.GUI.Box(new UnityEngine.Rect(10, 10, 550, 150), "Leg Fix SYSTEM DIAGNOSTIC");

            UnityEngine.GUIStyle labelStyle = new UnityEngine.GUIStyle(UnityEngine.GUI.skin.label);
            labelStyle.fontSize = 13;
            labelStyle.fontStyle = UnityEngine.FontStyle.Bold;
            labelStyle.normal.textColor = UnityEngine.Color.white;
            labelStyle.alignment = UnityEngine.TextAnchor.UpperLeft;

            // Выводим системное состояние плагина, которое работает ВСЕГДА
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
            if (cc != null) LegEqualizerCore.ExecuteFix(cc, "ХУК_МЕГАМОРФ");
        }
    }

    [HarmonyPatch(typeof(CharacterCustomization), "LateUpdate")]
    public static class GameLateUpdatePatch
    {
        [HarmonyPostfix]
        public static void Postfix(CharacterCustomization __instance)
        {
            MegamorphLegFixPlugin.LateUpdateCalls++;
            LegEqualizerCore.ExecuteFix(__instance, "ХУК_LATE_UPDATE");
        }
    }

    public static class LegEqualizerCore
    {
        private static bool _isProcessing = false;

        public static void ExecuteFix(CharacterCustomization cc, string source)
        {
            if (cc == null) return;

            // Запоминаем имя или ID персонажа, до которого долетел хук
            MegamorphLegFixPlugin.LastFoundCharacter = $"{cc.gameObject.name} (через {source})";

            if (cc.bones == null || cc.bones.Length == 0)
            {
                MegamorphLegFixPlugin.DebugMonitorText = $"[{source}] Ошибка: Массив костей пуст!";
                return;
            }

            if (_isProcessing) return;
            _isProcessing = true;

            try
            {
                bool thighFound = false;

                foreach (Transform bone in cc.bones)
                {
                    if (bone == null) continue;

                    if (bone.name.Equals("lThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        thighFound = true;
                        Transform twist = FindChildRecursive(bone, "lThighTwist");
                        Transform shin = FindChildRecursive(bone, "lShin");
                        Transform foot = FindChildRecursive(bone, "lFoot");

                        if (twist != null) twist.localScale = Vector3.one;

                        Vector3 currentThighLocal = bone.localScale;
                        Vector3 currentShinLocal = (shin != null) ? shin.localScale : Vector3.one;
                        Vector3 currentFootLocal = (foot != null) ? foot.localScale : Vector3.one;

                        // ОБНОВЛЯЕМ ДАННЫЕ О СКАЛЕ КОСТЕЙ
                        MegamorphLegFixPlugin.DebugMonitorText =
                            $"[ДАННЫЕ ИЗ {source}] Кость: {bone.name}\n" +
                            $"Thigh Local Scale: X={currentThighLocal.x:F2}, Y={currentThighLocal.y:F2}, Z={currentThighLocal.z:F2}\n" +
                            $"Shin Local Scale:  X={currentShinLocal.x:F2}, Y={currentShinLocal.y:F2}, Z={currentShinLocal.z:F2}";

                        if (currentFootLocal.sqrMagnitude < 0.01f) currentFootLocal = Vector3.one;

                        if (Mathf.Abs(currentThighLocal.x - 1.0f) < 0.02f && Mathf.Abs(currentShinLocal.x - 1.0f) < 0.02f)
                        {
                            continue;
                        }

                        Vector3 parentOfThighWorldScale = bone.parent != null ? bone.parent.lossyScale : Vector3.one;

                        Vector3 targetWorldShinScale = new Vector3(
                            currentShinLocal.x * currentThighLocal.x * parentOfThighWorldScale.x,
                            currentShinLocal.y * currentThighLocal.y * parentOfThighWorldScale.y,
                            currentShinLocal.z * currentThighLocal.z * parentOfThighWorldScale.z
                        );

                        Vector3 targetWorldFootScale = new Vector3(
                            currentFootLocal.x * currentShinLocal.x * currentThighLocal.x * parentOfThighWorldScale.x,
                            currentFootLocal.y * currentShinLocal.y * currentThighLocal.y * parentOfThighWorldScale.y,
                            currentFootLocal.z * currentShinLocal.z * currentThighLocal.z * parentOfThighWorldScale.z
                        );

                        if (shin != null) ForcePureWorldScale(shin, targetWorldShinScale);
                        if (foot != null) ForcePureWorldScale(foot, targetWorldFootScale);

                        break;
                    }
                }

                if (!thighFound)
                {
                    MegamorphLegFixPlugin.DebugMonitorText = $"[{source}] Персонаж найден, но кость 'lThighBend' отсутствует в cc.bones!";
                }
            }
            catch (Exception ex)
            {
                MegamorphLegFixPlugin.DebugMonitorText = $"Ошибка в математике: {ex.Message}";
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private static void ForcePureWorldScale(Transform target, Vector3 desiredWorldScale)
        {
            if (target.parent == null) return;
            target.localScale = Vector3.one;
            Vector3 currentParentWorldScale = target.parent.lossyScale;
            float parentX = Mathf.Abs(currentParentWorldScale.x) > 0.001f ? currentParentWorldScale.x : 1.0f;
            float parentY = Mathf.Abs(currentParentWorldScale.y) > 0.001f ? currentParentWorldScale.y : 1.0f;
            float parentZ = Mathf.Abs(currentParentWorldScale.z) > 0.001f ? currentParentWorldScale.z : 1.0f;
            target.localScale = new Vector3(desiredWorldScale.x / parentX, desiredWorldScale.y / parentY, desiredWorldScale.z / parentZ);
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

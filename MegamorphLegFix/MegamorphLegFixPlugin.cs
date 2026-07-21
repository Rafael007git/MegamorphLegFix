using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MegamorphLegFix
{
    [BepInPlugin("com.lorifel007.megamorphlegfix", "Megamorph Mod Fix", "0.0.2")]
    public class MegamorphLegFixPlugin : BaseUnityPlugin
    {
        public static string DebugMonitorText = "Инициализация радара...";
        public static int TotalFramesCount = 0;
        public static int LateUpdateCalls = 0;
        public static string LastFoundCharacter = "Никого";

        private void Awake()
        {
            var harmony = new Harmony("com.lorifel007.megamorphlegfix");

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
                bool leftThighFound = false;
                bool rightThighFound = false;

                foreach (Transform bone in cc.bones)
                {
                    if (bone == null) continue;

                    if (bone.name.Equals("lThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        leftThighFound = true;
                        ProcessLegBranch(bone, "lThighTwist", "lShin", "lFoot", source);
                    }
                    else if (bone.name.Equals("rThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        rightThighFound = true;
                        ProcessLegBranch(bone, "rThighTwist", "rShin", "rFoot", source);
                    }
                }

                MegamorphLegFixPlugin.DebugMonitorText = $"[{source}] Фикс активен. L: {(leftThighFound ? "ОК" : "НЕТ")}, R: {(rightThighFound ? "ОК" : "НЕТ")}";
            }
            catch (Exception ex)
            {
                MegamorphLegFixPlugin.DebugMonitorText = $"Ошибка: {ex.Message}";
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private static void ProcessLegBranch(Transform thighBone, string twistName, string shinName, string footName, string source)
        {
            Vector3 parentScale = thighBone.parent != null ? thighBone.parent.lossyScale : Vector3.one;
            Vector3 thighScale = thighBone.localScale;

            Transform shin = FindChildRecursive(thighBone, shinName);
            Transform foot = FindChildRecursive(thighBone, footName);

            Vector3 shinScale = (shin != null) ? shin.localScale : Vector3.one;
            Vector3 footScale = (foot != null) ? foot.localScale : Vector3.one;

            bool isParentUniform = Mathf.Abs(parentScale.x - parentScale.y) < 0.005f && Mathf.Abs(parentScale.y - parentScale.z) < 0.005f;
            bool isThighUniform = Mathf.Abs(thighScale.x - thighScale.y) < 0.005f && Mathf.Abs(thighScale.y - thighScale.z) < 0.005f;
            bool isShinUniform = Mathf.Abs(shinScale.x - shinScale.y) < 0.005f && Mathf.Abs(shinScale.y - shinScale.z) < 0.005f;
            bool isFootUniform = Mathf.Abs(footScale.x - footScale.y) < 0.005f && Mathf.Abs(footScale.y - footScale.z) < 0.005f;

            if (isParentUniform && isThighUniform && isShinUniform && isFootUniform) return;

            Transform twist = FindChildRecursive(thighBone, twistName);
            if (twist != null) twist.localScale = Vector3.one;

            if (footScale.sqrMagnitude < 0.01f) footScale = Vector3.one;

            Vector3 targetShinWorld = new Vector3(shinScale.x * thighScale.x * parentScale.x, shinScale.y * thighScale.y * parentScale.y, shinScale.z * thighScale.z * parentScale.z);
            Vector3 targetFootWorld = new Vector3(footScale.x * shinScale.x * thighScale.x * parentScale.x, footScale.y * shinScale.y * thighScale.y * parentScale.y, footScale.z * shinScale.z * parentScale.z);

            if (shin != null && shin.parent != null)
            {
                shin.localScale = Vector3.one;
                Vector3 pShin = shin.parent.lossyScale;
                shin.localScale = new Vector3(targetShinWorld.x / (Mathf.Abs(pShin.x) > 0.001f ? pShin.x : 1f), targetShinWorld.y / (Mathf.Abs(pShin.y) > 0.001f ? pShin.y : 1f), targetShinWorld.z / (Mathf.Abs(pShin.z) > 0.001f ? pShin.z : 1f));
            }

            if (foot != null && foot.parent != null)
            {
                foot.localScale = Vector3.one;
                Vector3 pFoot = foot.parent.lossyScale;
                foot.localScale = new Vector3(targetFootWorld.x / (Mathf.Abs(pFoot.x) > 0.001f ? pFoot.x : 1f), targetFootWorld.y / (Mathf.Abs(pFoot.y) > 0.001f ? pFoot.y : 1f), targetFootWorld.z / (Mathf.Abs(pFoot.z) > 0.001f ? pFoot.z : 1f));
            }
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

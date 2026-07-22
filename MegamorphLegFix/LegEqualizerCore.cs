using System;
using UnityEngine;

namespace MMorphFix
{
    public static class LegEqualizerCore
    {
        private static bool _isProcessing = false;

        // ПЕРЕГРУЗКА ДЛЯ СТАРАХ ХУКОВ: Если кто-то вызовет метод с одним аргументом,
        // мы перенаправим его в основной метод, защитив игру от TypeLoadException!
        public static void ExecuteFix(CharacterCustomization cc)
        {
            ExecuteFix(cc, "КОМПЕНСАЦИЯ");
        }

        public static void ExecuteFix(CharacterCustomization cc, string source)
        {
            if (cc == null || _isProcessing) return;
            if (cc.bones == null || cc.bones.Length == 0) return;

            _isProcessing = true;

            try
            {
                MMorphFixPlugin.LastFoundCharacter = $"{cc.gameObject.name} ({source})";

                bool leftOk = false;
                bool rightOk = false;

                foreach (Transform bone in cc.bones)
                {
                    if (bone == null) continue;

                    if (bone.name.Equals("lThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyPureInverseFix(bone, "lThighTwist", "lShin", "lFoot");
                        leftOk = true;
                    }
                    else if (bone.name.Equals("rThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyPureInverseFix(bone, "rThighTwist", "rShin", "rFoot");
                        rightOk = true;
                    }
                }

                if (leftOk || rightOk)
                {
                    MMorphFixPlugin.FixExecutionCalls++;
                    MMorphFixPlugin.DebugMonitorText = "Защитный щит суставов активен.";
                }
            }
            catch (Exception ex)
            {
                // Выводим развернутый отчет, если падение произойдет внутри математики
                MMorphFixPlugin.DebugMonitorText = $"Сбой: {ex.GetType().Name} в {ex.TargetSite} -> {ex.Message}";
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private static void ApplyPureInverseFix(Transform thighBone, string twistName, string shinName, string footName)
        {
            Transform shin = FindChildRecursive(thighBone, shinName);
            Transform foot = FindChildRecursive(thighBone, footName);
            if (shin == null || foot == null) return;

            Transform twist = FindChildRecursive(thighBone, twistName);
            if (twist != null) twist.localScale = Vector3.one;

            Vector3 liveThighScale = SanitizeVector(thighBone.localScale);

            // --- КАСКАДНАЯ МАТРИЧНАЯ ИНВЕРСИЯ (КОЛЕННЫЙ СУСТАВ) ---
            Quaternion shinRot = shin.localRotation;
            Vector3 parentScaleInShinSpace = SanitizeVector(new Vector3(
                Vector3.Magnitude(shinRot * Vector3.right * liveThighScale.x),
                Vector3.Magnitude(shinRot * Vector3.up * liveThighScale.y),
                Vector3.Magnitude(shinRot * Vector3.forward * liveThighScale.z)
            ));

            shin.localScale = new Vector3(
                1.0f / parentScaleInShinSpace.x,
                1.0f / parentScaleInShinSpace.y,
                1.0f / parentScaleInShinSpace.z
            );

            // --- КАСКАДНАЯ МАТРИЧНАЯ ИНВЕРСИЯ (ГОЛЕНОСТОП) ---
            Quaternion footRot = foot.localRotation;
            Vector3 currentCombinedParentScale = SanitizeVector(shin.lossyScale);

            Vector3 parentScaleInFootSpace = SanitizeVector(new Vector3(
                Vector3.Magnitude(footRot * Vector3.right * currentCombinedParentScale.x),
                Vector3.Magnitude(footRot * Vector3.up * currentCombinedParentScale.y),
                Vector3.Magnitude(footRot * Vector3.forward * currentCombinedParentScale.z)
            ));

            Vector3 pelvisScale = SanitizeVector(thighBone.parent != null ? thighBone.parent.lossyScale : Vector3.one);

            foot.localScale = new Vector3(
                pelvisScale.x / parentScaleInFootSpace.x,
                pelvisScale.y / parentScaleInFootSpace.y,
                pelvisScale.z / parentScaleInFootSpace.z
            );

            // --- ОТПРАВКА ДАННЫХ НА РАДАР ---
            string side = thighBone.name.StartsWith("l") ? "ЛЕВАЯ" : "ПРАВАЯ";
            string telemetry =
                $"[{side} НОГА] Живое бедро: {liveThighScale.ToString("F2")} | Угол колена гизмо: {shinRot.eulerAngles.ToString("F0")}°\n" +
                $"            -> Локальный scale голени: {shin.localScale.ToString("F2")}\n" +
                $"            -> Локальный scale стопы: {foot.localScale.ToString("F2")}";

            if (thighBone.name.StartsWith("l"))
                MMorphFixPlugin.LeftLegTelemetry = telemetry;
            else
                MMorphFixPlugin.RightLegTelemetry = telemetry;
        }

        private static Vector3 SanitizeVector(Vector3 vector)
        {
            float x = (float.IsNaN(vector.x) || float.IsInfinity(vector.x) || Mathf.Abs(vector.x) < 0.01f) ? 1.0f : vector.x;
            float y = (float.IsNaN(vector.y) || float.IsInfinity(vector.y) || Mathf.Abs(vector.y) < 0.01f) ? 1.0f : vector.y;
            float z = (float.IsNaN(vector.z) || float.IsInfinity(vector.z) || Mathf.Abs(vector.z) < 0.01f) ? 1.0f : vector.z;
            return new Vector3(x, y, z);
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
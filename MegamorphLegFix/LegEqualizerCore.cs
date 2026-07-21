using System;
using UnityEngine;

namespace MMorphFix
{
    public static class LegEqualizerCore
    {
        private static bool _isProcessing = false;

        public static void ExecuteFix(CharacterCustomization cc, string source)
        {
            if (cc == null || _isProcessing) return;
            _isProcessing = true;

            try
            {
                MMorphFixPlugin.LastFoundCharacter = cc.gameObject.name;

                if (cc.bones == null || cc.bones.Length == 0) return;

                bool leftOk = false;
                bool rightOk = false;

                foreach (Transform bone in cc.bones)
                {
                    if (bone == null) continue;

                    // Левая нога
                    if (bone.name.Equals("lThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyDynamicJointFix(bone, "lThighTwist", "lShin", "lFoot");
                        leftOk = true;
                    }
                    // Правая нога
                    else if (bone.name.Equals("rThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyDynamicJointFix(bone, "rThighTwist", "rShin", "rFoot");
                        rightOk = true;
                    }
                }

                MMorphFixPlugin.DebugMonitorText = "Все пресеты учтены. Геометрия стабильна.";
            }
            catch (Exception ex)
            {
                MMorphFixPlugin.DebugMonitorText = $"Ошибка: {ex.Message}";
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private static void ApplyDynamicJointFix(Transform thighBone, string twistName, string shinName, string footName)
        {
            Transform shin = FindChildRecursive(thighBone, shinName);
            Transform foot = FindChildRecursive(thighBone, footName);
            if (shin == null || foot == null) return;

            // РЕЗУЛЬТИРУЮЩИЙ ИСТОЧНИК ИСТИНЫ:
            // Читаем итоговый масштаб бедра, куда MegaMorph уже вшил ВСЕ применённые слайдеры
            Vector3 finalThighScale = thighBone.localScale;

            // Защита от нулевого деления
            if (Mathf.Abs(finalThighScale.x) < 0.001f) finalThighScale.x = 1f;
            if (Mathf.Abs(finalThighScale.y) < 0.001f) finalThighScale.y = 1f;
            if (Mathf.Abs(finalThighScale.z) < 0.001f) finalThighScale.z = 1f;

            // Сбрасываем скручивание мышц
            Transform twist = FindChildRecursive(thighBone, twistName);
            if (twist != null) twist.localScale = Vector3.one;

            // --- КОМПЕНСАЦИЯ ДЛЯ КОЛЕНА (Thigh -> Shin) ---
            Quaternion shinRot = shin.localRotation;

            // Проецируем итоговый масштаб бедра на текущий поворот колена (из FreePose или анимации)
            Vector3 thighScaleInShinSpace = new Vector3(
                Vector3.Magnitude(shinRot * Vector3.right * finalThighScale.x),
                Vector3.Magnitude(shinRot * Vector3.up * finalThighScale.y),
                Vector3.Magnitude(shinRot * Vector3.forward * finalThighScale.z)
            );

            // Вычисляем идеальный масштаб голени.
            Vector3 targetShinScale = new Vector3(
                1.0f / (Mathf.Abs(thighScaleInShinSpace.x) > 0.001f ? thighScaleInShinSpace.x : 1f),
                1.0f / (Mathf.Abs(thighScaleInShinSpace.y) > 0.001f ? thighScaleInShinSpace.y : 1f),
                1.0f / (Mathf.Abs(thighScaleInShinSpace.z) > 0.001f ? thighScaleInShinSpace.z : 1f)
            );
            shin.localScale = targetShinScale;

            // --- КОМПЕНСАЦИЯ ДЛЯ СТОПЫ (Shin -> Foot) ---
            Quaternion footRot = foot.localRotation;
            Vector3 combinedParentScale = shin.lossyScale; // Мировое давление на стопу

            Vector3 parentScaleInFootSpace = new Vector3(
                Vector3.Magnitude(footRot * Vector3.right * combinedParentScale.x),
                Vector3.Magnitude(footRot * Vector3.up * combinedParentScale.y),
                Vector3.Magnitude(footRot * Vector3.forward * combinedParentScale.z)
            );

            Vector3 pelvisScale = thighBone.parent != null ? thighBone.parent.lossyScale : Vector3.one;

            Vector3 targetFootScale = new Vector3(
                pelvisScale.x / (Mathf.Abs(parentScaleInFootSpace.x) > 0.001f ? parentScaleInFootSpace.x : 1f),
                pelvisScale.y / (Mathf.Abs(parentScaleInFootSpace.y) > 0.001f ? parentScaleInFootSpace.y : 1f),
                pelvisScale.z / (Mathf.Abs(parentScaleInFootSpace.z) > 0.001f ? parentScaleInFootSpace.z : 1f)
            );

            // Чтобы не перегружать вычисления стопы во время первого теста, 
            // пока оставим её локальный масштаб базовым. Если стопа поплывёт — включим targetFootScale.
            foot.localScale = Vector3.one;

            // --- ОБНОВЛЕНИЕ ТЕЛЕМЕТРИИ НА РАДАРЕ ---
            string side = thighBone.name.StartsWith("l") ? "ЛЕВАЯ" : "ПРАВАЯ";
            string telemetry =
                $"[{side} НОГА] Итоговый масштаб бедра: {finalThighScale.ToString("F2")} | Угол колена: {shinRot.eulerAngles.ToString("F0")}\n" +
                $"            -> Применённый масштаб голени: {targetShinScale.ToString("F2")}";

            if (thighBone.name.StartsWith("l"))
                MMorphFixPlugin.LeftLegTelemetry = telemetry;
            else
                MMorphFixPlugin.RightLegTelemetry = telemetry;
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
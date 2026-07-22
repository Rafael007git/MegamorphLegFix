using System;
using UnityEngine;

namespace MMorphFix
{
    public static class LegEqualizerCore
    {
        private static bool _isProcessing = false;

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

                // Извлекаем компонент Мегаморфа, который успешно работает на текущем персонаже
                Component megaMorphComp = cc.gameObject.GetComponent("MegaMorph");

                bool leftOk = false;
                bool rightOk = false;

                foreach (Transform bone in cc.bones)
                {
                    if (bone == null) continue;

                    if (bone.name.Equals("lThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyDirectOffsetsFix(bone, "lThighTwist", "lShin", "lFoot", "lThighBend_scale", megaMorphComp);
                        leftOk = true;
                    }
                    else if (bone.name.Equals("rThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyDirectOffsetsFix(bone, "rThighTwist", "rShin", "rFoot", "rThighBend_scale", megaMorphComp);
                        rightOk = true;
                    }
                }

                if (leftOk || rightOk)
                {
                    MMorphFixPlugin.FixExecutionCalls++;
                    MMorphFixPlugin.DebugMonitorText = "Инверсия осей слайдеров активна.";
                }
            }
            catch (Exception ex)
            {
                MMorphFixPlugin.DebugMonitorText = "Ошибка каскада: " + ex.Message;
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private static void ApplyDirectOffsetsFix(Transform thighBone, string twistName, string shinName, string footName, string thighKey, Component megaMorphComp)
        {
            Transform shin = FindChildRecursive(thighBone, shinName);
            Transform foot = FindChildRecursive(thighBone, footName);
            if (shin == null || foot == null) return;

            Transform twist = FindChildRecursive(thighBone, twistName);
            if (twist != null) twist.localScale = Vector3.one;

            // ИСТОЧНИК ИСТИНЫ (Ползунки Мегаморфа)
            Vector3 configThigh = Vector3.one;
            Vector3 configShin = Vector3.one;
            Vector3 configFoot = Vector3.one;

            if (megaMorphComp != null)
            {
                dynamic megaMorph = megaMorphComp;
                object tObj, sObj, fObj;

                if (megaMorph.offsets.TryGetValue(thighKey, out tObj) && tObj != null) configThigh = ((dynamic)tObj).offset / 100f;
                if (megaMorph.offsets.TryGetValue(shinName + "_scale", out sObj) && sObj != null) configShin = ((dynamic)sObj).offset / 100f;
                if (megaMorph.offsets.TryGetValue(footName + "_scale", out fObj) && fObj != null) configFoot = ((dynamic)fObj).offset / 100f;
            }

            Vector3 liveThighScale = SanitizeVector(thighBone.localScale);

            // --- КАСКАДНАЯ МАТРИЧНАЯ ИНВЕРСИЯ (КОЛЕННЫЙ СУСТАВ) ---
            Quaternion shinRot = shin.localRotation;
            Vector3 parentScaleInShinSpace = SanitizeVector(new Vector3(
                Vector3.Magnitude(shinRot * Vector3.right * liveThighScale.x),
                Vector3.Magnitude(shinRot * Vector3.up * liveThighScale.y),
                Vector3.Magnitude(shinRot * Vector3.forward * liveThighScale.z)
            ));

            // ИСПРАВЛЕНО: Вместо жесткой единицы делим РЕАЛЬНЫЕ значения ползунков (configShin) на перекос осей бедра!
            shin.localScale = new Vector3(
                configShin.x / parentScaleInShinSpace.x,
                configShin.y / parentScaleInShinSpace.y,
                configShin.z / parentScaleInShinSpace.z
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

            // ИСПРАВЛЕНО: Применяем реальный масштаб стопы из ползунков (configFoot)
            foot.localScale = new Vector3(
                (configFoot.x * pelvisScale.x) / parentScaleInFootSpace.x,
                (configFoot.y * pelvisScale.y) / parentScaleInFootSpace.y,
                (configFoot.z * pelvisScale.z) / parentScaleInFootSpace.z
            );

            // --- ОТПРАВКА ДАННЫХ НА РАДАР ---
            string side = thighBone.name.StartsWith("l") ? "ЛЕВАЯ" : "ПРАВАЯ";
            string telemetry =
                $"[{side} НОГА] Слайдер_Бедра: {configThigh.ToString("F2")} | Слайдер_Голени: {configShin.ToString("F2")}\n" +
                $"            -> Угол колена гизмо: {shinRot.eulerAngles.ToString("F0")}°\n" +
                $"            -> Выставлен scale голени: {shin.localScale.ToString("F2")} | Стопы: {foot.localScale.ToString("F2")}";

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
using System;
using UnityEngine;

namespace MMorphFix
{
    public static class LegEqualizerCore
    {
        private static bool _isProcessing = false;

        public static void ExecuteFix(CharacterCustomization cc, object megaMorphInstance, string source)
        {
            if (cc == null || megaMorphInstance == null || _isProcessing) return;

            // Если скелет еще пуст, пропускаем кадр
            if (cc.bones == null || cc.bones.Length == 0) return;

            _isProcessing = true;

            try
            {
                // Выводим РЕАЛЬНОЕ имя 3D-объекта, которое Unity дала персонажу на сцене!
                MMorphFixPlugin.LastFoundCharacter = $"{cc.gameObject.name} ({source})";

                dynamic megaMorph = megaMorphInstance;
                bool leftOk = false;
                bool rightOk = false;

                foreach (Transform bone in cc.bones)
                {
                    if (bone == null) continue;

                    // Анатомический фильтр Genesis 8
                    if (bone.name.Equals("lThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyDirectOffsetsFix(bone, "lThighTwist", "lShin", "lFoot", "lThighBend_scale", megaMorph);
                        leftOk = true;
                    }
                    else if (bone.name.Equals("rThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyDirectOffsetsFix(bone, "rThighTwist", "rShin", "rFoot", "rThighBend_scale", megaMorph);
                        rightOk = true;
                    }
                }

                if (leftOk || rightOk)
                {
                    MMorphFixPlugin.FixExecutionCalls++;
                    MMorphFixPlugin.DebugMonitorText = "Анатомический захват успешен. Ноги защищены.";
                }
                else
                {
                    MMorphFixPlugin.DebugMonitorText = "ВНИМАНИЕ: На объекте нет костей lThighBend/rThighBend!";
                }
            }
            catch (Exception ex)
            {
                MMorphFixPlugin.DebugMonitorText = $"Ошибка расчетов: {ex.Message}";
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private static void ApplyDirectOffsetsFix(Transform thighBone, string twistName, string shinName, string footName, string thighKey, dynamic megaMorph)
        {
            Transform shin = FindChildRecursive(thighBone, shinName);
            Transform foot = FindChildRecursive(thighBone, footName);
            if (shin == null || foot == null) return;

            Transform twist = FindChildRecursive(thighBone, twistName);
            if (twist != null) twist.localScale = Vector3.one;

            Vector3 configThigh = Vector3.one;
            Vector3 configShin = Vector3.one;
            Vector3 configFoot = Vector3.one;

            object tObj, sObj, fObj;

            // Вытягиваем чистые значения ползунков Мегаморфа из его внутренней памяти
            if (megaMorph.offsets.TryGetValue(thighKey, out tObj) && tObj != null)
            {
                dynamic dObj = tObj;
                configThigh = dObj.offset / 100f;
            }
            if (megaMorph.offsets.TryGetValue(shinName + "_scale", out sObj) && sObj != null)
            {
                dynamic dObj = sObj;
                configShin = dObj.offset / 100f;
            }
            if (megaMorph.offsets.TryGetValue(footName + "_scale", out fObj) && fObj != null)
            {
                dynamic dObj = fObj;
                configFoot = dObj.offset / 100f;
            }

            Vector3 liveThighScale = thighBone.localScale;

            // --- КАСКАДНАЯ МАТРИЧНАЯ ИНВЕРСИЯ (КОЛЕННЫЙ СУСТАВ) ---
            Quaternion shinRot = shin.localRotation;
            Vector3 parentScaleInShinSpace = new Vector3(
                Vector3.Magnitude(shinRot * Vector3.right * liveThighScale.x),
                Vector3.Magnitude(shinRot * Vector3.up * liveThighScale.y),
                Vector3.Magnitude(shinRot * Vector3.forward * liveThighScale.z)
            );

            shin.localScale = new Vector3(
                configShin.x / (Mathf.Abs(parentScaleInShinSpace.x) > 0.001f ? parentScaleInShinSpace.x : 1f),
                configShin.y / (Mathf.Abs(parentScaleInShinSpace.y) > 0.001f ? parentScaleInShinSpace.y : 1f),
                configShin.z / (Mathf.Abs(parentScaleInShinSpace.z) > 0.001f ? parentScaleInShinSpace.z : 1f)
            );

            // --- КАСКАДНАЯ МАТРИЧНАЯ ИНВЕРСИЯ (ГОЛЕНОСТОП) ---
            Quaternion footRot = foot.localRotation;
            Vector3 currentCombinedParentScale = shin.lossyScale;

            Vector3 parentScaleInFootSpace = new Vector3(
                Vector3.Magnitude(footRot * Vector3.right * currentCombinedParentScale.x),
                Vector3.Magnitude(footRot * Vector3.up * currentCombinedParentScale.y),
                Vector3.Magnitude(footRot * Vector3.forward * currentCombinedParentScale.z)
            );

            Vector3 pelvisScale = thighBone.parent != null ? thighBone.parent.lossyScale : Vector3.one;

            foot.localScale = new Vector3(
                (configFoot.x * pelvisScale.x) / (Mathf.Abs(parentScaleInFootSpace.x) > 0.001f ? parentScaleInFootSpace.x : 1f),
                (configFoot.y * pelvisScale.y) / (Mathf.Abs(parentScaleInFootSpace.y) > 0.001f ? parentScaleInFootSpace.y : 1f),
                (configFoot.z * pelvisScale.z) / (Mathf.Abs(parentScaleInFootSpace.z) > 0.001f ? parentScaleInFootSpace.z : 1f)
            );

            // --- ОБНОВЛЕНИЕ ДАННЫХ ТЕЛЕМЕТРИИ НА РАДАРЕ ---
            string side = thighBone.name.StartsWith("l") ? "ЛЕВАЯ" : "ПРАВАЯ";
            string telemetry =
                $"[{side} НОГА] Ползунок_Бедра: {configThigh.ToString("F2")} | Ползунок_Голени: {configShin.ToString("F2")}\n" +
                $"            -> Угол колена гизмо: {shinRot.eulerAngles.ToString("F0")}°\n" +
                $"            -> Итоговый scale голени: {shin.localScale.ToString("F2")} | Стопы: {foot.localScale.ToString("F2")}";

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
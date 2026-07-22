using System;
using UnityEngine;

namespace MMorphFix
{
    public static class LegEqualizerCore
    {
        private static bool _isProcessing = false;

        public static void ExecuteFix(CharacterCustomization cc, object megaMorphInstance)
        {
            if (cc == null || megaMorphInstance == null || _isProcessing) return;

            // Фильтр только на персонажа игрока
            bool isPlayer = cc.gameObject.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            cc.gameObject.CompareTag("Player") ||
                            (cc.transform.root != null && cc.transform.root.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0);

            if (!isPlayer) return;

            _isProcessing = true;

            try
            {
                MMorphFixPlugin.LastFoundCharacter = cc.gameObject.name;

                if (cc.bones == null || cc.bones.Length == 0) return;

                // Приводим к dynamic для беспрепятственного чтения чужих словарей напрямую
                dynamic megaMorph = megaMorphInstance;

                foreach (Transform bone in cc.bones)
                {
                    if (bone == null) continue;

                    if (bone.name.Equals("lThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyDirectOffsetsFix(bone, "lThighTwist", "lShin", "lFoot", "lThighBend_scale", megaMorph);
                    }
                    else if (bone.name.Equals("rThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyDirectOffsetsFix(bone, "rThighTwist", "rShin", "rFoot", "rThighBend_scale", megaMorph);
                    }
                }

                MMorphFixPlugin.FixExecutionCalls++;
                MMorphFixPlugin.DebugMonitorText = "Прямая синхронизация словарей offsets активна.";
            }
            catch (Exception ex)
            {
                MMorphFixPlugin.DebugMonitorText = $"Ошибка чтения словарей: {ex.Message}";
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

            // ИСТОЧНИК ИСТИНЫ ИЗ ПАМЯТИ МЕГАМОРФА:
            // Объявляем переменные как object, чтобы компилятор не ругался на out var с dynamic-объектами
            Vector3 configThigh = Vector3.one;
            Vector3 configShin = Vector3.one;
            Vector3 configFoot = Vector3.one;

            object tObj, sObj, fObj;

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

            // Читаем живой масштаб бедра из кадра
            Vector3 liveThighScale = thighBone.localScale;

            // --- МАТРИЧНАЯ ИНВЕРСИЯ ОСЕЙ ПРИ СГИБАХ ---
            // Проецируем итоговый масштаб бедра на текущий поворот колена (из FreePose или анимации гизмо)
            Quaternion shinRot = shin.localRotation;
            Vector3 parentScaleInShinSpace = new Vector3(
                Vector3.Magnitude(shinRot * Vector3.right * liveThighScale.x),
                Vector3.Magnitude(shinRot * Vector3.up * liveThighScale.y),
                Vector3.Magnitude(shinRot * Vector3.forward * liveThighScale.z)
            );

            // Идеальная компенсация: делим чистый эталон голени из словаря (configShin) на перекос повернутых осей бедра
            Vector3 targetShinScale = new Vector3(
                configShin.x / (Mathf.Abs(parentScaleInShinSpace.x) > 0.001f ? parentScaleInShinSpace.x : 1f),
                configShin.y / (Mathf.Abs(parentScaleInShinSpace.y) > 0.001f ? parentScaleInShinSpace.y : 1f),
                configShin.z / (Mathf.Abs(parentScaleInShinSpace.z) > 0.001f ? parentScaleInShinSpace.z : 1f)
            );
            shin.localScale = targetShinScale;

            // Повторяем процедуру для стопы
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

            // --- ТЕЛЕМЕТРИЯ НА РАДАР ---
            string side = thighBone.name.StartsWith("l") ? "ЛЕВАЯ" : "ПРАВАЯ";
            string telemetry =
                $"[{side} НОГА] Слайдер_Бедра: {configThigh.ToString("F2")} | Слайдер_Голени: {configShin.ToString("F2")}\n" +
                $"            -> Угол колена гизмо: {shinRot.eulerAngles.ToString("F0")}°\n" +
                $"            -> Итоговый скейл голени: {shin.localScale.ToString("F2")} | Стопы: {foot.localScale.ToString("F2")}";

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
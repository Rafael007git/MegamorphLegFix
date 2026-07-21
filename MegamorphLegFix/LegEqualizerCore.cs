extern alias MM;

using MM::MegaMorph;
using System;
using UnityEngine;
using static MM::MegaMorph.BepInExPlugin;

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

                if (cc.bones == null || cc.bones.Length == 0)
                {
                    MMorphFixPlugin.DebugMonitorText = "Ошибка: Скелет персонажа еще не инициализирован!";
                    return;
                }

                // ПРЯМОЙ ДОСТУП: Теперь компилятор без труда находит статический словарь originals в чужой DLL!
                if (BepInExPlugin.MegaMorph.originals.TryGetValue(cc.gameObject.name, out var megaMorphInstance) && megaMorphInstance != null)
                {
                    bool leftOk = false;
                    bool rightOk = false;

                    foreach (Transform bone in cc.bones)
                    {
                        if (bone == null) continue;

                        if (bone.name.Equals("lThighBend", StringComparison.OrdinalIgnoreCase))
                        {
                            ApplyLocalMirrorFix(bone, "lShin", "lFoot", "lThighBend_scale", megaMorphInstance);
                            leftOk = true;
                        }
                        else if (bone.name.Equals("rThighBend", StringComparison.OrdinalIgnoreCase))
                        {
                            ApplyLocalMirrorFix(bone, "rShin", "rFoot", "rThighBend_scale", megaMorphInstance);
                            rightOk = true;
                        }
                    }

                    MMorphFixPlugin.DebugMonitorText = $"[{source}] Векторный фикс (Прямой). L: {(leftOk ? "ОК" : "НЕТ")}, R: {(rightOk ? "ОК" : "НЕТ")}";
                }
                else
                {
                    MMorphFixPlugin.DebugMonitorText = $"[{source}] Ожидание регистрации {cc.gameObject.name} в MegaMorph...";
                }
            }
            catch (Exception ex)
            {
                MMorphFixPlugin.DebugMonitorText = $"Критическая ошибка математики: {ex.Message}";
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private static void ApplyLocalMirrorFix(Transform thighBone, string shinName, string footName, string thighKey, BepInExPlugin.MegaMorph megaMorph)
        {
            Transform shin = FindChildRecursive(thighBone, shinName);
            Transform foot = FindChildRecursive(thighBone, footName);
            if (shin == null || foot == null) return;

            Vector3 configThigh = Vector3.one;
            Vector3 configShin = Vector3.one;
            Vector3 configFoot = Vector3.one;

            if (megaMorph.offsets.TryGetValue(thighKey, out var thighOffset)) configThigh = thighOffset.offset / 100f;
            if (megaMorph.offsets.TryGetValue(shinName + "_scale", out var shinOffset)) configShin = shinOffset.offset / 100f;
            if (megaMorph.offsets.TryGetValue(footName + "_scale", out var footOffset)) configFoot = footOffset.offset / 100f;

            // --- ДИНАМИЧЕСКИЙ РАСЧЕТ ОСЕЙ (КОЛЕННЫЙ СУСТАВ) ---
            Quaternion shinRot = shin.localRotation;
            Vector3 invThighScale = new Vector3(
                Vector3.Magnitude(shinRot * Vector3.right * configThigh.x),
                Vector3.Magnitude(shinRot * Vector3.up * configThigh.y),
                Vector3.Magnitude(shinRot * Vector3.forward * configThigh.z)
            );

            Vector3 finalShinScale = new Vector3(
                configShin.x / (Mathf.Abs(invThighScale.x) > 0.001f ? invThighScale.x : 1f),
                configShin.y / (Mathf.Abs(invThighScale.y) > 0.001f ? invThighScale.y : 1f),
                configShin.z / (Mathf.Abs(invThighScale.z) > 0.001f ? invThighScale.z : 1f)
            );
            shin.localScale = finalShinScale;

            // --- ДИНАМИЧЕСКИЙ РАСЧЕТ ОСЕЙ (ГОЛЕНОСТОП) ---
            Quaternion footRot = foot.localRotation;
            Vector3 currentCombinedParentScale = shin.lossyScale;

            Vector3 invParentScale = new Vector3(
                Vector3.Magnitude(footRot * Vector3.right * currentCombinedParentScale.x),
                Vector3.Magnitude(footRot * Vector3.up * currentCombinedParentScale.y),
                Vector3.Magnitude(footRot * Vector3.forward * currentCombinedParentScale.z)
            );

            Vector3 pelvisScale = thighBone.parent != null ? thighBone.parent.lossyScale : Vector3.one;

            Vector3 finalFootScale = new Vector3(
                (configFoot.x * pelvisScale.x) / (Mathf.Abs(invParentScale.x) > 0.001f ? invParentScale.x : 1f),
                (configFoot.y * pelvisScale.y) / (Mathf.Abs(invParentScale.y) > 0.001f ? invParentScale.y : 1f),
                (configFoot.z * pelvisScale.z) / (Mathf.Abs(invParentScale.z) > 0.001f ? invParentScale.z : 1f)
            );
            foot.localScale = finalFootScale;

            // --- ОТПРАВКА ДАННЫХ НА ВНУТРИИГРОВОЙ РАДАР ---
            string sidePrefix = thighBone.name.StartsWith("l") ? "ЛЕВАЯ" : "ПРАВАЯ";
            string telemetryLine =
                $"[{sidePrefix} НОГА] Бедро_XML: {configThigh.ToString("F2")} | Колено_Угол: {shinRot.eulerAngles.ToString("F0")}\n" +
                $"            -> Голень локальный scale: {finalShinScale.ToString("F2")} (Конфиг_XML: {configShin.ToString("F2")})\n" +
                $"            -> Стопа локальный scale: {finalFootScale.ToString("F2")} (Конфиг_XML: {configFoot.ToString("F2")})";

            if (thighBone.name.StartsWith("l"))
                MMorphFixPlugin.LeftLegTelemetry = telemetryLine;
            else
                MMorphFixPlugin.RightLegTelemetry = telemetryLine;
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
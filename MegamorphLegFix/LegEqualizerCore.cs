extern alias MM;

using MM::MegaMorph;
using System;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MMorphFix
{
    public static class LegEqualizerCore
    {
        private static bool _isProcessing = false;

        // Кэш для быстрого сбора общей телеметрии таза
        private static string _pelvisTelemetry = "[ТАЗ] pelvis: (1.00, 1.00, 1.00)";

        public static void ExecuteFix(CharacterCustomization cc)
        {
            ExecuteFix(cc, "АВТО_КОМПЕНСАЦИЯ");
        }

        public static void ExecuteFix(CharacterCustomization cc, string source)
        {
            if (cc == null || _isProcessing) return;
            if (cc.bones == null || cc.bones.Length == 0) return;

            _isProcessing = true;

            try
            {
                bool isMainPlayer = cc.gameObject.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isMainPlayer)
                    MMorphFixPlugin.LastFoundCharacter = $"{cc.gameObject.name} ({source})";

                InnerExecute(cc, source, isMainPlayer);
            }
            catch (TypeLoadException tex)
            {
                string briefError = $"[TypeLoad] Ошибка в {tex.TargetSite} -> Не найден тип: {tex.TypeName}";
                MMorphFixPlugin.DebugMonitorText = briefError;

                if (MMorphFixPlugin.InstanceLogger != null)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("=== [LegFix КРИТИЧЕСКИЙ СБОЙ ЗАГРУЗКИ ТИПОВ] ===");
                    sb.AppendLine($"Метод вызова: {tex.TargetSite}");
                    sb.AppendLine($"Имя проблемного типа: {tex.TypeName}");
                    sb.AppendLine($"Сообщение: {tex.Message}");
                    sb.AppendLine($"StackTrace:\n{tex.StackTrace}");
                    sb.AppendLine("================================================");
                    MMorphFixPlugin.InstanceLogger.LogError(sb.ToString());
                }
            }
            catch (Exception ex)
            {
                string briefError = $"[Ошибка] {ex.GetType().Name} в {ex.TargetSite} -> {ex.Message}";
                MMorphFixPlugin.DebugMonitorText = briefError;

                if (MMorphFixPlugin.InstanceLogger != null)
                {
                    MMorphFixPlugin.InstanceLogger.LogError($"[LegFix Ошибка Сустава]: {ex.GetType().Name} в {ex.TargetSite}. Подробности:\n{ex}");
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private static void InnerExecute(CharacterCustomization cc, string source, bool isMainPlayer)
        {
            BepInExPlugin.MegaMorph megaMorphComp = cc.gameObject.GetComponent<BepInExPlugin.MegaMorph>();

            bool leftOk = false;
            bool rightOk = false;

            // Обновляем данные по тазу (pelvis) для радара главного игрока
            if (isMainPlayer)
            {
                Transform pelvis = FindPelvisRecursive(cc.gameObject.transform);
                if (pelvis != null)
                {
                    _pelvisTelemetry = $"[ТАЗ] pelvis scale: {SanitizeVector(pelvis.localScale).ToString("F2")}";
                }
                else
                {
                    _pelvisTelemetry = "[ТАЗ] кость 'pelvis' не найдена в иерархии";
                }
            }

            foreach (Transform bone in cc.bones)
            {
                if (bone == null) continue;

                if (bone.name.Equals("lThighBend", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyMatrixOffsetsFix(bone, "lThighTwist", "lShin", "lFoot", megaMorphComp, isMainPlayer);
                    leftOk = true;
                }
                else if (bone.name.Equals("rThighBend", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyMatrixOffsetsFix(bone, "rThighTwist", "rShin", "rFoot", megaMorphComp, isMainPlayer);
                    rightOk = true;
                }
            }

            if (isMainPlayer && (leftOk || rightOk))
            {
                MMorphFixPlugin.FixExecutionCalls++;
                // Склеиваем статус: состояние таза + уведомление об активной матричной защите
                MMorphFixPlugin.DebugMonitorText = $"{_pelvisTelemetry} | Матричный щит колен активен.";
            }
        }

        private static void ApplyMatrixOffsetsFix(Transform thighBone, string twistName, string shinName, string footName, BepInExPlugin.MegaMorph megaMorph, bool isMainPlayer)
        {
            Transform shin = FindChildRecursive(thighBone, shinName);
            Transform foot = FindChildRecursive(thighBone, footName);
            if (shin == null || foot == null) return;

            Transform twist = FindChildRecursive(thighBone, twistName);
            if (twist != null) twist.localScale = Vector3.one;

            // Читаем идеальный целевой масштаб из ползунков Мегаморфа
            Vector3 configShin = Vector3.one;
            Vector3 configFoot = Vector3.one;

            if (megaMorph != null && megaMorph.offsets != null)
            {
                if (megaMorph.offsets.TryGetValue(shinName + "_scale", out var shinOffset))
                    configShin = shinOffset.offset / 100f;
                if (megaMorph.offsets.TryGetValue(footName + "_scale", out var footOffset))
                    configFoot = footOffset.offset / 100f;
            }

            // --- ЭТАП 1: Матричная нейтрализация искажения бедра (Кандидат №1) ---
            Quaternion localRot = shin.localRotation;

            // 1. Строим виртуальную матрицу голени без собственного масштаба
            Matrix4x4 localMatrix = Matrix4x4.TRS(Vector3.zero, localRot, Vector3.one);

            // 2. Моделируем в памяти Unity-наследование масштаба от "живого" бедра
            Matrix4x4 parentScaleMatrix = Matrix4x4.Scale(SanitizeVector(thighBone.localScale));
            Matrix4x4 combinedMatrix = parentScaleMatrix * localMatrix;

            // 3. Вычисляем длины получившихся базисных векторов (чистое искажение бедра)
            Vector3 inheritedDistortion = new Vector3(
                combinedMatrix.GetColumn(0).magnitude,
                combinedMatrix.GetColumn(1).magnitude,
                combinedMatrix.GetColumn(2).magnitude
            );

            // Страховка от деления на ноль при микро-масштабах
            inheritedDistortion.x = Mathf.Abs(inheritedDistortion.x) < 0.001f ? 1f : inheritedDistortion.x;
            inheritedDistortion.y = Mathf.Abs(inheritedDistortion.y) < 0.001f ? 1f : inheritedDistortion.y;
            inheritedDistortion.z = Mathf.Abs(inheritedDistortion.z) < 0.001f ? 1f : inheritedDistortion.z;

            // --- ЭТАП 2: Накатываем целевой масштаб, полностью гася искажение ---
            shin.localScale = new Vector3(
                configShin.x / inheritedDistortion.x,
                configShin.y / inheritedDistortion.y,
                configShin.z / inheritedDistortion.z
            );

            // Выравниваем стопу строго по её ползунку
            foot.localScale = configFoot;

            // --- ОТПРАВКА ТОЧНОЙ ТЕЛЕМЕТРИИ НА РАДАР ---
            if (isMainPlayer)
            {
                string side = thighBone.name.StartsWith("l") ? "ЛЕВАЯ" : "ПРАВАЯ";

                string telemetry =
                    $"[{side} НОГА ИГРОКА]\n" +
                    $" -> Thigh (Бедро) scale: {SanitizeVector(thighBone.localScale).ToString("F2")}\n" +
                    $" -> Shin  (Голень) scale: {shin.localScale.ToString("F2")} (Конфиг: {configShin.ToString("F2")})\n" +
                    $" -> Foot  (Стопа)  scale: {foot.localScale.ToString("F2")} (Конфиг: {configFoot.ToString("F2")})\n" +
                    $" -> Угол колена гизмо : {localRot.eulerAngles.ToString("F0")}°";

                if (thighBone.name.StartsWith("l"))
                    MMorphFixPlugin.LeftLegTelemetry = telemetry;
                else
                    MMorphFixPlugin.RightLegTelemetry = telemetry;
            }
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

        private static Transform FindPelvisRecursive(Transform current)
        {
            if (current.name.Equals("pelvis", StringComparison.OrdinalIgnoreCase)) return current;
            for (int i = 0; i < current.childCount; i++)
            {
                Transform result = FindPelvisRecursive(current.GetChild(i));
                if (result != null) return result;
            }
            return null;
        }
    }
}

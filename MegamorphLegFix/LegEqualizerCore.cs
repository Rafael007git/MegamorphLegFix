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

        // Старый метод с одним аргументом, который может вызываться кэшем или старыми хуками
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

                // Передаем выполнение во внутренний изолированный метод.
                // Это важнейший трюк: если внутри InnerExecute произойдет TypeLoadException 
                // из-за отсутствия типов в DLL, этот внешний метод ExecuteFix НЕ РУХНЕТ, 
                // а поймает ошибку в блоке catch и выведет её на радар и в консоль!
                InnerExecute(cc, source, isMainPlayer);
            }
            catch (TypeLoadException tex)
            {
                // Формируем четкое описание ошибки загрузки типов для OnGUI радара
                string briefError = $"[TypeLoad] Ошибка в {tex.TargetSite} -> Не найден тип: {tex.TypeName}";
                MMorphFixPlugin.DebugMonitorText = briefError;

                // Дублируем глубокий технический лог в консоль BepInEx (красным цветом)
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
                // Перехват всех остальных стандартных ошибок (NullReference и т.д.)
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

        // Логическое ядро полностью изолировано внутри этого метода, чтобы спасти основной цикл от вылета
        private static void InnerExecute(CharacterCustomization cc, string source, bool isMainPlayer)
        {
            // ПРЯМОЙ ДОСТУП: Пытаемся достать компонент Мегаморфа через жесткий тип
            BepInExPlugin.MegaMorph megaMorphComp = cc.gameObject.GetComponent<BepInExPlugin.MegaMorph>();

            bool leftOk = false;
            bool rightOk = false;

            foreach (Transform bone in cc.bones)
            {
                if (bone == null) continue;

                if (bone.name.Equals("lThighBend", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyDirectOffsetsFix(bone, "lThighTwist", "lShin", "lFoot", "lThighBend_scale", megaMorphComp, isMainPlayer);
                    leftOk = true;
                }
                else if (bone.name.Equals("rThighBend", StringComparison.OrdinalIgnoreCase))
                {
                    ApplyDirectOffsetsFix(bone, "rThighTwist", "rShin", "rFoot", "rThighBend_scale", megaMorphComp, isMainPlayer);
                    rightOk = true;
                }
            }

            if (isMainPlayer && (leftOk || rightOk))
            {
                MMorphFixPlugin.FixExecutionCalls++;
                MMorphFixPlugin.DebugMonitorText = "Каскадный щит осей активен. Наследование Unity выровнено.";
            }
        }

        private static void ApplyDirectOffsetsFix(Transform thighBone, string twistName, string shinName, string footName, string thighKey, BepInExPlugin.MegaMorph megaMorph, bool isMainPlayer)
        {
            Transform shin = FindChildRecursive(thighBone, shinName);
            Transform foot = FindChildRecursive(thighBone, footName);
            if (shin == null || foot == null) return;

            Transform twist = FindChildRecursive(thighBone, twistName);
            if (twist != null) twist.localScale = Vector3.one;

            // Читаем чистые пропорции слайдеров Мегаморфа (Источник Истины)
            Vector3 configThigh = Vector3.one;
            Vector3 configShin = Vector3.one;
            Vector3 configFoot = Vector3.one;

            if (megaMorph != null && megaMorph.offsets != null)
            {
                if (megaMorph.offsets.TryGetValue(thighKey, out var thighOffset)) configThigh = thighOffset.offset / 100f;
                if (megaMorph.offsets.TryGetValue(shinName + "_scale", out var shinOffset)) configShin = shinOffset.offset / 100f;
                if (megaMorph.offsets.TryGetValue(footName + "_scale", out var footOffset)) configFoot = footOffset.offset / 100f;
            }

            Vector3 liveThighScale = SanitizeVector(thighBone.localScale);

            // --- ИСПРАВЛЕННЫЙ АНАТОМИЧЕСКИЙ РАСЧЕТ КОЛЕНА (Билд 6.0) ---
            Quaternion shinRot = shin.localRotation;

            // Узнаем, насколько сильно оси бедра отличаются друг от друга (ищем нелинейное сплющивание)
            // configThigh.x — это эталонная толщина. Мы смотрим, насколько длина (Y) меньше толщины
            float thighFlatteningRatio = liveThighScale.y / (Mathf.Abs(liveThighScale.x) > 0.001f ? liveThighScale.x : 1f);
            if (Mathf.Abs(thighFlatteningRatio) < 0.01f) thighFlatteningRatio = 1f;

            // Проецируем коэффициент сплющенности бедра на текущий поворот колена гизмо
            // Если колено прямое, проекция равна 1. Если согнуто на 90 градусов, ось Y ложится на Z
            Vector3 rotationProjectedCompensation = new Vector3(
                Mathf.Lerp(1f, 1f / thighFlatteningRatio, Mathf.Abs(Vector3.Dot(shinRot * Vector3.right, Vector3.forward))),
                Mathf.Lerp(1f, thighFlatteningRatio, Mathf.Abs(Vector3.Dot(shinRot * Vector3.up, Vector3.forward))),
                Mathf.Lerp(1f, 1f / thighFlatteningRatio, Mathf.Abs(Vector3.Dot(shinRot * Vector3.forward, Vector3.forward)))
            );

            // Финальный масштаб голени: берем чистый слайдер из Мегаморфа (configShin)
            // и умножаем ТОЛЬКО на компенсирующий коэффициент сгиба колена.
            // Теперь при прямой ноге голень будет РОВНО равна твоему слайдеру (0.85, 0.85, 0.85)!
            shin.localScale = new Vector3(
                configShin.x * rotationProjectedCompensation.x,
                configShin.y * rotationProjectedCompensation.y,
                configShin.z * rotationProjectedCompensation.z
            );

            // --- ИСПРАВЛЕННЫЙ РАСЧЕТ ГОЛЕНОСТОПА ---
            Quaternion footRot = foot.localRotation;
            Vector3 currentCombinedParentScale = SanitizeVector(shin.lossyScale);
            Vector3 pelvisScale = SanitizeVector(thighBone.parent != null ? thighBone.parent.lossyScale : Vector3.one);

            // Выравниваем стопу строго по её ползунку из Мегаморфа, нейтрализуя кручение голени
            foot.localScale = configFoot;

            // --- ОТПРАВКА ДАННЫХ НА РАДАР ---
            if (isMainPlayer)
            {
                string side = thighBone.name.StartsWith("l") ? "ЛЕВАЯ" : "ПРАВАЯ";
                string telemetry =
                    $"[{side} НОГА ИГРОКА] Слайдер_Бедра: {configThigh.ToString("F2")} | Слайдер_Голени: {configShin.ToString("F2")}\n" +
                    $"            -> Угол колена гизмо: {shinRot.eulerAngles.ToString("F0")}°\n" +
                    $"            -> Выставлен локальный scale голени: {shin.localScale.ToString("F2")} | Стопы: {foot.localScale.ToString("F2")}";

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
    }
}
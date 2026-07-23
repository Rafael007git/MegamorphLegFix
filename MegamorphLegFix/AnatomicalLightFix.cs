using System;
using UnityEngine;
using HarmonyLib;

namespace MMorphFix
{
    public static class AnatomicalLightFix
    {
        // Переменные для вывода на ваш UI-радар
        public static float Radar_HipScaleY = 1.0f;
        public static float Radar_HipLossyScaleY = 1.0f;
        public static float Radar_Light1_Intensity = 0.0f;
        public static float Radar_Light5_Intensity = 0.0f;
        public static float Radar_Light6_Intensity = 0.0f;
        public static string Radar_Status = "Ожидание...";

        private static System.Reflection.FieldInfo _letItBeDarkField = null;
        private static bool _reflectionChecked = false;

        /// <summary>
        /// Универсальный поисковик активного персонажа, устойчивый к клонам во FreePose.
        /// </summary>
        public static void MonitorAndExecute(GameObject fallBackRoot)
        {
            // ШАГ 1: Вместо переданного корня мы ищем ВСЕ CharacterCustomization на сцене.
            // Во FreePose один из них будет активным клоном, перед которым стоит камера.
            CharacterCustomization activeCC = null;
            var allCCs = UnityEngine.Object.FindObjectsOfType<CharacterCustomization>();

            if (allCCs != null && allCCs.Length > 0)
            {
                foreach (var cc in allCCs)
                {
                    // Ищем тот объект, который активен в иерархии Unity прямо сейчас
                    if (cc != null && cc.gameObject != null && cc.gameObject.activeInHierarchy)
                    {
                        activeCC = cc;
                        break;
                    }
                }
            }

            // Если не нашли активный скрипт на сцене, берем то, что передала корутина
            GameObject targetRoot = (activeCC != null) ? activeCC.gameObject : fallBackRoot;

            if (targetRoot == null)
            {
                Radar_Status = "Ошибка: Персонаж не найден на сцене";
                return;
            }

            // ШАГ 2: Ищем кость "hip" во всем дереве активного персонажа
            Transform activeHip = FindBoneRecursive(targetRoot.transform, "hip");

            if (activeHip == null)
            {
                // На случай если кодовое имя совсем другое, ищем просто по слову "hip" внутри
                activeHip = FindBoneContainsRecursive(targetRoot.transform, "hip");
            }

            if (activeHip == null)
            {
                Radar_Status = $"Hip не найден в объекте: {targetRoot.name}";
                return;
            }

            // ШАГ 3: Записываем масштабы в реальном времени для нашего радара
            Radar_HipScaleY = activeHip.localScale.y;
            Radar_HipLossyScaleY = activeHip.lossyScale.y;

            // ШАГ 4: Находим группу источников света
            Transform activeLightGroup = FindBoneRecursive(activeHip, "character light group");
            if (activeLightGroup == null)
            {
                // Пробуем прямой путь, если рекурсия не сработала
                activeLightGroup = activeHip.Find("abdomenLower/abdomenUpper/chestLower/character light group");
            }

            if (activeLightGroup == null)
            {
                Radar_Status = $"Огни не найдены в {activeHip.name} ({targetRoot.name})";
                return;
            }

            Radar_Status = $"СВЕТ НАЙДЕН! Цель: {targetRoot.name}";

            // Чтение Let It Be Dark
            if (!_reflectionChecked)
            {
                _reflectionChecked = true;
                try
                {
                    Type letItBeDarkClass = AccessTools.TypeByName("LetItBeDark.LetItBeDarkPlugin")
                                         ?? AccessTools.TypeByName("LetItBeDarkPlugin");
                    if (letItBeDarkClass != null)
                        _letItBeDarkField = AccessTools.Field(letItBeDarkClass, "GlobalLightMultiplier");
                }
                catch { _letItBeDarkField = null; }
            }

            float letItBeDarkMultiplier = 1.0f;
            if (_letItBeDarkField != null)
            {
                try { letItBeDarkMultiplier = (float)_letItBeDarkField.GetValue(null); }
                catch { letItBeDarkMultiplier = 1.0f; }
            }

            // Фикс света на основе ГЛОБАЛЬНОГО масштаба персонажа (спасет, если гизмо крутит родительский меш)
            // Мы берем lossyScale.y, так как он учитывает сжатие всей цепочки от самого верха
            float hipModifier = (float.IsNaN(Radar_HipLossyScaleY) || float.IsInfinity(Radar_HipLossyScaleY) || Mathf.Abs(Radar_HipLossyScaleY) < 0.01f) ? 1.0f : Radar_HipLossyScaleY;

            // Применяем формулу произведения
            for (int i = 0; i < activeLightGroup.childCount; i++)
            {
                Transform lightObj = activeLightGroup.GetChild(i);
                Light light = lightObj.GetComponent<Light>();

                if (light != null && light.type == LightType.Point)
                {
                    float baseIntensity = 1.0f;
                    string name = lightObj.name;

                    if (name.Equals("Point Light (1)", StringComparison.OrdinalIgnoreCase))
                        baseIntensity = ModConfigManager.Light1Intensity;
                    else if (name.Equals("Point Light (5)", StringComparison.OrdinalIgnoreCase))
                        baseIntensity = ModConfigManager.Light5Intensity;
                    else if (name.Equals("Point Light (6)", StringComparison.OrdinalIgnoreCase))
                        baseIntensity = ModConfigManager.Light6Intensity;
                    else
                        continue;

                    light.intensity = baseIntensity * letItBeDarkMultiplier * hipModifier;

                    // Запись значений на радар
                    if (name.Equals("Point Light (1)", StringComparison.OrdinalIgnoreCase))
                        Radar_Light1_Intensity = light.intensity;
                    else if (name.Equals("Point Light (5)", StringComparison.OrdinalIgnoreCase))
                        Radar_Light5_Intensity = light.intensity;
                    else if (name.Equals("Point Light (6)", StringComparison.OrdinalIgnoreCase))
                        Radar_Light6_Intensity = light.intensity;
                }
            }
        }

        private static Transform FindBoneRecursive(Transform parent, string targetName)
        {
            if (parent.name.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
                parent.name.EndsWith("/" + targetName, StringComparison.OrdinalIgnoreCase) ||
                parent.name.EndsWith("_" + targetName, StringComparison.OrdinalIgnoreCase))
            {
                return parent;
            }
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindBoneRecursive(parent.GetChild(i), targetName);
                if (result != null) return result;
            }
            return null;
        }

        private static Transform FindBoneContainsRecursive(Transform parent, string partialName)
        {
            if (parent.name.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return parent;
            }
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindBoneContainsRecursive(parent.GetChild(i), partialName);
                if (result != null) return result;
            }
            return null;
        }
    }
}

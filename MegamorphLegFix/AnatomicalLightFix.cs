using System;
using UnityEngine;
using HarmonyLib;

namespace MMorphFix
{
    public static class AnatomicalLightFix
    {
        // Переменные для вывода на отладочный UI-радар
        public static float Radar_HipScaleY = 1.0f;
        public static float Radar_HipLossyScaleY = 1.0f;
        public static float Radar_Light1_Intensity = 0.0f;
        public static float Radar_Light5_Intensity = 0.0f;
        public static float Radar_Light6_Intensity = 0.0f;
        public static string Radar_Status = "Ожидание изменений...";

        // Статические поля для кэширования Reflection мода Let It Be Dark
        private static System.Reflection.FieldInfo _letItBeDarkField = null;
        private static bool _reflectionChecked = false;

        /// <summary>
        /// Чистый событийный фикс: вызывается из хуков MegaMorph и Гизмо FreePose.
        /// Нагрузка в простое — 0%.
        /// </summary>
        public static void ExecuteFix(GameObject characterRoot)
        {
            if (characterRoot == null)
            {
                Radar_Status = "Нет корня персонажа";
                return;
            }

            // Находим настоящую кость hip персонажа
            Transform hip = FindBoneRecursive(characterRoot.transform, "hip");
            if (hip == null)
            {
                Radar_Status = $"Кость hip не найдена в {characterRoot.name}";
                return;
            }

            // Находим группу огоньков
            Transform lightGroup = FindBoneRecursive(hip, "character light group");
            if (lightGroup == null)
            {
                Radar_Status = $"character light group не найдена в {hip.name}";
                return;
            }

            // Пишем статус успеха на радар
            Radar_Status = $"СВЕТ ОБНОВЛЕН! Цель: {characterRoot.name}";

            // Обновляем масштабы для радара
            Radar_HipScaleY = hip.localScale.y;
            Radar_HipLossyScaleY = hip.lossyScale.y;

            // Защита от NaN, бесконечности и критического деления
            float hipModifier = (float.IsNaN(Radar_HipLossyScaleY) || float.IsInfinity(Radar_HipLossyScaleY) || Mathf.Abs(Radar_HipLossyScaleY) < 0.01f) ? 1.0f : Radar_HipLossyScaleY;

            // КВАДРАТИЧНЫЙ КОЭФФИЦИЕНТ: Реализация закона обратных квадратов
            float quadraticModifier = hipModifier * hipModifier;

            // Мост к Let It Be Dark (поиск класса строго 1 раз за весь запуск игры)
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

            // Применяем формулу произведения к огонькам
            for (int i = 0; i < lightGroup.childCount; i++)
            {
                Transform lightObj = lightGroup.GetChild(i);
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

                    // Бесконфликтная физическая интенсивность: [Ползунок F1] * [Let It Be Dark] * [Масштаб^2]
                    light.intensity = baseIntensity * letItBeDarkMultiplier * quadraticModifier;

                    // Записываем финальные значения яркости лампочек прямо на радар
                    if (name.Equals("Point Light (1)", StringComparison.OrdinalIgnoreCase)) Radar_Light1_Intensity = light.intensity;
                    if (name.Equals("Point Light (5)", StringComparison.OrdinalIgnoreCase)) Radar_Light5_Intensity = light.intensity;
                    if (name.Equals("Point Light (6)", StringComparison.OrdinalIgnoreCase)) Radar_Light6_Intensity = light.intensity;
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
    }
}

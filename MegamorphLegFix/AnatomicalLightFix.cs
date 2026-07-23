using System;
using UnityEngine;
using HarmonyLib;

namespace MMorphFix
{
    public static class AnatomicalLightFix
    {
        // Переменные для вывода на ваш UI-радар (прочитайте их в вашем OnGUI)
        public static float Radar_HipScaleY = 1.0f;
        public static float Radar_HipLossyScaleY = 1.0f;
        public static float Radar_Light1_Intensity = 0.0f;
        public static float Radar_Light5_Intensity = 0.0f;
        public static float Radar_Light6_Intensity = 0.0f;
        public static string Radar_Status = "Ожидание...";

        private static System.Reflection.FieldInfo _letItBeDarkField = null;
        private static bool _reflectionChecked = false;

        public static void MonitorAndExecute(GameObject characterRoot)
        {
            if (characterRoot == null)
            {
                Radar_Status = "Нет корня персонажа";
                return;
            }

            // ТЕСТОВЫЙ РЕЖИМ: Полностью отключаем кэш! 
            // Ищем кость hip заново каждый кадр, чтобы поймать подмену костей во FreePose
            Transform activeHip = FindBoneRecursive(characterRoot.transform, "hip");

            if (activeHip == null)
            {
                Radar_Status = "Кость 'hip' не найдена";
                return;
            }

            // Записываем масштаб активной кости для радара
            Radar_HipScaleY = activeHip.localScale.y;
            Radar_HipLossyScaleY = activeHip.lossyScale.y;

            // Ищем группу огоньков рекурсивно, так как гизмо во FreePose может менять иерархию пути
            Transform activeLightGroup = FindBoneRecursive(activeHip, "character light group");

            if (activeLightGroup == null)
            {
                Radar_Status = "Группа огней не найдена в hip";
                return;
            }

            Radar_Status = "Фикс Активен (Огни найдены)";

            // Быстрый поиск Let It Be Dark
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

            // Масштаб сжатия кости hip
            float hipModifier = (float.IsNaN(Radar_HipScaleY) || float.IsInfinity(Radar_HipScaleY) || Mathf.Abs(Radar_HipScaleY) < 0.01f) ? 1.0f : Radar_HipScaleY;

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

                    // Устанавливаем итоговую интенсивность
                    light.intensity = baseIntensity * letItBeDarkMultiplier * hipModifier;

                    // Записываем РЕАЛЬНОЕ значение яркости лампочки из движка Unity прямо на радар
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
    }
}

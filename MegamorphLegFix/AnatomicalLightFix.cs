using System;
using UnityEngine;
using HarmonyLib;

namespace MMorphFix
{
    public static class AnatomicalLightFix
    {
        // Кэш ссылок на компоненты конкретного персонажа
        private static Transform _cachedHip = null;
        private static Transform _cachedLightGroup = null;
        private static int _cachedCharacterId = 0;

        // Кэш Reflection для мода Let It Be Dark
        private static System.Reflection.FieldInfo _letItBeDarkField = null;
        private static bool _reflectionChecked = false;

        /// <summary>
        /// Основной метод коррекции скелетного освещения. 
        /// Вызывается в конце кадра из RunFixAtEndOfFrame.
        /// </summary>
        public static void MonitorAndExecute(GameObject characterRoot)
        {
            if (characterRoot == null) return;

            int currentId = characterRoot.GetInstanceID();

            // 1. Инициализируем или обновляем кэш ссылок при смене персонажа на сцене
            if (_cachedCharacterId != currentId || _cachedHip == null || _cachedLightGroup == null)
            {
                _cachedCharacterId = currentId;

                // Умный поиск кости hip (устойчивый к префиксам вида "Bone/hip")
                _cachedHip = FindBoneRecursive(characterRoot.transform, "hip");

                if (_cachedHip != null)
                {
                    // Пробуем найти группу по прямому пути
                    _cachedLightGroup = _cachedHip.Find("abdomenLower/abdomenUpper/chestLower/character light group");

                    // Защитный fallback: если путь сбился, ищем группу рекурсивно внутри hip
                    if (_cachedLightGroup == null)
                    {
                        _cachedLightGroup = FindBoneRecursive(_cachedHip, "character light group");
                    }
                }
            }

            if (_cachedHip == null || _cachedLightGroup == null) return;

            // 2. КЭШ РЕФЛЕКСИИ: Ищем поле мода Let It Be Dark строго один раз за весь запуск игры
            if (!_reflectionChecked)
            {
                _reflectionChecked = true;
                try
                {
                    Type letItBeDarkClass = AccessTools.TypeByName("LetItBeDark.LetItBeDarkPlugin")
                                         ?? AccessTools.TypeByName("LetItBeDarkPlugin");
                    if (letItBeDarkClass != null)
                    {
                        _letItBeDarkField = AccessTools.Field(letItBeDarkClass, "GlobalLightMultiplier");
                    }
                }
                catch
                {
                    _letItBeDarkField = null;
                }
            }

            // Динамически вытягиваем множитель темноты за наносекунды
            float letItBeDarkMultiplier = 1.0f;
            if (_letItBeDarkField != null)
            {
                try
                {
                    letItBeDarkMultiplier = (float)_letItBeDarkField.GetValue(null);
                }
                catch
                {
                    letItBeDarkMultiplier = 1.0f;
                }
            }

            // 3. Рассчитываем коэффициент усадки на основе вертикального масштаба кости hip
            float hipModifier = 1.0f;
            if (ModConfigManager.IsFixEnabled)
            {
                float yScale = _cachedHip.localScale.y;
                // Защита от NaN, бесконечности и деления на критически малые величины
                hipModifier = (float.IsNaN(yScale) || float.IsInfinity(yScale) || Mathf.Abs(yScale) < 0.01f) ? 1.0f : yScale;
            }

            // 4. Применяем формулу произведения к огонькам, забирая базы из ModConfigManager (меню F1)
            for (int i = 0; i < _cachedLightGroup.childCount; i++)
            {
                Transform lightObj = _cachedLightGroup.GetChild(i);
                Light light = lightObj.GetComponent<Light>();

                if (light != null && light.type == LightType.Point)
                {
                    float baseIntensity = 1.0f;
                    string name = lightObj.name;

                    // Связываем каждый огонек со своим персональным ползунком в конфиге мода
                    if (name.Equals("Point Light (1)", StringComparison.OrdinalIgnoreCase))
                        baseIntensity = ModConfigManager.Light1Intensity;
                    else if (name.Equals("Point Light (5)", StringComparison.OrdinalIgnoreCase))
                        baseIntensity = ModConfigManager.Light5Intensity;
                    else if (name.Equals("Point Light (6)", StringComparison.OrdinalIgnoreCase))
                        baseIntensity = ModConfigManager.Light6Intensity;
                    else
                        continue; // Игнорируем сторонние объекты в группе, если они есть

                    // МАТЕМАТИЧЕСКАЯ СИНЕРГИЯ: [Ползунок F1] * [Интерьерный мод] * [Масштаб тела]
                    // Спам в лог полностью убран, процессор крутит чистые регистры
                    light.intensity = baseIntensity * letItBeDarkMultiplier * hipModifier;
                }
            }
        }

        /// <summary>
        /// Рекурсивный поиск костей, устойчивый к иерархическим префиксам движка (например, "Bone/hip")
        /// </summary>
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

        /// <summary>
        /// Сброс кэша ссылок (полезно вызывать при полной очистке сцены или смене локации)
        /// </summary>
        public static void ResetCache()
        {
            _cachedLightGroup = null;
            _cachedHip = null;
            _cachedCharacterId = 0;
        }
    }
}

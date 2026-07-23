using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace MMorphFix
{
    public static class AnatomicalLightFix
    {
        // 1. Наш жесткий эталон констант (зашьем после замера)
        private static readonly Dictionary<string, float> _referenceIntensities = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            { "Point Light (1)", 2.5f },
            { "Point Light (5)", 2.5f },
            { "Point Light (6)", 2.5f }
        };

        // Переменные для отслеживания изменений масштаба без тяжелого поиска
        private static Vector3 _lastTrackedScale = Vector3.one;
        private static float _lastLetItBeDarkMultiplier = -1f;

        // Кэш ссылок на компоненты, чтобы не искать их каждый кадр
        private static Transform _cachedHip = null;
        private static Transform _cachedLightGroup = null;
        private static int _cachedCharacterId = 0;
        private static System.Reflection.FieldInfo _letItBeDarkField = null;
        private static bool _reflectionChecked = false;

        /// <summary>
        /// Вызывается в LateUpdate. Проверяет дельту масштаба кости hip. 
        /// Если масштаб изменился (пресет, поза, слайдер) — мгновенно корректирует свет.
        /// </summary>
        public static void MonitorAndExecute(GameObject characterRoot)
        {
            if (characterRoot == null) return;

            int currentId = characterRoot.GetInstanceID();

            // 1. Инициализируем или обновляем кэш ссылок при смене персонажа
            if (_cachedCharacterId != currentId || _cachedHip == null || _cachedLightGroup == null)
            {
                _cachedCharacterId = currentId;
                _cachedHip = FindBoneRecursive(characterRoot.transform, "hip");
                if (_cachedHip != null)
                {
                    _cachedLightGroup = _cachedHip.Find("abdomenLower/abdomenUpper/chestLower/character light group");
                    _lastTrackedScale = _cachedHip.localScale; // Сбрасываем трекер масштаба
                }
            }

            if (_cachedHip == null || _cachedLightGroup == null) return;

            // 2. Быстрый и легкий опрос Let It Be Dark (раз в кадр)
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

            // 3. ПРОВЕРКА НА ИЗМЕНЕНИЯ (Ядро эргономики):
            // Мы сравниваем текущий масштаб кости и значение слайдера с прошлым кадром.
            // Если они ТОЧНО ТАКИЕ ЖЕ, мы просто выходим из метода. Процессор отдыхает!
            Vector3 currentScale = _cachedHip.localScale;
            if (currentScale == _lastTrackedScale && Mathf.Approximately(letItBeDarkMultiplier, _lastLetItBeDarkMultiplier))
            {
                return;
            }

            // Если управление дошло сюда — значит сработал пресет, анимация, поза или слайдер!
            _lastTrackedScale = currentScale;
            _lastLetItBeDarkMultiplier = letItBeDarkMultiplier;

            // Защита от NaN / деления на ноль
            float yScale = currentScale.y;
            float hipModifier = (float.IsNaN(yScale) || float.IsInfinity(yScale) || Mathf.Abs(yScale) < 0.01f) ? 1.0f : yScale;

            // 4. Мгновенно применяем формулу произведения к огонькам
            for (int i = 0; i < _cachedLightGroup.childCount; i++)
            {
                Transform lightObj = _cachedLightGroup.GetChild(i);
                if (_referenceIntensities.TryGetValue(lightObj.name, out float baseIntensity))
                {
                    Light light = lightObj.GetComponent<Light>();
                    if (light != null && light.type == LightType.Point)
                    {
                        // ВРЕМЕННЫЙ ЗАМЕР: Вызовется ТОЛЬКО в момент смены позы/пресета/анимации!
                        MMorphFixPlugin.InstanceLogger.LogInfo($"[ЗАМЕР ОГНЕЙ] {lightObj.name} зафиксировал изменения! Исходная intensity: {light.intensity}");

                        light.intensity = baseIntensity * letItBeDarkMultiplier * hipModifier;
                    }
                }
            }
        }

        private static Transform FindBoneRecursive(Transform parent, string name)
        {
            if (parent.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindBoneRecursive(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }

        public static void ResetCache()
        {
            _cachedLightGroup = null;
            _cachedHip = null;
            _cachedCharacterId = 0;
            _lastTrackedScale = Vector3.one;
            _lastLetItBeDarkMultiplier = -1f;
        }
    }
}

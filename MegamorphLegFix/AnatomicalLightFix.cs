using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;

namespace MMorphFix
{
    public static class AnatomicalLightFix
    {
        private static readonly Dictionary<string, float> _referenceIntensities = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
        {
            { "Point Light (1)", 2.5f },
            { "Point Light (5)", 2.5f },
            { "Point Light (6)", 2.5f }
        };

        private static Vector3 _lastTrackedScale = Vector3.one;
        private static float _lastLetItBeDarkMultiplier = -1f;

        private static Transform _cachedHip = null;
        private static Transform _cachedLightGroup = null;
        private static int _cachedCharacterId = 0;
        private static System.Reflection.FieldInfo _letItBeDarkField = null;
        private static bool _reflectionChecked = false;

        public static void MonitorAndExecute(GameObject characterRoot)
        {
            if (characterRoot == null) return;

            int currentId = characterRoot.GetInstanceID();

            if (_cachedCharacterId != currentId || _cachedHip == null || _cachedLightGroup == null)
            {
                _cachedCharacterId = currentId;

                // УЛУЧШЕННЫЙ ПОИСК: ищем кость hip, даже если она называется "Bone/hip"
                _cachedHip = FindBoneRecursive(characterRoot.transform, "hip");

                if (_cachedHip != null)
                {
                    // Пробуем найти группу по вашему пути
                    _cachedLightGroup = _cachedHip.Find("abdomenLower/abdomenUpper/chestLower/character light group");

                    // Защита: если путь изменился из-за префикса "Bone/", ищем группу рекурсивно внутри hip
                    if (_cachedLightGroup == null)
                    {
                        _cachedLightGroup = FindBoneRecursive(_cachedHip, "character light group");
                    }
                }
                else
                {
                    // Глубокий отладочный лог: пишется только один раз при спавне, если скелет вообще не распознан
                    MMorphFixPlugin.InstanceLogger.LogWarning($"[LegFix-Light] Не удалось найти кость 'hip' в корне {characterRoot.name}");
                }
            }

            if (_cachedHip == null || _cachedLightGroup == null) return;

            // Опрос Let It Be Dark
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

            Vector3 currentScale = _cachedHip.localScale;
            if (currentScale == _lastTrackedScale && Mathf.Approximately(letItBeDarkMultiplier, _lastLetItBeDarkMultiplier))
            {
                return;
            }

            _lastTrackedScale = currentScale;
            _lastLetItBeDarkMultiplier = letItBeDarkMultiplier;

            float yScale = currentScale.y;
            float hipModifier = (float.IsNaN(yScale) || float.IsInfinity(yScale) || Mathf.Abs(yScale) < 0.01f) ? 1.0f : yScale;

            // Применяем формулу и выводим заветный замер
            for (int i = 0; i < _cachedLightGroup.childCount; i++)
            {
                Transform lightObj = _cachedLightGroup.GetChild(i);
                if (_referenceIntensities.ContainsKey(lightObj.name))
                {
                    Light light = lightObj.GetComponent<Light>();
                    if (light != null && light.type == LightType.Point)
                    {
                        // ЭТА СТРОКА ОБЯЗАНА ВСПЛЫТЬ В КОНСОЛИ ПРИ СМЕНЕ ПОЗЫ / ПРЕСЕТА
                        MMorphFixPlugin.InstanceLogger.LogInfo($"[ЗАМЕР ОГНЕЙ] {lightObj.name} зафиксирован! Базовая Intensity: {light.intensity}");
                    }
                }
            }
        }

        /// <summary>
        /// Умный поиск кости, устойчивый к префиксам вида "Bone/hip" или "Pose/hip"
        /// </summary>
        private static Transform FindBoneRecursive(Transform parent, string targetName)
        {
            // Проверяем, оканчивается ли имя объекта на нужную нам кость (например, "Bone/hip" оканчивается на "hip")
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

extern alias MM;

using MM::MegaMorph;
using System;
using UnityEngine;

namespace MMorphFix
{
    public static class MegamorphMathCore
    {
        /// <summary>
        /// ЭТАП 1 и 2: Защита сустава в изолированном матричном бункере в памяти
        /// </summary>
        public static void ProtectJoint(
            Transform childBone,
            Vector3 idealChildScale,
            Vector3 idealParentScale,
            Vector3 idealGrandparentScale)
        {
            if (!MMorphFixPlugin.IsFixEnabled) return;

            Quaternion localRot = childBone.localRotation;

            // 1. Виртуальная матрица сустава (только изгиб, масштаб чистая единица)
            Matrix4x4 localMatrix = Matrix4x4.TRS(Vector3.zero, localRot, Vector3.one);

            // 2. ВМЕСТО ЛОКАЛЬНЫХ КОСТЕЙ СЦЕНЫ: Строим честный масштаб родителей из памяти Мегаморфа!
            // Перемножаем эталон бедра и эталон твиста, полученные из словаря Мегаморфа.
            // Это автоматически учитывает ВСЕ десятки ползунков, которые на них повлияли.
            Vector3 accumulatedParentScale = Vector3.Scale(idealGrandparentScale, idealParentScale);
            accumulatedParentScale = SanitizeVector(accumulatedParentScale);

            // 3. Загружаем этот честный каскад в матрицу искажения
            Matrix4x4 parentScaleMatrix = Matrix4x4.Scale(accumulatedParentScale);
            Matrix4x4 combinedMatrix = parentScaleMatrix * localMatrix;

            // 4. Вычисляем чистый вектор пространственной деформации колена
            Vector3 inheritedDistortion = new Vector3(
                combinedMatrix.GetColumn(0).magnitude,
                combinedMatrix.GetColumn(1).magnitude,
                combinedMatrix.GetColumn(2).magnitude
            );

            // Страховка от деления на ноль
            inheritedDistortion.x = Mathf.Abs(inheritedDistortion.x) < 0.001f ? 1f : inheritedDistortion.x;
            inheritedDistortion.y = Mathf.Abs(inheritedDistortion.y) < 0.001f ? 1f : inheritedDistortion.y;
            inheritedDistortion.z = Mathf.Abs(inheritedDistortion.z) < 0.001f ? 1f : inheritedDistortion.z;

            // 5. НАКАТЫВАНИЕ: Корректируем локальный масштаб голени
            childBone.localScale = new Vector3(
                idealChildScale.x / inheritedDistortion.x,
                idealChildScale.y / inheritedDistortion.y,
                idealChildScale.z / inheritedDistortion.z
            );
        }


        /// <summary>
        /// Безопасное извлечение дельты ползунка из памяти Мегаморфа
        /// </summary>
        public static Vector3 GetMegamorphScale(BepInExPlugin.MegaMorph megaMorph, string boneScaleKey)
        {
            if (megaMorph != null && megaMorph.offsets != null)
            {
                if (megaMorph.offsets.TryGetValue(boneScaleKey, out var offsetData))
                {
                    return offsetData.offset / 100f; // Перевод из процентов в шкалу Unity
                }
            }
            return Vector3.one;
        }

        /// <summary>
        /// Очистка векторов от NaN, Infinity и критических микро-значений
        /// </summary>
        public static Vector3 SanitizeVector(Vector3 vector)
        {
            float x = (float.IsNaN(vector.x) || float.IsInfinity(vector.x) || Mathf.Abs(vector.x) < 0.01f) ? 1.0f : vector.x;
            float y = (float.IsNaN(vector.y) || float.IsInfinity(vector.y) || Mathf.Abs(vector.y) < 0.01f) ? 1.0f : vector.y;
            float z = (float.IsNaN(vector.z) || float.IsInfinity(vector.z) || Mathf.Abs(vector.z) < 0.01f) ? 1.0f : vector.z;
            return new Vector3(x, y, z);
        }
    }
}

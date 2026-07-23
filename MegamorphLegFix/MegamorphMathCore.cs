extern alias MM;

using MM::MegaMorph;
using System;
using UnityEngine;

namespace MMorphFix
{
    public static class MegamorphMathCore
    {
        /// <summary>
        /// ЭТАП 1 и 2: Изолированный матричный бункер компенсации углов (v0.8.5)
        /// </summary>
        public static void ProtectJoint(Transform childBone, Vector3 idealChildScaleWithParent, Transform parentBone)
        {
            // Если пользователь выключил щит кнопкой на радаре — мгновенно выходим
            if (!MMorphFixPlugin.IsFixEnabled) return;

            Quaternion localRot = childBone.localRotation;

            // 1. Виртуальная матрица сустава (только изгиб, масштаб чистая единица)
            Matrix4x4 localMatrix = Matrix4x4.TRS(Vector3.zero, localRot, Vector3.one);

            // 2. Считываем живой локальный масштаб родительской кости со сцены
            Vector3 parentScale = SanitizeVector(parentBone.localScale);

            // 3. Загружаем живой каскад в матрицу искажения
            Matrix4x4 parentScaleMatrix = Matrix4x4.Scale(parentScale);
            Matrix4x4 combinedMatrix = parentScaleMatrix * localMatrix;

            // 4. Вычисляем чистый вектор пространственной деформации
            Vector3 inheritedDistortion = new Vector3(
                combinedMatrix.GetColumn(0).magnitude,
                combinedMatrix.GetColumn(1).magnitude,
                combinedMatrix.GetColumn(2).magnitude
            );

            // Страховка от деления на ноль
            inheritedDistortion.x = Mathf.Abs(inheritedDistortion.x) < 0.001f ? 1f : inheritedDistortion.x;
            inheritedDistortion.y = Mathf.Abs(inheritedDistortion.y) < 0.001f ? 1f : inheritedDistortion.y;
            inheritedDistortion.z = Mathf.Abs(inheritedDistortion.z) < 0.001f ? 1f : inheritedDistortion.z;

            // 5. НАКАТЫВАНИЕ: Делим симметричный числитель на честное искажение предков
            childBone.localScale = new Vector3(
                idealChildScaleWithParent.x / inheritedDistortion.x,
                idealChildScaleWithParent.y / inheritedDistortion.y,
                idealChildScaleWithParent.z / inheritedDistortion.z
            );
        }

        public static Vector3 GetMegamorphScale(BepInExPlugin.MegaMorph megaMorph, string boneScaleKey)
        {
            if (megaMorph != null && megaMorph.offsets != null)
            {
                if (megaMorph.offsets.TryGetValue(boneScaleKey, out var offsetData))
                {
                    return offsetData.offset / 100f;
                }
            }
            return Vector3.one;
        }

        public static Vector3 SanitizeVector(Vector3 vector)
        {
            float x = (float.IsNaN(vector.x) || float.IsInfinity(vector.x) || Mathf.Abs(vector.x) < 0.01f) ? 1.0f : vector.x;
            float y = (float.IsNaN(vector.y) || float.IsInfinity(vector.y) || Mathf.Abs(vector.y) < 0.01f) ? 1.0f : vector.y;
            float z = (float.IsNaN(vector.z) || float.IsInfinity(vector.z) || Mathf.Abs(vector.z) < 0.01f) ? 1.0f : vector.z;
            return new Vector3(x, y, z);
        }
    }
}

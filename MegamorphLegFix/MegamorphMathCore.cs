extern alias MM;

using MM::MegaMorph;
using System;
using UnityEngine;

namespace MMorphFix
{
    public static class MegamorphMathCore
    {
        /// <summary>
        /// Матричный Щит Изгиба: компенсирует ТОЛЬКО тригонометрический перекос от поворота (v0.8.6)
        /// </summary>
        public static void ProtectJoint(Transform childBone, Vector3 idealChildScale, Transform parentBone)
        {
            if (!MMorphFixPlugin.IsFixEnabled) return;

            Quaternion localRot = childBone.localRotation;
            Vector3 parentScale = SanitizeVector(parentBone.localScale);

            // Если родителем является ThighTwist, его собственный localScale равен 1, 
            // но на него давит реальное бедро ThighBend. Берем масштаб дедушки для честности.
            if (parentBone.name.EndsWith("ThighTwist", StringComparison.OrdinalIgnoreCase) && parentBone.parent != null)
            {
                parentScale = SanitizeVector(parentBone.parent.localScale);
            }

            // 1. Матрица при ЖИВОМ ПОВОРОТЕ сустава на сцене
            Matrix4x4 localMatrixLive = Matrix4x4.TRS(Vector3.zero, localRot, Vector3.one);
            Matrix4x4 parentScaleMatrix = Matrix4x4.Scale(parentScale);
            Matrix4x4 combinedMatrixLive = parentScaleMatrix * localMatrixLive;

            Vector3 distortionLive = new Vector3(
                combinedMatrixLive.GetColumn(0).magnitude,
                combinedMatrixLive.GetColumn(1).magnitude,
                combinedMatrixLive.GetColumn(2).magnitude
            );

            // 2. ЭТАЛОННАЯ Матрица при ПРЯМОМ суставе (Угол строго 0 градусов)
            Matrix4x4 localMatrixStraight = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
            Matrix4x4 combinedMatrixStraight = parentScaleMatrix * localMatrixStraight;

            Vector3 distortionStraight = new Vector3(
                combinedMatrixStraight.GetColumn(0).magnitude,
                combinedMatrixStraight.GetColumn(1).magnitude,
                combinedMatrixStraight.GetColumn(2).magnitude
            );

            // Санитарная проверка от деления на ноль
            distortionLive.x = Mathf.Abs(distortionLive.x) < 0.001f ? 1f : distortionLive.x;
            distortionLive.y = Mathf.Abs(distortionLive.y) < 0.001f ? 1f : distortionLive.y;
            distortionLive.z = Mathf.Abs(distortionLive.z) < 0.001f ? 1f : distortionLive.z;

            distortionStraight.x = Mathf.Abs(distortionStraight.x) < 0.001f ? 1f : distortionStraight.x;
            distortionStraight.y = Mathf.Abs(distortionStraight.y) < 0.001f ? 1f : distortionStraight.y;
            distortionStraight.z = Mathf.Abs(distortionStraight.z) < 0.001f ? 1f : distortionStraight.z;

            // ЧИСТЫЙ КОЭФФИЦИЕНТ ИСКАЖЕНИЯ ОТ ИЗГИБА СУСТАВА
            // На прямой ноге distortionLive и distortionStraight равны один в один! 
            // Их деление друг на друга даст чистую, идеальную единицу (1, 1, 1).
            Vector3 angularPureDistortion = new Vector3(
                distortionLive.x / distortionStraight.x,
                distortionLive.y / distortionStraight.y,
                distortionLive.z / distortionStraight.z
            );

            // НАКАТЫВАНИЕ: Корректируем масштаб строго на дельту изгиба
            childBone.localScale = new Vector3(
                idealChildScale.x / angularPureDistortion.x,
                idealChildScale.y / angularPureDistortion.y,
                idealChildScale.z / angularPureDistortion.z
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

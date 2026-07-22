extern alias MM;

using MM::MegaMorph;
using UnityEngine;

namespace MMorphFix
{
    public static class MegamorphMathCore
    {
        /// <summary>
        /// ЭТАП 1 и 2: Защита сустава в изолированном матричном бункере в памяти
        /// </summary>
        public static void ProtectJoint(Transform parentBone, Transform childBone, Vector3 idealCascadeScale)
        {
            // ЛОВУШКА КНОПКИ: Если пользователь выключил щит на радаре, 
            // мы мгновенно выходим, позволяя Unity и оригинальному Мегаморфу ломать кости.
            if (!MMorphFixPlugin.IsFixEnabled) return;

            Quaternion localRot = childBone.localRotation;

            // Виртуальная локальная матрица дочерней кости без собственного масштаба
            Matrix4x4 localMatrix = Matrix4x4.TRS(Vector3.zero, localRot, Vector3.one);

            // Моделируем в памяти Unity-наследование от ТЕКУЩЕГО ЖИВОГО родителя со сцены
            Matrix4x4 parentScaleMatrix = Matrix4x4.Scale(SanitizeVector(parentBone.localScale));
            Matrix4x4 combinedMatrix = parentScaleMatrix * localMatrix;

            // Извлекаем длины векторов получившейся матрицы (чистое искажение)
            Vector3 inheritedDistortion = new Vector3(
                combinedMatrix.GetColumn(0).magnitude,
                combinedMatrix.GetColumn(1).magnitude,
                combinedMatrix.GetColumn(2).magnitude
            );

            // Страховка от деления на ноль
            inheritedDistortion.x = Mathf.Abs(inheritedDistortion.x) < 0.001f ? 1f : inheritedDistortion.x;
            inheritedDistortion.y = Mathf.Abs(inheritedDistortion.y) < 0.001f ? 1f : inheritedDistortion.y;
            inheritedDistortion.z = Mathf.Abs(inheritedDistortion.z) < 0.001f ? 1f : inheritedDistortion.z;

            // Накатывание эмулированной «Истины»
            childBone.localScale = new Vector3(
                idealCascadeScale.x / inheritedDistortion.x,
                idealCascadeScale.y / inheritedDistortion.y,
                idealCascadeScale.z / inheritedDistortion.z
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

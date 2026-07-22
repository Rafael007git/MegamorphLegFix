using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MMorphFix
{
    // --- ХРАНИЛИЩЕ РЕЗУЛЬТИРУЮЩИХ (ИТОГОВЫХ) МАСШТАБОВ ---
    public class CharacterBoneSnapshot
    {
        public bool IsInitialized = false;

        // Сюда мы запишем "Источник Истины" — результирующие масштабы цепочки до сгибания коленей
        public Vector3 TargetThighWorldScale = Vector3.one;
        public Vector3 TargetShinWorldScale = Vector3.one;
        public Vector3 TargetFootWorldScale = Vector3.one;
    }

    public static class BoneSnapshotRegistry
    {
        private static readonly Dictionary<string, CharacterBoneSnapshot> _snapshots =
            new Dictionary<string, CharacterBoneSnapshot>(StringComparer.OrdinalIgnoreCase);

        public static CharacterBoneSnapshot GetSnapshot(string charName)
        {
            if (!_snapshots.TryGetValue(charName, out var snapshot))
            {
                snapshot = new CharacterBoneSnapshot();
                _snapshots[charName] = snapshot;
            }
            return snapshot;
        }
    }

    // --- СИСТЕМНОЕ ИНЖЕНЕРНОЕ ЯДРО РАСЧЕТОВ ---
    public static class LegEqualizerCore
    {
        private static bool _isProcessing = false;

        public static void ExecuteFix(CharacterCustomization cc, string source)
        {
            if (cc == null || _isProcessing) return;

            // Жесткий фильтр: радар и сквозной пересчет работают только для игрока
            // ИСПРАВЛЕНО: Заменяем Contains на IndexOf >= 0 для совместимости со старыми версиями .NET
            bool isPlayer = cc.gameObject.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            cc.gameObject.CompareTag("Player") ||
                            (cc.transform.root != null && cc.transform.root.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0);
            if (!isPlayer) return;

            _isProcessing = true;

            try
            {
                MMorphFixPlugin.LastFoundCharacter = $"{cc.gameObject.name} ({source})";

                if (cc.bones == null || cc.bones.Length == 0) return;

                // Получаем Snapshot-контекст для игрока (раздельно для левой и правой ноги)
                CharacterBoneSnapshot leftLeg = BoneSnapshotRegistry.GetSnapshot(cc.gameObject.name + "_L");
                CharacterBoneSnapshot rightLeg = BoneSnapshotRegistry.GetSnapshot(cc.gameObject.name + "_R");

                foreach (Transform bone in cc.bones)
                {
                    if (bone == null) continue;

                    // Левая нога
                    if (bone.name.Equals("lThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        CalculateAndApplyCascadeFix(bone, "lThighTwist", "lShin", "lFoot", leftLeg);
                    }
                    // Правая нога
                    else if (bone.name.Equals("rThighBend", StringComparison.OrdinalIgnoreCase))
                    {
                        CalculateAndApplyCascadeFix(bone, "rThighTwist", "rShin", "rFoot", rightLeg);
                    }
                }

                MMorphFixPlugin.FixExecutionCalls++;
                MMorphFixPlugin.DebugMonitorText = "Каскадный матричный расчет активен. Спираль разорвана.";
            }
            catch (Exception ex)
            {
                MMorphFixPlugin.DebugMonitorText = $"Критическая ошибка расчетов: {ex.Message}";
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private static void CalculateAndApplyCascadeFix(Transform thighBone, string twistName, string shinName, string footName, CharacterBoneSnapshot snapshot)
        {
            Transform shin = FindChildRecursive(thighBone, shinName);
            Transform foot = FindChildRecursive(thighBone, footName);
            if (shin == null || foot == null) return;

            // Отключаем скручивание мышц
            Transform twist = FindChildRecursive(thighBone, twistName);
            if (twist != null) twist.localScale = Vector3.one;

            // --- ЭТАП 1: СКВОЗНОЙ РАСЧЕТ ИСТОЧНИКА ИСТИНЫ (Выполняется один раз в покое) ---
            if (!snapshot.IsInitialized)
            {
                // Извлекаем чистый масштаб родительского таза (Pelvis)
                Vector3 pelvisScale = thighBone.parent != null ? thighBone.parent.lossyScale : Vector3.one;

                // Читаем локальные коэффициенты, которые выдал Мегаморф в Т-позе
                Vector3 localThigh = thighBone.localScale;
                Vector3 localShin = shin.localScale;
                Vector3 localFoot = foot.localScale;

                // Считаем итоговые целевые масштабы по нашей формуле перемножения цепочки:
                // 1. Цель Бедра = Pelvis * Thigh
                snapshot.TargetThighWorldScale = new Vector3(pelvisScale.x * localThigh.x, pelvisScale.y * localThigh.y, pelvisScale.z * localThigh.z);

                // 2. Цель Голени = Цель Бедра * Shin (В примере: ~1.39, ~0.798, ~1.39)
                snapshot.TargetShinWorldScale = new Vector3(snapshot.TargetThighWorldScale.x * localShin.x, snapshot.TargetThighWorldScale.y * localShin.y, snapshot.TargetThighWorldScale.z * localShin.z);

                // 3. Цель Стопы = Цель Голени * Foot
                snapshot.TargetFootWorldScale = new Vector3(snapshot.TargetShinWorldScale.x * localFoot.x, snapshot.TargetShinWorldScale.y * localFoot.y, snapshot.TargetShinWorldScale.z * localFoot.z);

                snapshot.IsInitialized = true;
            }

            // --- ЭТАП 2: ДИНАМИЧЕСКАЯ МАТРИЧНАЯ ИНВЕРСИЯ (Каждый кадр во FreePose / Гизмо) ---

            // 1. Выравнивание Голени (Колено)
            Quaternion shinRot = shin.localRotation;
            // Проецируем накопленный масштаб бедра на повернутые локальные оси голени
            Vector3 parentScaleInShinSpace = new Vector3(
                Vector3.Magnitude(shinRot * Vector3.right * thighBone.localScale.x),
                Vector3.Magnitude(shinRot * Vector3.up * thighBone.localScale.y),
                Vector3.Magnitude(shinRot * Vector3.forward * thighBone.localScale.z)
            );

            // Делим наш нерушимый эталон голени на пришедший от родителя перекос осей
            Vector3 compensatedShinScale = new Vector3(
                (snapshot.TargetShinWorldScale.x / (thighBone.parent != null ? thighBone.parent.lossyScale.x : 1f)) / (Mathf.Abs(parentScaleInShinSpace.x) > 0.001f ? parentScaleInShinSpace.x : 1f),
                (snapshot.TargetShinWorldScale.y / (thighBone.parent != null ? thighBone.parent.lossyScale.y : 1f)) / (Mathf.Abs(parentScaleInShinSpace.y) > 0.001f ? parentScaleInShinSpace.y : 1f),
                (snapshot.TargetShinWorldScale.z / (thighBone.parent != null ? thighBone.parent.lossyScale.z : 1f)) / (Mathf.Abs(parentScaleInShinSpace.z) > 0.001f ? parentScaleInShinSpace.z : 1f)
            );
            shin.localScale = compensatedShinScale;

            // 2. Выравнивание Стопы (Голеностоп)
            Quaternion footRot = foot.localRotation;
            Vector3 currentCombinedParentScale = shin.lossyScale; // Реальное мировое давление на стопу в этот микрокадр

            Vector3 parentScaleInFootSpace = new Vector3(
                Vector3.Magnitude(footRot * Vector3.right * currentCombinedParentScale.x),
                Vector3.Magnitude(footRot * Vector3.up * currentCombinedParentScale.y),
                Vector3.Magnitude(footRot * Vector3.forward * currentCombinedParentScale.z)
            );

            // Заставляем стопу соответствовать её итоговому эталону из Snapshot-базы
            foot.localScale = new Vector3(
                snapshot.TargetFootWorldScale.x / (Mathf.Abs(parentScaleInFootSpace.x) > 0.001f ? parentScaleInFootSpace.x : 1f),
                snapshot.TargetFootWorldScale.y / (Mathf.Abs(parentScaleInFootSpace.y) > 0.001f ? parentScaleInFootSpace.y : 1f),
                snapshot.TargetFootWorldScale.z / (Mathf.Abs(parentScaleInFootSpace.z) > 0.001f ? parentScaleInFootSpace.z : 1f)
            );

            // --- ЭТАП 3: ОТПРАВКА ДАННЫХ СКВОЗНОГО РАСЧЕТА НА РАДАР ---
            string side = thighBone.name.StartsWith("l") ? "ЛЕВАЯ" : "ПРАВАЯ";
            string telemetry =
                $"[{side} НОГА] Эталон_Цели Голени: {snapshot.TargetShinWorldScale.ToString("F2")} | Стопы: {snapshot.TargetFootWorldScale.ToString("F2")}\n" +
                $"            -> Угол колена: {shinRot.eulerAngles.ToString("F0")}\n" +
                $"            -> Выставлен локальный scale голени: {shin.localScale.ToString("F2")}";

            if (thighBone.name.StartsWith("l"))
                MMorphFixPlugin.LeftLegTelemetry = telemetry;
            else
                MMorphFixPlugin.RightLegTelemetry = telemetry;
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform result = FindChildRecursive(parent.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }
    }
}
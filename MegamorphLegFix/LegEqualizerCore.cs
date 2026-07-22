extern alias MM;

using MM::MegaMorph;
using System;
using UnityEngine;

namespace MMorphFix
{
    public static class LegEqualizerCore
    {
        private static bool _isProcessing = false;
        private static string _pelvisTelemetry = "[ТАЗ] pelvis: (1.00, 1.00, 1.00)";

        public static void ExecuteFix(CharacterCustomization cc) => ExecuteFix(cc, "АВТО_КОМПЕНСАЦИЯ");

        public static void ExecuteFix(CharacterCustomization cc, string source)
        {
            if (cc == null || _isProcessing) return;
            if (cc.bones == null || cc.bones.Length == 0) return;

            _isProcessing = true;
            try
            {
                bool isMainPlayer = cc.gameObject.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;
                if (isMainPlayer)
                    MMorphFixPlugin.LastFoundCharacter = $"{cc.gameObject.name} ({source})";

                InnerExecute(cc, isMainPlayer);
            }
            catch (Exception ex)
            {
                string briefError = $"[Ошибка] {ex.GetType().Name} в {ex.TargetSite} -> {ex.Message}";
                MMorphFixPlugin.DebugMonitorText = briefError;
                if (MMorphFixPlugin.InstanceLogger != null)
                    MMorphFixPlugin.InstanceLogger.LogError($"[LegFix Сбой]: {ex.GetType().Name} в {ex.TargetSite}.\n{ex}");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private static void InnerExecute(CharacterCustomization cc, bool isMainPlayer)
        {
            BepInExPlugin.MegaMorph megaMorph = cc.gameObject.GetComponent<BepInExPlugin.MegaMorph>();

            Transform pelvis = FindBoneRecursive(cc.gameObject.transform, "pelvis");
            if (pelvis == null)
            {
                if (isMainPlayer) MMorphFixPlugin.DebugMonitorText = "[ОШИБКА] Кость 'pelvis' не найдена!";
                return;
            }

            if (isMainPlayer)
                _pelvisTelemetry = $"[ТАЗ] pelvis scale: {MegamorphMathCore.SanitizeVector(pelvis.localScale).ToString("F2")}";

            // --- ШАГ 0: СБОР ПОЛЗУНКОВ И РАСЧЕТ ИДЕАЛЬНОГО ПРЯМОГО КАСКАДА ---
            Vector3 deltaPelvis = MegamorphMathCore.GetMegamorphScale(megaMorph, "pelvis_scale");

            // Левая нога
            Vector3 idealLThighBend = Vector3.Scale(deltaPelvis, MegamorphMathCore.GetMegamorphScale(megaMorph, "lThighBend_scale"));
            Vector3 idealLThighTwist = Vector3.Scale(idealLThighBend, MegamorphMathCore.GetMegamorphScale(megaMorph, "lThighTwist_scale"));
            Vector3 idealLShin = Vector3.Scale(idealLThighTwist, MegamorphMathCore.GetMegamorphScale(megaMorph, "lShin_scale"));
            Vector3 idealLFoot = Vector3.Scale(idealLShin, MegamorphMathCore.GetMegamorphScale(megaMorph, "lFoot_scale"));

            // Правая нога
            Vector3 idealRThighBend = Vector3.Scale(deltaPelvis, MegamorphMathCore.GetMegamorphScale(megaMorph, "rThighBend_scale"));
            Vector3 idealRThighTwist = Vector3.Scale(idealRThighBend, MegamorphMathCore.GetMegamorphScale(megaMorph, "rThighTwist_scale"));
            Vector3 idealRShin = Vector3.Scale(idealRThighTwist, MegamorphMathCore.GetMegamorphScale(megaMorph, "rShin_scale"));
            Vector3 idealRFoot = Vector3.Scale(idealRShin, MegamorphMathCore.GetMegamorphScale(megaMorph, "rFoot_scale"));

            bool processOk = false;

            // 1. ПРИМЕНЕНИЕ К ЛЕВОЙ НОГЕ (3 СУСТАВА)
            Transform lThighBend = FindBoneRecursive(pelvis, "lThighBend");
            Transform lThighTwist = FindBoneRecursive(pelvis, "lThighTwist");
            Transform lShin = FindBoneRecursive(pelvis, "lShin");
            Transform lFoot = FindBoneRecursive(pelvis, "lFoot");

            if (lThighBend != null && lShin != null && lFoot != null)
            {
                MegamorphMathCore.ProtectJoint(pelvis, lThighBend, idealLThighBend);
                if (lThighTwist != null) lThighTwist.localScale = MegamorphMathCore.GetMegamorphScale(megaMorph, "lThighTwist_scale");

                MegamorphMathCore.ProtectJoint(lThighTwist != null ? lThighTwist : lThighBend, lShin, idealLShin);
                MegamorphMathCore.ProtectJoint(lShin, lFoot, idealLFoot);

                if (isMainPlayer) UpdateLegTelemetry("ЛЕВАЯ", lThighBend, lShin, lFoot, idealLShin, idealLFoot);
                processOk = true;
            }

            // 2. ПРИМЕНЕНИЕ К ПРАВОЙ НОГЕ (3 СУСТАВА)
            Transform rThighBend = FindBoneRecursive(pelvis, "rThighBend");
            Transform rThighTwist = FindBoneRecursive(pelvis, "rThighTwist");
            Transform rShin = FindBoneRecursive(pelvis, "rShin");
            Transform rFoot = FindBoneRecursive(pelvis, "rFoot");

            if (rThighBend != null && rShin != null && rFoot != null)
            {
                MegamorphMathCore.ProtectJoint(pelvis, rThighBend, idealRThighBend);
                if (rThighTwist != null) rThighTwist.localScale = MegamorphMathCore.GetMegamorphScale(megaMorph, "rThighTwist_scale");

                MegamorphMathCore.ProtectJoint(rThighTwist != null ? rThighTwist : rThighBend, rShin, idealRShin);
                MegamorphMathCore.ProtectJoint(rShin, rFoot, idealRFoot);

                if (isMainPlayer) UpdateLegTelemetry("ПРАВАЯ", rThighBend, rShin, rFoot, idealRShin, idealRFoot);
                processOk = true;
            }

            if (isMainPlayer && processOk)
            {
                MMorphFixPlugin.FixExecutionCalls++;
                MMorphFixPlugin.DebugMonitorText = $"{_pelvisTelemetry} | Каскадный щит 6 суставов активен.";
            }
        }

        private static void UpdateLegTelemetry(string side, Transform thigh, Transform shin, Transform foot, Vector3 idealShin, Vector3 idealFoot)
        {
            string telemetry =
                $"[{side} НОГА ИГРОКА]\n" +
                $" -> Thigh (Бедро) scale: {MegamorphMathCore.SanitizeVector(thigh.localScale).ToString("F2")}\n" +
                $" -> Shin  (Голень) scale: {shin.localScale.ToString("F2")} (Прямой Эталон: {idealShin.ToString("F2")})\n" +
                $" -> Foot  (Стопа)  scale: {foot.localScale.ToString("F2")} (Прямой Эталон: {idealFoot.ToString("F2")})\n" +
                $" -> Угол вращения сустава: {shin.localRotation.eulerAngles.ToString("F0")}°";

            if (side == "ЛЕВАЯ") MMorphFixPlugin.LeftLegTelemetry = telemetry;
            else MMorphFixPlugin.RightLegTelemetry = telemetry;
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
    }
}

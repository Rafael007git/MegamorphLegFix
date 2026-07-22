extern alias MM;

using MM::MegaMorph;
using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
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

                InnerExecute(cc, isMainPlayer, source);
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

        private static void InnerExecute(CharacterCustomization cc, bool isMainPlayer, string source)
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

            // --- ШАГ 0: ИСТИННЫЙ ЭТАЛОН (v0.8.0) — ЭТО ЧИСТЫЕ ПОЛЗУНКИ ИЗ КОНФИГА МЕГАМОРФА ---
            // Больше никакого ручного искусственного перемножения векторов!
            // Unity и Мегаморф сами знают, как масштабировать прямую ногу.
            Vector3 idealLThighBend = MegamorphMathCore.GetMegamorphScale(megaMorph, "lThighBend_scale");
            Vector3 idealLThighTwist = MegamorphMathCore.GetMegamorphScale(megaMorph, "lThighTwist_scale");
            Vector3 idealLShin = MegamorphMathCore.GetMegamorphScale(megaMorph, "lShin_scale");
            Vector3 idealLFoot = MegamorphMathCore.GetMegamorphScale(megaMorph, "lFoot_scale");

            Vector3 idealRThighBend = MegamorphMathCore.GetMegamorphScale(megaMorph, "rThighBend_scale");
            Vector3 idealRThighTwist = MegamorphMathCore.GetMegamorphScale(megaMorph, "rThighTwist_scale");
            Vector3 idealRShin = MegamorphMathCore.GetMegamorphScale(megaMorph, "rShin_scale");
            Vector3 idealRFoot = MegamorphMathCore.GetMegamorphScale(megaMorph, "rFoot_scale");

            bool processOk = false;

            // ==========================================
            // ПРОХОД №1: ЗАЩИТА РЕАЛЬНОГО СКЕЛЕТА ПЕРСОНАЖА
            // ==========================================
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

                if (isMainPlayer && !source.StartsWith("FreePose", StringComparison.OrdinalIgnoreCase))
                    UpdateLegTelemetry("ЛЕВАЯ", lThighBend, lShin, lFoot, idealLShin, idealLFoot);
                processOk = true;
            }

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

                if (isMainPlayer && !source.StartsWith("FreePose", StringComparison.OrdinalIgnoreCase))
                    UpdateLegTelemetry("ПРАВАЯ", rThighBend, rShin, rFoot, idealRShin, idealRFoot);
                processOk = true;
            }

            // ==========================================
            // ПРОХОД №2: СИНХРОННАЯ ЗАЩИТА ФАНТОМОВ ГИЗМО (FREEPOSE)
            // ==========================================
            if (source.StartsWith("FreePose", StringComparison.OrdinalIgnoreCase) || source == "ГЛИФ_ФИКС" || source == "ГЛИФ_РАДАР")
            {
                Type gizmoType = AccessTools.TypeByName("RuntimeGizmos.TransformGizmo") ?? AccessTools.TypeByName("TransformGizmo");
                if (gizmoType != null)
                {
                    object gizmoInstance = AccessTools.Field(gizmoType, "transformGizmo_")?.GetValue(null);
                    if (gizmoInstance != null)
                    {
                        IList tempTransform = AccessTools.Field(gizmoType, "tempTransform")?.GetValue(gizmoInstance) as IList;
                        if (tempTransform != null && tempTransform.Count > 0)
                        {
                            Transform fPelvis = null, fLThighBend = null, fLThighTwist = null, fLShin = null, fLFoot = null;
                            Transform fRThighBend = null, fRThighTwist = null, fRShin = null, fRFoot = null;

                            foreach (object obj in tempTransform)
                            {
                                Transform t = obj as Transform;
                                if (t == null) continue;

                                if (t.name.EndsWith("pelvis", StringComparison.OrdinalIgnoreCase)) fPelvis = t;
                                else if (t.name.EndsWith("lThighBend", StringComparison.OrdinalIgnoreCase)) fLThighBend = t;
                                else if (t.name.EndsWith("lThighTwist", StringComparison.OrdinalIgnoreCase)) fLThighTwist = t;
                                else if (t.name.EndsWith("lShin", StringComparison.OrdinalIgnoreCase)) fLShin = t;
                                else if (t.name.EndsWith("lFoot", StringComparison.OrdinalIgnoreCase)) fLFoot = t;
                                else if (t.name.EndsWith("rThighBend", StringComparison.OrdinalIgnoreCase)) fRThighBend = t;
                                else if (t.name.EndsWith("rThighTwist", StringComparison.OrdinalIgnoreCase)) fRThighTwist = t;
                                else if (t.name.EndsWith("rShin", StringComparison.OrdinalIgnoreCase)) fRShin = t;
                                else if (t.name.EndsWith("rFoot", StringComparison.OrdinalIgnoreCase)) fRFoot = t;
                            }

                            // Лечим левые фантомы
                            if (fPelvis != null && fLThighBend != null && fLShin != null && fLFoot != null)
                            {
                                MegamorphMathCore.ProtectJoint(fPelvis, fLThighBend, idealLThighBend);
                                if (fLThighTwist != null) fLThighTwist.localScale = MegamorphMathCore.GetMegamorphScale(megaMorph, "lThighTwist_scale");

                                MegamorphMathCore.ProtectJoint(fLThighTwist != null ? fLThighTwist : fLThighBend, fLShin, idealLShin);
                                MegamorphMathCore.ProtectJoint(fLShin, fLFoot, idealLFoot);

                                if (isMainPlayer) UpdateLegTelemetry("ЛЕВАЯ", fLThighBend, fLShin, fLFoot, idealLShin, idealLFoot);
                            }

                            // Лечим правые фантомы (ИСПРАВЛЕНО: строго правые кости в Проходе 2)
                            if (fPelvis != null && fRThighBend != null && fRShin != null && fRFoot != null)
                            {
                                MegamorphMathCore.ProtectJoint(fPelvis, fRThighBend, idealRThighBend);
                                if (fRThighTwist != null) fRThighTwist.localScale = MegamorphMathCore.GetMegamorphScale(megaMorph, "rThighTwist_scale");

                                MegamorphMathCore.ProtectJoint(fRThighTwist != null ? fRThighTwist : fRThighBend, fRShin, idealRShin);
                                MegamorphMathCore.ProtectJoint(fRShin, fRFoot, idealRFoot);
                                if (isMainPlayer) UpdateLegTelemetry("ПРАВАЯ", fRThighBend, fRShin, fRFoot, idealRShin, idealRFoot);
                            }
                        }
                    }
                }
            }

            if (isMainPlayer && processOk)
            {
                MMorphFixPlugin.FixExecutionCalls++;
                // ИСПРАВЛЕНО: Теперь на радар честно выводится источник вызова (source), 
                // и вы увидите надпись МЕБЕЛЬ или ИГРОВОЙ_МИР прямо на экране!
                MMorphFixPlugin.DebugMonitorText = $"{_pelvisTelemetry} | Щит активен [{source}]";
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

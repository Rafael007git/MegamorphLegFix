extern alias MM;
using HarmonyLib;
using MM::MegaMorph;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
                string briefError = $"[Ошибка] {ex.GetType().Name} in {ex.TargetSite} -> {ex.Message}";
                MMorphFixPlugin.DebugMonitorText = briefError;
                if (MMorphFixPlugin.InstanceLogger != null)
                    MMorphFixPlugin.InstanceLogger.LogError($"[LegFix Сбой]: {ex.GetType().Name} in {ex.TargetSite}.\n{ex}");
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

            // --- ШАГ 0: ЧИСТЫЕ ПОЛЗУНКИ ИЗ ПАМЯТИ МЕГАМОРФА (v0.8.6) ---
            // Больше никаких ручных перемножений. Берем голые дельты, под которые настроен меш.
            Vector3 dLThighTwist = MegamorphMathCore.GetMegamorphScale(megaMorph, "lThighTwist_scale");
            Vector3 dLShin = MegamorphMathCore.GetMegamorphScale(megaMorph, "lShin_scale");
            Vector3 dLFoot = MegamorphMathCore.GetMegamorphScale(megaMorph, "lFoot_scale");

            Vector3 dRThighTwist = MegamorphMathCore.GetMegamorphScale(megaMorph, "rThighTwist_scale");
            Vector3 dRShin = MegamorphMathCore.GetMegamorphScale(megaMorph, "rShin_scale");
            Vector3 dRFoot = MegamorphMathCore.GetMegamorphScale(megaMorph, "rFoot_scale");

            bool processOk = false;

            // =================================================================
            // ПРОХОД №1: ЗАЩИТА РЕАЛЬНОГО СКЕЛЕТА ПЕРСОНАЖА (ИГРОВОЙ МИР / МЕБЕЛЬ)
            // =================================================================
            Transform lThighBend = FindBoneRecursive(pelvis, "lThighBend");
            Transform lThighTwist = FindBoneRecursive(pelvis, "lThighTwist");
            Transform lShin = FindBoneRecursive(pelvis, "lShin");
            Transform lFoot = FindBoneRecursive(pelvis, "lFoot");

            if (lThighBend != null && lShin != null && lFoot != null)
            {
                Transform lParent = lThighTwist != null ? lThighTwist : lThighBend;

                // Промежуточный твист не гнется, накатываем на него чистый эталон
                if (lThighTwist != null) lThighTwist.localScale = dLThighTwist;

                // Накатываем голый ползунок. Математическое ядро само вычтет прямые масштабы бедер!
                MegamorphMathCore.ProtectJoint(lShin, dLShin, lParent);
                MegamorphMathCore.ProtectJoint(lFoot, dLFoot, lShin);

                if (isMainPlayer && !source.StartsWith("FreePose", StringComparison.OrdinalIgnoreCase))
                    UpdateLegTelemetry("ЛЕВАЯ", lThighBend, lShin, lFoot, dLShin, dLFoot);
                processOk = true;
            }

            Transform rThighBend = FindBoneRecursive(pelvis, "rThighBend");
            Transform rThighTwist = FindBoneRecursive(pelvis, "rThighTwist");
            Transform rShin = FindBoneRecursive(pelvis, "rShin");
            Transform rFoot = FindBoneRecursive(pelvis, "rFoot");

            if (rThighBend != null && rShin != null && rFoot != null)
            {
                Transform rParent = rThighTwist != null ? rThighTwist : rThighBend;

                if (rThighTwist != null) rThighTwist.localScale = dRThighTwist;

                MegamorphMathCore.ProtectJoint(rShin, dRShin, rParent);
                MegamorphMathCore.ProtectJoint(rFoot, dRFoot, rShin);

                if (isMainPlayer && !source.StartsWith("FreePose", StringComparison.OrdinalIgnoreCase))
                    UpdateLegTelemetry("ПРАВАЯ", rThighBend, rShin, rFoot, dRShin, dRFoot);
                processOk = true;
            }

            // =================================================================
            // ПРОХОД №2: СИНХРОННАЯ ЗАЩИТА ФАНТОМОВ ГИЗМО (РЕЖИМ FREEPOSE)
            // =================================================================
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

                                switch (t.name)
                                {
                                    case string s when s.EndsWith("pelvis", StringComparison.OrdinalIgnoreCase): fPelvis = t; break;
                                    case string s when s.EndsWith("lThighBend", StringComparison.OrdinalIgnoreCase): fLThighBend = t; break;
                                    case string s when s.EndsWith("lThighTwist", StringComparison.OrdinalIgnoreCase): fLThighTwist = t; break;
                                    case string s when s.EndsWith("lShin", StringComparison.OrdinalIgnoreCase): fLShin = t; break;
                                    case string s when s.EndsWith("lFoot", StringComparison.OrdinalIgnoreCase): fLFoot = t; break;
                                    case string s when s.EndsWith("rThighBend", StringComparison.OrdinalIgnoreCase): fRThighBend = t; break;
                                    case string s when s.EndsWith("rThighTwist", StringComparison.OrdinalIgnoreCase): fRThighTwist = t; break;
                                    case string s when s.EndsWith("rShin", StringComparison.OrdinalIgnoreCase): fRShin = t; break;
                                    case string s when s.EndsWith("rFoot", StringComparison.OrdinalIgnoreCase): fRFoot = t; break;
                                }
                            }

                            // Лечим левые фантомы на экране FreePose
                            if (fPelvis != null && fLThighBend != null && fLShin != null && fLFoot != null)
                            {
                                Transform flParent = fLThighTwist != null ? fLThighTwist : fLThighBend;
                                if (fLThighTwist != null) fLThighTwist.localScale = dLThighTwist;

                                MegamorphMathCore.ProtectJoint(fLShin, dLShin, flParent);
                                MegamorphMathCore.ProtectJoint(fLFoot, dLFoot, fLShin);

                                if (isMainPlayer) UpdateLegTelemetry("ЛЕВАЯ", fLThighBend, fLShin, fLFoot, dLShin, dLFoot);
                            }

                            // Лечим правые фантомы на экране FreePose
                            if (fPelvis != null && fRThighBend != null && fRShin != null && fRFoot != null)
                            {
                                Transform frParent = fRThighTwist != null ? fRThighTwist : fRThighBend;
                                if (fRThighTwist != null) fRThighTwist.localScale = dRThighTwist;

                                MegamorphMathCore.ProtectJoint(fRShin, dRShin, frParent);
                                MegamorphMathCore.ProtectJoint(fRFoot, dRFoot, fRShin);

                                if (isMainPlayer) UpdateLegTelemetry("ПРАВАЯ", fRThighBend, fRShin, fRFoot, dRShin, dRFoot);
                            }
                        }
                    }
                }
            }

            if (isMainPlayer && processOk)
            {
                MMorphFixPlugin.FixExecutionCalls++;
                MMorphFixPlugin.DebugMonitorText = $"{_pelvisTelemetry} | Щит активен [{source}]";
            }
        }


        private static void UpdateLegTelemetry(string side, Transform thigh, Transform shin, Transform foot, Vector3 configShin, Vector3 configFoot)
        {
            string telemetry =
                $"[{side} НОГА ИГРОКА]\n" +
                $" -> Thigh (Бедро) scale: {MegamorphMathCore.SanitizeVector(thigh.localScale).ToString("F2")}\n" +
                $" -> Shin  (Голень) scale: {shin.localScale.ToString("F2")} (Конфиг: {configShin.ToString("F2")})\n" +
                $" -> Foot  (Стопа)  scale: {foot.localScale.ToString("F2")} (Конфиг: {configFoot.ToString("F2")})\n" +
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

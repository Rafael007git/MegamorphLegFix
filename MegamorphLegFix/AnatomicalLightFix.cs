using System;
using UnityEngine;
using HarmonyLib;

namespace MMorphFix
{
    public static class AnatomicalLightFix
    {
        // Global variables for live telemetry tracking on the UI radar panel
        public static float Radar_HipScaleY = 1.0f;
        public static float Radar_HipLossyScaleY = 1.0f;
        public static float Radar_Light1_Intensity = 0.0f;
        public static float Radar_Light5_Intensity = 0.0f;
        public static float Radar_Light6_Intensity = 0.0f;
        public static string Radar_Status = "Awaiting changes...";

        // Static fields for caching "Let It Be Dark" mod Reflection data
        private static System.Reflection.FieldInfo _letItBeDarkField = null;
        private static bool _reflectionChecked = false;

        /// <summary>
        /// Event-driven lighting fix: triggered by MegaMorph sliders or FreePose gizmo manipulation.
        /// Automatically scales bone-attached point and spot lights using the inverse-square law.
        /// </summary>
        public static void ExecuteFix(GameObject characterRoot)
        {
            if (characterRoot == null)
            {
                Radar_Status = "No character root found";
                return;
            }

            // Step 1: Find the true skeletal root bone (resilient to engine prefixes like "Bone/hip")
            Transform hip = FindBoneRecursive(characterRoot.transform, "hip");
            if (hip == null)
            {
                Radar_Status = $"Hip bone not found in {characterRoot.name}";
                return;
            }

            // Step 2: Locate the container holding character-specific lighting setup
            Transform lightGroup = FindBoneRecursive(hip, "character light group");
            if (lightGroup == null)
            {
                Radar_Status = $"Character light group not found in {hip.name}";
                return;
            }

            // Update diagnostic telemetry panel strings
            Radar_Status = $"LIGHTS UPDATED! Target: {characterRoot.name}";
            Radar_HipScaleY = hip.localScale.y;
            Radar_HipLossyScaleY = hip.lossyScale.y;

            // Step 3: Extract the absolute world-space vertical scale modifier to accurately capture Gizmo shifts
            float hipModifier = (float.IsNaN(Radar_HipLossyScaleY) || float.IsInfinity(Radar_HipLossyScaleY) || Mathf.Abs(Radar_HipLossyScaleY) < 0.01f) ? 1.0f : Radar_HipLossyScaleY;

            // Step 4: Apply the Inverse-Square Law coefficient. 
            // As distance scales down, intensity must drop quadratically to maintain constant surface irradiance.
            float quadraticModifier = hipModifier * hipModifier;

            // Step 5: Safely fetch the global multiplier from the independent "Let It Be Dark" mod via cached Reflection
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
                catch
                {
                    _letItBeDarkField = null;
                }
            }

            float letItBeDarkMultiplier = 1.0f;
            if (_letItBeDarkField != null)
            {
                try { letItBeDarkMultiplier = (float)_letItBeDarkField.GetValue(null); }
                catch { letItBeDarkMultiplier = 1.0f; }
            }

            // Step 6: Iterate through the light group and re-calculate intensity values for both Point and Spot lights
            for (int i = 0; i < lightGroup.childCount; i++)
            {
                Transform lightObj = lightGroup.GetChild(i);
                Light light = lightObj.GetComponent<Light>();

                if (light != null)
                {
                    string name = lightObj.name;
                    float baseIntensity = 1.0f;

                    // CASE A: Standard omnidirectional character skeletal point lights
                    if (light.type == LightType.Point)
                    {
                        // Resilient token-based search protecting against unintended padding or whitespace typos
                        if (name.IndexOf("1", StringComparison.OrdinalIgnoreCase) >= 0)
                            baseIntensity = ModConfigManager.Light1Intensity;
                        else if (name.IndexOf("5", StringComparison.OrdinalIgnoreCase) >= 0)
                            baseIntensity = ModConfigManager.Light5Intensity;
                        else if (name.IndexOf("6", StringComparison.OrdinalIgnoreCase) >= 0)
                            baseIntensity = ModConfigManager.Light6Intensity;
                        else
                            continue;

                        // Apply non-destructive product formula combining user preferences, darkness sliders, and physical scale delta
                        light.intensity = baseIntensity * letItBeDarkMultiplier * quadraticModifier;

                        // Push live telemetry data back to the debug radar
                        if (name.IndexOf("1", StringComparison.OrdinalIgnoreCase) >= 0) Radar_Light1_Intensity = light.intensity;
                        if (name.IndexOf("5", StringComparison.OrdinalIgnoreCase) >= 0) Radar_Light5_Intensity = light.intensity;
                        if (name.IndexOf("6", StringComparison.OrdinalIgnoreCase) >= 0) Radar_Light6_Intensity = light.intensity;
                    }
                    // CASE B: Frontal cinematic filling Spot Light attached exclusively to the Player character
                    else if (light.type == LightType.Spot)
                    {
                        // Default game asset fallback intensity for the player's core directional spot light
                        float spotBaseIntensity = 50.0f;

                        // Perform the same quadratic scaling to prevent the light cone from burning out textures on tiny meshes
                        light.intensity = spotBaseIntensity * letItBeDarkMultiplier * quadraticModifier;

                        // Append a visual flag to confirm the player spot light interception was successful
                        Radar_Status = $"LIGHTS + PLAYER SPOT UPDATED! Target: {characterRoot.name}";
                    }
                }
            }
        }

        /// <summary>
        /// Highly resilient recursive bone finder. Handles structural engine prefixes like "Bone/hip" or "Model_hip".
        /// </summary>
        private static Transform FindBoneRecursive(Transform parent, string targetName)
        {
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

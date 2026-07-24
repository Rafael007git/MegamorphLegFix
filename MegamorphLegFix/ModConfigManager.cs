using BepInEx.Configuration;

namespace MMorphFix
{
    public static class ModConfigManager
    {
        // Существующие конфиги
        private static ConfigEntry<bool> _isFixEnabledConfig;
        public static bool IsFixEnabled => _isFixEnabledConfig != null && _isFixEnabledConfig.Value;

        // 👇 НОВЫЙ КОНФИГ: Выключатель UI-радара
        private static ConfigEntry<bool> _showRadarConfig;
        public static bool ShowRadar => _showRadarConfig != null && _showRadarConfig.Value;

        private static ConfigEntry<float> _light1IntensityConfig;
        private static ConfigEntry<float> _light5IntensityConfig;
        private static ConfigEntry<float> _light6IntensityConfig;

        public static float Light1Intensity => _light1IntensityConfig != null ? _light1IntensityConfig.Value : 122.0f;
        public static float Light5Intensity => _light5IntensityConfig != null ? _light5IntensityConfig.Value : 472.0f;
        public static float Light6Intensity => _light6IntensityConfig != null ? _light6IntensityConfig.Value : 620.0f;

        /// <summary>
        /// Регистрирует все настройки мода в меню BepInEx Configuration Manager (F1).
        /// </summary>
        public static void Initialize(ConfigFile config)
        {
            // 1. Main Settings
            _isFixEnabledConfig = config.Bind(
                "1. General Settings",
                "Enable Matrix Compensation",
                true,
                "Automatically fixes leg deformation and skeletal lighting anomalies when changing body proportions."
            );

            _showRadarConfig = config.Bind(
                "1. General Settings",
                "Show Debug Radar",
                false,
                "Displays the real-time engineering telemetry panel for leg bones and skeletal lights on your screen."
            );

            // 2. Skeletal Lights Adjustment
            _light1IntensityConfig = config.Bind(
                "2. Skeletal Lights Settings",
                "Point Light (1) Intensity",
                122.0f,
                new ConfigDescription("Base brightness of the first character skeletal light source.", new AcceptableValueRange<float>(0f, 1000f))
            );

            _light5IntensityConfig = config.Bind(
                "2. Skeletal Lights Settings",
                "Point Light (5) Intensity",
                472.0f,
                new ConfigDescription("Base brightness of the fifth character skeletal light source.", new AcceptableValueRange<float>(0f, 1000f))
            );

            _light6IntensityConfig = config.Bind(
                "2. Skeletal Lights Settings",
                "Point Light (6) Intensity",
                620.0f,
                new ConfigDescription("Base brightness of the sixth character skeletal light source.", new AcceptableValueRange<float>(0f, 1000f))
            );

            // Подписка на события ползунков (свет)
            _light1IntensityConfig.SettingChanged += (sender, args) => TriggerLightsUpdate();
            _light5IntensityConfig.SettingChanged += (sender, args) => TriggerLightsUpdate();
            _light6IntensityConfig.SettingChanged += (sender, args) => TriggerLightsUpdate();
            _isFixEnabledConfig.SettingChanged += (sender, args) => TriggerLightsUpdate();
        }

        private static void TriggerLightsUpdate()
        {
            var allCharacters = UnityEngine.Object.FindObjectsOfType<CharacterCustomization>();
            if (allCharacters == null || allCharacters.Length == 0) return;

            foreach (var cc in allCharacters)
            {
                if (cc != null && cc.gameObject != null && cc.gameObject.activeInHierarchy)
                {
                    AnatomicalLightFix.ExecuteFix(cc.gameObject);
                }
            }
        }
    }
}

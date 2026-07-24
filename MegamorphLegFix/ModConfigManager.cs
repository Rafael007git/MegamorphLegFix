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
            // 1. Основные настройки
            _isFixEnabledConfig = config.Bind(
                "1. Основные настройки",
                "Включить Матричную Компенсацию",
                true,
                "Автоматически исправляет деформацию ног и скелетного освещения при изменении пропорций тела."
            );

            // 👇 РЕГИСТРИРУЕМ ЧЕКБОКС В МЕНЮ F1: По умолчанию выключен (false) для чистого релиза
            _showRadarConfig = config.Bind(
                "1. Основные настройки",
                "Показывать отладочный радар",
                false,
                "Отображает на экране инженерную панель телеметрии костей ног и суставных огоньков."
            );

            // 2. Настройка скелетных огней
            _light1IntensityConfig = config.Bind(
                "2. Настройка скелетных огней",
                "Интенсивность Point Light (1)",
                122.0f,
                new ConfigDescription("Базовая яркость первого источника света персонажа.", new AcceptableValueRange<float>(0f, 1000f))
            );

            _light5IntensityConfig = config.Bind(
                "2. Настройка скелетных огней",
                "Интенсивность Point Light (5)",
                472.0f,
                new ConfigDescription("Базовая яркость пятого источника света персонажа.", new AcceptableValueRange<float>(0f, 1000f))
            );

            _light6IntensityConfig = config.Bind(
                "2. Настройка скелетных огней",
                "Интенсивность Point Light (6)",
                620.0f,
                new ConfigDescription("Базовая яркость шестого источника света персонажа.", new AcceptableValueRange<float>(0f, 1000f))
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

using BepInEx.Configuration;

namespace MMorphFix
{
    public static class ModConfigManager
    {
        // Главный выключатель компенсации
        private static ConfigEntry<bool> _isFixEnabledConfig;
        public static bool IsFixEnabled => _isFixEnabledConfig != null && _isFixEnabledConfig.Value;

        // Конфиги для трех скелетных огоньков
        private static ConfigEntry<float> _light1IntensityConfig;
        private static ConfigEntry<float> _light5IntensityConfig;
        private static ConfigEntry<float> _light6IntensityConfig;

        // Публичные свойства для удобного чтения из других файлов
        public static float Light1Intensity => _light1IntensityConfig != null ? _light1IntensityConfig.Value : 122.0f;
        public static float Light5Intensity => _light5IntensityConfig != null ? _light5IntensityConfig.Value : 472.0f;
        public static float Light6Intensity => _light6IntensityConfig != null ? _light6IntensityConfig.Value : 620.0f;

        /// <summary>
        /// Регистрирует все настройки мода в меню BepInEx Configuration Manager (F1).
        /// </summary>
        public static void Initialize(ConfigFile config)
        {
            // 1. Основной выключатель мода
            _isFixEnabledConfig = config.Bind(
                "1. Основные настройки",
                "Включить Матричную Компенсацию",
                true,
                "Автоматически исправляет деформацию ног и скелетного освещения при изменении пропорций тела."
            );

            // 2. Ползунки для скелетных огоньков персонажей с зашитыми эталонами
            _light1IntensityConfig = config.Bind(
                "2. Настройка скелетных огней",
                "Интенсивность Point Light (1)",
                122.0f, // Заводской дефолт из логов
                new ConfigDescription("Базовая яркость первого источника света персонажа.", new AcceptableValueRange<float>(0f, 1000f))
            );

            _light5IntensityConfig = config.Bind(
                "2. Настройка скелетных огней",
                "Интенсивность Point Light (5)",
                472.0f, // Заводской дефолт из логов
                new ConfigDescription("Базовая яркость пятого источника света персонажа.", new AcceptableValueRange<float>(0f, 1000f))
            );

            _light6IntensityConfig = config.Bind(
                "2. Настройка скелетных огней",
                "Интенсивность Point Light (6)",
                620.0f, // Заводской дефолт из логов
                new ConfigDescription("Базовая яркость шестого источника света персонажа.", new AcceptableValueRange<float>(0f, 1000f))
            );

            // ДОБАВЛЯЕМ ПОДПИСКУ НА ИЗМЕНЕНИЕ ПОЛЗУНКОВ В КОНЦЕ МЕТОДА:
            _light1IntensityConfig.SettingChanged += (sender, args) => TriggerLightsUpdate();
            _light5IntensityConfig.SettingChanged += (sender, args) => TriggerLightsUpdate();
            _light6IntensityConfig.SettingChanged += (sender, args) => TriggerLightsUpdate();
            _isFixEnabledConfig.SettingChanged += (sender, args) => TriggerLightsUpdate();
        }

        /// <summary>
        /// Находит всех активных персонажей на сцене и мгновенно обновляет им свет при сдвиге ползунка в F1.
        /// </summary>
        private static void TriggerLightsUpdate()
        {
            // Находим главного героя и всех компаньонок, которые сейчас активны на экране
            var allCharacters = UnityEngine.Object.FindObjectsOfType<CharacterCustomization>();
            if (allCharacters == null || allCharacters.Length == 0) return;

            foreach (var cc in allCharacters)
            {
                if (cc != null && cc.gameObject != null && cc.gameObject.activeInHierarchy)
                {
                    // Точечно пинаем фикс света ровно 1 раз по факту клика в меню!
                    AnatomicalLightFix.ExecuteFix(cc.gameObject);
                }
            }
        }

    }
}

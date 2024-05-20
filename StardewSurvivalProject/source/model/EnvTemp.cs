using System;
using System.Linq;
using StardewSurvivalProject.source.utils;
using StardewValley;
using System.Collections.Generic;
using SObject = StardewValley.Object;

namespace StardewSurvivalProject.source.model
{
    public class EnvTemp
    {
        private static double DEFAULT_VALUE = ModConfig.GetInstance().EnvironmentBaseTemperature;
        public double value { get; set; }
        private bool fixedTemp;
        private double dayNightCycleTempDiffScale;
        private double timeTempModifier;
        private double decTime;
        private double fluctuationTempScale;
        private Random rand = null;

        public EnvTemp()
        {
            this.value = DEFAULT_VALUE;
            this.fixedTemp = false;
            this.rand = new Random();
        }

        private void applySeason(string season)
        {
            switch (season)
            {
                case "spring":
                    this.value *= ModConfig.GetInstance().SpringSeasonTemperatureMultiplier; break;
                case "summer":
                    this.value *= ModConfig.GetInstance().SummerSeasonTemperatureMultiplier; break;
                case "fall":
                    this.value *= ModConfig.GetInstance().FallSeasonTemperatureMultiplier; break;
                case "winter":
                    this.value *= ModConfig.GetInstance().WinterSeasonTemperatureMultiplier; break;
            }

        }

        private void applyWeather(int weatherIconId)
        {
            switch (weatherIconId)
            {
                case (int)weatherIconType.SUNNY:
                    this.value *= ModConfig.GetInstance().SunnyWeatherTemperatureMultiplier; break;
                case (int)weatherIconType.FESTIVAL:
                    this.value *= ModConfig.GetInstance().FestivalWeatherTemperatureMultiplier; break;
                case (int)weatherIconType.WEDDING:
                    this.value *= ModConfig.GetInstance().WeddingWeatherTemperatureMultiplier; break;
                case (int)weatherIconType.STORM:
                    this.value *= ModConfig.GetInstance().StormWeatherTemperatureMultiplier; break;
                case (int)weatherIconType.RAIN:
                    this.value *= ModConfig.GetInstance().RainWeatherTemperatureMultiplier; break;
                case (int)weatherIconType.WINDY_SPRING:
                    this.value *= ModConfig.GetInstance().WindySpringWeatherTemperatureMultiplier; break;
                case (int)weatherIconType.WINDY_FALL:
                    this.value *= ModConfig.GetInstance().WindySpringWeatherTemperatureMultiplier; break;
                case (int)weatherIconType.SNOW:
                    this.value *= ModConfig.GetInstance().SnowWeatherTemperatureMultiplier; break;
                default: break;
            }

        }

        private void applyLocation(GameLocation location, int currentMineLevel)
        {
            if (location != null)
            {
                data.LocationEnvironmentData locationData = data.CustomEnvironmentDictionary.GetEnvironmentData(location.Name);
                if (locationData != null)
                {
                    this.value += locationData.tempModifierAdditive;
                    this.value *= locationData.tempModifierMultiplicative;
                    if (locationData.tempModifierFixedValue > -273)
                    {
                        this.value = locationData.tempModifierFixedValue;
                        this.fixedTemp = true;
                    }
                    this.dayNightCycleTempDiffScale = locationData.tempModifierTimeDependentScale;
                    this.fluctuationTempScale = locationData.tempModifierFluctuationScale;
                }

                if (location.Name.Contains("UndergroundMine"))
                {
                    switch (currentMineLevel)
                    {
                        case 77377:
                            value = DEFAULT_VALUE; break;
                        case >= 121:
                            value = DEFAULT_VALUE + 0.045 * currentMineLevel; break;
                        case >= 80:
                            value = 1.1 * Math.Pow(currentMineLevel - 60, 1.05); break;
                        case >= 40:
                            value = 0.03 * Math.Pow(currentMineLevel - 60, 2) - 12; break;
                        case >= 0:
                            value = DEFAULT_VALUE + 0.22 * currentMineLevel; break;
                    }
                    this.fixedTemp = true;
                }

                if (!location.IsOutdoors)
                {
                    if (location.IsFarm)
                        this.value += (DEFAULT_VALUE - this.value) * ModConfig.GetInstance().FarmIndoorTemperatureMultiplier;
                    else
                        this.value += (DEFAULT_VALUE - this.value) * ModConfig.GetInstance().IndoorTemperatureMultiplier;
                }
            }
        }

        public void updateEnvTemp(int time, string season, int weatherIconId, GameLocation location = null, int currentMineLevel = 0)
        {
            this.dayNightCycleTempDiffScale = ModConfig.GetInstance().DefaultDayNightCycleTemperatureDiffScale;
            this.fluctuationTempScale = ModConfig.GetInstance().DefaultTemperatureFluctuationScale;

            applySeason(season);
            applyWeather(weatherIconId);
            applyLocation(location, currentMineLevel);

            // day cycle
            this.decTime = time / 100 + time % 100 / 60.0;
            this.timeTempModifier = Math.Sin((this.decTime - 8.5) / (Math.PI * 1.2)) * this.dayNightCycleTempDiffScale;
            this.value += fixedTemp ? 0 : this.timeTempModifier;

            // fluctuation
            this.value += rand.NextDouble() * this.fluctuationTempScale - 0.5 * this.fluctuationTempScale;
        }
    }
}

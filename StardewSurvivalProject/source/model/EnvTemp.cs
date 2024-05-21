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
        public double value { get; set; }
        private bool fixedTemp;
        private double timeTempModifier;
        private double decTime;
        private double dayNightCycleTempDiffScale;
        private double fluctuationTempScale;
        private Random rand = null;
        private static double DEFAULT_VALUE = ModConfig.GetInstance().EnvironmentBaseTemperature;

        public EnvTemp()
        {
            this.value = DEFAULT_VALUE;
            this.fixedTemp = false;
            this.rand = new Random();
        }

        private double distance_square(double aX, double aY, double bX, double bY)
        {
            return (aX - bX) * (aX - bX) + (aY - bY) * (aY - bY);
        }

        private bool checkIfItemIsActive(KeyValuePair<int, SObject> o, int checkType = 0)
        {
            //check if the object checking is a big craftable craftable
            if (checkType == 1)
            {
                //check if said big craftable is being used
                if (o.Value.MinutesUntilReady > 0 && o.Value.heldObject.Value != null)
                {
                    //LogHelper.Debug($"there is an active {o.Value.name} nearby (machine)");
                    return true;
                }
                else
                {
                    //LogHelper.Debug($"there is an inactive {o.Value.name} nearby (machine)");
                    return false;
                }
            }
            else
            {
                //if not big craftable (assuming furniture), check if said furniture is active
                if (o.Value.IsOn)
                {
                    //LogHelper.Debug($"there is an active {o.Value.name} nearby");
                    return true;
                }
                else
                {
                    //LogHelper.Debug($"there is an inactive {o.Value.name} nearby");
                    return false;
                }
            }
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

        private void applyTempControl(GameLocation location, int playerTileX, int playerTileY)
        {
            int proximityCheckBound = (int)Math.Ceiling(data.TempControlObjectDictionary.maxEffectiveRange);
            Dictionary<int, SObject> nearbyObject = new Dictionary<int, SObject>();

            for (int i = playerTileX - proximityCheckBound; i <= playerTileX + proximityCheckBound; i++)
            {
                for (int j = playerTileY - proximityCheckBound; j <= playerTileY + proximityCheckBound; j++)
                {
                    SObject obj = location.getObjectAtTile(i, j);
                    if (obj != null && !nearbyObject.ContainsKey(obj.GetHashCode()))
                    {
                        LogHelper.Debug($"there is a {obj.Name} nearby");
                        nearbyObject.Add(obj.GetHashCode(), obj);
                    }
                }
            }

            double oldVal = this.value;

            foreach (KeyValuePair<int, SObject> o in nearbyObject)
            {
                data.TempControlObject tempControl = data.TempControlObjectDictionary.GetTempControlData(o.Value.Name);
                if (tempControl != null)
                {
                    //if this item need to be active
                    if (tempControl.needActive)
                    {
                        if (!checkIfItemIsActive(o, tempControl.activeType))
                        {
                            LogHelper.Debug($"{o.Value.Name} need to be active continue");
                            continue;
                        }
                    }

                    //prioritize ambient temp if it exceed device's core temp
                    if ((tempControl.deviceType.Equals("heating") && tempControl.coreTemp < this.value) ||
                        (tempControl.deviceType.Equals("cooling") && tempControl.coreTemp > this.value))
                    {
                        LogHelper.Debug($"{tempControl.name} priority contionue");
                        continue;
                    }

                    //dealing with target temp this.value here?
                    double distance_sqr = distance_square(o.Value.TileLocation.X, o.Value.TileLocation.Y, playerTileX, playerTileY);
                    LogHelper.Debug($"Distance square from player to {o.Value.Name} is {distance_sqr}");

                    double effRange = tempControl.effectiveRange;
                    if (distance_sqr <= effRange * effRange)
                    {
                        double tempModifierEntry = (tempControl.coreTemp - this.value) * (1 / (1 + distance_sqr));
                        LogHelper.Debug($"tempModifierEntry {tempModifierEntry}");
                        this.value += tempModifierEntry;
                    }
                }

            }
            LogHelper.Debug($"Final temperature modifier is {this.value - oldVal}");
        }

        public void updateEnvTemp(int playerTileX, int playerTileY, int time, string season, int weatherIconId, GameLocation location = null, int currentMineLevel = 0)
        {
            if (location != null)
            {
                applySeason(season);
                applyWeather(weatherIconId);
                applyLocation(location, currentMineLevel);
                applyTempControl(location, playerTileX, playerTileY);
            }
            else this.value = DEFAULT_VALUE;

            // day cycle
            this.dayNightCycleTempDiffScale = ModConfig.GetInstance().DefaultDayNightCycleTemperatureDiffScale;
            this.decTime = time / 100 + time % 100 / 60.0;
            this.timeTempModifier = Math.Sin((this.decTime - 8.5) / (Math.PI * 1.2)) * this.dayNightCycleTempDiffScale;
            this.value += fixedTemp ? 0 : this.timeTempModifier;

            // fluctuation
            this.fluctuationTempScale = ModConfig.GetInstance().DefaultTemperatureFluctuationScale;
            this.value += rand.NextDouble() * this.fluctuationTempScale - 0.5 * this.fluctuationTempScale;
        }
    }
}

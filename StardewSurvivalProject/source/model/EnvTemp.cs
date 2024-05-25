using System;
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

        private double pixelToTile(int pixel)
        {
            return (double)pixel / Game1.tileSize;
        }

        private double distance(double aX, double aY, double bX, double bY)
        {
            return Math.Sqrt((aX - bX) * (aX - bX) + (aY - bY) * (aY - bY));
        }

        private bool checkIfItemIsActive(SObject obj, int checkType = 0)
        {
            //check if the object is a machine for crafting (eg. Furnace, Charcoal Kiln)
            if (checkType == 1)
            {
                if (obj.MinutesUntilReady > 0 && obj.heldObject.Value != null) return true;
                else return false;
            }
            else
            {
                //if not a machine for crafting (assuming furniture), check if said furniture is active
                if (obj.IsOn) return true;
                else return false;
            }
        }

        private void applySeason(string season)
        {
            // season temperature modifiers

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
            // weather temperature modifiers

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
            // location temperature modifiers

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

        private void applyTempControlObjects(GameLocation location, double playerTileX, double playerTileY)
        {
            // local temperature emitted by objects

            int proximityCheckBound = (int)Math.Ceiling(data.TempControlObjectDictionary.maxEffectiveRange);
            List<SObject> nearbyObjects = new List<SObject>();
            double oldVal = this.value;
            for (double i = playerTileX - proximityCheckBound; i <= playerTileX + proximityCheckBound; i++)
                for (double j = playerTileY - proximityCheckBound; j <= playerTileY + proximityCheckBound; j++)
                {
                    SObject obj = location.getObjectAtTile((int)i, (int)j);

                    if (obj != null && !nearbyObjects.Contains(obj)) // skip objects, that already calculated
                    {
                        data.TempControlObject tempControlObject = data.TempControlObjectDictionary.GetTempControlData(obj.Name);
                        if (tempControlObject != null)
                        {
                            nearbyObjects.Add(obj);

                            // skip inactive objects, that need to be activated
                            if (tempControlObject.needActive && !checkIfItemIsActive(obj, tempControlObject.activeType)) continue;

                            // prioritize ambient temp if it exceed device's core temp
                            if ((tempControlObject.deviceType.Equals("heating") && tempControlObject.coreTemp < this.value) ||
                                (tempControlObject.deviceType.Equals("cooling") && tempControlObject.coreTemp > this.value))
                                continue;

                            // dealing with target temp this.value here?
                            double dist = Math.Max(distance(pixelToTile(obj.GetBoundingBox().Center.X), pixelToTile(obj.GetBoundingBox().Center.Y), playerTileX, playerTileY), 1);
                            if (dist <= tempControlObject.effectiveRange)
                            {
                                double tempModifierEntry = (tempControlObject.coreTemp - this.value) / (15 * (dist - 1) / tempControlObject.effectiveRange + 1);
                                this.value += tempModifierEntry;
                            }
                        }
                    }
                }
        }

        private void applyAmbient(GameLocation location)
        {
            // ambient by temperature control objects

            if (!location.IsOutdoors)
            {
                List<data.TempControlObject> tempControlObjects = new List<data.TempControlObject>();

                // objects
                foreach (SObject obj in location.objects.Values)
                {
                    data.TempControlObject tempControlObject = data.TempControlObjectDictionary.GetTempControlData(obj.name);
                    if (tempControlObject != null)
                        if (!tempControlObject.needActive || checkIfItemIsActive(obj, tempControlObject.activeType)) // skips inactive objects, that need to be activated
                            tempControlObjects.Add(tempControlObject);
                }

                // furniture
                foreach (SObject obj in location.furniture)
                {
                    data.TempControlObject tempControlObject = data.TempControlObjectDictionary.GetTempControlData(obj.name);
                    if (tempControlObject != null)
                        if (!tempControlObject.needActive || checkIfItemIsActive(obj, tempControlObject.activeType)) // skips inactive objects, that need to be activated
                            tempControlObjects.Add(tempControlObject);
                }

                double area = location.Map.GetLayer("Back").Tiles.Array.Length;
                double power = 0;

                foreach (data.TempControlObject tempControlObject in tempControlObjects)
                {
                    //calculate indoor heating power base on core temp and range (assume full effectiveness if object is placed indoor)
                    if (tempControlObject.deviceType.Equals("general"))
                    {
                        double perfectAmbientPower = area * DEFAULT_VALUE;
                        double maxPowerFromDevice = tempControlObject.operationalRange * (tempControlObject.effectiveRange * 2 + 1) * (tempControlObject.effectiveRange * 2 + 1) * tempControlObject.ambientCoefficient;
                        if (DEFAULT_VALUE > this.value)
                            power = Math.Min(perfectAmbientPower, power + maxPowerFromDevice);
                        else
                            power = Math.Max(perfectAmbientPower, power - maxPowerFromDevice);
                    }
                    else power += (tempControlObject.coreTemp - DEFAULT_VALUE) * (tempControlObject.effectiveRange * 2 + 1) * (tempControlObject.effectiveRange * 2 + 1) * tempControlObject.ambientCoefficient;
                }
                this.value += 0.5 * power / area;
            }
        }

        public void updateEnvTemp(int playerPixelX, int playerPixelY, int time, string season, int weatherIconId, GameLocation location = null, int currentMineLevel = 0)
        {
            this.fixedTemp = false;
            this.value = DEFAULT_VALUE;
            if (location != null)
            {
                applySeason(season);
                applyWeather(weatherIconId);
                applyLocation(location, currentMineLevel);
                applyTempControlObjects(location, pixelToTile(playerPixelX), pixelToTile(playerPixelY));
                applyAmbient(location);
            }
            else
            {
                this.value = DEFAULT_VALUE;
                this.fixedTemp = true;
            }

            // day cycle
            this.dayNightCycleTempDiffScale = ModConfig.GetInstance().DefaultDayNightCycleTemperatureDiffScale;
            this.decTime = time / 100 + time % 100 / 60.0;
            this.value += fixedTemp ? 0 : Math.Sin((this.decTime - 8.5) / (Math.PI * 1.2)) * this.dayNightCycleTempDiffScale;

            // fluctuation
            this.fluctuationTempScale = ModConfig.GetInstance().DefaultTemperatureFluctuationScale;
            this.value += rand.NextDouble() * this.fluctuationTempScale - 0.5 * this.fluctuationTempScale;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using StardewModdingAPI;
using System.IO;
using Newtonsoft.Json;

namespace StardewSurvivalProject.source.data
{
    public class HydrationItemData
    {
        public string name { get; set; } = "";
        public double value { get; set; } = 0;
        public double coolingModifier { get; set; } = 1;
    }

    public class CustomHydrationDictionary
    {
        //load a whitelist of item that can be used to heal player (healing value is separated from edibility)
        public static Dictionary<string, HydrationItemData> value_list = new Dictionary<string, HydrationItemData>();
        public static Dictionary<string, HydrationItemData> wildcard_value_list = new Dictionary<string, HydrationItemData>();

        public static void loadList(Mod context)
        {

            string RelativePath = Path.Combine("customHydrationData.json");
            HydrationItemData[] tempArray = context.Helper.Data.ReadJsonFile<HydrationItemData[]>(RelativePath);
            if (tempArray == null)
            {
                LogHelper.Warn("No hydration item entry is found");
                return;
            }
            for (int i = 0; i < tempArray.Length; i++)
                if (tempArray[i].name.Contains("*"))
                    wildcard_value_list.TryAdd(tempArray[i].name, tempArray[i]);
                else
                    value_list.TryAdd(tempArray[i].name, tempArray[i]);
            LogHelper.Debug("Hydration Item Data loaded");
        }

        public static double getHydrationValue(string itemName)
        {
            return getItemData(itemName)?.value ?? 0;
        }

        public static double getCoolingModifierValue(string itemName)
        {
            return getItemData(itemName)?.coolingModifier ?? 1;
        }

        public static (double, double) getHydrationAndCoolingModifierValue(string itemName, bool isDrinkable)
        {
            HydrationItemData data = getItemData(itemName);
            return (data?.value ?? (isDrinkable ? ModConfig.GetInstance().DefaultHydrationGainOnDrinkableItems : 0), data?.coolingModifier ?? 0);
        }

        public static HydrationItemData getItemData(string itemName)
        {
            // should we match case-insensitive?
            if (value_list.ContainsKey(itemName))
            {
                return value_list[itemName];
            }
            else
            {
                // iterate through wildcard list
                foreach (KeyValuePair<string, HydrationItemData> entry in wildcard_value_list)
                {
                    // contain match
                    if (entry.Key.StartsWith("*") && entry.Key.EndsWith("*") && itemName.Contains(entry.Key.Substring(1, entry.Key.Length - 2)))
                    {
                        return entry.Value;
                    }
                    // prefix match
                    else if (entry.Key.StartsWith("*") && itemName.EndsWith(entry.Key.Substring(1)))
                    {
                        return entry.Value;
                    }
                    // postfix match
                    else if (entry.Key.EndsWith("*") && itemName.StartsWith(entry.Key.Substring(0, entry.Key.Length - 1)))
                    {
                        return entry.Value;
                    }
                }
                return null;
            }
        }
    }
}

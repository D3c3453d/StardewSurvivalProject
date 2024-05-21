﻿using System;
using System.Collections.Generic;
using System.IO;
using StardewModdingAPI;
using Newtonsoft.Json;

namespace StardewSurvivalProject.source.data
{
    public class HealingItemData
    {
        public string name { get; set; } = "";
        public int value { get; set; } = 0;
    }

    public class HealingItemDictionary
    {
        //load a whitelist of item that can be used to heal player (healing value is separated from edibility)
        public static Dictionary<string, HealingItemData> value_list = new Dictionary<string, HealingItemData>();

        public static void loadList(Mod context)
        {
            string RelativePath = Path.Combine(context.Helper.DirectoryPath, "healingItemData.json");
            string jsonData = File.ReadAllText(RelativePath);
            HealingItemData[] tempArray = JsonConvert.DeserializeObject<HealingItemData[]>(jsonData);

            if (tempArray == null)
            {
                LogHelper.Error("Failed to load list");
                return;
            }
            for (int i = 0; i < tempArray.Length; i++)
                value_list.Add(tempArray[i].name, tempArray[i]);
            LogHelper.Debug("Healing Item Data loaded");
        }

        public static int getHealingValue(string itemName)
        {
            if (value_list.ContainsKey(itemName))
            {
                return value_list[itemName].value;
            }
            else
            {
                return 0;
            }
        }

        public static HealingItemData getItemData(string itemName)
        {
            if (value_list.ContainsKey(itemName))
            {
                return value_list[itemName];
            }
            else
            {
                return null;
            }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CineGame.SDK.Editor
{
    internal class CineGameMarketEditor
    {
        [MenuItem("CineGame SDK/Market/Leffapeli", true)]
        public static bool GetLeffapeli()
        {
            if (EditorPrefs.GetString("CineGameMarket") == CineGameMarket.Markets.Leffapeli_Finnkino_FI)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        [MenuItem("CineGame SDK/Market/Leffapeli", false)]
        public static void SetProduction()
        {
            EditorPrefs.SetString("CineGameMarket", CineGameMarket.Markets.Leffapeli_Finnkino_FI);
            CineGameMarket.Names.TryGetValue(CineGameMarket.Markets.Leffapeli_Finnkino_FI, out string marketName);
            Debug.Log(string.Format("CineGame Market: {0} ({1})" + marketName, CineGameMarket.Markets.Leffapeli_Finnkino_FI));
        }
    }
}


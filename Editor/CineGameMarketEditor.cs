using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using static CineGame.SDK.Editor.CineGameLogin;

namespace CineGame.SDK.Editor
{
    public static class CineGameMarketEditor
    {

        public static Action<string> OnMarketChanged;

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            if (!EditorPrefs.HasKey("CineGameMarket"))
            {
                EditorPrefs.SetString("CineGameMarket", CineGameMarket.Markets.CineGame_Cinemataztic_EN);
                CineGameMarketEditor.OnMarketChanged?.Invoke(CineGameMarket.Markets.CineGame_Cinemataztic_EN);
            }
        }
    }

    public class CineGameMarketEditorWindow : EditorWindow
    {
        static CineGameMarketEditorWindow instance;

        private string market;
        private int marketIndex;
        private string[] marketNames;

        [MenuItem("CineGame SDK/Market", false, 1)]
        internal static void Init()
        {
            if (instance == null)
            {
                instance = GetWindow<CineGameMarketEditorWindow>("CineGame Market");
            }

            instance.market = EditorPrefs.GetString("CineGameMarket");
            instance.marketNames = CineGameMarket.Names.Values.ToArray();

            if(CineGameMarket.Names.TryGetValue(instance.market, out string marketName))
            {
                for (int i = 0; i < instance.marketNames.Length; i++)
                {
                    if (instance.marketNames[i] == marketName)
                    {
                        instance.marketIndex = i;
                    }
                }
            }

            if (!CineGameLogin.RefreshAccessToken())
            {
                CineGameLogin.Init();
            }

            instance.Focus();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            int guiMarketIndex = EditorGUILayout.Popup(new GUIContent("Market:"), marketIndex, marketNames);

            if (EditorGUI.EndChangeCheck())
            {
                marketIndex = guiMarketIndex;
                EditorPrefs.SetString("CineGameMarket", CineGameMarket.MarketIDs[marketIndex]);
                CineGameMarketEditor.OnMarketChanged?.Invoke(CineGameMarket.MarketIDs[marketIndex]);
                Debug.Log(string.Format("CineGame Market: {0} ({1})", CineGameMarket.Names.Values.ToArray()[marketIndex], CineGameMarket.MarketIDs[marketIndex]));

                if(!CineGameLogin.RefreshAccessToken())
                {
                    CineGameLogin.Init();
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}


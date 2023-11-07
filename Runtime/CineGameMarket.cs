using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using UnityEditor;

namespace CineGame.SDK
{
	public class CineGameMarket
    {
        public static class Markets
        {
            public const string BioSpil_DRF_DK = "57ff5b54359bc3000f1e1303";
            public const string CineGame_Cinemataztic_AE = "5c12f1c58c2a1a5509cad589";
            public const string CineGame_Cinemataztic_EN = "57e79e61bb29b2000f22c705";
            public const string CineGame_Cinemataztic_SE = "653676850c50fc8ecda86b43";
            public const string CineGame_ITV_IN = "627049112c827460088db3fd";
            public const string CineGame_ValMorgan_AU = "5ba2a95eb81b02b3d8198f89";
            public const string CineGame_ValMorgan_NZ = "62a741d8709ea7ac02336c29";
            public const string CineGame_WideEyeMedia_IE = "618301a5be9b8d3befa0b589";
            public const string CinesaFun_Cinesa_ES = "5df786218c2a1a550974e19d";
            public const string ForumFun_Cinemataztic_EE = "5ced2b5a8c2a1a5509b0116b";
            public const string KinoSpill_DRF_NO = "57e79e40bb29b2000f22c704";
            public const string Leffapeli_Finnkino_FI = "5829676efd5ab2000f4eb252";
            public const string REDyPLAY_Weicher_DE = "5c44f3ba8c2a1a5509df3f6b";
        }

        public static List<string> MarketIDs = new List<string>
        {
            Markets.BioSpil_DRF_DK,
            Markets.CineGame_Cinemataztic_AE,
            Markets.CineGame_Cinemataztic_EN,
            Markets.CineGame_Cinemataztic_SE,
            Markets.CineGame_ITV_IN,
            Markets.CineGame_ValMorgan_AU,
            Markets.CineGame_ValMorgan_NZ,
            Markets.CineGame_WideEyeMedia_IE,
            Markets.CinesaFun_Cinesa_ES,
            Markets.ForumFun_Cinemataztic_EE,
            Markets.KinoSpill_DRF_NO,
            Markets.Leffapeli_Finnkino_FI,
            Markets.REDyPLAY_Weicher_DE
        };

        public static string GetID()
        {
            return CineGameSDK.Market;
        }

        public static Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { Markets.BioSpil_DRF_DK, "BioSpil_DRF_DK" },
            { Markets.CineGame_Cinemataztic_AE, "CineGame_Cinemataztic_AE" },
            { Markets.CineGame_Cinemataztic_EN, "CineGame_Cinemataztic_EN" },
            { Markets.CineGame_Cinemataztic_SE, "CineGame_Cinemataztic_SE" },
            { Markets.CineGame_ITV_IN, "CineGame_ITV_IN" },
            { Markets.CineGame_ValMorgan_AU, "CineGame_ValMorgan_AU" },
            { Markets.CineGame_ValMorgan_NZ, "CineGame_ValMorgan_NZ" },
            { Markets.CineGame_WideEyeMedia_IE, "CineGame_WideEyeMedia_IE" },
            { Markets.CinesaFun_Cinesa_ES, "CinesaFun_Cinesa_ES" },
            { Markets.ForumFun_Cinemataztic_EE, "ForumFun_Cinemataztic_EE" },
            { Markets.KinoSpill_DRF_NO, "KinoSpill_DRF_NO" },
            { Markets.Leffapeli_Finnkino_FI, "Leffapeli_Finnkino_FI" },
            { Markets.REDyPLAY_Weicher_DE, "REDyPLAY_Weicher_DE" }
        };

        public static string GetName()
        {
            return Names[CineGameSDK.Market];
        }

        public static string GetSimpleName()
        {
            return Names[CineGameSDK.Market].Split("_")[0];
        }

        public static Dictionary<string, string> Slugs = new Dictionary<string, string>
        {
            { Markets.BioSpil_DRF_DK, "drf-dk" },
            { Markets.CineGame_Cinemataztic_AE,  "cinemataztic-ae" },
            { Markets.CineGame_Cinemataztic_EN, "cinemataztic-en" },
            { Markets.CineGame_Cinemataztic_SE, "filmstaden-se" },
            { Markets.CineGame_ITV_IN, "ivt-in" },
            { Markets.CineGame_ValMorgan_AU, "valmorgan-au" },
            { Markets.CineGame_ValMorgan_NZ, "valmorgan-nz" },
            { Markets.CineGame_WideEyeMedia_IE, "wideeyemedia-ie" },
            { Markets.CinesaFun_Cinesa_ES, "cinesafun-es" },
            { Markets.ForumFun_Cinemataztic_EE, "forumfun-ee" },
            { Markets.KinoSpill_DRF_NO, "mdn-no" },
            { Markets.Leffapeli_Finnkino_FI, "finnkino-fi" },
            { Markets.REDyPLAY_Weicher_DE, "weischer-de" }
        };

        public static Dictionary<string, string> Languages = new Dictionary<string, string>
        {
            { Markets.BioSpil_DRF_DK, "da" },
            { Markets.CineGame_Cinemataztic_AE, "en" },
            { Markets.CineGame_Cinemataztic_EN, "en" },
            { Markets.CineGame_Cinemataztic_SE, "se" },
            { Markets.CineGame_ITV_IN, "en" },
            { Markets.CineGame_ValMorgan_AU, "en-au" },
            { Markets.CineGame_ValMorgan_NZ, "en-nz" },
            { Markets.CineGame_WideEyeMedia_IE, "en-ie" },
            { Markets.CinesaFun_Cinesa_ES, "en" },
            { Markets.ForumFun_Cinemataztic_EE, "en" },
            { Markets.KinoSpill_DRF_NO, "no" },
            { Markets.Leffapeli_Finnkino_FI, "fi" },
            { Markets.REDyPLAY_Weicher_DE, "de" }
        };

        public static Dictionary<string, int> Durations = new Dictionary<string, int>
        {
            { Markets.BioSpil_DRF_DK, 420 },
            { Markets.CineGame_Cinemataztic_AE, 420 },
            { Markets.CineGame_Cinemataztic_EN, 420 },
            { Markets.CineGame_Cinemataztic_SE, 360 },
            { Markets.CineGame_ITV_IN, 220 },
            { Markets.CineGame_ValMorgan_AU, 180 },
            { Markets.CineGame_ValMorgan_NZ, 240 },
            { Markets.CineGame_WideEyeMedia_IE, 420 },
            { Markets.CinesaFun_Cinesa_ES, 420 },
            { Markets.ForumFun_Cinemataztic_EE, 420 },
            { Markets.KinoSpill_DRF_NO, 120 },
            { Markets.Leffapeli_Finnkino_FI, 600 },
            { Markets.REDyPLAY_Weicher_DE, 360 }
        };

        public static int GetDuration()
        {
            return Durations[CineGameSDK.Market];
        }

        public static Dictionary<string, string> APIs = new Dictionary<string, string>
        {
            { Markets.BioSpil_DRF_DK, "https://biospil.cinegamecore.drf-1.cinemataztic.com/api/" },
            { Markets.CineGame_Cinemataztic_AE, "https://cinegame-ae.cinegamecore.au-1.cinemataztic.com/api/" },
            { Markets.CineGame_Cinemataztic_EN, "https://cinemataztic-en.cinegamecore.eu-1.cinemataztic.com/api/" },
            { Markets.CineGame_Cinemataztic_SE, "https://filmstaden-se.cinegamecore.eu-1.cinemataztic.com/api/" },
            { Markets.CineGame_ITV_IN, "https://cinegame-in.cinegamecore.asia-1.cinemataztic.com/api/" },
            { Markets.CineGame_ValMorgan_AU, "https://cinegame-au.cinegamecore.au-1.cinemataztic.com/api/" },
            { Markets.CineGame_ValMorgan_NZ, "https://cinegame-nz.cinegamecore.au-1.cinemataztic.com/api/" },
            { Markets.CineGame_WideEyeMedia_IE, "https://cinegame-ie.cinegamecore.eu-2.cinemataztic.com/api/" },
            { Markets.CinesaFun_Cinesa_ES, "https://cinesafun-es.cinegamecore.eu-2.cinemataztic.com/api/" },
            { Markets.ForumFun_Cinemataztic_EE, "https://forumfun.cinegamecore.eu-1.cinemataztic.com/api/" },
            { Markets.KinoSpill_DRF_NO, "https://kinospill-no.cinegamecore.drf-1.cinemataztic.com/api/" },
            { Markets.Leffapeli_Finnkino_FI, "https://finnkino-fi.cinegamecore.eu-1.cinemataztic.com/api/" },
            { Markets.REDyPLAY_Weicher_DE, "https://redyplay.cinegamecore.eu-2.cinemataztic.com/api/" }
        };

        //https://redyplay-de.cinegamecore.eu-2.cinemataztic.com/api/

        public static string GetAPI()
        {

            string url = APIs[CineGameSDK.Market];
            string cineGameEnvironment;

#if UNITY_EDITOR
            cineGameEnvironment = EditorPrefs.GetString("CineGameEnvironment");
#else
            cineGameEnvironment = Configuration.CLUSTER_NAME;
#endif

            if (!String.IsNullOrEmpty(cineGameEnvironment))
            {
                switch (cineGameEnvironment)
                {
                    case "dev":
                        url = Regex.Replace(url, "(.+?)\\.[^.]+?\\.(cinemataztic\\.com.+)", "$1.dev.$2");
                        break;
                    case "staging":
                        url = Regex.Replace(url, "(.+?)\\.[^.]+?\\.(cinemataztic\\.com.+)", "$1.staging.$2");
                        break;
                    default:
                        break;
                }
            }

            return url;
        }

    }
}


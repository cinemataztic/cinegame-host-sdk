using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CineGame.SDK.Editor { 
    internal class CineGameEnvironment
    {
        [MenuItem("CineGame SDK/Environment/Production", true)]
        public static bool GetProduction()
        {
            if (EditorPrefs.GetString("CineGameEnvironment") == "Production")
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        [MenuItem("CineGame SDK/Environment/Production", false)]
        public static void SetProduction()
        {
            EditorPrefs.SetString("CineGameEnvironment", "Production");
            Debug.Log("CineGame Environment: Production");
        }

        [MenuItem("CineGame SDK/Environment/Staging", true)]
        public static bool GetStaging()
        {
            if (EditorPrefs.GetString("CineGameEnvironment") == "Staging")
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        [MenuItem("CineGame SDK/Environment/Staging", false)]
        public static void SetStaging()
        {
            EditorPrefs.SetString("CineGameEnvironment", "Staging");
            Debug.Log("CineGame Environment: Staging");
        }

        [MenuItem("CineGame SDK/Environment/Development", true)]
        public static bool GetDevelopment()
        {
            if (EditorPrefs.GetString("CineGameEnvironment") == "Development")
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        [MenuItem("CineGame SDK/Environment/Development", false)]
        public static void SetDevelopment()
        {
            EditorPrefs.SetString("CineGameEnvironment", "Development");
            Debug.Log("CineGame Environment: Development");
        }
    }
}

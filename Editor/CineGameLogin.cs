﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

using CineGame.Host;

namespace CineGame.HostEditor
{
    public class CineGameLogin : EditorWindow
    {
        static CineGameLogin instance;

        static string Username;
        static string Password;
        static bool StayLoggedIn;

        public static bool IsLoggedIn {
            get {
                return !string.IsNullOrWhiteSpace (Configuration.CINEMATAZTIC_ACCESS_TOKEN) && AccessTokenExpiry > DateTime.Now;
            }
        }



        bool KeyDown(KeyCode keyCode)
        {
            return Event.current.type == EventType.KeyDown && Event.current.keyCode == keyCode;
        }

        [InitializeOnLoadMethod]
        private static void OnLoad () {
            CineGameSDK.OnError -= OnGameCodeError;
            CineGameSDK.OnError += OnGameCodeError;
            LoginRegionIndex = Mathf.Clamp (Array.IndexOf (LoginRegions, EditorPrefs.GetString ("CineGameMarketRegion", LoginRegions [0])), 0, LoginRegions.Length - 1);
            if (RefreshAccessToken ()) {
                CineGameBuild.GetGameTypeFromSceneOrProject ();
            } else {
                Logout ();
            }
        }

        static void OnGameCodeError (int code) {
            Init ();
            EditorApplication.ExitPlaymode ();
            if (code == (int)HttpStatusCode.Unauthorized) {
                //If backend returns Unauthorized, the token probably has expired. Try to refresh it automatically if possible.
                if (RefreshAccessToken ()) {
                    EditorUtility.DisplayDialog (instance.titleContent.text, "Access token refreshed. Please re-enter play mode", "OK");
                    return;
                }
                //User needs to manually log in.
                EditorUtility.DisplayDialog (instance.titleContent.text, "You are not logged in. Please log in with your credentials!", "OK");
            }
        }

        [MenuItem ("CineGame/Login")]
        internal static void Init () {
            if (instance == null) {
                instance = GetWindow<CineGameLogin> ("CineGame Login", typeof(CineGameBuild), typeof(CineGameTest));
            }
            instance.Focus ();
        }

        void OnGUI()
        {
            var enterKeyPressed = KeyDown(KeyCode.Return);
            var focusedControl = GUI.GetNameOfFocusedControl();
            var passwordEntered = focusedControl == ControlNames.Password && enterKeyPressed;

            if (!IsLoggedIn) {
                EditorGUILayout.HelpBox ("Not logged in!", MessageType.Error);
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal ();
            EditorGUILayout.PrefixLabel ("Username:");
            if (!IsLoggedIn) {
                Username = EditorGUILayout.TextField (Username);
            } else {
                EditorGUILayout.LabelField (Username);
            }
            EditorGUILayout.EndHorizontal ();

            EditorGUILayout.Space();

            if (!IsLoggedIn)
            {
                LoginRegionIndex = EditorGUILayout.Popup (new GUIContent ("Region:"), LoginRegionIndex, LoginRegions);
                GUI.SetNextControlName (ControlNames.Password);
                Password = EditorGUILayout.PasswordField ("Password:", Password);
                StayLoggedIn = EditorGUILayout.Toggle ("Stay logged in", StayLoggedIn);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(" ");
                var loginPressed = GUILayout.Button("Login", GUILayout.MaxWidth(200f));
                EditorGUILayout.EndHorizontal();
                if (loginPressed || passwordEntered)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes (Password);
                    for (int i = 0; i < bytes.Length; i++) {
                        bytes [i] ^= 0x5a;
                    }
                    EditorPrefs.SetString ("CGSP", Convert.ToBase64String (bytes));
                    EditorPrefs.SetBool ("CineGameStayLoggedIn", StayLoggedIn);

                    EditorPrefs.SetString("CineGameMarketRegion", LoginRegionDisplayName);

                    if (!GetAccessToken (Username, Password)) {
                        EditorUtility.DisplayDialog (titleContent.text, "Failed to login. Check username and password and that you are connected to the internet", "OK");
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField ("Region:", LoginRegionDisplayName);
                EditorGUILayout.Space ();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(" ");
                var logOutPressed = GUILayout.Button("Log Out", GUILayout.MaxWidth(200f));
                EditorGUILayout.EndHorizontal();
                if (logOutPressed)
                {
                    Logout ();
                }
            }
        }

        public static void Logout () {
            //EditorPrefs.DeleteKey("CineGameUserName");
            //Username = string.Empty;
            EditorPrefs.DeleteKey ("CGSP");
            Password = string.Empty;
            EditorPrefs.DeleteKey ("CineGameStayLoggedIn");
            StayLoggedIn = false;
            RemoveAccessToken ();
            MarketIdsAvailable = null;
            GameTypesAvailable = null;
        }

        static DateTime AccessTokenExpiry = DateTime.MinValue;

        public static int LoginRegionIndex;
        public static string [] LoginRegions = {
            "Staging",
            "Cinemataztic",
            "Egmont (Nordisk Film)",
            "Wide Eye Media",
            "ITV India",
        };
        public static string LoginRegionDisplayName {
            get {
                return LoginRegions [LoginRegionIndex];
            }
        }

        public static bool IsSuperAdmin;
        public static string [] MarketIdsAvailable;
        public static string [] MarketSlugsAvailable;
        public static string [] AppNamesAvailable;
        public static string [] GameTypesAvailable;

        public static class ControlNames {
            public const string Password = "Password";
            public const string GameType = "GameType";
        }

        public static bool GetAccessToken (string userName, string userPassword) {
            if (!string.IsNullOrEmpty (userName) && !string.IsNullOrEmpty (userPassword)) {
                var tokenUri = GetTokenUri (LoginRegionIndex);
                var jsonReq = "{\"type\":\"user\","
                              + "\"email\":" + MiniJSON.Json.Serialize (userName) + ","
                              + "\"password\":" + MiniJSON.Json.Serialize (userPassword)
                              + "}";
                var request = new UnityWebRequest (
                                  tokenUri,
                                  "POST",
                                  new DownloadHandlerBuffer (),
                                  new UploadHandlerRaw (System.Text.Encoding.UTF8.GetBytes (jsonReq))
                              );
                //if (Debug.isDebugBuild) {
                //    Debug.Log ($"{request.method} {request.url}");
                //}
                request.SetRequestHeader ("Content-Type", "application/json; charset=utf-8");
                request.SendWebRequest ();
                while (!request.isDone) {
                    System.Threading.Thread.Sleep (100);
                }
                if (request.result == UnityWebRequest.Result.ConnectionError) {
                    Debug.LogError ($"Network error: {request.error}");
                    return false;
                }
                if (request.responseCode != 200) {
                    Debug.LogError ($"Unable to get token: {request.responseCode} {request.downloadHandler?.text}");
                    Configuration.CINEMATAZTIC_ACCESS_TOKEN = string.Empty;
                    return false;
                }
                try {
                    var d = MiniJSON.Json.Deserialize (request.downloadHandler.text) as Dictionary<string, object>;
                    Configuration.CINEMATAZTIC_ACCESS_TOKEN = d ["access_token"] as string;
                    AccessTokenExpiry = DateTime.Now.AddHours (0.8);
                    /*
                    var exp = (long)d ["exp"];
                    AccessTokenExpiry = new DateTime (1970, 1, 1, 0, 0, 0).AddSeconds (exp);
                    if (Debug.isDebugBuild) {
                        Debug.Log ($"AccessToken expires {AccessTokenExpiry:s} {AccessToken}");
                    }
                    */

                    MarketIdsAvailable = (d ["markets"] as List<object>).Select (m => m as string).ToArray ();
                    MarketSlugsAvailable = MarketIdsAvailable.Select (id => CineGameSDK.MarketSlugMap.GetValueOrDefault (id, "???")).ToArray ();

                    var appNames = new List<string> (MarketIdsAvailable.Length);
                    foreach (var id in MarketIdsAvailable) {
                        appNames.Add (CineGameSDK.MarketDisplayNamesMap [id]);
                    }
                    AppNamesAvailable = appNames.ToArray ();

                    var roles = (d ["role"] as List<object>).Select (s => s as string);
                    IsSuperAdmin = roles.Contains ("super-admin");

                    //TODO there's a security issue here because login is across markets, but game-access list should be per market.
                    GameTypesAvailable = d.ContainsKey ("game-access") ? (d ["game-access"] as List<object>).Select (s => s as string).ToArray () : new string [0];

                    EditorPrefs.SetString ("CineGameUserName", userName);

                    CineGameBuild.GetGameTypeFromSceneOrProject ();
                    return true;
                } catch (Exception e) {
                    Debug.LogErrorFormat ("Exception while parsing JSON {0}: {1}", request.downloadHandler.text, e.ToString ());
                }
            }
            return false;
        }

        public static Uri GetTokenUri (int loginRegionIndex) {
            switch (loginRegionIndex) {
            case 0:
                return new Uri ("https://auth.iam.staging.cinemataztic.com/");
            case 1:
                return new Uri ("https://auth.iam.eu-2.cinemataztic.com");
            case 2:
                return new Uri ("https://biospil.auth.iam.nordiskfilm.cinemataztic.com");
            case 3:
                return new Uri ("https://wideeyemedia.ie.auth.iam.eu-2.cinemataztic.com");
            case 4:
                return new Uri ("https://itv.in.auth.iam.asia-1.cinemataztic.com");
            default:
                return null;
            }
        }

        public static bool RefreshAccessToken () {
            if (CGSP ()) {
                if (GetAccessToken (Username, Password)) {
                    Debug.Log ("CineGameSDK: Access token refreshed");
                    return true;
                }
            } else {
                Debug.Log ("CineGameSDK: Not logged in");
            }
            return false;
        }

        public static void RemoveAccessToken () {
            Configuration.CINEMATAZTIC_ACCESS_TOKEN = null;
        }

        public void OnEnable()
        {
            if (!IsLoggedIn) {
                RefreshAccessToken ();
            }

            StayLoggedIn = EditorPrefs.GetBool ("CineGameStayLoggedIn");

            titleContent = new GUIContent("CineGame Login", CineGameBuild.IconTexture);

            EditorApplication.quitting -= OnEditorApplicationQuit;
            EditorApplication.quitting += OnEditorApplicationQuit;
        }

        private static void OnEditorApplicationQuit () {
            if (!EditorPrefs.GetBool ("CineGameStayLoggedIn", false)) {
                EditorPrefs.DeleteKey ("CGSP");
            }
        }

        private static bool CGSP () {
            Username = EditorPrefs.GetString ("CineGameUserName");
            if (!string.IsNullOrWhiteSpace (Username) && EditorPrefs.HasKey ("CGSP")) {
                var bytes = Convert.FromBase64String (EditorPrefs.GetString ("CGSP"));
                for (int i = 0; i < bytes.Length; i++) {
                    bytes [i] ^= 0x5a;
                }
                Password = System.Text.Encoding.UTF8.GetString (bytes);
                return true;
            }
            return false;
        }
    }
}

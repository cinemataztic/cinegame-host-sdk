using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

using Newtonsoft.Json.Linq;

namespace CineGame.Host.Editor
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

        static string [] MarketSlugs;
        static int MarketSlugIndex;
        static string CurrentMarketSlug {
            get {
                if (MarketSlugs == null) {
                    MarketSlugs = CineGameSDK.MarketSlugMap.Values.ToArray ();
                }
                return MarketSlugs [MarketSlugIndex];
            }
        }

        [InitializeOnLoadMethod]
        private static void OnLoad () {
            CineGameSDK.OnError -= OnGameCodeError;
            CineGameSDK.OnError += OnGameCodeError;

            MarketSlugs = CineGameSDK.MarketSlugMap.Values.ToArray ();
            MarketSlugIndex = Mathf.Clamp (Array.IndexOf (MarketSlugs, EditorPrefs.GetString ("CineGameMarket", MarketSlugs [0])), 0, MarketSlugs.Length - 1);
            if (Application.internetReachability == NetworkReachability.NotReachable) {
                Debug.LogError ("Internet not reachable. Unable to refresh token.");
                return;
            }
            if (RefreshAccessToken ()) {
                CineGameBuild.GetGameTypeFromSceneOrProject ();
            } else {
                Logout ();
            }
        }

        static void OnGameCodeError (int code) {
            Init ();
            EditorApplication.ExitPlaymode ();
            if (code == 0 || Application.internetReachability == NetworkReachability.NotReachable) {
                EditorUtility.DisplayDialog (instance.titleContent.text, "No internet connection.", "OK");
            } else if (code == (int)HttpStatusCode.Unauthorized) {
                //If backend returns Unauthorized, the token probably has expired. Try to refresh it automatically if possible.
                if (RefreshAccessToken ()) {
                    EditorUtility.DisplayDialog (instance.titleContent.text, "Access token refreshed. Please re-enter play mode", "OK");
                    return;
                }
                //User needs to manually log in.
                EditorUtility.DisplayDialog (instance.titleContent.text, "You are not logged in. Please log in with your credentials!", "OK");
            } else if (code == 9933) {
                EditorUtility.DisplayDialog (instance.titleContent.text, "No connection to Smartfox server.", "OK");
            } else {
                EditorUtility.DisplayDialog (instance.titleContent.text, $"{(HttpStatusCode)code} while communicating with backend.", "OK");
            }
        }

        [MenuItem ("CineGame/Login")]
        internal static void Init () {
            if (instance == null) {
                instance = GetWindow<CineGameLogin> ("CineGame Login", typeof (CloudBuild), typeof (CineGameTest), typeof (CineGameBuild));
            }
            instance.Focus ();
        }

        void OnGUI()
        {
            var enterKeyPressed = KeyDown(KeyCode.Return);
            var focusedControl = GUI.GetNameOfFocusedControl();
            var passwordEntered = focusedControl == ControlNames.Password && enterKeyPressed;

            EditorGUILayout.Space ();

            if (Application.internetReachability == NetworkReachability.NotReachable) {
                EditorGUILayout.HelpBox ("Internet not reachable.", MessageType.Error);
                return;
            }

            if (!IsLoggedIn) {
                EditorGUILayout.HelpBox ("Not logged in!", MessageType.Error);
            }

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
                MarketSlugIndex = EditorGUILayout.Popup (new GUIContent ("Market:"), MarketSlugIndex, MarketSlugs);
                GUI.SetNextControlName (ControlNames.Password);
                Password = EditorGUILayout.PasswordField ("Password:", Password);
                StayLoggedIn = EditorGUILayout.Toggle ("Stay logged in", StayLoggedIn);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(" ");
                var loginPressed = GUILayout.Button("Login", GUILayout.MaxWidth(200f));
                EditorGUILayout.EndHorizontal();
                if (loginPressed || passwordEntered)
                {
                    EditorPrefs.SetBool ("CineGameStayLoggedIn", StayLoggedIn);
                    EditorPrefs.SetString ("CineGameMarket", CurrentMarketSlug);
                    if (!GetAccessToken (Username, Password)) {
                        EditorUtility.DisplayDialog (titleContent.text, "Failed to login. Check username and password and that you are connected to the internet", "OK");
                    }
                }
            }
            else
            {
                var _msi = EditorGUILayout.Popup (new GUIContent ("Market:"), MarketSlugIndex, MarketSlugs);
                if (MarketSlugIndex != _msi) {
                    MarketSlugIndex = _msi;
					EditorPrefs.SetString ("CineGameMarket", CurrentMarketSlug);
					if (!GetAccessToken (Username, Password)) {
                        EditorUtility.DisplayDialog (titleContent.text, "Failed to login. Check username and password and that you are connected to the internet", "OK");
                    }
                }
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
            for (var i = 0; i < MarketSlugs.Length; i++) {
                EditorPrefs.DeleteKey ("CGSP_" + MarketSlugs [i]);
            }
            Password = string.Empty;
            EditorPrefs.DeleteKey ("CineGameStayLoggedIn");
            StayLoggedIn = false;
            RemoveAccessToken ();
            MarketIdsAvailable = null;
            GameTypesAvailable = null;
        }

        static DateTime AccessTokenExpiry = DateTime.MinValue;

        internal static Uri CinematazticApiBaseUri {
            get {
                return CurrentMarketSlug switch {
                    "biospil-dk" => new Uri ("https://biospil.api.player.drf-1.cinemataztic.com/v2/"),
                    "cinegame-en" => new Uri ("https://cinegame.en.api.player.eu-1.cinemataztic.com/v2/"),
                    "finnkino-fi" => new Uri ("https://finnkino.fi.api.player.eu-1.cinemataztic.com/v2/"),
                    "itv-in" => new Uri ("https://itv.in.api.player.asia-1.cinemataztic.com/v2/"),
                    "redyplay-de" => new Uri ("https://weischer.de.api.player.eu-2.cinemataztic.com/v2/"),
                    "wideeyemedia-ie" => new Uri ("https://wideeyemedia.ie.api.player.eu-2.cinemataztic.com/v2/"),
                    _ => new Uri ("https://api.staging.cinemataztic.com/v2/"),
                };
            }
        }

        static Uri CinematazticAuthUri {
            get {
                return CurrentMarketSlug switch {
                    "biospil-dk" => new Uri ("https://biospil.auth.iam.nordiskfilm.cinemataztic.com"),
                    "cinegame-en" => new Uri ("https://cinegame.en.auth.iam.eu-1.cinemataztic.com"),
                    "finnkino-fi" => new Uri ("https://finnkino.fi.auth.iam.eu-1.cinemataztic.com"),
                    "itv-in" => new Uri ("https://itv.in.auth.iam.asia-1.cinemataztic.com"),
                    "redyplay-de" => new Uri ("https://weischer.de.auth.iam.eu-2.cinemataztic.com"),
                    "wideeyemedia-ie" => new Uri ("https://wideeyemedia.ie.auth.iam.eu-2.cinemataztic.com"),
                    _ => new Uri ("https://auth.iam.staging.cinemataztic.com"),
                };
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
                var jsonReq = new JObject {
                    ["type"] = "user",
                    ["email"] = userName,
                    ["password"] = userPassword,
                };
                var request = new UnityWebRequest (
                                  CinematazticAuthUri,
                                  "POST",
                                  new DownloadHandlerBuffer (),
                                  new UploadHandlerRaw (System.Text.Encoding.UTF8.GetBytes (jsonReq.ToString ()))
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
                    var d = JObject.Parse (request.downloadHandler.text);
                    Configuration.CINEMATAZTIC_ACCESS_TOKEN = (string)d ["access_token"];
                    AccessTokenExpiry = DateTime.Now.AddHours (0.8);
                    /*
                    var exp = (long)d ["exp"];
                    AccessTokenExpiry = new DateTime (1970, 1, 1, 0, 0, 0).AddSeconds (exp);
                    if (Debug.isDebugBuild) {
                        Debug.Log ($"AccessToken expires {AccessTokenExpiry:s} {AccessToken}");
                    }
                    */

                    MarketIdsAvailable = (d ["markets"] as JArray).Select (m => (string)m).ToArray ();
                    MarketSlugsAvailable = MarketIdsAvailable.Select (id => CineGameSDK.MarketSlugMap.GetValueOrDefault (id, "???")).ToArray ();

                    var appNames = new List<string> (MarketIdsAvailable.Length);
                    foreach (var id in MarketIdsAvailable) {
                        appNames.Add (CineGameSDK.MarketDisplayNamesMap [id]);
                    }
                    AppNamesAvailable = appNames.ToArray ();

                    var roles = (d ["role"] as JArray).Select (s => (string)s);
                    IsSuperAdmin = roles.Contains ("super-admin");

                    //TODO there's a security issue here because login is across markets, but game-access list should be per market.
                    GameTypesAvailable = d.ContainsKey ("game-access") ? (d ["game-access"] as JArray).Select (s => (string)s).ToArray () : new string [0];

                    EditorPrefs.SetString ("CineGameUserName_" + CurrentMarketSlug, userName);

                    var bytes = System.Text.Encoding.UTF8.GetBytes (Password);
                    for (int i = 0; i < bytes.Length; i++) {
                        bytes [i] ^= 0x5a;
                    }
                    var cgspB64 = Convert.ToBase64String (bytes);
                    EditorPrefs.SetString ("CGSP", cgspB64);
                    EditorPrefs.SetString ("CGSP_" + CurrentMarketSlug, cgspB64);

                    CineGameBuild.GetGameTypeFromSceneOrProject ();
                    return true;
                } catch (Exception e) {
                    Debug.LogErrorFormat ("Exception while parsing JSON {0}: {1}", request.downloadHandler.text, e.ToString ());
                }
            }
            return false;
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
            StayLoggedIn = EditorPrefs.GetBool ("CineGameStayLoggedIn");

            if (!IsLoggedIn) {
                RefreshAccessToken ();
            }

            titleContent = new GUIContent("CineGame Login", CineGameBuild.IconTexture);

            EditorApplication.quitting -= OnEditorApplicationQuit;
            EditorApplication.quitting += OnEditorApplicationQuit;
        }

        private static void OnEditorApplicationQuit () {
            if (!EditorPrefs.GetBool ("CineGameStayLoggedIn", false)) {
                EditorPrefs.DeleteKey ("CGSP");
                for (var i = 0; i < MarketSlugs.Length; i++) {
                    EditorPrefs.DeleteKey ("CGSP_" + MarketSlugs [i]);
                }
            }
        }

        private static bool CGSP () {
            var regionUserKey = "CineGameUserName_" + CurrentMarketSlug;
            var regionCgspKey = "CGSP_" + CurrentMarketSlug;
            Username = EditorPrefs.HasKey (regionUserKey) ? EditorPrefs.GetString (regionUserKey) : EditorPrefs.GetString ("CineGameUserName");
            var cgspKey = EditorPrefs.HasKey (regionCgspKey) ? regionCgspKey : "CGSP";
            if (!string.IsNullOrWhiteSpace (Username) && EditorPrefs.HasKey (cgspKey)) {
                var bytes = Convert.FromBase64String (EditorPrefs.GetString (cgspKey));
                for (int i = 0; i < bytes.Length; i++) {
                    bytes [i] ^= 0x5a;
                }
                Password = System.Text.Encoding.UTF8.GetString (bytes);
                return true;
            }
            RemoveAccessToken ();
            return false;
        }
    }
}

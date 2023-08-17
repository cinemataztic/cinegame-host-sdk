using System;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Collections;
using System.Net.NetworkInformation;
using System.Net.Sockets;

using UnityEngine;
using UnityEngine.Networking;

using Sfs2X.Entities.Data;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CineGame.SDK {

    public class CineGameSDK : MonoBehaviour {
        private static CineGameSDK instance;

        public CineGameSettings Settings;
        [HideInInspector]
        [Obsolete ("GameType should be set in the asset referenced from Settings property")]
        public string GameType;


        public static string Market;

        private static string GameCode;

        private static int GetGameCodeTries = 0;
        private float refreshRate;
        private float avgFPS;
        private float minFPS;
        private int numAvgFpsWarnings = 3;

        public static bool GameEnded = false;
        public static bool GameEndSentToServer = false;
        public static JObject CreateResponse;
        private static string GameEndSerializedJson;

        /// <summary>
        /// Device Information
        /// </summary>
        private static string Hostname = null;
        private static string MacAddress = null;
        public static string DeviceId = null;
        private static string DeviceInfo = null;
        public static string UserEmail;
        public static string UserName;
        public static string UserId;

        /// <summary>
        /// User
        /// </summary>
        /// 
        public struct Player {
            public int BackendID;
            public Version AppVersion;
            public string Name;
            public int Age;
            public string Gender;
            public int Score;
        }

        public class PlayerObjectMessage {
            ISFSObject smartfoxObject;
            internal static PlayerObjectMessage FromSmartFoxObject (ISFSObject smartfoxObj) {
                return new PlayerObjectMessage {
                    smartfoxObject = smartfoxObj
                };
            }
            internal ISFSObject GetSmartFoxObject () {
                return smartfoxObject;
            }

            public PlayerObjectMessage () {
                smartfoxObject = new SFSObject ();
            }
            public bool ContainsKey (string key) {
                return smartfoxObject.ContainsKey (key);
            }
            public bool IsNull (string key) {
                return smartfoxObject.IsNull (key);
            }
            public string[] GetKeys()
            {
                return smartfoxObject.GetKeys();
            }
            public int GetInt (string key) {
                return smartfoxObject.GetInt (key);
            }
            public int [] GetIntArray (string key) {
                return smartfoxObject.GetIntArray (key);
            }
            public float GetFloat (string key) {
                return smartfoxObject.GetFloat (key);
            }
            public float [] GetFloatArray (string key) {
                return smartfoxObject.GetFloatArray (key);
            }
            public bool GetBool (string key) {
                return smartfoxObject.GetBool (key);
            }
            public bool [] GetBoolArray (string key) {
                return smartfoxObject.GetBoolArray (key);
            }
            public string GetString (string key) {
                return smartfoxObject.GetUtfString (key);
            }
            public string [] GetStringArray (string key) {
                return smartfoxObject.GetUtfStringArray (key);
            }
            public byte [] GetByteArray (string key) {
                return smartfoxObject.GetByteArray (key).Bytes;
            }
            public Color GetColor (string key) {
                var val = smartfoxObject.GetFloatArray (key);
                return (val.Length == 3) ? new Color (val [0], val [1], val [2]) : new Color (val [0], val [1], val [2], val [3]);
            }
            public string ToJson() {
                return smartfoxObject.ToJson();
            }

            public void PutInt (string key, int value) {
                smartfoxObject.PutInt (key, value);
            }
            public void PutIntArray (string key, int [] value) {
                smartfoxObject.PutIntArray (key, value);
            }
            public void PutFloat (string key, float value) {
                smartfoxObject.PutFloat (key, value);
            }
            public void PutFloatArray (string key, float [] value) {
                smartfoxObject.PutFloatArray (key, value);
            }
            public void PutBool (string key, bool value) {
                smartfoxObject.PutBool (key, value);
            }
            public void PutBoolArray (string key, bool [] value) {
                smartfoxObject.PutBoolArray (key, value);
            }
            public void PutString (string key, string value) {
                smartfoxObject.PutUtfString (key, value);
            }
            public void PutStringArray (string key, string [] value) {
                smartfoxObject.PutUtfStringArray (key, value);
            }
            public void PutByteArray (string key, byte [] value) {
                smartfoxObject.PutByteArray (key, new Sfs2X.Util.ByteArray (value));
            }
            public void PutColor (string key, Color value) {
                smartfoxObject.PutFloatArray (key, new float [] { value.r, value.g, value.b, value.a });
            }
            public void PutNull (string key) {
                smartfoxObject.PutNull (key);
            }
        }

        /// <summary>
        /// Structure for communicating mini game wins to server in EndGame request
        /// </summary>
        public struct MiniGame {
            public string MiniGameID;
            public List<long> WinnerIDs;
        }

        /// <summary>
		/// When system setup is completed, but game has not been created yet
		/// </summary>
        public static Action OnSetupCompleted;
        /// <summary>
		/// When game has been created and game code is available
		/// </summary>
        public static Action<Dictionary<string, object>> OnGameReady;
        /// <summary>
        /// When the game code is available
        /// </summary>
        public static Action<string> OnGameCodeLoaded;
        /// <summary>
		/// When a WiFi network is available. Name,Password
		/// </summary>
        public static Action<string, string> OnWiFiAvailable;
        /// <summary>
		/// When seats layout json is loaded
		/// </summary>
        public static Action<string> OnSeatsLoaded;
        /// <summary>
		/// When profanity regex for the current market is ready to use
		/// </summary>
        public static Action<Regex> OnProfanityRegexLoaded;

        /// <summary>
		/// When a player joins OR rejoins the game
		/// </summary>
        public static Action<Player> OnPlayerJoined;
        /// <summary>
		/// When a player leaves the game
		/// </summary>
        public static Action<int> OnPlayerLeft;
        /// <summary>
		/// When a player sends a string message to host
		/// </summary>
        public static Action<int, string> OnPlayerStringMessage;
        /// <summary>
		/// When a player sends a data object message to host
		/// </summary>
        public static Action<int, PlayerObjectMessage> OnPlayerObjectMessage;
        /// <summary>
		/// When a player sends a public chat message to host
		/// </summary>
        public static Action<int, string> OnPlayerChatMessage;
        /// <summary>
		/// When a supporter/spectator joins
		/// </summary>
        public static Action<int, int, string> OnSupporterJoined;
        /// <summary>
		/// When a player avatar 2D texture is loaded or changed (can happen multiple times)
		/// </summary>
        public static Action<int, Texture2D> OnPlayerAvatarChanged;

        /// <summary>
		/// When an error response is received from backend
		/// </summary>
        public static Action<int> OnError;

        delegate void BackendCallback (HttpStatusCode statusCode, string response);

        private static Dictionary<string, string> BackendHeaders = new Dictionary<string, string> (10) {
            {"Content-Type", "application/json; charset=utf-8"},
        };

        private static bool IsWebGL {
            get {
#if UNITY_WEBGL
                return true;
#else
                return false;
#endif
            }
        }

        private static bool IsStagingEnv {
            get { return Configuration.CLUSTER_NAME != null && Configuration.CLUSTER_NAME.Equals ("staging", StringComparison.InvariantCultureIgnoreCase); }
        }

        private static string ApiURL {
            get {
                return CineGameMarket.GetAPI();
            }
        }


        /// <summary>
		/// MonoBehavior Awake event
		/// </summary>
		void Awake () {
            if (instance != null) {
                Debug.LogWarning ("SDK already instanced. Ignoring this.", gameObject);
                return;
            }
            instance = this;
            SetDeviceInfo ();
        }

        /// <summary>
		/// MonoBehavior Start event
		/// </summary>
        IEnumerator Start () {
            if (instance != this)
                yield break;
            var t = Time.realtimeSinceStartup;
            while (Application.internetReachability == NetworkReachability.NotReachable) {
                var _t = Time.realtimeSinceStartup;
                //Log warning every second if internet is not reachable
                if (_t - t > 1f) {
                    t = _t;
                    Debug.LogWarning ("WARNING Internet not reachable-- waiting to set up game");
                }
                yield return null;
            }
            Setup ();
        }

        /// <summary>
		/// MonoBehavior Update event
		/// </summary>
        void Update () {
            if (instance != this)
                return;
            SmartfoxClient.Update ();
            var newAvgFPS = avgFPS * 0.99f + (1f / Time.unscaledDeltaTime) * 0.01f;
            if (refreshRate > 25f && newAvgFPS < 25f && avgFPS >= 25f && numAvgFpsWarnings-- > 0) {
                Debug.LogError ($"Average framerate dropped to {minFPS}");
            }
            avgFPS = newAvgFPS;
            minFPS = Mathf.Min (minFPS, avgFPS);
        }

        /// <summary>
		/// Collect Device/System Info, used for updating backend db of players
		/// </summary>
        void SetDeviceInfo () {
            Hostname = Environment.MachineName;

            DeviceInfo = string.Format ("{0}, {1}, {2} cores. {3} MB RAM. Graphics: {4} {5} ({6} {7}) Resolution: {8} OS: {9}",
               SystemInfo.deviceModel, SystemInfo.processorType, SystemInfo.processorCount,
               SystemInfo.systemMemorySize,
               SystemInfo.graphicsDeviceVendor, SystemInfo.graphicsDeviceName, SystemInfo.graphicsDeviceVendorID, SystemInfo.graphicsDeviceID,
               Screen.currentResolution,
               SystemInfo.operatingSystem
            );

            CineGameLogger.GameType = Settings.GameType;

            if (Screen.currentResolution.refreshRate < 25) {
                Debug.LogError ($"ERROR: Refresh rate too low: {Screen.currentResolution.refreshRate}");
            }
        }

        void Setup () {

            GetMacAddress ();

            if (!Application.isEditor) {
                Cursor.visible = false;
            }

            GameEnded = false;
            GameEndSentToServer = false;

            QualitySettings.vSyncCount = 1;
            refreshRate = minFPS = avgFPS = Screen.currentResolution.refreshRate;

            Market = Configuration.MARKET_ID;
            DeviceId = null;

            if (!IsWebGL && !Application.isEditor) {

    	          DeviceId = Configuration.CINEMATAZTIC_SCREEN_ID;
		           

                if (string.IsNullOrWhiteSpace (DeviceId) || string.IsNullOrWhiteSpace (Market)) {
                    var hostConfigFilename = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), "conf-db.json");
                    try {
                        // Read player/system info from ~/conf-db.json if it exists
                        var file = new FileInfo (hostConfigFilename);
                        if (file != null && file.Exists) {
                            var reader = file.OpenText ();
                            if (reader != null) {
                                var confDb = JObject.Parse (reader.ReadToEnd ());
#pragma warning disable 0618
                                Market = (string)confDb ["cloudtaztic"] ["config"] ["player"] ["market"];
                                DeviceId = (string)confDb ["cloudtaztic"] ["config"] ["_id"];
                                Debug.Log ($"DeviceId from ~/conf-db.json:cloudtaztic.config._id: {DeviceId}");
                                Debug.Log ($"Market from ~/conf-db.json:cloudtaztic.config.player.market: {Market} ({CineGameMarket.GetName()})");
#pragma warning restore
                            }
                        } else {
                            Debug.LogFormat ("File not found: {0} - using fallback config", hostConfigFilename);
                        }
                    } catch (Exception e) {
                        Debug.LogError ($"Exception while reading deviceId from {hostConfigFilename}-- falling back to systemInfo.deviceUniqueIdentifier. Exception: {e}");
                    }
                }

            }

            if (!IsWebGL) {

                var marketFromEnv = Configuration.MARKET_ID;
                if (!string.IsNullOrEmpty (marketFromEnv)) {
#pragma warning disable 0618
                    Market = marketFromEnv;
                    Debug.Log ($"Market from environment: {Market} ({CineGameMarket.GetName()})");
#pragma warning restore
                }

                //Get access token from environment. If this is run in editor, the CinemaBuild script should already have retrieved a fresh token at load, if username and password were already filled in.
                //If this is run as standalone, the desktop player software should already have set it.
                //You can since january 2020 no longer run standalone builds without the player software.
                var accessToken = Configuration.CINEMATAZTIC_ACCESS_TOKEN;

                if (string.IsNullOrEmpty (accessToken)) {
                    Debug.LogError ("Missing API key (jwt). Please check that the environment variable has been initialized by editor or player software!");
                    OnError?.Invoke (0);
                    Debug.Break ();
                    return;
                } else {
                    //Get gamecode from the backend. We are seeing network errors when trying this in first frame so we delay it a little
                    BackendHeaders ["Authorization"] = "Bearer " + accessToken;

                    var accessTokenParts = accessToken.Split ('.');
                    if (accessTokenParts.Length > 1) {
                        var payloadString = Encoding.UTF8.GetString (CineGameUtility.Base64UrlDecode (accessTokenParts [1]));
                        var payloadJson = JObject.Parse (payloadString);
                        JToken o;
                        if (payloadJson.TryGetValue ("email", out o)) {
                            UserEmail = (string)o;
                        }
                        if (payloadJson.TryGetValue ("_id", out o)) {
                            UserId = (string)o;
                        }
                        if (payloadJson.TryGetValue ("name", out o)) {
                            var nameObj = (JObject)o;
                            UserName = nameObj ["first"] + " " + nameObj ["last"];
                        }
                        Debug.Log ($"Logging in as user/device {UserName} ({UserEmail}) id={UserId}");
                    }
                }
            }

            if (string.IsNullOrEmpty (DeviceId)) {
                var did = SystemInfo.deviceUniqueIdentifier;
                if (did == SystemInfo.unsupportedIdentifier) {
                    if (PlayerPrefs.HasKey ("deviceUniqueIdentifier")) {
                        did = PlayerPrefs.GetString ("deviceUniqueIdentifier");
                    } else {
                        did = Guid.NewGuid ().ToString ();
                        PlayerPrefs.SetString ("deviceUniqueIdentifier", did);
                    }
                }
                DeviceId =
                    did;
                Debug.Log ($"DeviceId from deviceUniqueIdentifier: {did}");
            }

            Invoke ("RequestGameCode", .1f);

            OnSetupCompleted?.Invoke ();
        }

        /// <summary>
		/// Determine MAC address of inet interface
		/// </summary>
        internal static void GetMacAddress () {
            if (IsWebGL) {
                Debug.Log ("MAC address not available on WebGL builds.");
                return;
            }

            try {
                var hostname = new Uri (ApiURL).Host;
                string localAddr;
                using (var u = new UdpClient (hostname, 1)) {
                    localAddr = ((IPEndPoint)u.Client.LocalEndPoint).Address.ToString ();
                }
                Debug.LogFormat ("UDPClient address: {0} - determining network adapter ...", localAddr);

                foreach (var net in NetworkInterface.GetAllNetworkInterfaces ()) {
                    var macAddr = net.GetPhysicalAddress ().ToString ();
                    //Debug.LogFormat ("Network adapter: {0} mac={1} type={2}", net.Name, macAddr, net.NetworkInterfaceType.ToString ());
                    if (net.NetworkInterfaceType != NetworkInterfaceType.Loopback) {
                        foreach (var addrInfo in net.GetIPProperties ().UnicastAddresses) {
                            var niAddr = addrInfo.Address.ToString ();
                            if (localAddr == niAddr) {
                                Debug.LogFormat ("Network adapter found: {0} mac={1} type={2}", net.Name, macAddr, net.NetworkInterfaceType.ToString ());
                                MacAddress = macAddr;
                                return;
                            }
                        }
                    }
                }
                Debug.LogError ("No MAC address found for this host");
            } catch (Exception e) {
                Debug.LogErrorFormat ("Exception while trying to determine adapter MAC Address: {0}", e);
                MacAddress = null;
            }
        }


        static void API (string uri, string json, BackendCallback callback = null) {
            if (Debug.isDebugBuild) {
                Debug.LogFormat ("POST {0} {1}", uri, json);
            }
            var request = new UnityWebRequest (
                ApiURL + uri,
                "POST",
                new DownloadHandlerBuffer (),
                new UploadHandlerRaw (Encoding.UTF8.GetBytes (json))
            );
            var enHeaders = BackendHeaders.GetEnumerator ();
            while (enHeaders.MoveNext ()) {
                request.SetRequestHeader (enHeaders.Current.Key, enHeaders.Current.Value);
            }
            instance.StartCoroutine (instance.E_Backend (request, callback));
        }

        private IEnumerator E_Backend (UnityWebRequest request, BackendCallback callback) {
            var timeBegin = Time.realtimeSinceStartup;
            yield return request.SendWebRequest ();
            var statusCode = (HttpStatusCode)request.responseCode;
            callback?.Invoke (statusCode, request.downloadHandler.text);
        }

        static void PostFile (string uri, string filename, byte [] file, BackendCallback callback = null) {
            var wwwForm = new WWWForm ();
            wwwForm.AddBinaryData ("file", file, filename);
            var request = UnityWebRequest.Post (ApiURL + uri, wwwForm);
            var enHeaders = BackendHeaders.GetEnumerator ();
            while (enHeaders.MoveNext ()) {
                request.SetRequestHeader (enHeaders.Current.Key, enHeaders.Current.Value);
            }
            instance.StartCoroutine (instance.E_Backend (request, callback));
        }

        internal static void RequestGameCodeStatic () {
            instance.RequestGameCode ();
        }

		internal class CreateGameRequest {
			public string hostName;
			public string gameType;
			public string mac;
			public string deviceId;
			public string platform;
			public string showId;
			public string blockId;
			public string deviceInfo;
		}

		internal void RequestGameCode () {
            Debug.Log ("Environment: " + (IsStagingEnv ? "staging" : "production"));

            var req = new CreateGameRequest {
                hostName = Hostname,
                gameType = Settings.GameType,
                mac = MacAddress,
                deviceId = DeviceId,
                platform = Application.platform.ToString (),
                showId = Configuration.CINEMATAZTIC_SHOW_ID,
                blockId = Configuration.CINEMATAZTIC_BLOCK_ID,
                deviceInfo = DeviceInfo,
            };

            API (IsWebGL ? "game/create/webgl" : "game/create", JsonConvert.SerializeObject (req), (statusCode, response) => {
                if (statusCode == HttpStatusCode.OK) {
                    CreateResponse = JObject.Parse (response);
                    //Debug.LogFormat ("API CreateGame response: {0}", response);

                    ParseConfig (CreateResponse);

                    GameCode = (string)CreateResponse ["gameCode"];
                    var gameZone = (string)CreateResponse ["gameZone"];
                    var gameServer = (string)CreateResponse ["gameServer"];
                    var webGlSecure = (bool)(CreateResponse ["webGlSecure"] ?? false);
                    SmartfoxClient.ConnectAndCreateGame (gameServer, GameCode, gameZone, GetGameType (), webGlSecure);

                    var creditsForParticipating = 0;
                    var creditsForSupporterParticipating = 0;
                    var creditsForWinning = 0;
                    var creditsForSupporterWinning = 0;
                    if (CreateResponse.ContainsKey ("creditsForParticipating")) {
                        creditsForParticipating = (int)CreateResponse ["creditsForParticipating"];
                        creditsForSupporterParticipating = (int)CreateResponse ["creditsForSupporterParticipating"];
                        creditsForWinning = (int)CreateResponse ["creditsForWinning"];
                        creditsForSupporterWinning = (int)CreateResponse ["creditsForSupporterWinning"];
                    }

                    if (CreateResponse.ContainsKey ("maxSupportersPerPlayer")) {
                        SmartfoxClient.MaxSpectators = (int)(long)CreateResponse ["maxSupportersPerPlayer"] * SmartfoxClient.MaxPlayers;
                    }

                    OnGameReady?.Invoke (new Dictionary<string, object> {
                        { "GameCode", GameCode },
                        { "CreditsForParticipating", creditsForParticipating },
                        { "CreditsForSupporterParticipating", creditsForSupporterParticipating },
                        { "CreditsForWinning", creditsForWinning },
                        { "CreditsForSupporterWinning", creditsForSupporterWinning },
                    });

                    OnGameCodeLoaded?.Invoke(GameCode);

                } else if (statusCode == HttpStatusCode.Unauthorized) {
                    OnError?.Invoke (401);
                } else if ((int)statusCode > 500) {
                    if (++GetGameCodeTries > 2) {
                        Debug.LogErrorFormat ("Backend responded with error while creating game - retrying in one second: {0} {1}", statusCode, response);
                        OnError?.Invoke ((int)statusCode);
                    } else {
                        Debug.LogWarningFormat ("WARNING Backend responded with error while creating game - retrying in one second: {0} {1}", statusCode, response);
                    }
                    Invoke ("RequestGameCode", 1f);
                } else {
                    Debug.LogError ($"Backend responded with error while creating game - retrying in one second: {statusCode} {response}");
                    OnError?.Invoke ((int)statusCode);
                }
            });
        }


        ///<summary>
        /// Set avatar to use for player with specific backendId.
		/// avatarID can be an internal avatar ID from options, or it can be an absolute url (typically a custom uploaded avatar or from Google Play)
        /// </summary>
        internal static void SetPlayerAvatar (int backendID, string avatarID) {
            if (Uri.TryCreate (avatarID, UriKind.Absolute, out Uri uri)) {
                //We only allow avatars hosted on cinemataztic.com or googleusercontent.com
                if (!uri.Host.EndsWith (".cinemataztic.com") && !uri.Host.EndsWith (".googleusercontent.com")) {
                    Debug.LogError ($"Avatar from non-whitelisted domain {uri.Host}");
                    return;
                }
            } else if (CreateResponse.TryGetValue ("avatarOptions", out JToken o)) {
                var avatarOptions = (JArray)o;
                foreach (JObject avatarOption in avatarOptions) {
                    if ((string)avatarOption ["title"] == avatarID) {
                        uri = new Uri ((string)avatarOption ["imageUrl"]);
                        break;
                    }
                }
                if (uri == null) {
                    Debug.LogError ($"GameType {GetGameType ()} does not support avatarID={avatarID}");
                    return;
                }
            }

            instance.StartCoroutine (instance.LoadAvatarPicture (backendID, uri.ToString ()));
        }


        IEnumerator LoadAvatarPicture (int backendID, string sUrl) {
            var startTime = Time.realtimeSinceStartup;
            Texture2D texture = null;
            var nRetryTimes = 3;
            for (; ; )
            {
                using (var request = UnityWebRequestTexture.GetTexture (sUrl)) {
                    request.SetRequestHeader ("User-Agent", "Mozilla");
                    var timeBegin = Time.realtimeSinceStartup;
                    yield return request.SendWebRequest ();
                    if (request.result == UnityWebRequest.Result.Success) {
                        texture = DownloadHandlerTexture.GetContent (request);
                        if (texture != null) {
                            var texMipMap = new Texture2D (texture.width, texture.height, texture.format, true);
                            texMipMap.SetPixels (texture.GetPixels ());
                            texMipMap.Apply (true);
                            texture = texMipMap;
                            break;
                        }
                    }
                    if (request.result == UnityWebRequest.Result.ConnectionError && --nRetryTimes != 0) {
                        Debug.LogWarning ($"{request.error} while downloading profile picture, retrying: {request.downloadHandler?.text} {sUrl}");
                        //Wait a little, then retry
                        yield return new WaitForSeconds (.5f);
                        continue;
                    }
                    //Other errors, or giving up after n retries
                    Debug.LogError ($"{request.error} while downloading profile picture, giving up: {request.downloadHandler?.text} {sUrl}");
                    yield break;
                }
            }

            var timeElapsed = Time.realtimeSinceStartup - startTime;
            Debug.Log ($"BackendID={backendID} avatar texture ({texture.width}, {texture.height}) downloaded in {timeElapsed:##.00}s: {sUrl}");

            OnPlayerAvatarChanged?.Invoke (backendID, texture);
        }


        private void ParseConfig (JObject d) {
            string wifiName = null, wifiPassword = null;
            long v;
            string s;
            var en = d.GetEnumerator ();
            while (en.MoveNext ()) {
                if (en.Current.Value == null) {
                    continue;
                }
                switch (en.Current.Key) {

                case "wifiCode":
                    s = (string)en.Current.Value;

                    wifiPassword = s;

                    Debug.LogFormat ("Wifi password set to {0}", s);
                    break;

                case "wifiName":
                    s = (string)en.Current.Value;
                    wifiName = s;
                    break;

                case "lagWarningThreshold":
                    v = (long)en.Current.Value;
                    SmartfoxClient.LagWarningThreshold = (int)v;
                    Debug.LogFormat ("LagWarningThreshold set to {0} ms.", v);
                    break;

                case "lagMonitorInterval":
                    v = (long)en.Current.Value;
                    SmartfoxClient.LagMonitorInterval = (int)v;
                    Debug.LogFormat ("LagMonitorInterval set to {0} seconds.", v);
                    break;

                case "lagSamplesPerInterval":
                    v = (long)en.Current.Value;
                    SmartfoxClient.LagSamplesPerInterval = (int)v;
                    Debug.LogFormat ("LagSamplesPerInterval set to {0}.", v);
                    break;

                case "_g":
                    s = (string)en.Current.Value;
                    CineGameChatController.GiphyApiKey = s;
                    Debug.Log ("Giphy API Key set");
                    break;

                case "_t":
                    s = (string)en.Current.Value;
                    CineGameChatController.TenorApiKey = s;
                    Debug.Log ("Tenor API Key set");
                    break;

                case "chatGifSize":
                    v = (long)en.Current.Value;
                    CineGameChatController.GifSize = (int)v;
                    Debug.LogFormat ("Chat GIF size configured to {0} pixels.", v);
                    break;

                case "chatGifDuration":
                    v = (long)en.Current.Value;
                    CineGameChatController.GifDefaultDuration = (int)v;
                    Debug.LogFormat ("Chat GIF default duration configured to {0} seconds.", v);
                    break;

                default:
                    break;
                }
            }

            if (OnWiFiAvailable != null) {
                OnWiFiAvailable?.Invoke (wifiName, wifiPassword);
            }
        }

        public static void EndGame (List<Player> users, List<Player> winners = null, List<MiniGame> miniGames = null) {
            instance.SendDataToServer (users, winners, miniGames);
        }

        private void SendDataToServer (List<Player> users, List<Player> winners = null, List<MiniGame> miniGames = null) {
            //Reset average FPS. This will log an error if the average FPS has been low without recovering
            avgFPS = refreshRate;

            GameEnded = true;
            SmartfoxClient.GameOver ();

            var d = new Dictionary<string, object> {
                ["gameCode"] = GameCode
            };
            if (users != null && users.Count > 0) {
                var l = new List<object> ();
                foreach (var u in users) {
                    var dp = new Dictionary<string, object> {
                        { "userId", u.BackendID },
                        { "points", u.Score }
                    };
                    l.Add (dp);
                }
                d.Add ("userPoints", l);
            }
            if (winners != null && winners.Count > 0) {
                var l = new List<object> ();
                foreach (var w in winners) {
                    l.Add (w.BackendID);
                }
                d.Add ("winners", l);
            }
            if (miniGames != null && miniGames.Count > 0) {
                var l = new List<object> ();
                foreach (var usw in miniGames) {
                    var dp = new Dictionary<string, object> {
                        { "userIds", usw.WinnerIDs },
                        { "gameID", usw.MiniGameID }
                    };
                    l.Add (dp);
                }
                d.Add ("userSubgameWins", l);
            }

            GameEndSerializedJson = JsonConvert.SerializeObject (d);
            SendGameEndToServer ();
        }

        private void SendGameEndToServer () {
            API (IsWebGL ? "game/end/webgl" : "game/end", GameEndSerializedJson, delegate (HttpStatusCode statusCode, string response) {
                if (statusCode == HttpStatusCode.OK) {
                    GameEndSentToServer = true;
                    Debug.Log ("API response end game: " + statusCode + " - " + response);
                } else {
                    Debug.LogWarningFormat ("Error while sending 'game end' message to server - retry in 1 second: {0} {1}", statusCode, response);
                    Invoke (nameof (SendGameEndToServer), 1f);
                }
            });
        }

        internal static string GetGameType () {
            if (string.IsNullOrEmpty (instance.Settings.GameType)) {
                Debug.LogError ("<b>FATAL ERROR</b> GetGameType() called before property initialized!");
            }
            return instance.Settings.GameType;
        }

        /// <summary>
		/// Stop further joins by either players or supporters
		/// </summary>
        public static void StopGameRoomJoins () {
            SmartfoxClient.StopGameRoomJoins ();
        }

        /// <summary>
		/// Change game capacity. Note that the server determines max values for these and they cannot be exceeded
		/// </summary>
        public static void UpdateGameCapacity (int maxPlayers, int maxSupportersPerPlayer) {
            SmartfoxClient.UpdateRoomCapacity (maxPlayers, maxPlayers * maxSupportersPerPlayer);
        }

        /// <summary>
		/// Broadcast public object message to either all players, all supporters or all users
		/// </summary>
        public static void BroadcastObjectMessage (PlayerObjectMessage dataObj, bool toPlayers = true, bool toSpectators = false) {
            SmartfoxClient.BroadcastObjectMessage (dataObj.GetSmartFoxObject (), toPlayers: toPlayers, toSpectators: toSpectators);
            CineGameBots.BroadcastObjectMessage (dataObj, toPlayers: toPlayers, toSpectators: toSpectators);
        }

        /// <summary>
		/// Send private object message to specific CineGame user
		/// </summary>
        public static void SendObjectMessage (PlayerObjectMessage dataObj, int backendId) {
            if (backendId >= 0) {
                SmartfoxClient.SendObjectMessage (dataObj.GetSmartFoxObject (), backendId);
            } else {
                CineGameBots.SendObjectMessage (dataObj, backendId);
            }
        }

        /// <summary>
		/// Send private string message to specific CineGame user
		/// </summary>
        public static void SendPrivateMessage (string msg, int backendId) {
            if (backendId >= 0) {
                SmartfoxClient.SendPrivateMessage (msg, backendId);
            } else {
                CineGameBots.SendStringMessage (msg, backendId);
            }
        }

        /// <summary>
		/// Kick player out of game
		/// </summary>
        public static void KickPlayer (int backendId) {
            SmartfoxClient.KickUser (backendId);
        }

        /// <summary>
		/// Kick supporter/spectator out of game
		/// </summary>
        public static void KickSupporter (int backendId) {
            SmartfoxClient.KickUser (backendId);
        }

        internal static string GetRegionProfanityUrl () {
#pragma warning disable 0618
            var marketId = Market;
#pragma warning restore
            if (string.IsNullOrWhiteSpace (marketId)) {
                marketId = instance.Settings?.MarketId;
            }
            return string.Format ("https://profanity.cinemataztic.com/{0}/txt-file", marketId);
        }

        void OnApplicationQuit () {
            //Log error if communication with server was incomplete
            if (GameEnded && (!GameEndSentToServer)) {
                Debug.LogError (">>> ERROR! Server communication incomplete. Winners may not have received their prices. <<<");
            }
            SmartfoxClient.Disconnect ();
            PlayerPrefs.DeleteAll ();
            CineGameLogger.OnApplicationQuit ();
        }

    }
}
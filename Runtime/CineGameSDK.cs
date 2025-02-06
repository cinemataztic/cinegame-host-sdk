using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

using UnityEngine;
using UnityEngine.Networking;

using Sfs2X.Entities.Data;
using Sfs2X.Entities.Variables;
using Smartfox;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CineGame.SDK {

    public class CineGameSDK : MonoBehaviour {
        private static CineGameSDK instance;

        public CineGameSettings Settings;

        public static string GameID;
        public static string Market;
        public static string CineGameEnvironment;

        private static string GameCode;
        private static float BlockStartTime;

        private static int GetGameCodeTries = 0;

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
        private static string LocalIP;
        public static string UserEmail;
        public static string UserName;
        public static string UserId;


        /// <summary>
        /// Game Request
        /// </summary>
        internal class CreateGameRequest
        {
            public string hostName;
            public string gameType;
            public string mac;
            public string localIp;
            public bool localGameServerRunning;
            public string deviceId;
            public string platform;
            public string showId;
            public string blockId;
            public string deviceInfo;
        }

        /// <summary>
        /// User
        /// </summary>
        public struct User {
            public int BackendID;
            public Version AppVersion;
            public string Name;
            public int Age;
            public string Gender;
            public int Score;
        }

        /// <summary>
        /// Supporter
        /// </summary>
        public struct Supporter
        {
            public int BackendID;
            public int SupportingID;
            public string Name;
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

            /// <summary>
            /// Create object message from JSON string
            /// </summary>
            public PlayerObjectMessage (string json) {
                smartfoxObject = SFSObject.NewFromJsonData (json);
            }

            public bool ContainsKey (string key) {
                return smartfoxObject.ContainsKey (key);
            }
            public bool IsNull (string key) {
                return smartfoxObject.IsNull (key);
            }
            public string[] GetKeys() {
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
            public void PutObject (string key, PlayerObjectMessage value) {
                smartfoxObject.PutSFSObject (key, value.smartfoxObject);
            }
            public void PutObjectArray (string key, PlayerObjectMessage [] objects) {
                var arr = new SFSArray ();
                for (int i = 0; i < objects.Length; i++) {
                    arr.AddSFSObject (objects [i].GetSmartFoxObject ());
                }
                smartfoxObject.PutSFSArray (key, arr);
            }

            /// <summary>
            /// Creates a new object property, returns the newly created object
            /// </summary>
            public PlayerObjectMessage AddObject (string key) {
                var obj = new PlayerObjectMessage ();
                smartfoxObject.PutSFSObject (key, obj.smartfoxObject);
                return obj;
            }

            public void Remove (string key) {
                smartfoxObject.RemoveElement (key);
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
        /// If a WiFi network is available and a QR code to join it was downloaded, this will be invoked with the QR texture
        /// </summary>
        public static Action<Texture2D> OnWiFiQR;
        /// <summary>
        /// When the block duration is updated dynamically (always invoked at start with initial duration)
        /// </summary>
        public static Action<float> OnBlockDurationUpdated;
        /// <summary>
		/// When seats layout is loaded
		/// </summary>
        public static Action<Dictionary<string, CineGameSeatController.Seat[]>> OnSeatsLoaded;
        /// <summary>
        /// When a seat gets taken by user
        /// </summary>
        public static Action<int, CineGameSeatController.Seat> OnSeatTaken;
        /// <summary>
		/// When profanity regex for the current market is ready to use
		/// </summary>
        public static Action<Regex> OnProfanityRegexLoaded;

        /// <summary>
		/// When a player joins OR rejoins the game
		/// </summary>
        public static Action<User> OnPlayerJoined;
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
        public static Action<Supporter> OnSupporterJoined;
        /// <summary>
		/// When a player avatar 2D texture is loaded or changed (can happen multiple times)
		/// </summary>
        public static Action<int, Texture2D> OnPlayerAvatarChanged;

        /// <summary>
		/// When an error response is received from backend
		/// </summary>
        public static Action<int> OnError;

        static int MaxPlayers = 75;
        static int MaxSpectators = 75 * 5;
        static int numBkIdWarnings = 1;

        delegate void BackendCallback (HttpStatusCode statusCode, string response);

        private static readonly Dictionary<string, string> BackendHeaders = new (10) {
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


        /// <summary>
		/// MonoBehavior Awake event
		/// </summary>
		void Awake () {
            if (instance != null) {
                Debug.LogWarning ("SDK already instanced. Ignoring this.", gameObject);
                return;
            }
            instance = this;
            var sfc = gameObject.AddComponent<SmartfoxClient> ();
            sfc.InitEvents ();

            var clusterName = Configuration.CLUSTER_NAME;
            CineGameEnvironment = (clusterName != "dev" && clusterName != "staging") ? "production" : clusterName;

#if UNITY_EDITOR
            if (Settings != null)
            {
                Market = Settings.MarketId;
            }
            else
            {
                Market = UnityEditor.EditorPrefs.GetString("CineGameMarket");
            }
#else
            Market = Configuration.MARKET_ID;
#endif

            try
            {
                // Read BLOCK_START_TICKS from parent process. This will be in JavaScript ticks, ie miliseconds since Jan 1 1970.
                // .NET ticks are in 1e-7 seconds since Jan 1 0001, so we need to convert it by scaling and offsetting.
                var blockStartTicksJS = Configuration.BLOCK_START_TICKS;
                var offsetFromJSEpochToDotNetEpoch = 62135596800000L;
                var blockStartTicksDotNet = (blockStartTicksJS + offsetFromJSEpochToDotNetEpoch) * 10000;
                var nowTicks = DateTime.Now.Ticks;
                BlockStartTime = (nowTicks - blockStartTicksDotNet) * 1e-7f;
                if (BlockStartTime < 0) {
                    Debug.LogError ($"ERROR: The computer's clock is set in the past ({DateTime.Now}). The lobby timing will be off by a few seconds (BlockStartTime forced to 0s)");
                    BlockStartTime = 0f;
                }
                Debug.Log ($"BLOCK_START_TICKS={blockStartTicksJS} --- BlockStartTime={BlockStartTime:0.##}s");
            } catch (Exception) {
                Debug.Log ("BLOCK_START_TICKS not defined, using time t0 as starting point");
            }
        }

        void Setup () {

            DeviceId = null;
            GameEnded = false;
            GameEndSentToServer = false;

            QualitySettings.vSyncCount = 1;

            if (Settings != null)
            {
                GameID = Settings.GameID;
            }

            if (!IsWebGL && !Application.isEditor)
            {
                DeviceId = Configuration.CINEMATAZTIC_SCREEN_ID;
            }

            SetDeviceInfo();
            GetNetworkInfo();

            if (!Application.isEditor) {
                Cursor.visible = false;
            }

            if (!IsWebGL) {
                //Get access token from environment. If this is run in editor, the CinemaBuild script should already have retrieved a fresh token at load, if username and password were already filled in.
                //If this is run as standalone, the desktop player software should already have set it.
                //You can since january 2020 no longer run standalone builds without the player software.
                string accessToken = Configuration.CINEMATAZTIC_ACCESS_TOKEN;

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
                        if (payloadJson.TryGetValue ("email", out JToken o)) {
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

            RequestGameCode ();

            OnSetupCompleted?.Invoke ();
        }


        /// <summary>
        /// Collect Device/System Info, used for updating backend db of players
        /// </summary>
        void SetDeviceInfo()
        {
            Hostname = Environment.MachineName;

            DeviceInfo = string.Format("{0}, {1}, {2} cores. {3} MB RAM. Graphics: {4} {5} ({6} {7}) Resolution: {8} OS: {9}",
               SystemInfo.deviceModel, SystemInfo.processorType, SystemInfo.processorCount,
               SystemInfo.systemMemorySize,
               SystemInfo.graphicsDeviceVendor, SystemInfo.graphicsDeviceName, SystemInfo.graphicsDeviceVendorID, SystemInfo.graphicsDeviceID,
               Screen.currentResolution,
               SystemInfo.operatingSystem
            );

            CineGameLogger.GameID = GameID;
#if UNITY_2022_2_OR_NEWER
            var currentRate = Screen.currentResolution.refreshRateRatio.value;
#else
            var currentRate = Screen.currentResolution.refreshRate;
#endif
            if (currentRate < 25)
            {
                Debug.LogError($"ERROR: Refresh rate too low: {currentRate}");
            }
        }

        /// <summary>
		/// Determine Local IP and MAC address of inet interface
		/// </summary>
        internal static void GetNetworkInfo () {
            if (IsWebGL) {
                Debug.Log ("NetworkInfo not available on WebGL builds.");
                return;
            }

            try {
                var hostname = new Uri (CineGameMarket.GetAPI()).Host;
                using (var u = new UdpClient (hostname, 1)) {
                    LocalIP = ((IPEndPoint)u.Client.LocalEndPoint).Address.ToString ();
                }

                foreach (var net in NetworkInterface.GetAllNetworkInterfaces ()) {
                    var macAddr = net.GetPhysicalAddress ().ToString ();
                    //Debug.LogFormat ("Network adapter: {0} mac={1} type={2}", net.Name, macAddr, net.NetworkInterfaceType.ToString ());
                    if (net.NetworkInterfaceType != NetworkInterfaceType.Loopback) {
                        foreach (var addrInfo in net.GetIPProperties ().UnicastAddresses) {
                            var niAddr = addrInfo.Address.ToString ();
                            if (LocalIP == niAddr) {
                                Debug.Log ($"Network adapter found: {net.Name} localIp={LocalIP} mac={macAddr} type={net.NetworkInterfaceType}");
                                MacAddress = macAddr;
                                return;
                            }
                        }
                    }
                }
                Debug.LogError ($"No MAC address found for this host. LocalIP={LocalIP}");
            } catch (Exception e) {
                Debug.LogError ("Exception while trying to determine adapter MAC Address: " + e);
                MacAddress = null;
            }
        }

        /// <summary>
        /// MonoBehavior Start event
        /// </summary>
        IEnumerator Start()
        {
            if (instance != this)
                yield break;
            var t = Time.realtimeSinceStartup;
            while (Application.internetReachability == NetworkReachability.NotReachable)
            {
                var _t = Time.realtimeSinceStartup;
                //Log warning every second if internet is not reachable
                if (_t - t > 1f)
                {
                    t = _t;
                    Debug.LogWarning ("WARNING Internet not reachable-- waiting to set up game");
                }
                yield return null;
            }
            Setup ();
            CineGameDCHP.Start ();
        }

        /// <summary>
        /// MonoBehavior Update event
        /// </summary>
        void Update()
        {
            if (instance != this)
                return;

            if(!Application.isEditor) {
                CineGameDCHP.Update();
            }

            /*var newAvgFPS = avgFPS * 0.99f + (1f / Time.unscaledDeltaTime) * 0.01f;
            if (refreshRate > 25f && newAvgFPS < 25f && avgFPS >= 25f && numAvgFpsWarnings-- > 0) {
                Debug.LogError ($"Average framerate dropped to {minFPS}");
            }
            avgFPS = newAvgFPS;
            minFPS = Mathf.Min (minFPS, avgFPS);*/

            //When user presses Shift+C, the game crashes! Testing the system's robustness and error logging, sentry events etc
            if (Input.GetKeyDown (KeyCode.C) && Input.GetKey (KeyCode.LeftShift)) {
                SegmentationFault ();
            }
        }

        internal static void RequestGameCodeStatic () {
            instance.RequestGameCode ();
        }

		void RequestGameCode () {

            Debug.Log("Game: " + GameID);
            Debug.Log("Market: " + Market);
            Debug.Log("Environment: " + CineGameEnvironment);
            Debug.Log("Player Capacity: " + MaxPlayers);

            var _localGameServer = IsGameServerRunningLocally ();
            if (_localGameServer) {
                Debug.Log ("Local gameserver IP: " + LocalIP);
            }

            var req = new CreateGameRequest {
                hostName = Hostname,
                gameType = GameID,
                mac = MacAddress,
                localGameServerRunning = _localGameServer,
                localIp = LocalIP,
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
                        MaxSpectators = (int)(long)CreateResponse ["maxSupportersPerPlayer"] * MaxPlayers;
                    }

                    //var webGlSecure = (bool)(CreateResponse ["webGlSecure"] ?? false);
                    SmartfoxClient.Connect (gameServer, gameZone, (success) => {
                        if (success) {
                            SmartfoxClient.Login ("Host" + GameCode, (error) => {
                                if (string.IsNullOrEmpty (error)) {
                                    SmartfoxClient.CreateAndJoinRoom (GameCode, MaxPlayers, MaxSpectators, new List<RoomVariable> {
                                        new SFSRoomVariable ("HostId", SmartfoxClient.MySfsUser.Id),
                                        new SFSRoomVariable ("GameType", GameID),
                                    }, (room, alreadyExists) => {
                                        if (room != null) {
                                            var sfc = SmartfoxClient.Instance;
                                            //sfc.OnUserLeftRoom += _OnUserLeft;
                                            sfc.OnObjectMessage.AddListener (HandleObjectMessage);
                                            sfc.OnPrivateMessage.AddListener (HandlePrivateMessage);

                                            //Invoke event with details about the created game
                                            OnGameReady?.Invoke (new Dictionary<string, object> {
                                                { "GameCode", GameCode },
                                                { "CreditsForParticipating", creditsForParticipating },
                                                { "CreditsForSupporterParticipating", creditsForSupporterParticipating },
                                                { "CreditsForWinning", creditsForWinning },
                                                { "CreditsForSupporterWinning", creditsForSupporterWinning },
                                            });

                                            //Invoke event with just the gamecode (eg for displaying the code in a Text component)
                                            OnGameCodeLoaded?.Invoke (GameCode);
                                        } else {
                                            Debug.LogError ("CineGameSDK: Room not created, alreadyExists=" + alreadyExists);
                                            OnError?.Invoke (409);
                                        }
                                    });
                                } else {
                                    Debug.LogError ("Connected but unable to log in to realtime game server");
                                    OnError?.Invoke (403);
                                }
                            });
                        } else {
                            Debug.LogError ("Unable to connect to realtime game server");
                            OnError?.Invoke (0);
                        }
                    });
                } else if (statusCode == HttpStatusCode.Unauthorized) {
                    OnError?.Invoke (401);
                } else if ((int)statusCode > 500) {
                    if (++GetGameCodeTries > 2) {
                        Debug.LogErrorFormat ("Backend responded with error while creating game - retrying in one second: {0} {1}", statusCode, response);
                        OnError?.Invoke ((int)statusCode);
                    } else {
                        Debug.LogWarningFormat ("WARNING Backend responded with error while creating game - retrying in one second: {0} {1}", statusCode, response);
                    }
                    Invoke (nameof (RequestGameCode), 1f);
                } else {
                    Debug.LogError ($"Backend responded with error while creating game - retrying in one second: {statusCode} {response}");
                    OnError?.Invoke ((int)statusCode);
                }
            });
        }


        public static void RequestSeat(bool state) {
            CineGameSeatController.Activate (state);
        }


        ///<summary>
        /// Set avatar to use for player with specific backendId.
		/// avatarID can be an internal avatar ID from options, or it can be an absolute url (typically a custom uploaded avatar or from Google Play)
        /// </summary>
        internal static void SetPlayerAvatar (int backendID, string avatarID) {
            if (Uri.TryCreate (avatarID, UriKind.Absolute, out Uri uri)) {
                //We only allow avatars hosted on cinemataztic.com or googleusercontent.com
                if (!uri.Host.EndsWith (".cinemataztic.com") && !uri.Host.EndsWith (".googleusercontent.com")) {
                    Debug.LogWarning($"Avatar from non-whitelisted domain {uri.Host}");
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
                    Debug.LogWarning ($"GameID {GameID} does not support avatarID={avatarID}");
                    return;
                }
            }

            var startTime = Time.realtimeSinceStartup;
            instance.StartCoroutine (instance.E_DownloadPicture (uri.ToString (), (texture) => {
                if (texture != null) {
                    var timeElapsed = Time.realtimeSinceStartup - startTime;
                    Debug.Log ($"BackendID={backendID} avatar texture {texture.width}x{texture.height} downloaded in {timeElapsed:##.00}s: {uri}");
                    OnPlayerAvatarChanged?.Invoke (backendID, texture);
                }
            }));
        }


        /// <summary>
        /// Generic coroutine to download a picture from a URL and invoke a callback with the resulting Texture2D. Optionally, the picture can be cached
        /// </summary>
        IEnumerator E_DownloadPicture (string sUrl, Action<Texture2D> callback, bool cache = false) {
            Texture2D texture = null;
            var nRetryTimes = 3;
            for (; ; ) {
                using var request = GetTextureFromCacheOrUrl (sUrl);
                request.SetRequestHeader ("User-Agent", "Mozilla");
                var timeBegin = Time.realtimeSinceStartup;
                yield return request.SendWebRequest ();
                if (request.result == UnityWebRequest.Result.Success) {
                    if (cache) {
                        StoreTextureInCache (request);
                    }
                    texture = DownloadHandlerTexture.GetContent (request);
                    if (texture != null) {
                        if (SystemInfo.copyTextureSupport != UnityEngine.Rendering.CopyTextureSupport.None) {
                            var texMipMap = new Texture2D (texture.width, texture.height, texture.format, true);
                            Graphics.CopyTexture (texture, 0, 0, texMipMap, 0, 0);
                            texMipMap.Apply (true, true);
                        } else if (texture.isReadable) {
                            Debug.Log ("DownloadPicture: CopyTexture not available, using Get/LoadRawTextureData to generate mipmaps (CPU)");
                            var texMipMap = new Texture2D (texture.width, texture.height, texture.format, true);
                            //copy on the CPU
                            texMipMap.LoadRawTextureData (texture.GetRawTextureData<byte> ());
                            texMipMap.Apply (true, true);
                            texture = texMipMap;
                        } else {
                            Debug.LogWarning ("Texture not readable, no mipmaps created: " + sUrl);
                        }
                        break;
                    }
                }
                if (request.result == UnityWebRequest.Result.ConnectionError && --nRetryTimes != 0) {
                    Debug.LogWarning ($"{request.error} while downloading picture, retrying: {request.downloadHandler?.text} {sUrl}");
                    //Wait a little, then retry
                    yield return new WaitForSeconds (.5f);
                    continue;
                }
                //Other errors, or giving up after n retries
                Debug.LogWarning ($"{request.error} while downloading picture, giving up: {request.downloadHandler?.text} {sUrl}");
                yield break;
            }
            callback.Invoke (texture);
        }


        /// <summary>
        /// Get a UnityWebRequest to load a texture either from cache or URL. Cache expires after 20 hours
        /// </summary>
        static UnityWebRequest GetTextureFromCacheOrUrl (string url) {
            var filename = GetCacheFileName (url);
            if (File.Exists (filename)) {
                var lastWriteTimeUtc = File.GetLastWriteTimeUtc (filename);
                var nowUtc = DateTime.UtcNow;
                var totalHours = (int)nowUtc.Subtract (lastWriteTimeUtc).TotalHours;
                if (totalHours < 20) { //cache expires in 20 hours
                    return UnityWebRequestTexture.GetTexture ("file://" + filename, true);
                }
            }
            var webRequest = UnityWebRequestTexture.GetTexture (url, true);
            webRequest.timeout = 10;
            return webRequest;
        }


        /// <summary>
        /// Store a downloaded binary as a temp file (name based on download url)
        /// </summary>
        static void StoreTextureInCache (UnityWebRequest w) {
            try {
                if (!w.url.StartsWith ("file://") && w.result == UnityWebRequest.Result.Success) {
                    var filename = GetCacheFileName (w.url);
                    File.WriteAllBytes (filename, w.downloadHandler.data);
                }
            } catch (Exception ex) {
                Debug.LogWarning ($"{ex.GetType ()} happened while storing texture in cache: {ex}");
            }
        }


        /// <summary>
        /// Generate a unique temp filename based on the original download URL
        /// </summary>
        static string GetCacheFileName (string url) {
            return Path.Combine (Application.temporaryCachePath, CineGameUtility.ComputeMD5Hash (url));
        }


        private void ParseConfig (JObject d) {
            string wifiName = null, wifiPassword = null, wifiQR = null;
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

                case "wifiQR":
                    s = (string)en.Current.Value;
                    wifiQR = s;
                    break;

                case "lagWarningThreshold":
                    v = (long)en.Current.Value;
                    SmartfoxClient.LagWarningThreshold = (int)v;
                    Debug.LogFormat ("LagWarningThreshold set to {0} ms.", v);
                    break;

                case "lagMonitorInterval":
                    v = (long)en.Current.Value;
                    SmartfoxClient.LagMeasureIntervalSeconds = (int)v;
                    Debug.LogFormat ("LagMonitorInterval set to {0} seconds.", v);
                    break;

                case "lagSamplesPerInterval":
                    v = (long)en.Current.Value;
                    SmartfoxClient.LagMeasureNumSamples = (int)v;
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

            if(wifiName != null || wifiPassword != null || wifiQR != null) {
                OnWiFiAvailable?.Invoke(wifiName, wifiPassword);
                if (!string.IsNullOrWhiteSpace (wifiQR) && OnWiFiQR != null) {
                    StartCoroutine (E_DownloadPicture (wifiQR, OnWiFiQR, cache: true));
                }
            }
        }

        public static void EndGame (List<User> users, List<User> winners = null, List<MiniGame> miniGames = null) {
            instance.SendDataToServer (users, winners, miniGames);
        }

        private void SendDataToServer (List<User> users, List<User> winners = null, List<MiniGame> miniGames = null) {

            GameEnded = true;
            /*if (MaxLagValue > SmartfoxClient.LagWarningThreshold) {
                Debug.LogErrorFormat ("SFS Max lag of {0} ms exceeded threshold of {1} ms", MaxLagValue, LagWarningThreshold);
            }*/

            var d = new Dictionary<string, object> {
                ["gameCode"] = GameCode
            };
            if (users != null && users.Count > 0) {
                var l = new List<object> ();
                foreach (var u in users) {
                    if (u.BackendID <= 0)
                        continue;
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
                    if (w.BackendID <= 0)
                        continue;
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

        /// <summary>
		/// Returns true if there is a smartfox server running on the local computer
		/// </summary>
        internal static bool IsGameServerRunningLocally () {
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor) {
                Debug.Log ("IsGameServerRunningLocally disabled on this platform for now, returning false");
                return false;
            }
            var isGameServerRunning = false;
            return ExternalProcess.Run ("/usr/bin/pgrep", "-f smartfoxserver", null, (msg, pct) => {
                isGameServerRunning = int.TryParse (msg, out int pid);
                return false;
            }) && isGameServerRunning;
        }

        static void API (string uri, string json, BackendCallback callback = null) {
            if (Debug.isDebugBuild) {
                Debug.LogFormat ("POST {0} {1}", uri, json);
            }
            var request = new UnityWebRequest (
                CineGameMarket.GetAPI () + uri,
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
            var responseString = request.downloadHandler.text;
            request.Dispose ();
            callback?.Invoke (statusCode, responseString);
        }

        static void PostFile (string uri, string filename, byte [] file, BackendCallback callback = null) {
            var wwwForm = new WWWForm ();
            wwwForm.AddBinaryData ("file", file, filename);
            var request = UnityWebRequest.Post (CineGameMarket.GetAPI () + uri, wwwForm);
            var enHeaders = BackendHeaders.GetEnumerator ();
            while (enHeaders.MoveNext ()) {
                request.SetRequestHeader (enHeaders.Current.Key, enHeaders.Current.Value);
            }
            instance.StartCoroutine (instance.E_Backend (request, callback));
        }

        static readonly Dictionary<int, Sfs2X.Entities.User> SmartfoxUserDictionary = new ();

        static void HandlePrivateMessage (string message, Sfs2X.Entities.User user) {
            if (user.Properties.TryGetValue ("bkid", out object v)) {
                var backendID = (int)v;
                if (message.StartsWith ("/m ") || message.StartsWith ("/giphy ") || message.StartsWith ("/tenor ")) {
                    OnPlayerChatMessage?.Invoke (backendID, message);
                } else {
                    OnPlayerStringMessage?.Invoke (backendID, message);
                }
            }
        }

        static void HandleObjectMessage (ISFSObject dataObj, Sfs2X.Entities.User user) {
            if (dataObj.ContainsKey ("bkid")) {
                var backendID = dataObj.GetInt ("bkid");
                var userName = dataObj.GetUtfString ("name");
                var userAge = dataObj.GetInt ("age");
                var userGender = dataObj.GetUtfString ("gender");

                if (user.Properties == null) {
                    user.Properties = new Dictionary<string, object> ();
                }
                user.Properties ["bkid"] = backendID;
                user.Properties ["name"] = userName;

                SmartfoxUserDictionary [backendID] = user;

                if (user.IsPlayer) {
                    if (GameEnded) {
                        Debug.LogWarning ($"Received bkid object from {userName} ({backendID}) but game has already ended");
                        return;
                    }

                    string avatarID = null;

                    if (dataObj.ContainsKey ("avatar")) {
                        avatarID = dataObj.GetUtfString ("avatar");
                    }

                    Version appVer = null;
                    if (dataObj.ContainsKey ("appVer")) {
                        string appVerString = dataObj.GetUtfString ("appVer");
                        appVer = new Version (appVerString);
                    }

                    if (CineGameChatController.IsProfanityFilterLoaded) {
                        CineGameChatController.RunProfanityFilter (userName, (filteredUserName) => {
                            OnPlayerJoined?.Invoke (new CineGameSDK.User {
                                BackendID = backendID,
                                AppVersion = appVer,
                                Name = filteredUserName,
                                Age = userAge,
                                Gender = userGender,
                                Score = 0
                            });

                            if (!string.IsNullOrWhiteSpace (avatarID)) {
                                SetPlayerAvatar (backendID, avatarID);
                            }
                        });
                    } else {
                        Debug.LogWarning ("SFS Player name is unfiltered (CineGameChatController instance not found)");

                        OnPlayerJoined?.Invoke (new User {
                            BackendID = backendID,
                            AppVersion = appVer,
                            Name = userName,
                            Age = userAge,
                            Gender = userGender,
                            Score = 0
                        });

                        if (!string.IsNullOrWhiteSpace (avatarID)) {
                            SetPlayerAvatar (backendID, avatarID);
                        }
                    }
                } else if (user.IsSpectator) {
                    var supportingID = dataObj.GetInt ("supportingId");
                    if (CineGameChatController.IsProfanityFilterLoaded) {
                        CineGameChatController.RunProfanityFilter (userName, (filteredUserName) => {
                            OnSupporterJoined?.Invoke (new Supporter {
                                BackendID = backendID,
                                SupportingID = supportingID,
                                Name = filteredUserName
                            });
                        });
                    } else {
                        Debug.LogWarning ("SFS Supporter name is unfiltered (CineGameChatController instance not found)");
                        OnSupporterJoined?.Invoke (new Supporter {
                            BackendID = backendID,
                            SupportingID = supportingID,
                            Name = userName
                        });
                    }
                }
            } else if (dataObj.ContainsKey ("avatar") && user.Properties != null) {
                var avatarID = dataObj.GetUtfString ("avatar");
                var backendID = (int)user.Properties ["bkid"];
                SetPlayerAvatar (backendID, avatarID);
            }
            if (user.Properties != null && user.Properties.TryGetValue ("bkid", out object o)) {
                int backendID = (int)o;
                OnPlayerObjectMessage?.Invoke (backendID, PlayerObjectMessage.FromSmartFoxObject (dataObj));
            } else if (numBkIdWarnings-- > 0) {
                Debug.LogWarning ($"SFS OnObjectMessage: {user.Name} has no bkid property! Happens when packages get lost or are received out of order");
            }
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
            SmartfoxClient.Broadcast (dataObj.GetSmartFoxObject (), toPlayers: toPlayers, toSpectators: toSpectators);
            CineGameBots.BroadcastObjectMessage (dataObj, toPlayers: toPlayers, toSpectators: toSpectators);
        }

        /// <summary>
		/// Broadcast public object message to either all players, all supporters or all users
		/// </summary>
        public static void BroadcastObjectMessage (string json, bool toPlayers = true, bool toSpectators = false) {
            var dataObj = new PlayerObjectMessage (json);
            SmartfoxClient.Broadcast (dataObj.GetSmartFoxObject (), toPlayers: toPlayers, toSpectators: toSpectators);
            CineGameBots.BroadcastObjectMessage (dataObj, toPlayers: toPlayers, toSpectators: toSpectators);
        }

        /// <summary>
		/// Send private object message to specific CineGame user
		/// </summary>
        public static void SendObjectMessage (PlayerObjectMessage dataObj, int backendId) {
            if (backendId > 0) {
                SmartfoxClient.Send (dataObj.GetSmartFoxObject (), SmartfoxUserDictionary [backendId]);
            } else {
                CineGameBots.SendObjectMessage (dataObj, backendId);
            }
        }

        /// <summary>
		/// Send private object message to specific CineGame user
		/// </summary>
        public static void SendObjectMessage (string json, int backendId) {
            var dataObj = new PlayerObjectMessage (json);
            if (backendId > 0) {
                SmartfoxClient.Send (dataObj.GetSmartFoxObject (), SmartfoxUserDictionary [backendId]);
            } else {
                CineGameBots.SendObjectMessage (dataObj, backendId);
            }
        }

        /// <summary>
        /// Helper to send a simple response. Should be used sparsely as it will result in a complete network package for each call
        /// </summary>
        public static void Send (int backendId, string key, bool value) {
            var dataObj = new PlayerObjectMessage ();
            dataObj.PutBool (key, value);
            SendObjectMessage (dataObj, backendId);
        }

        /// <summary>
        /// Helper to send a simple response. Should be used sparsely as it will result in a complete network package for each call
        /// </summary>
        public static void Send (int backendId, string key, string value) {
            var dataObj = new PlayerObjectMessage ();
            dataObj.PutString (key, value);
            SendObjectMessage (dataObj, backendId);
        }

        /// <summary>
        /// Helper to send a simple response. Should be used sparsely as it will result in a complete network package for each call
        /// </summary>
        public static void Send (int backendId, string key, int value) {
            var dataObj = new PlayerObjectMessage ();
            dataObj.PutInt (key, value);
            SendObjectMessage (dataObj, backendId);
        }

        /// <summary>
        /// Helper to send a simple response. Should be used sparsely as it will result in a complete network package for each call
        /// </summary>
        public static void Send (int backendId, string key, int [] value) {
            var dataObj = new PlayerObjectMessage();
            dataObj.PutIntArray(key, value);
            SendObjectMessage(dataObj, backendId);
        }

        /// <summary>
        /// Helper to broadcast a simple response. Should be used sparsely as it will result in a complete network package to each receiver for each call
        /// </summary>
        public static void Broadcast (string key, bool value, bool toPlayers = true, bool toSpectators = false) {
            var dataObj = new PlayerObjectMessage ();
            dataObj.PutBool (key, value);
            BroadcastObjectMessage (dataObj, toPlayers, toSpectators);
        }

        /// <summary>
        /// Helper to broadcast a simple response. Should be used sparsely as it will result in a complete network package to each receiver for each call
        /// </summary>
        public static void Broadcast (string key, string value, bool toPlayers = true, bool toSpectators = false) {
            var dataObj = new PlayerObjectMessage ();
            dataObj.PutString (key, value);
            BroadcastObjectMessage (dataObj, toPlayers, toSpectators);
        }

        /// <summary>
        /// Helper to broadcast a simple response. Should be used sparsely as it will result in a complete network package to each receiver for each call
        /// </summary>
        public static void Broadcast (string key, int value, bool toPlayers = true, bool toSpectators = false) {
            var dataObj = new PlayerObjectMessage ();
            dataObj.PutInt (key, value);
            BroadcastObjectMessage (dataObj, toPlayers, toSpectators);
        }

        /// <summary>
		/// Send private string message to specific CineGame user
		/// </summary>
        public static void SendPrivateMessage (string msg, int backendId) {
            if (backendId > 0) {
                SmartfoxClient.Send (msg, SmartfoxUserDictionary [backendId]);
            } else {
                CineGameBots.SendStringMessage (msg, backendId);
            }
        }

        /// <summary>
		/// Kick player out of game
		/// </summary>
        public static void KickPlayer (int backendId) {
            if (backendId > 0) {
                SmartfoxClient.KickUser (SmartfoxUserDictionary [backendId]);
            } else {
                //CineGameBots.KickPlayer (backendId);
            }
        }

        /// <summary>
		/// Kick supporter/spectator out of game
		/// </summary>
        public static void KickSupporter (int backendId) {
            if (backendId > 0) {
                SmartfoxClient.KickUser (SmartfoxUserDictionary [backendId]);
            } else {
                //CineGameBots.KickSupporter (backendId);
            }
        }

        /// <summary>
		/// Get total amount of time since parent process started game executable (or since Unity Player initialized, if not applicable)
		/// </summary>
        public static float GetTimeSinceBlockStart () {
            return Time.realtimeSinceStartup + BlockStartTime;
        }

        internal static string GetRegionProfanityUrl () {
			var marketId = Market;
            if (string.IsNullOrWhiteSpace (marketId) && instance.Settings != null) {
                marketId = instance.Settings.MarketId;
            }
            return string.Format ("https://profanity.cinemataztic.com/{0}/txt-file", marketId);
        }

        void OnApplicationQuit () {
            //Log error if communication with server was incomplete
            if (GameEnded && (!GameEndSentToServer)) {
                Debug.LogError (">>> ERROR! Server communication incomplete. Winners may not have received their prices. <<<");
            }
            SmartfoxClient.Disconnect ();
            CineGameDCHP.Stop ();
        }

        [UnmanagedFunctionPointer (CallingConvention.Cdecl)]
        delegate void UNMANAGED_CALLBACK ();

        /// <summary>
		/// Deliberately cause Segmentation Fault to test how the system handles it
		/// </summary>
        static void SegmentationFault () {
            Debug.Log ("*** USER GENERATED SEGMENTATION FAULT");

            var crash = (UNMANAGED_CALLBACK)Marshal.GetDelegateForFunctionPointer ((IntPtr)123, typeof (UNMANAGED_CALLBACK));
            crash ();
        }
    }
}
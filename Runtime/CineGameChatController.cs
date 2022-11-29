using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;

namespace CineGame.Host {

    public class CineGameChatController : MonoBehaviour {

        static CineGameChatController instance;

        internal static string GiphyApiKey;
        internal static string TenorApiKey;

        public static string ReplacementString = "***";
        public static char EmojiSpace = '\u2001';
        public static int MaxChatMessageLength;

        /// <summary>
		/// Default duration of a looping gif animation in seconds
		/// </summary>
        public static int GifDefaultDuration = 5;
        /// <summary>
		/// Max dimension (width or height) of a gif animation
		/// </summary>
        public static int GifSize = 150;

        [Tooltip("TextAsset where emoji UV map is stored")]
        public TextAsset EmojiInfo;

        /// <summary>
		/// Deserialized emoji UV map
		/// </summary>
        private Dictionary<string, Rect> emojiRects = new Dictionary<string, Rect> ();

        private static Regex regexp;

        delegate void WordFilterCallback (string filteredMessage);
        static readonly Queue<Action> _executionQueue = new Queue<Action> ();

        public static Action<int, string, Dictionary<string, Rect>> OnChatMessage;
        public static Action<int, string, int, int> OnGIFMessage;

        void Awake () {
            if (instance != null)
                return;
            instance = this;
        }

        void Start () {
            if (instance != this)
                return;

            MaxChatMessageLength = 80;

            if (CineGameSDK.Region == CineGameSDK.APIRegion.FI) {
                MaxChatMessageLength = 12;
            }

            StartCoroutine (E_LoadProfanity ());

            if (EmojiInfo != null && !string.IsNullOrWhiteSpace (EmojiInfo.text)) {
                SetupEmojiDictionary ();
            }

            CineGameSDK.OnPlayerChatMessage += Message;
        }

        internal static string GetProfanitiesCacheFileName (string url) {
            return string.Format ("{0}/{1}", Application.temporaryCachePath, CineGameUtility.ComputeMD5Hash (url));
        }

        IEnumerator E_LoadProfanity () {
            var url = CineGameSDK.GetRegionProfanityUrl ();
            var filename = string.Format ("{0}/{1}", Application.temporaryCachePath, CineGameUtility.ComputeMD5Hash (url));
            var cacheExists = File.Exists (filename);

            var request = UnityWebRequest.Get (url);
            request.timeout = 10;
            yield return request.SendWebRequest ();

            string profanitiesRegexStr = null;
            if ((HttpStatusCode)request.responseCode == HttpStatusCode.OK) {
                var sb = new StringBuilder (16384);
                var reader = new StringReader (request.downloadHandler.text);
                string line;
                sb.Append ("(?:\\S+?\\.(?:com|com\\.au|co\\.uk|co\\.nz|de|dk|ee|es|eu|fi|fr|gov|gr|info|io|it|jp|lt|net|no|org|ru|se|xxx)|\\b(?:");
                var numLines = 0;
                while ((line = reader.ReadLine ()) != null) {
                    sb.Append (Regex.Escape (line.Trim ()));
                    sb.Append ('|');
                    numLines++;
                }
                if (numLines > 0) {
                    sb.Length -= 1;
                }
                sb.Append (")\\b)");
                profanitiesRegexStr = sb.ToString ();
            } else {
                Debug.LogErrorFormat ("Error while attempting to download profanities: {0}", request.error);
            }
            if (string.IsNullOrEmpty (profanitiesRegexStr)) {
                if (cacheExists) {
                    Debug.Log ("Using cached version of profanities from " + File.GetLastWriteTimeUtc (filename));
                    profanitiesRegexStr = File.ReadAllText (filename);
                } else {
                    Debug.LogError ("Profanities unavailable to download, and no cache exists. Chat will be unfiltered!");
                }
            } else {
                File.WriteAllText (filename, profanitiesRegexStr);
            }
            if (!string.IsNullOrEmpty (profanitiesRegexStr)) {
                regexp = new Regex (profanitiesRegexStr, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
        }

        /// <summary>
		/// Set up dictionary of {emoji,UVs}
		/// </summary>
        void SetupEmojiDictionary () {
            using (StringReader reader = new StringReader (EmojiInfo.text)) {
                string line = reader.ReadLine ();
                while (line != null && line.Length > 1) {
                    // We add each emoji to emojiRects
                    string [] split = line.Split (' ');
                    float x = float.Parse (split [1], System.Globalization.CultureInfo.InvariantCulture);
                    float y = float.Parse (split [2], System.Globalization.CultureInfo.InvariantCulture);
                    float width = float.Parse (split [3], System.Globalization.CultureInfo.InvariantCulture);
                    float height = float.Parse (split [4], System.Globalization.CultureInfo.InvariantCulture);


                    string [] converted = split [0].Split ('-');
                    for (int j = 0; j < converted.Length; j++) {
                        converted [j] = char.ConvertFromUtf32 (Convert.ToInt32 (converted [j], 16));
                    }

                    emojiRects.Add (string.Join (string.Empty, converted), new Rect (x, y, width, height));

                    line = reader.ReadLine ();
                }
            }
        }

        void RunProfanityFilter (string input, WordFilterCallback callback) {
            System.Threading.ThreadPool.QueueUserWorkItem (delegate (object state) {
                var filtered = regexp.Replace ((string)state, ReplacementString);

                lock (_executionQueue) {
                    _executionQueue.Enqueue (() => {
                        callback (filtered);
                    });
                }
            }, input);
        }

        void Update () {
            lock (_executionQueue) {
                while (_executionQueue.Count > 0) {
                    _executionQueue.Dequeue ().Invoke ();
                }
            }
        }

        /// <summary>
		/// Handle chat message received from user. Decode and setup emojis, or load mp4 animation from Giphy or Tenor
		/// </summary>
        internal static void Message (int backendID, string message) {

            //message = "/giphy jhFUy6eCy6xs4";
            //message = "/tenor 8776030";

            if (message.StartsWith ("/giphy ")) {
                if (!string.IsNullOrEmpty (GiphyApiKey)) {
                    var giphyId = message.Substring (7);
                    instance.StartCoroutine (instance.E_SpeakGiphy (backendID, giphyId));
                } else {
                    Debug.LogError ("Giphy message received but API key not provided!");
                }
            } else if (message.StartsWith ("/tenor ")) {
                if (!string.IsNullOrEmpty (TenorApiKey)) {
                    var tenorId = message.Substring (7);
                    instance.StartCoroutine (instance.E_SpeakTenor (backendID, tenorId));
                } else {
                    Debug.LogError ("Tenor message received but API key not provided!");
                }
            } else {
                var chatMessage = message.Substring (3);
                try {
                    Debug.LogFormat ("Player {0} says (unfiltered): {2}", backendID, chatMessage);
                } catch (Exception) {
                    //Surrogate-tail emojis will throw an exception when logging. We will probably do nothing about it.
                }

                instance.RunProfanityFilter (chatMessage, delegate (string filteredMessage) {
                    string emojiMessage = filteredMessage;
                    Dictionary<string, Rect> emojiDictionary = new Dictionary<string, Rect> ();

                    int i = 0;
                    int numChars = emojiMessage.Length;
                    while (i < numChars) {
                        string singleChar = emojiMessage.Substring (i, 1);
                        string doubleChar = string.Empty;
                        string fourChar = string.Empty;

                        if (i < (emojiMessage.Length - 1)) {
                            doubleChar = emojiMessage.Substring (i, 2);
                        }
                        if (i < (emojiMessage.Length - 3)) {
                            fourChar = emojiMessage.Substring (i, 4);
                        }

                        if (instance.emojiRects.ContainsKey (fourChar)) {
                            if (!emojiDictionary.ContainsKey (fourChar)) {
                                instance.emojiRects.TryGetValue (fourChar, out Rect uvRect);
                                emojiDictionary.Add (fourChar, uvRect);
                            }
                        } else if (instance.emojiRects.ContainsKey (doubleChar)) {
                            if (!emojiDictionary.ContainsKey (doubleChar)) {
                                instance.emojiRects.TryGetValue (doubleChar, out Rect uvRect);
                                emojiDictionary.Add (doubleChar, uvRect);
                            }
                        } else if (instance.emojiRects.ContainsKey (singleChar)) {
                            if (!emojiDictionary.ContainsKey (singleChar)) {
                                instance.emojiRects.TryGetValue (singleChar, out Rect uvRect);
                                emojiDictionary.Add (singleChar, uvRect);
                            }
                        }

                        i++;
                    }

                    OnChatMessage?.Invoke (backendID, emojiMessage, emojiDictionary);

                });
            }
        }

        IEnumerator E_SpeakGiphy (int backendID, string giphyId) {
            using (var www = UnityWebRequest.Get ($"https://api.giphy.com/v1/gifs/{giphyId}?api_key={GiphyApiKey}")) {
                yield return www.SendWebRequest ();
                if (www.responseCode == 200) {
                    var result = JsonConvert.DeserializeObject<GiphyResult> (www.downloadHandler.text);
                    var img = result.data.images.original;
                    if (!string.IsNullOrWhiteSpace (img.mp4)) {
                        //TODO maybe cache common gifs:
                        /*var pathToVideo = $"{Application.temporaryCachePath}/giphy/{giphyId}.mp4";
                        if (!File.Exists (pathToVideo)) {
                            using (var wwwFile = UnityWebRequest.Get (img.mp4)) {
                                yield return wwwFile.SendWebRequest ();
                                if (wwwFile.responseCode == 200) {
                                    Directory.CreateDirectory (Path.GetDirectoryName (pathToVideo));
                                    File.WriteAllBytes (pathToVideo, wwwFile.downloadHandler.data);
                                }
                            }
                        }
                        */
                        OnGIFMessage?.Invoke (backendID, img.mp4, img.width, img.height);
                        Debug.LogFormat ("Player {0} plays mp4 @ {1}", backendID, img.mp4);
                    } else {
                        Debug.LogError ("SpeakGiphy: Empty url for mp4 animation");
                    }
                }
            }
        }

        IEnumerator E_SpeakTenor (int backendID, string tenorId) {
            using (var www = UnityWebRequest.Get ($"https://g.tenor.com/v1/gifs?ids={tenorId}&key={TenorApiKey}")) {
                yield return www.SendWebRequest ();
                if (www.responseCode == 200) {
                    var result = JsonConvert.DeserializeObject<TenorResult> (www.downloadHandler.text);
                    if (result.results.Length != 0 && result.results [0].media.Length != 0) {
                        var img = result.results [0].media [0].mp4;
                        if (!string.IsNullOrWhiteSpace (img?.url)) {
                            OnGIFMessage?.Invoke (backendID, img.url, img.dims [0], img.dims [1]);
                            Debug.LogFormat ("Player {0} plays mp4 @ {1}", backendID, img.url);
                        } else {
                            Debug.LogError ("SpeakTenor: Empty url for mp4 animation");
                        }
                    } else {
                        Debug.LogError ("SpeakTenor: Empty result set");
                    }
                }
            }
        }

        class GiphyResult {
            public class GiphyImage {
                public int frames { get; set; }
                public int width { get; set; }
                public int height { get; set; }
                public int mp4_size { get; set; }
                public string mp4 { get; set; }
            }
            public class GiphyResultDataImages {
                public GiphyImage original { get; set; }
            }
            public class GiphyResultData {
                public string id { get; set; }
                public GiphyResultDataImages images { get; set; }
            }
            public GiphyResultData data { get; set; }
        }

        class TenorResult {
            public class TenorMediaObject {
                public int size { get; set; }
                public int [] dims { get; set; }
                public float duration { get; set; }
                public string url { get; set; }
            }
            public class TenorMedia {
                public TenorMediaObject mp4 { get; set; }
            }
            public class TenorResultData {
                public string id { get; set; }
                public TenorMedia [] media { get; set; }
            }
            public TenorResultData [] results { get; set; }
        }
    }
}

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
using UnityEngine.UI;
using TMPro;

namespace CineGame.SDK {

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

        [Tooltip ("TextAsset where emoji UV map is stored")]
        public TextAsset EmojiInfo;

        /// <summary>
		/// Deserialized emoji UV map
		/// </summary>
        readonly Dictionary<string, Rect> emojiRects = new ();

        public delegate void WordFilterCallback (string filteredMessage);

        public static Action<int, string, Dictionary<string, Rect>> OnChatMessage;
        public static Action<int, string, int, int> OnGIFMessage;

        /// <summary>
        /// List of top-level domains scanned for
        /// </summary>
        static readonly string [] topLevelDomains = new [] {
            "app",
            "ai",
            "ch",
            "com",
            "com.au",
            "co.uk",
            "co.nz",
            "de",
            "dk",
            "ee",
            "es",
            "et",
            "eu",
            "fi",
            "fr",
            "gov",
            "gr",
            "ie",
            "info",
            "io",
            "it",
            "jp",
            "lt",
            "net",
            "no",
            "org",
            "ru",
            "se",
            "xxx",
        };

        /// <summary>
        /// Global buffer (we don't need to be thread-safe, just keep allocations to a minimum)
        /// </summary>
        static readonly StringBuilder sb = new (256);

        class TrieNode {
            public Dictionary<char, TrieNode> Children = new ();
            public bool IsEndOfWord;
            public int WordLength; // Stores length to allow replacing exact matches
        }

        static TrieNode _root;
        static int _totalNodes;

        public static bool IsProfanityFilterLoaded {
            get { return instance != null && _root != null; }
        }

        void Awake () {
            if (instance != null)
                return;
            instance = this;
        }

        void Start () {
            if (instance != this)
                return;

            MaxChatMessageLength = 80;

            if (CineGameSDK.Market == CineGameMarket.Markets.Leffapeli_Finnkino_FI) {
                MaxChatMessageLength = 12;
            }

            StartCoroutine (E_LoadProfanity ());

            if (EmojiInfo != null && !string.IsNullOrWhiteSpace (EmojiInfo.text)) {
                SetupEmojiDictionary ();
            }

            CineGameSDK.OnPlayerChatMessage += Message;
        }

        /// <summary>
        /// Adds a word to the Trie structure, building nodes as required
        /// </summary>
        public void AddWord (string word) {
            if (string.IsNullOrWhiteSpace (word)) return;

            var current = _root;

            for (int i = 0; i < word.Length; i++) {
                var c = char.ToLowerInvariant (word [i]);
                if (!current.Children.TryGetValue (c, out var nextNode)) {
                    nextNode = new TrieNode ();
                    _totalNodes++;
                    current.Children [c] = nextNode;
                }
                current = nextNode;
            }

            current.IsEndOfWord = true;
            current.WordLength = word.Length;
        }


        IEnumerator E_LoadProfanity () {
            var url = CineGameSDK.GetRegionProfanityUrl ();
            var filename = string.Format ("{0}/{1}", Application.temporaryCachePath, CineGameUtility.ComputeMD5Hash (url));
            var cacheExists = File.Exists (filename);

            using var request = UnityWebRequest.Get (url);
            request.timeout = 10;
            yield return request.SendWebRequest ();

            if ((HttpStatusCode)request.responseCode == HttpStatusCode.OK) {
                var file = new FileInfo (filename);
                file.Directory.Create ();
                File.WriteAllText (file.FullName, request.downloadHandler.text);
                cacheExists = true;
            } else if (cacheExists) {
                Debug.Log ("Using cached version of profanities from " + File.GetLastWriteTimeUtc (filename));
            }

            if (!cacheExists) {
                Debug.LogError ("Profanities unavailable to download, and no cache exists. Chat will be 100% filtered!");
                yield break;
            }

            _root = new TrieNode ();
            _totalNodes = 0;
            var reader = new StreamReader (File.Open (filename, FileMode.Open));
            string line;
            var numLines = 0;
            while ((line = reader.ReadLine ()) != null) {
                var trimmed = line.Trim ();
                if (string.IsNullOrEmpty (trimmed)) {
                    Debug.LogWarning ("Empty profanity on line " + numLines);
                    continue;
                }
                AddWord (line);
                numLines++;
            }
            Debug.Log ($"CineGameChatController: Constructed {_totalNodes} Trie nodes from {numLines} profanities");
        }

        /// <summary>
		/// Set up dictionary of {emoji,UVs}
		/// </summary>
        void SetupEmojiDictionary () {
            using var reader = new StringReader (EmojiInfo.text);
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


        /// <summary>
		/// First check with backend is the input is allowed or moderated. Then, if market-specific profanity filter is loaded, run it on another thread. Result is returned in the callback argument.
		/// </summary>
        public static void RunProfanityFilter (string input, WordFilterCallback callback) {
            CineGameSDK.API ("game/moderate", "{\"message\":" + JsonConvert.SerializeObject (input) + "}", (statusCode, responseString) => {
                if (statusCode == HttpStatusCode.NoContent) {
                    Debug.Log ("User input moderated by backend: " + input);
                    callback.Invoke (ReplacementString);
                    return;
                }
                if (!IsProfanityFilterLoaded) {
                    callback.Invoke(ReplacementString);
                    return;
                }

                sb.Clear ();

                var match = false;
                int i = 0;

                while (i < input.Length) {
                    while (i < input.Length && !char.IsLetterOrDigit (input [i])) {
                        sb.Append (input [i++]);
                    }

                    var current = _root;
                    int matchLength = 0;
                    int j = i;

                    // Traverse Trie matching lowercase characters
                    while (j < input.Length) {
                        char c = char.ToLowerInvariant (input [j]);

                        if (!current.Children.TryGetValue (c, out var nextNode))
                            break;

                        current = nextNode;
                        j++;

                        if (current.IsEndOfWord) {
                            matchLength = current.WordLength; // Record longest match found
                        }
                    }

                    if (matchLength > 0 && (j == input.Length || !char.IsLetterOrDigit (input [j]))) {
                        match = true;
                        sb.Append (ReplacementString);
                        i += matchLength;
                    } else {
                        while (i < input.Length && char.IsLetterOrDigit (input [i])) {
                            sb.Append (input [i++]);
                        }
                        if (i < input.Length && input [i] == '.') {
                            //Scan for a toplevel domain (we do not allow messaging links)
                            for (int l = 0; l < topLevelDomains.Length; l++) {
                                var str = topLevelDomains [l];
                                int k = i + 1;
                                int kl = 0;
                                while (k < input.Length && kl < str.Length) {
                                    if (str [kl] != input [k])
                                        break;
                                    k++;
                                    kl++;
                                }
                                if (kl == str.Length && (k == input.Length || !char.IsLetter (input [k]))) {
                                    Debug.Log ("User input: blocked potential website address: " + input);
                                    callback.Invoke (ReplacementString);
                                    return;
                                }
                            }
                        }
                    }
                }

                if (match) {
                    var filtered = sb.ToString ();
                    Debug.Log ($"User input: profanity \"{input}\" => \"{filtered}\"");
                    callback.Invoke (filtered);
                } else {
                    callback.Invoke (input);
                }

            });
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

                RunProfanityFilter (chatMessage, (filteredMessage) => {
                    SetupEmojisAndChat (backendID, filteredMessage);
                });
            }
        }

        static void SetupEmojisAndChat (int backendID, string filteredMessage) {
            string emojiMessage = filteredMessage;
            var emojiDictionary = new Dictionary<string, Rect> ();

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
        }

        public static void SetupEmoji (Texture2D emojiAtlas, TMP_Text text, int index, Rect emojiUv) {
            text.ForceMeshUpdate ();
            var info = text.textInfo;

            var newEmojiObj = new GameObject {
                name = "Emoji_" + index
            };
            newEmojiObj.transform.parent = text.transform;
            var newEmoji = newEmojiObj.AddComponent<RawImage> ();
            newEmoji.texture = emojiAtlas;
            newEmoji.rectTransform.sizeDelta = new Vector2 (text.fontSize, text.fontSize);
            newEmoji.rectTransform.pivot = Vector2.zero;
            newEmoji.rectTransform.anchorMin = new Vector2 (0f, 1f);
            newEmoji.rectTransform.anchorMax = new Vector2 (0f, 1f);
            newEmoji.uvRect = emojiUv;
            newEmoji.transform.localScale = Vector3.one;
            newEmoji.transform.localRotation = Quaternion.identity;

            var emojiPos = info.characterInfo [index].bottomLeft;
            emojiPos.y -= newEmoji.rectTransform.sizeDelta.y / 4 - 1;
            newEmoji.transform.localPosition = emojiPos;
        }

        IEnumerator E_SpeakGiphy (int backendID, string giphyId) {
            using var www = UnityWebRequest.Get ($"https://api.giphy.com/v1/gifs/{giphyId}?api_key={GiphyApiKey}");
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

        IEnumerator E_SpeakTenor (int backendID, string tenorId) {
            using var www = UnityWebRequest.Get ($"https://g.tenor.com/v1/gifs?ids={tenorId}&key={TenorApiKey}"); yield return www.SendWebRequest ();
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

#pragma warning disable IDE1006
        /// <summary>
		/// Json response from Giphy API
		/// </summary>
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

        /// <summary>
		/// Json response from Tenor API
		/// </summary>
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
#pragma warning restore
    }
}

using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Unity.EditorCoroutines.Editor;
//using UnityEngine.Video;

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;

using CineGame.Host;

//using Ionic.Zip;
//using System.Threading;

namespace CineGame.HostEditor {

    internal class CineGameBuild : EditorWindow {

        static CineGameBuild instance;

        [DllImport ("libc", EntryPoint = "chmod", SetLastError = true)]
        private static extern int sys_chmod (string path, uint mode);

        static BuildTarget buildTarget;
        static BuildOptions buildOptions = BuildOptions.None;
        static BuildTarget [] targets = {
            BuildTarget.StandaloneOSX,
            BuildTarget.StandaloneLinux64,
            BuildTarget.StandaloneWindows
        };

        static string ProgressBarTitle = "CineGame Build";

        static string resultMessage = string.Empty;
        //contains result description from last scene build+upload

        internal static string GameType;

        /// <summary>
		/// Path to certificate for signing Windows builds
		/// </summary>
        static string CrtFilePath;
        /// <summary>
		/// Path to private key for signing Windows builds
		/// </summary>
        static string KeyFilePath;

        static int KeychainCertIndex;
        static string [] KeychainCertNames;

        static string MarketSlug;

        static int MarketIndex;
        static int GameTypeIndex;

        static string LatestCommit;
        static string LatestBranch;
        static string LatestAuthor;
        static string LatestCommitMessage;
        static string LatestDiff;

        static string progressMessage = "Waiting ...";

        static float buildProgress;
        static bool IsBuilding = false;

        const int UPLOAD_REQUEST_TIMEOUT = 600;

        static string LastBuildReportString;

        static Texture iconTexture;
        internal static Texture IconTexture {
            get {
                if (iconTexture == null) {
                    //Assembly _assembly = Assembly.GetExecutingAssembly ();
                    //Debug.Log ("Manifest resource names: \n" + string.Join ("\n", _assembly.GetManifestResourceNames ()));
                    //iconTexture = AssemblyHelpers.LoadTextureFromAssembly ("CineGameSDK.HostEditor", "cinemataztic-icon-16.png");
                    iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D> (AssetDatabase.GUIDToAssetPath ("efec9089586ac4574995396036ef8524"));
                }
                return iconTexture;
            }
        }

        Vector2 buildReportScrollPosition = Vector2.zero;

        static bool HasWinBuildSupport, HasMacOSBuildSupport, HasLinux64BuildSupport, HasLinuxIl2cppSupport;
        static bool BuildOnlyForLinux;

        public CineGameBuild () {
            instance = this;
        }

        EditorCoroutine StartCoroutine (IEnumerator coroutine) {
            return EditorCoroutineUtility.StartCoroutine (coroutine, this);
        }

        void StopCoroutine (EditorCoroutine coroutine) {
            EditorCoroutineUtility.StopCoroutine (coroutine);
        }

        bool KeyDown (KeyCode keyCode) {
            return Event.current.type == EventType.KeyDown && Event.current.keyCode == keyCode;
        }

        void OnGUI () {
            EditorGUILayout.Space ();

            if (!HasLinux64BuildSupport) {
                EditorGUILayout.HelpBox ("Linux64 build support required! Preferable IL2CPP", MessageType.Error);
                return;
            }
            if (!HasLinuxIl2cppSupport) {
                EditorGUILayout.HelpBox ("Linux64 IL2CPP build support recommended", MessageType.Warning);
            }

            if (!CineGameLogin.IsLoggedIn) {
                EditorGUILayout.HelpBox ("Not logged in!", MessageType.Error);
                return;
            }

            var gtTooltip = "GameType extracted from CineGameSettings asset";
            EditorGUILayout.LabelField (new GUIContent ("GameType: ", gtTooltip), new GUIContent (GameType, gtTooltip));

            var _marketIndex = Mathf.Clamp (MarketIndex, 0, CineGameLogin.MarketSlugsAvailable.Length);
            _marketIndex = EditorGUILayout.Popup (new GUIContent ("Current market:", "Market to upload game build to. Also used for testing inside the Editor"), _marketIndex, CineGameLogin.MarketSlugsAvailable);
            if (MarketIndex != _marketIndex) {
                SetMarketIndex (_marketIndex);
            }

            if (!string.IsNullOrEmpty (LatestCommit)) {
                EditorGUILayout.Space ();
                EditorGUILayout.LabelField ("Branch: ", LatestBranch);
                EditorGUILayout.LabelField ("Last commit: #", LatestCommit);
                EditorGUILayout.LabelField ("Last author: ", LatestAuthor);
                EditorGUILayout.LabelField ("Last commit msg:", LatestCommitMessage);
            }

            var width100Option = GUILayout.Width (100f);
            EditorGUILayout.Space ();

            if (Application.platform == RuntimePlatform.OSXEditor) {
                EditorGUILayout.BeginHorizontal ();
                CrtFilePath = EditorGUILayout.TextField (new GUIContent ("CRT File:", "Certificate PEM file used to sign Windows build"), CrtFilePath);
                if (GUILayout.Button ("Browse", width100Option)) {
                    var result = EditorUtility.OpenFilePanel ("Select certificate for signing Windows build", CrtFilePath, "pem");
                    if (!string.IsNullOrWhiteSpace (result)) {
                        CrtFilePath = result;
                    }
                }
                EditorGUILayout.EndHorizontal ();
                EditorGUILayout.BeginHorizontal ();
                KeyFilePath = EditorGUILayout.TextField (new GUIContent ("KEY File: ", "Private key PEM file used to sign Windows build"), KeyFilePath);
                if (GUILayout.Button ("Browse", width100Option)) {
                    var result = EditorUtility.OpenFilePanel ("Select private key for signing Windows build", KeyFilePath, "pem");
                    if (!string.IsNullOrWhiteSpace (result)) {
                        KeyFilePath = result;
                    }
                }
                EditorGUILayout.EndHorizontal ();
                EditorGUILayout.Space ();
                var _kci = EditorGUILayout.Popup (new GUIContent ("macOS Signing identity:", "Signing identity used to sign macOS build"), KeychainCertIndex, KeychainCertNames);
                if (_kci != KeychainCertIndex) {
                    KeychainCertIndex = _kci;
                    EditorPrefs.SetString ("CinemaBuildKeychainSearchPattern", KeychainCertNames [KeychainCertIndex]);
                }
            } else {
                EditorGUILayout.BeginHorizontal ();
                CrtFilePath = EditorGUILayout.TextField (new GUIContent ("PFX File:", "PFX File containing certificate and private key for signing Windows build"), CrtFilePath);
                if (GUILayout.Button ("Browse", width100Option)) {
                    var result = EditorUtility.OpenFilePanel ("Select certificate for signing Windows build", CrtFilePath, "pfx");
                    if (!string.IsNullOrWhiteSpace (result)) {
                        CrtFilePath = result;
                    }
                }
                EditorGUILayout.EndHorizontal ();
            }
            EditorGUILayout.Space ();

            if (!IsBuilding) {
                var _buildOnlyForLinux = EditorGUILayout.Toggle ("Build only for Linux", BuildOnlyForLinux);
                if (_buildOnlyForLinux != BuildOnlyForLinux) {
                    BuildOnlyForLinux = _buildOnlyForLinux;
                    EditorPrefs.SetBool ("CinemaBuildOnlyForLinux", BuildOnlyForLinux);
                }
                if (GUILayout.Button ("Build " + GameType + " for " + (BuildOnlyForLinux ? "Linux" : "all platforms"))) {
                    OnClickBuild ();
                }
            } else {
                Rect r = EditorGUILayout.BeginVertical ();
                EditorGUI.ProgressBar (r, buildProgress, progressMessage);
                GUILayout.Space (18);
                EditorGUILayout.EndVertical ();

                if (GUILayout.Button ("Cancel")) {
                    IsBuilding = false;
                }
            }

            if (!IsBuilding) {
                var lastBuildReportPath = GetLastBuildReportPath ();
                if (string.IsNullOrEmpty (LastBuildReportString) && File.Exists (lastBuildReportPath)) {
                    LastBuildReportString = File.ReadAllText (lastBuildReportPath);
                }

                if (!string.IsNullOrEmpty (LastBuildReportString)) {
                    Rect rScroll = EditorGUILayout.BeginVertical ();
                    buildReportScrollPosition = EditorGUILayout.BeginScrollView (buildReportScrollPosition, false, true, GUILayout.Height (rScroll.height));
                    EditorGUILayout.SelectableLabel (LastBuildReportString, EditorStyles.textArea, GUILayout.ExpandHeight (true));
                    EditorGUILayout.EndScrollView ();
                    EditorGUILayout.EndVertical ();
                }

                var outputPathFormat = GetOutputPathFormat ();
                var macPath = string.Format (outputPathFormat, GameType, BuildTarget.StandaloneOSX);
                var winPath = string.Format (outputPathFormat, GameType, BuildTarget.StandaloneWindows);
                var linuxPath = string.Format (outputPathFormat, GameType, BuildTarget.StandaloneLinux64);

                if ((File.Exists (macPath) || File.Exists (winPath) || File.Exists (linuxPath)) && GUILayout.Button ("Upload " + GameType + " for " + MarketSlug)) {
                    //All three builds exist and user has clicked on Upload button
                    OnClickUpload ();
                }

            }
        }


        static void SetMarketIndex (int _marketIndex) {
            if (_marketIndex >= 0 && _marketIndex < CineGameLogin.MarketIdsAvailable.Length) {
                MarketIndex = _marketIndex;
                var marketId = CineGameLogin.MarketIdsAvailable [_marketIndex];
                MarketSlug = CineGameSDK.MarketSlugMap [marketId];
                Configuration.MARKET_ID = marketId;
            }
        }


        /// <summary>
        /// Scene hierarchy has changed (gameobjects created, deleted or scene has loaded).
        /// Determine gametype and market
        /// </summary>
        void OnHierarchyChange () {
            if (CineGameLogin.IsLoggedIn && !IsBuilding && !EditorApplication.isPlayingOrWillChangePlaymode) {
                GetGameTypeFromSceneOrProject ();
                //RepaintWindow ();
            }
        }


        void OnClickBuild () {
            var crtFileExists = !string.IsNullOrWhiteSpace (CrtFilePath) && File.Exists (CrtFilePath);
            if (!BuildOnlyForLinux && HasWinBuildSupport && !crtFileExists) {
                if (!EditorUtility.DisplayDialog (ProgressBarTitle, "WARNING: No Crt and Keyfile specified. Windows builds will not be signed. Continue?", "OK", "Cancel")) {
                    Debug.Log ("User canceled build due to missing Crt and Keyfile");
                    return;
                }
            }
            if (!BuildOnlyForLinux && HasMacOSBuildSupport && Application.platform != RuntimePlatform.OSXEditor) {
                if (!EditorUtility.DisplayDialog (ProgressBarTitle, "WARNING: OS X builds can only be signed on an OS X device with XCode installed. You will have to do this manually. Continue?", "OK", "Cancel")) {
                    Debug.Log ("User canceled build due to only being able to codesign on OS X");
                    return;
                }
            }

            if (crtFileExists) {
                EditorPrefs.SetString ("CinemaBuildCrtFile", CrtFilePath);
                EditorPrefs.SetString ("CinemaBuildKeyFile", KeyFilePath);
            }

            //if (HasMacOSBuildSupport && Application.platform == RuntimePlatform.OSXEditor) {
            //    EditorPrefs.SetString ("CinemaBuildKeychainSearchPattern", KeychainSearchPattern);
            //}

            if (string.IsNullOrEmpty (GameType)) {
                EditorUtility.DisplayDialog (ProgressBarTitle, "No GameType set! Did you add a SystemController component to the scene?", "OK");
                Debug.LogError ("No GameType set. Game will not be built.");
                return;
            }

            StartCoroutine (E_BuildSingleSceneForAllPlatforms ());
        }


        void OnClickUpload () {
            if (!CineGameLogin.IsLoggedIn) {
                CineGameLogin.Init ();
                EditorUtility.DisplayDialog (ProgressBarTitle, "Please log in first!", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog (ProgressBarTitle, "Are you sure you want to upload " + GameType + " to " + MarketSlug + "?", "OK", "Cancel")) {
                return;
            }

            StartCoroutine (E_UploadSingleSceneForAllPlatforms ());
        }


        void OnEnable () {

            var moduleManager = Type.GetType ("UnityEditor.Modules.ModuleManager,UnityEditor.dll");
            var isPlatformSupportLoaded = moduleManager.GetMethod ("IsPlatformSupportLoaded", BindingFlags.Static | BindingFlags.NonPublic);
            var getTargetStringFromBuildTarget = moduleManager.GetMethod ("GetTargetStringFromBuildTarget", BindingFlags.Static | BindingFlags.NonPublic);

            HasWinBuildSupport = (bool)isPlatformSupportLoaded.Invoke (null, new object [] { (string)getTargetStringFromBuildTarget.Invoke (null, new object [] {
                BuildTarget.StandaloneWindows }) });
            HasMacOSBuildSupport = (bool)isPlatformSupportLoaded.Invoke (null, new object [] { (string)getTargetStringFromBuildTarget.Invoke (null, new object [] {
                BuildTarget.StandaloneOSX }) });
            HasLinux64BuildSupport = (bool)isPlatformSupportLoaded.Invoke (null, new object [] { (string)getTargetStringFromBuildTarget.Invoke (null, new object [] {
                BuildTarget.StandaloneLinux64 }) });

            if (HasLinux64BuildSupport) {
                // Check if Linux64 IL2CPP build support variation is installed. Currently I don't know of a smarter way to do this
                var buildSupportIl2cppDir = Application.platform == RuntimePlatform.OSXEditor ?
                      "PlaybackEngines/LinuxStandaloneSupport/Variations/il2cpp"
                    : "Data\\PlaybackEngines\\LinuxStandaloneSupport\\Variations\\il2cpp";
                HasLinuxIl2cppSupport = Directory.Exists (Path.Combine (Path.GetDirectoryName (EditorApplication.applicationPath), buildSupportIl2cppDir));
            }

            EditorApplication.hierarchyChanged -= OnHierarchyChange;
            EditorApplication.hierarchyChanged += OnHierarchyChange;

            //LoginRegionIndex = Mathf.Clamp (Array.IndexOf (LoginRegions, EditorPrefs.GetString ("CinemaBuildMarketRegion", LoginRegions [0])), 0, LoginRegions.Length - 1);

            BuildOnlyForLinux = EditorPrefs.GetBool ("CinemaBuildOnlyForLinux", false);
            CrtFilePath = EditorPrefs.GetString ("CinemaBuildCrtFile");
            KeyFilePath = EditorPrefs.GetString ("CinemaBuildKeyFile");

            if (Application.platform == RuntimePlatform.OSXEditor && KeychainCertNames == null) {
                GetKeychainCertNames ();
            }

            GetLatestCommit ();

            titleContent = new GUIContent ("CineGame Build", IconTexture);
        }


        void OnDisable () {
            EditorApplication.hierarchyChanged -= OnHierarchyChange;
        }


        [MenuItem ("CineGame/Build")]
        internal static void Init () {
            if (instance == null) {
                instance = GetWindow<CineGameBuild> (ProgressBarTitle, typeof (CineGameBuild), typeof (CineGameTest));
            }
            instance.Focus ();

            if (!CineGameLogin.IsLoggedIn) {
                CineGameLogin.Init ();
            }
        }

        static void RepaintWindow () {
            if (instance != null) {
                EditorUtility.SetDirty (instance);
                instance.Repaint ();
            }
        }

        IEnumerator E_BuildSingleSceneForAllPlatforms () {
            var tmpDir = Application.temporaryCachePath + "/cinemabuild";
            string [] scenes = {
                UnityEngine.SceneManagement.SceneManager.GetActiveScene ().path,
            };

            uint rwxr_xr_x = 0x1ED;//Convert.ToUInt32 ("755", 8);

            var watch = new System.Diagnostics.Stopwatch ();
            watch.Start ();

            IsBuilding = true;

            buildProgress = 0f;
            float dpct = 1f / targets.Length;
            foreach (var target in targets) {
                buildTarget = target;
                if (BuildOnlyForLinux && target != BuildTarget.StandaloneLinux64) {
                    Debug.LogWarningFormat ("Skipping target {0} ...", PrettyPrintTarget (buildTarget));
                } else if (!HasMacOSBuildSupport && target == BuildTarget.StandaloneOSX) {
                    Debug.LogWarning ("MacOS build support not installed, skipping ...");
                } else if (!HasWinBuildSupport && target == BuildTarget.StandaloneWindows) {
                    Debug.LogWarning ("Windows build support not installed, skipping ...");
                } else {
                    if (target == BuildTarget.StandaloneLinux64 && HasLinuxIl2cppSupport && PlayerSettings.GetScriptingBackend (BuildTargetGroup.Standalone) != ScriptingImplementation.IL2CPP) {
                        Debug.Log ("IL2CPP build support installed for Linux, switching scripting backend ...");
                        PlayerSettings.SetScriptingBackend (BuildTargetGroup.Standalone, ScriptingImplementation.IL2CPP);
                    }

                    progressMessage = string.Format ("Building {0} ...", PrettyPrintTarget (buildTarget));
                    var platformName = buildTarget.ToString ();

                    //Make sure target dir is empty
                    try {
                        Directory.Delete (tmpDir, true);
                    } catch (Exception) {
                    }

                    var outPath = string.Format ("{0}/{1}_{2}", tmpDir, GameType, platformName);
                    Debug.Log ("Temp build path: " + outPath);

                    //Make sure GUI is updated before starting build
                    RepaintWindow ();
                    yield return null;
                    RepaintWindow ();
                    yield return null;

                    //Build player
                    var buildReport = BuildPipeline.BuildPlayer (scenes, outPath, buildTarget, buildOptions);
                    RepaintWindow ();
                    yield return null;

                    if (buildReport.summary.result != BuildResult.Succeeded) {
                        IsBuilding = false;
                        resultMessage = string.Format ("The build {0}", buildReport.summary.result == BuildResult.Cancelled ? "was cancelled" : "failed");
                    } else {

                        //Quickndirty hack for executable not having a filename extension on windows and linux builds (?!?!)
                        if (buildTarget == BuildTarget.StandaloneLinux64 || buildTarget == BuildTarget.StandaloneWindows) {
                            var newExePath = string.Format ("{0}.{1}", outPath, (buildTarget == BuildTarget.StandaloneWindows) ? "exe" : "x86_64");
                            try {
                                File.Move (outPath, newExePath);
                                if (Application.platform == RuntimePlatform.OSXEditor) {
                                    Debug.Log ("Setting executable permissions ...");
                                    sys_chmod (newExePath, rwxr_xr_x);
                                }
                            } catch (IOException e) {
                                Debug.LogWarningFormat ("Unable to rename {0} executable {1}: {2}", platformName, outPath, e.Message);
                            }
                        }

                        progressMessage = string.Format ("Signing {0} ...", PrettyPrintTarget (target));
                        RepaintWindow ();
                        yield return null;
                        RepaintWindow ();
                        yield return null;

                        if (!Codesign (outPath, target, delegate (string sMessage, float percent) {
                            if (EditorUtility.DisplayCancelableProgressBar (ProgressBarTitle, sMessage, 1f)) {
                                resultMessage = "Build canceled by user.";
                                IsBuilding = false;
                            }
                            RepaintWindow ();
                            return !IsBuilding;
                        })) {
                            Debug.LogError (string.Format ("Failed to sign build for {0}. See log for details. Did you forget to [brew install osslsigncode] ?", PrettyPrintTarget (target)));
                        }

                        /*if (PlayerSettings.GetScriptingBackend (BuildTargetGroup.Standalone) == ScriptingImplementation.IL2CPP) {
                            try {
                                Directory.Delete (outPath + "_BackUpThisFolder_ButDontShipItWithYourGame", recursive: true);
                            } catch (Exception ex) {
                                Debug.LogWarning ("Unable to delete IL2CPP debug info: " + ex.Message);
                            }
                        }*/

                        progressMessage = string.Format ("Compressing {0} ...", PrettyPrintTarget (target));
                        RepaintWindow ();
                        yield return null;
                        RepaintWindow ();
                        yield return null;

                        var spct = buildProgress;
                        var fakepct = 0f;
                        if (!CompressBuild (tmpDir, string.Format (GetOutputPathFormat (), GameType, platformName), delegate (string sMessage, float percent) {
                            percent = fakepct;
                            fakepct += (1f - fakepct) * .01f;
                            if (EditorUtility.DisplayCancelableProgressBar (ProgressBarTitle, sMessage, percent)) {
                                resultMessage = "Build canceled by user.";
                                IsBuilding = false;
                            }
                            buildProgress = spct + percent * dpct;
                            RepaintWindow ();
                            return !IsBuilding;
                        })) {
                            resultMessage = string.Format ("Compressing failed for {0} build. See log for details.", PrettyPrintTarget (target));
                            IsBuilding = false;
                        }
                        buildProgress = spct;

                        Directory.Delete (tmpDir, true);
                        EditorUtility.ClearProgressBar ();
                    }

                    if (!IsBuilding) {
                        break;
                    }
                }

                buildProgress += dpct;
            }

            if (!IsBuilding && !string.IsNullOrWhiteSpace (resultMessage)) {
                Debug.LogError (resultMessage);
            } else {
                IsBuilding = false;
                resultMessage = string.Format ("Build {0} succeeded in {1:##.000} seconds.", GameType, (float)watch.ElapsedMilliseconds / 1000f);
                Debug.Log (resultMessage);
                if (Application.platform == RuntimePlatform.OSXEditor) {
                    ExtractBuildReportFromEditorLog ();
                }
            }

            EditorUtility.ClearProgressBar ();
            RepaintWindow ();

            if (!string.IsNullOrEmpty (resultMessage)) {
                EditorUtility.DisplayDialog (ProgressBarTitle, resultMessage, "OK");
            }
        }

        IEnumerator E_UploadSingleSceneForAllPlatforms () {
            progressMessage = string.Format ("Uploading {0} to {1} ...", GameType, MarketSlug);
            RepaintWindow ();
            yield return null;

            //Try uploading all three builds until we succeed
            var cancelUpload = false;
            while (!cancelUpload) {
                var outputPathFormat = "../Builds/{0}_{1}.zip";
                var macPath = string.Format (outputPathFormat, GameType, BuildTarget.StandaloneOSX);
                var winPath = string.Format (outputPathFormat, GameType, BuildTarget.StandaloneWindows);
                var linuxPath = string.Format (outputPathFormat, GameType, BuildTarget.StandaloneLinux64);
                var mimeType = "application/zip";
                var form = new WWWForm ();
                if (File.Exists (macPath)) {
                    form.AddBinaryData ("mac", File.ReadAllBytes (macPath), Path.GetFileName (macPath), mimeType);
                }
                if (File.Exists (winPath)) {
                    form.AddBinaryData ("win", File.ReadAllBytes (winPath), Path.GetFileName (winPath), mimeType);
                }
                if (File.Exists (linuxPath)) {
                    form.AddBinaryData ("linux", File.ReadAllBytes (linuxPath), Path.GetFileName (linuxPath), mimeType);
                }
                form.AddField ("gameType", GameType);
                form.AddField ("market", CineGameLogin.MarketIdsAvailable [MarketIndex]);

                GetLatestCommit ();
                if (!string.IsNullOrEmpty (LatestBranch)) {
                    form.AddField ("commit-branch", LatestBranch);
                }
                if (!string.IsNullOrEmpty (LatestCommit)) {
                    form.AddField ("commit-hash", LatestCommit);
                }
                if (!string.IsNullOrEmpty (LatestAuthor)) {
                    form.AddField ("commit-author", LatestAuthor);
                }
                if (!string.IsNullOrEmpty (LatestCommitMessage)) {
                    form.AddField ("commit-msg", LatestCommitMessage);
                }
                if (!string.IsNullOrEmpty (LatestDiff)) {
                    form.AddField ("commit-diff", LatestDiff);
                }
                //Get build time from auto-generated assembly version
                var sdkVersion = Assembly.GetAssembly (typeof (CineGameSDK)).GetName ().Version;
                var sdkBuildDate = new DateTime (2000, 1, 1, 0, 0, 0 /*, DateTimeKind.Utc*/).Add (new TimeSpan (sdkVersion.Build, 0, 0, sdkVersion.Revision * 2));
                var sdkBuildTimeString = sdkBuildDate.ToString ("u");
                form.AddField ("sdk-version", sdkVersion.ToString ());
                form.AddField ("sdk-buildtime", sdkBuildTimeString);

                string submitURL;
                switch (CineGameLogin.LoginRegionIndex) {
                case 0:
                    submitURL = "https://api.staging.cinemataztic.com/v2/build-hook/upload";
                    break;
                case 1:
                    submitURL = "https://api.cinemataztic.com/v2/build-hook/upload";
                    break;
                case 2:
                    submitURL = "https://biospil.api.player.drf-1.cinemataztic.com/v2/build-hook/upload";
                    break;
                case 3:
                    submitURL = "https://wideeyemedia.ie.api.player.eu-2.cinemataztic.com/v2/build-hook/upload";
                    break;
                case 4:
                    submitURL = "https://itv.in.api.player.asia-1.cinemataztic.com/v2/build-hook/upload";
                    break;
                default:
                    submitURL = "https://api.staging.cinemataztic.com/v2/build-hook/upload";
                    break;
                }

                var headers = form.headers;
                headers ["Authorization"] = string.Format ("Bearer {0}", Configuration.CINEMATAZTIC_ACCESS_TOKEN);
                //headers["Keep-Alive"] = string.Format ("timeout={0}", UPLOAD_REQUEST_TIMEOUT);

                var watch = new System.Diagnostics.Stopwatch ();
                watch.Start ();

                var request = UnityWebRequest.Post (submitURL, form);
                var enHeaders = headers.GetEnumerator ();
                while (enHeaders.MoveNext ()) {
                    request.SetRequestHeader (enHeaders.Current.Key, enHeaders.Current.Value);
                }
                request.timeout = UPLOAD_REQUEST_TIMEOUT;
                request.useHttpContinue = false;
                var asyncOp = request.SendWebRequest ();

                var totalMB = form.data.Length / 1048576;
                while (!asyncOp.isDone && !cancelUpload) {
                    var p = request.uploadProgress;
                    if (EditorUtility.DisplayCancelableProgressBar (ProgressBarTitle, string.Format ("Uploading {0} of {1} MB ... {2}s", (int)(p * totalMB), totalMB, watch.ElapsedMilliseconds / 1000), p)) {
                        EditorUtility.ClearProgressBar ();
                        cancelUpload = true;
                        IsBuilding = false;
                        request.Dispose ();
                        request = null;
                    }
                    buildProgress = p;
                    RepaintWindow ();
                    yield return new WaitForSeconds (.25f);
                }
                EditorUtility.ClearProgressBar ();

                if (cancelUpload) {
                    resultMessage = "Upload canceled!";
                    Debug.Log (resultMessage);
                    break;
                } else {
                    if (request.result != UnityWebRequest.Result.Success) {
                        resultMessage = string.Format ("Error while uploading builds after {0}s: {1}", watch.ElapsedMilliseconds / 1000, request.error);
                        Debug.LogError (resultMessage);
                        Debug.LogError (request.downloadHandler.text);
                        if (!EditorUtility.DisplayDialog (ProgressBarTitle, string.Format ("{0}\n\nWant to try uploading again?", resultMessage), "Retry", "Cancel")) {
                            break;
                        }
                    } else {
                        var d = MiniJSON.Json.Deserialize (request.downloadHandler.text) as Dictionary<string, object>;
                        resultMessage = string.Format ("Uploaded {1} build #{2} ({0} MBs total) in {3:##.000}s", totalMB, (string)d ["title"], (int)(long)d ["build"], (float)watch.ElapsedMilliseconds / 1000f);
                        Debug.Log (resultMessage);
                        break;
                    }
                }
            }

            EditorUtility.ClearProgressBar ();
            IsBuilding = false;
            RepaintWindow ();
        }




        /// <summary>
        /// Codesign the specified build. Needs XCode command line tools (codesign for OSX) and homebrew osslsigncode for Windows
        /// </summary>
        /// <param name="pathToBuild">Path to build.</param>
        /// <param name="target">Target</param>
        /// <param name="progress">Progress callback whenever there is output from the signing command.</param>
        /// <returns><c>true</c> if build was signed.</returns>
        static bool Codesign (string pathToBuild, BuildTarget target, ExternalProcess.ProgressDelegate progress = null) {
            var isWindowsBuild = target == BuildTarget.StandaloneWindows || target == BuildTarget.StandaloneWindows64;
            if (target != BuildTarget.StandaloneOSX && !isWindowsBuild) {
                Debug.LogFormat ("Not codesigning for target {0}", target);
                return true;
            }
            if (isWindowsBuild && !File.Exists (CrtFilePath)) {
                Debug.LogWarning ("WARNING Windows build will not be signed. Users may be unable to run the build depending on their security settings.");
                return true;
            }
            if (target == BuildTarget.StandaloneOSX && Application.platform != RuntimePlatform.OSXEditor) {
                Debug.LogWarning ("WARNING OS X build can only be signed on an OS X system. You must do this manually.");
                return true;
            }
            string command;
            string arguments;
            if (target == BuildTarget.StandaloneOSX) {
                command = "codesign";
                arguments = $"-d -f --deep -s \"{KeychainCertNames [KeychainCertIndex]}\" {pathToBuild}.app";
            } else if (Application.platform == RuntimePlatform.OSXEditor) {
                //Sign windows build on an OS X system. Needs homebrew osslsigncode to be installed
                command = "/usr/local/bin/osslsigncode";
                arguments = $"sign -certs {CrtFilePath} -key {KeyFilePath} -n \"CinemaGame\" -i http://cinemataztic.com/ -in {pathToBuild}.exe -out {pathToBuild}.exe";
            } else if (Application.platform == RuntimePlatform.WindowsEditor) {
                // Sign windows build on a windows machine
                /*var pfxFilePath = Path.GetTempFileName ();
                command = "openssl";
                arguments = $"pkcs12 -inkey {KeyFilePath} -in {CrtFilePath} -export -out \"{pfxFilePath}\"";
                if (!SystemProcess.Run (command, arguments, null, progress)) {
                    Debug.LogError ("Failed to convert certificate and key files. Aborting codesigning Windows build");
                    return false;
                }*/
                command = "signtool";
                arguments = $"sign /f \"{CrtFilePath}\" /t http://timestamp.verisign.com/scripts/timestamp.dll \"{pathToBuild}\"";
            } else {
                return false;
            }
            return ExternalProcess.Run (command, arguments, null, progress);
        }


        /// <summary>
        /// Compress the specified build (entire folder). External 'zip' command is used
        /// </summary>
        /// <returns><c>true</c>, if build was compressed, <c>false</c> otherwise.</returns>
        /// <param name="sInDir">Folder to compress</param>
        /// <param name="sOutFile">Where to place the resulting archive</param>
        /// <param name="progress">Progress callback whenever there is output from zip command.</param>
        static bool CompressBuild (string sInDir, string sOutFile, ExternalProcess.ProgressDelegate progress = null) {
            var fullOutPath = Path.GetFullPath (sOutFile);
            Directory.CreateDirectory (Path.GetDirectoryName (fullOutPath));
            if (Application.platform == RuntimePlatform.WindowsEditor) {
                return ExternalProcess.Run ("powershell", string.Format ("Get-ChildItem . | where {{ $_.Name -notlike '*_BackUpThisFolder_ButDontShipItWithYourGame' }} | Compress-Archive -DestinationPath {0} -Update", fullOutPath), Path.GetFullPath (sInDir), progress);
            }
            return ExternalProcess.Run ("zip", string.Format ("-FSrD \"{0}\" . -x \"*_BackUpThisFolder_ButDontShipItWithYourGame/*\"", fullOutPath), Path.GetFullPath (sInDir), progress);
        }


        /// <summary>
        /// Determine the GameType from either currently loaded levels or the project.
		/// If no CineGameSettings are found, one is created. If different GameTypes are referenced in loaded scenes, error is displayed
        /// </summary>
        internal static string GetGameTypeFromSceneOrProject () {
            if (SceneManager.sceneCount == 0)
                return null;
            var sdks = FindObjectsOfType<CineGameSDK> ();
            CineGameSettings settings;
            if (sdks.Length == 0 || sdks.All (sdk => sdk.Settings == null)) {
                var assetGuids = AssetDatabase.FindAssets ("t:CineGameSettings");
                if (assetGuids.Length == 0) {
                    settings = CreateInstance<CineGameSettings> ();
                    settings.GameType = (sdks.Length != 0) ? sdks [0].GameType : CineGameLogin.GameTypesAvailable [0];
                    settings.MarketId = (sdks.Length != 0) ? sdks [0].Market : CineGameSDK.MarketID.DEMO_CineGame;
                    AssetDatabase.CreateAsset (settings, "Assets/CineGameSettings.asset");
                    AssetDatabase.SaveAssets ();
                    AssetDatabase.Refresh ();
                    EditorUtility.DisplayDialog ("CineGameSDK", "No CineGameSettings asset found in project, created new", "OK");
                    try {
                        EditorUtility.FocusProjectWindow ();
                    } catch (Exception ex) {
                        Debug.LogError ("Exception ignored in FocusProjectWindow: " + ex);
                    }
                    Selection.activeObject = settings;
                } else {
                    settings = AssetDatabase.LoadAssetAtPath<CineGameSettings> (AssetDatabase.GUIDToAssetPath (assetGuids [0]));
                }
                foreach (var sdk in sdks) {
                    var so = new SerializedObject (sdk);
                    so.FindProperty ("Settings").objectReferenceValue = settings;
                    so.ApplyModifiedProperties ();
                }
            } else {
                settings = sdks.First (sdk => sdk.Settings != null).Settings;
                var diff_found = false;
                foreach (var sdk in sdks) {
                    if (sdk.Settings != null && sdk.Settings.GameType != settings.GameType) {
                        diff_found = true;
                        break;
                    }
                }
                if (diff_found) {
                    Debug.LogError ("Multiple gametypes loaded at once: " + string.Join (",", sdks.Where (sdk => sdk.Settings != null).Select (sdk => sdk.Settings.GameType)));
                    EditorUtility.DisplayDialog ("CineGameSDK", "You cannot have multiple gametypes loaded at once.", "OK");
                }
            }
            GameType = settings.GameType;

            if (!CineGameLogin.IsSuperAdmin && CineGameLogin.GameTypesAvailable != null) {
                //Non-super-admins are only allowed a subset of gametypes to choose from
                GameTypeIndex = Array.IndexOf (CineGameLogin.GameTypesAvailable, GameType);
                if (GameTypeIndex == -1) {
                    GameTypeIndex = 0;
                    string newGameType = "N/A";
                    if (CineGameLogin.GameTypesAvailable.Length > 0) {
                        newGameType = CineGameLogin.GameTypesAvailable [GameTypeIndex];
                        EditorUtility.DisplayDialog ("Unavailable gametype", $"GameType was set to {GameType} but user has no access to this, so we force it to {newGameType}", "OK");
                    } else {
                        var msg = $"GameType was set to {GameType} but user has no gametypes available.";
                        Debug.LogWarning (msg);
                        EditorUtility.DisplayDialog ("No gametypes available", msg, "OK");
                    }
                    var so = new SerializedObject (settings);
                    so.FindProperty ("GameType").stringValue = newGameType;
                    so.ApplyModifiedProperties ();
                    AssetDatabase.SaveAssets ();
                    AssetDatabase.Refresh ();
                }
            }

            if (CineGameLogin.MarketIdsAvailable != null) {
                var currentMarketId = settings.MarketId;
                var _mi = Array.IndexOf (CineGameLogin.MarketIdsAvailable, currentMarketId);
                if (_mi == -1) {
                    _mi = 0;
                    var newMarketId = CineGameLogin.MarketIdsAvailable [0];
                    var so = new SerializedObject (settings);
                    so.FindProperty ("MarketId").stringValue = newMarketId;
                    so.ApplyModifiedProperties ();
                    AssetDatabase.SaveAssets ();
                    AssetDatabase.Refresh ();
                    var oldMarketSlug = currentMarketId != null ? CineGameSDK.MarketSlugMap.GetValueOrDefault (currentMarketId, "???") : "(null)";
                    var newMarketSlug = CineGameSDK.MarketSlugMap.GetValueOrDefault (newMarketId, "???");

                    var msg = $"Game fallback market was set to {oldMarketSlug} but user has no access to this, so we force it to {newMarketSlug}";
                    Debug.LogWarning (msg);
                    EditorUtility.DisplayDialog ("CineGameSDK", msg, "OK");
                }
                SetMarketIndex (_mi);
            } else {
                var msg = "No markets available. Contact admin.";
                Debug.LogError (msg);
                EditorUtility.DisplayDialog ("CineGameSDK", msg, "OK");
            }

            return GameType;
        }

        //[MenuItem("CinemaTaztic/Pack Textures")]
        static void PackSprites () {
            var textures = FindAllSpriteTextures ();
            Debug.LogFormat ("Checking packing tag on {0} sprite textures in loaded scenes ...", textures.Length);
            foreach (var tex in textures) {
                var ap = AssetDatabase.GetAssetPath (tex);
                if (!string.IsNullOrEmpty (ap)) {
                    var ti = (TextureImporter)AssetImporter.GetAtPath (ap);
                    if (ti != null && ti.spritePackingTag != GameType) {
                        Debug.LogFormat ("Changing packing tag of sprite texture {0} ...", tex.name);
                        ti.spritePackingTag = GameType;
                    }
                }
            }
            UnityEditor.Sprites.Packer.RebuildAtlasCacheIfNeeded (EditorUserBuildSettings.activeBuildTarget);
        }

        static Texture2D [] FindAllSpriteTextures () {
            var textures = new Dictionary<int, Texture2D> ();
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject> ();
            foreach (var go in allObjects) {
                /*
                            if (PrefabUtility.GetPrefabType (go) == PrefabType.PrefabInstance)
                            {
                                if (PrefabUtility.GetPrefabParent (go) == to)
                                {
                                    Debug.Log(string.Format("referenced by {0}, {1}", go.name, go.GetType()), go);
                                    referencedBy.Add(go);
                                }
                            }
                */
                var components = go.GetComponents<Component> ();
                for (int i = 0; i < components.Length; i++) {
                    var c = components [i];
                    if (!c)
                        continue;

                    var so = new SerializedObject (c);
                    var sp = so.GetIterator ();

                    while (sp.NextVisible (true)) {
                        if (sp.propertyType == SerializedPropertyType.ObjectReference) {
                            var sprite = sp.objectReferenceValue as Sprite;
                            if (sprite != null && sprite.texture != null) {
                                var hash = sprite.texture.GetHashCode ();
                                textures [hash] = sprite.texture;
                            }
                        }
                    }
                }
            }
            var ta = new Texture2D [textures.Count];
            textures.Values.CopyTo (ta, 0);
            return ta;
        }

        static string GetOutputPathFormat () {
            return "../Builds/{0}_{1}.zip";
        }

        static string GetLastBuildReportPath () {
            return "../Builds/buildreport.txt";
        }

        static string PrettyPrintTarget (BuildTarget target) {
            //Remove "Standalone" prefix
            return target.ToString ().Substring (10);
        }

        /// <summary>
        /// Extract latest build report from Editor.log.
        /// </summary>
        static void ExtractBuildReportFromEditorLog () {
            try {
                var logFileEditor = File.ReadAllText (Application.platform == RuntimePlatform.OSXEditor ?
                    Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), "Library/Logs/Unity/Editor.log") :
                    Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Unity/Editor/Editor.log"));
                var buildReportHeader = "-------------------------------------------------------------------------------\nBuild Report";
                var buildReportFooter = "-------------------------------------------------------------------------------\n";
                var idxOfBuildReport = logFileEditor.LastIndexOf (buildReportHeader);
                if (idxOfBuildReport >= 0) {
                    var buildReport = logFileEditor.Substring (idxOfBuildReport + buildReportHeader.Length);
                    var idxOfEndBuildReport = buildReport.IndexOf (buildReportFooter);
                    if (idxOfEndBuildReport >= 0) {
                        buildReport = buildReport.Substring (0, idxOfEndBuildReport + buildReportFooter.Length);
                    }

                    //Filter out assets taking up less than 0.0% build size
                    var sb = new System.Text.StringBuilder (buildReport.Length);
                    sb.AppendFormat ("Build report for {0}:\n{1}\n", GameType, DateTime.Now.ToString ("f"));
                    var reader = new StringReader (buildReport);
                    string line;
                    if (reader != null) {
                        while ((line = reader.ReadLine ()) != null) {
                            if (line.StartsWith ("Other Assets ")) {
                                line = line.Replace ("Other Assets ", "Other Assets (Movies, Fonts)");
                            }
                            if (line.IndexOf ("\t 0.0% Assets/") < 1) {
                                sb.AppendLine (line);
                            }
                        }
                    }
                    LastBuildReportString = sb.ToString ();

                    File.WriteAllText (GetLastBuildReportPath (), LastBuildReportString);

                    if (instance != null) {
                        instance.Focus ();
                        RepaintWindow ();
                    }
                } else {
                    throw new Exception ("Did not find build report in Editor.log");
                }
            } catch (Exception e) {
                Debug.LogErrorFormat ("Exception while extracting build report: {0}", e);
            }
        }

        /// <summary>
        /// Fills static properties with details of the latest commit on the current branch
        /// </summary>
        static void GetLatestCommit () {
            string parentDir = Path.Combine (Application.dataPath, "../");
            if (ExternalProcess.Run ("git", "log -1 --no-color --decorate", parentDir, delegate (string message, float pct) {
                if (message.StartsWith ("commit ")) {
                    var str = message.Substring (7);
                    var strHashEnd = str.IndexOf (' ');
                    LatestCommit = str.Substring (0, strHashEnd > 0 ? strHashEnd : str.Length);
                    var indexOfBranch = str.IndexOf ("(HEAD -> ") + 9;
                    LatestBranch = str.Substring (indexOfBranch, str.Length - indexOfBranch - 1);
                } else if (message.StartsWith ("Author: ")) {
                    LatestAuthor = message.Substring (8);
                } else if (message.Length > 1) {
                    LatestCommitMessage = message;
                }
                return false;
            })) {
                var sb = new System.Text.StringBuilder ();
                ExternalProcess.Run ("git", "--no-pager diff -w -D --no-color --staged", parentDir, delegate (string message, float pct) {
                    sb.AppendLine (message);
                    return false;
                });
                ExternalProcess.Run ("git", "--no-pager diff -w -D --no-color", parentDir, delegate (string message, float pct) {
                    sb.AppendLine (message);
                    return false;
                });
                LatestDiff = sb.ToString ();
            }
        }

        /// <summary>
		/// Populate dropdown array of valid code signing identities
		/// </summary>
        static void GetKeychainCertNames () {
            var l = new List<string> ();
            var regex = new Regex ("\"([^\"]+)\"", RegexOptions.Compiled);
            ExternalProcess.Run ("security", "find-identity -p basic -v", null, (message, pct) => {
                var m = regex.Match (message);
                if (m.Success) {
                    l.Add (m.Groups [1].Value);
                }
                return false;
            });
            KeychainCertNames = l.ToArray ();
            KeychainCertIndex = Mathf.Clamp (Array.IndexOf (KeychainCertNames, EditorPrefs.GetString ("CinemaBuildKeychainSearchPattern")), 0, KeychainCertNames.Length - 1);
        }
    }


    internal static class AssemblyHelpers {
        /// <summary>
		/// Load embedded image resource into Unity Texture2D
		/// </summary>
        internal static Texture LoadTextureFromAssembly (string nameSpace, string filename) {
            Texture2D newTex = new Texture2D (1, 1);
            Assembly _assembly = Assembly.GetExecutingAssembly ();

            Stream _imageStream;
            var fullyQualifiedName = nameSpace + "." + filename;
            try {
                _imageStream = _assembly.GetManifestResourceStream (fullyQualifiedName);
            } catch {
                Debug.LogWarning ("Unable to find " + fullyQualifiedName + " resource in " + _assembly.FullName);
                return newTex;
            }
            if (_imageStream == null) {
                Debug.LogWarning ("Unable to find " + fullyQualifiedName + " resource in " + _assembly.FullName);
                return newTex;
            }
            byte [] imageData = new byte [_imageStream.Length];
            _imageStream.Read (imageData, 0, (int)_imageStream.Length);
            _imageStream.Close ();

            if (!ImageConversion.LoadImage (newTex, imageData, true))
                Debug.LogWarning ("Unable to Load " + fullyQualifiedName + " resource from " + _assembly.FullName);
            return newTex;
        }
    }
}

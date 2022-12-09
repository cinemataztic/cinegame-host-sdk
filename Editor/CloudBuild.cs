using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace CineGame.Host.Editor {

	public class CloudBuild : EditorWindow {

		static CloudBuild instance;

		static SpriteMetaData [] ProgressMetaData;
		static int ProgressCurrentMetaIndex;
		static int LastTimeUpdate;
		static double LastTimeLoadedTargets;
		static Texture2D ProgressTexture;

		static Texture2D DownloadIconTexture, BuildIconTexture, InstallIconTexture, LogIconTexture, CancelIconTexture;
		static readonly Color RedColor = new Color (.937f, .231f, .192f);
		static readonly Color YellowColor = new Color (.937f, .441f, .192f);
		static readonly Color GreenColor = new Color (.134f, .905f, .444f);
		static readonly Color BlueColor = new Color (.11f, .441f, .911f);

		/// <summary>
		/// Guids of required resources
		/// </summary>
		static class Guids {
			public static string ProgressTexture = "1e7e3d3b79f6e4bc59f7503783aa4eef";
			public static string DownloadIconTexture = "b70af5f80aae94dd889771188c9227b9";
			public static string InstallIconTexture = "50108337a0e954666ac01892eb386244";
			public static string LogIconTexture = "eaca1286a053340f79e8861208376ab9";
			public static string BuildIconTexture = "fb51f571adf424ca083dcdd700fecc95";
			public static string CancelIconTexture = "70d1b25736b2847cd8a01350fa6e2173";
		}

		/// <summary>
		/// Map UcbPlatform to BuildTarget enum
		/// </summary>
		static readonly Dictionary<UcbPlatform, BuildTarget> BuildPlatformTargetMap = new Dictionary<UcbPlatform, BuildTarget> {
			{ UcbPlatform.android,  BuildTarget.Android },
			{ UcbPlatform.ios,      BuildTarget.iOS },
			{ UcbPlatform.standalonelinux64, BuildTarget.StandaloneLinux64 },
			{ UcbPlatform.standaloneosxuniversal, BuildTarget.StandaloneOSX },
			{ UcbPlatform.standalonewindows64, BuildTarget.StandaloneWindows64 },
		};

		EditorCoroutine StartCoroutine (IEnumerator coroutine) {
			return EditorCoroutineUtility.StartCoroutine (coroutine, this);
		}

		void StopCoroutine (EditorCoroutine coroutine) {
			EditorCoroutineUtility.StopCoroutine (coroutine);
		}

		static Uri CloudBuildBaseUri {
			get { return new Uri ($"https://build-api.cloud.unity3d.com/api/v1/orgs/{CloudProjectSettings.organizationId}/projects/{CloudProjectSettings.projectId}/"); }
		}

		public void OnEnable () {
			var progressAssetPath = AssetDatabase.GUIDToAssetPath (Guids.ProgressTexture);
			var ti = AssetImporter.GetAtPath (progressAssetPath) as TextureImporter;
			ProgressMetaData = ti.spritesheet;
			ProgressTexture = AssetDatabase.LoadAssetAtPath<Texture2D> (progressAssetPath);
			ProgressCurrentMetaIndex = 0;

			DownloadIconTexture = AssetDatabase.LoadAssetAtPath<Texture2D> (AssetDatabase.GUIDToAssetPath (Guids.DownloadIconTexture));
			InstallIconTexture = AssetDatabase.LoadAssetAtPath<Texture2D> (AssetDatabase.GUIDToAssetPath (Guids.InstallIconTexture));
			LogIconTexture = AssetDatabase.LoadAssetAtPath<Texture2D> (AssetDatabase.GUIDToAssetPath (Guids.LogIconTexture));
			BuildIconTexture = AssetDatabase.LoadAssetAtPath<Texture2D> (AssetDatabase.GUIDToAssetPath (Guids.BuildIconTexture));
			CancelIconTexture = AssetDatabase.LoadAssetAtPath<Texture2D> (AssetDatabase.GUIDToAssetPath (Guids.CancelIconTexture));

			titleContent = new GUIContent ("Cloud Build", CineGameBuild.IconTexture);
		}

		void Update () {
			var tf = (int)(EditorApplication.timeSinceStartup * 10.0);
			if (tf > LastTimeUpdate) {
				LastTimeUpdate = tf;
				ProgressCurrentMetaIndex = (ProgressCurrentMetaIndex + 1) % 30;
				Repaint ();
			}
		}

		class GridView {
			readonly int [] columnWidths;
			int rowHeight;
			Rect currentRow;
			int lastX;
			int col_ctr = 0;

			/// <summary>
			/// Total width of table
			/// </summary>
			int totalWidth;

			public GridView (int rowHeight, params int [] columnWidths) {
				this.columnWidths = columnWidths;
				this.rowHeight = rowHeight;
				totalWidth = columnWidths.Sum ();
			}

			/// <summary>
			/// Reset position to top left
			/// </summary>
			public void Begin () {
				col_ctr = 0;
				lastX = 0;
			}

			public Rect NextCell () {
				if (col_ctr >= columnWidths.Length) {
					col_ctr = 0;
					lastX = 0;
				}
				if (col_ctr == 0) {
					currentRow = GUILayoutUtility.GetRect (totalWidth, rowHeight);
				}
				var _w = columnWidths [col_ctr];
				var ret = new Rect (currentRow) { x = lastX, width = _w };
				lastX += _w;
				col_ctr += 1;
				return ret;
			}

			public void SetColumnWidth (int columnIndex, int width) {
				columnWidths [columnIndex] = width;
			}

			public void SetRowHeight (int height) {
				rowHeight = height;
			}

			public void ExpandColumn (int columnIndex, int totalWidth) {
				var sumOthers = 0;
				for (int i = 0; i < columnWidths.Length; i++) {
					if (i == columnIndex)
						continue;
					sumOthers += columnWidths [i];
				}
				columnWidths [columnIndex] = totalWidth - sumOthers;
			}
		}

		static Vector2 BuildTargetsScrollPosition = Vector2.zero;
		static GridView BuildTargetsGridView = new GridView (50, 200, 50, 100, 100);

		UcbBuildTarget [] BuildTargets;

		readonly Dictionary<string, UcbBuild> LatestBuild = new Dictionary<string, UcbBuild> ();
		static bool IsLoadingTargets, NetworkError;
		static string UcbApiKey;

		void OnGUI () {
			if (Application.internetReachability == NetworkReachability.NotReachable) {
				EditorGUILayout.Space ();
				EditorGUILayout.HelpBox ("Internet not reachable.", MessageType.Error);
				return;
			}

			var labelStyle = GUI.skin.GetStyle ("Label");
			var buttonStyle = GUI.skin.GetStyle ("Button");
			buttonStyle.padding = new RectOffset (2, 2, 2, 2);

			if (string.IsNullOrWhiteSpace (CloudProjectSettings.organizationId) || string.IsNullOrWhiteSpace (CloudProjectSettings.projectId)) {
				EditorGUILayout.HelpBox ("Please set up Cloud Build in Unity Gaming Services", MessageType.Error);
				return;
			}

			if (NetworkError) {
				EditorGUILayout.HelpBox ("NETWORK ERROR :(", MessageType.Error);
				if (GUILayout.Button ("Reload")) {
					NetworkError = false;
					BuildTargets = null;
				} else {
					return;
				}
			}

			//Get TexCoords for animated progress texture
			var rtProgress = ProgressMetaData [ProgressCurrentMetaIndex].rect;
			rtProgress = new Rect (rtProgress.x / ProgressTexture.width, rtProgress.y / ProgressTexture.height, rtProgress.width / ProgressTexture.width, rtProgress.height / ProgressTexture.height);

			if (BuildTargets == null || (EditorApplication.timeSinceStartup - LastTimeLoadedTargets) > 3600.0) {
				if (!IsLoadingTargets) {
					if (string.IsNullOrWhiteSpace (EditorPrefs.GetString ("UcbApiKey"))) {
						UcbApiKey = EditorGUILayout.PasswordField ("API Key:", UcbApiKey);
						if (!GUILayout.Button ("Authenticate")) {
							return;
						}
						EditorPrefs.SetString ("UcbApiKey", UcbApiKey);
					}
					GetCloudBuildTargets ();
				}
				_ = EditorGUILayout.BeginVertical (GUILayout.ExpandHeight (true));
				GUILayout.FlexibleSpace ();
				labelStyle.alignment = TextAnchor.MiddleCenter;
				labelStyle.fontStyle = FontStyle.Bold;
				GUILayout.Label ("Loading targets ...");
				EditorGUILayout.Space ();
				var rtTexture = GUILayoutUtility.GetRect (rtProgress.width * ProgressTexture.width, rtProgress.height * ProgressTexture.height);
				rtTexture.x = (rtTexture.width - rtTexture.height) / 2;
				rtTexture.width = rtTexture.height;
				GUI.DrawTextureWithTexCoords (rtTexture, ProgressTexture, rtProgress, true);
				GUILayout.FlexibleSpace ();
				EditorGUILayout.EndVertical ();
				return;
			}

			//centeredStyle.alignment = TextAnchor.UpperCenter;
			//centeredStyle.fontStyle = FontStyle.Bold;

			if (BuildTargets != null && BuildTargets.Length != 0) {
				Rect rScroll = EditorGUILayout.BeginVertical (GUILayout.ExpandHeight (true));
				BuildTargetsScrollPosition = EditorGUILayout.BeginScrollView (BuildTargetsScrollPosition, false, true, GUILayout.ExpandHeight (true));
				BuildTargetsGridView.SetRowHeight ((int)(labelStyle.lineHeight * 2f));
				BuildTargetsGridView.ExpandColumn (0, (int)rScroll.width);
				BuildTargetsGridView.Begin ();
				labelStyle.alignment = TextAnchor.MiddleLeft;
				labelStyle.fontStyle = FontStyle.Bold;
				GUI.Label (BuildTargetsGridView.NextCell (), "Target Name");
				GUI.Label (BuildTargetsGridView.NextCell (), "Build");
				GUI.Label (BuildTargetsGridView.NextCell (), "Status");
				GUI.Label (BuildTargetsGridView.NextCell (), "Action");
				labelStyle.fontStyle = FontStyle.Normal;
				BuildTargetsGridView.SetRowHeight ((int)(labelStyle.lineHeight * 1.2f));

				foreach (var target in BuildTargets) {
					GUI.Label (BuildTargetsGridView.NextCell (), new GUIContent (target.name, target.buildtargetid));
					if (!LatestBuild.TryGetValue (target.buildtargetid, out UcbBuild build)) {
						LatestBuild [target.buildtargetid] = new UcbBuild {
							buildStatus = UcbBuildStatus.fetching,
						};
						GetLatestBuildForTarget (target.buildtargetid);
						GUI.Label (BuildTargetsGridView.NextCell (), "…");
						GUI.Label (BuildTargetsGridView.NextCell (), "…");
						_ = BuildTargetsGridView.NextCell ();
					} else if (build.buildStatus == UcbBuildStatus.queueing || build.buildStatus == UcbBuildStatus.fetching) {
						GUI.Label (BuildTargetsGridView.NextCell (), "…");
						var rt = BuildTargetsGridView.NextCell ();
						var rtTexture = new Rect (rt);
						rtTexture.width = rtTexture.height;
						GUI.DrawTextureWithTexCoords (rtTexture, ProgressTexture, rtProgress, true);
						_ = BuildTargetsGridView.NextCell ();
					} else if (build.buildStatus != UcbBuildStatus.failure && build.buildStatus != UcbBuildStatus.success && build.buildStatus != UcbBuildStatus.unknown && build.buildStatus != UcbBuildStatus.canceled) {
						GUI.Label (BuildTargetsGridView.NextCell (), new GUIContent (build.build.ToString (), NiceTime (build.created) + (string.IsNullOrEmpty (build.lastBuiltRevision) ? string.Empty : "\n#" + build.lastBuiltRevision.Truncate (9))));

						var rt = BuildTargetsGridView.NextCell ();
						var rtTexture = new Rect (rt);
						rtTexture.width = rtTexture.height;
						GUI.DrawTextureWithTexCoords (rtTexture, ProgressTexture, rtProgress, true);

						rt.x += rtTexture.width;
						rt.width -= rtTexture.width;
						var statusString = build.buildStatus.ToString ();
						if (build.buildStatus != UcbBuildStatus.started) {
							GUI.Label (rt, char.ToUpper (statusString [0]) + statusString.Substring (1));
						} else {
							GUI.Label (rt, DateTime.UtcNow.Subtract (build.created).ToString ("c"));
						}

						if (build.buildStatus != UcbBuildStatus.canceling) {
							rt = BuildTargetsGridView.NextCell ();
							var rt1 = new Rect (rt.x, rt.y, rt.height, rt.height);
							if (GUI.Button (rt1, new GUIContent (CancelIconTexture, $"Cancel {build.buildTargetName} #{build.build}"))) {
								CancelBuild (build.buildtargetid);
							}
						} else {
							_ = BuildTargetsGridView.NextCell ();
						}
					} else if (build.buildStatus == UcbBuildStatus.unknown) {
						//Not built yet, just show build button
						_ = BuildTargetsGridView.NextCell ();
						_ = BuildTargetsGridView.NextCell ();
						var rt = BuildTargetsGridView.NextCell ();
						var rt1 = new Rect (rt.x, rt.y, rt.height, rt.height);
						if (GUI.Button (rt1, new GUIContent (BuildIconTexture, $"Build {target.name}"))) {
							CreateBuild (target);
						}
					} else {
						Rect rt, rt1, rt2, rt3;
						var oldColor = GUI.contentColor;
						var bs = build.buildStatus;
						var failedCanceledText = bs == UcbBuildStatus.failure ? "failed" : "canceled";
						GUI.contentColor = labelStyle.normal.textColor = bs == UcbBuildStatus.canceled ? YellowColor : (bs == UcbBuildStatus.failure ? RedColor : Color.white);
						GUI.Label (BuildTargetsGridView.NextCell (), new GUIContent (build.build.ToString (), NiceTime (build.created) + (string.IsNullOrEmpty (build.lastBuiltRevision) ? string.Empty : "\n#" + build.lastBuiltRevision.Truncate (9))));
						if (build.buildStatus == UcbBuildStatus.success) {
							rt = BuildTargetsGridView.NextCell ();
							rt1 = new Rect (rt.x, rt.y, rt.height, rt.height);
							rt2 = new Rect (rt.x + rt.height, rt.y, rt.height, rt.height);
							rt3 = new Rect (rt.x + rt.height * 2, rt.y, rt.height, rt.height);

							if (GUI.Button (rt1, new GUIContent (LogIconTexture, $"Download log for {build.buildTargetName} #{build.build}"))) {
								StartCoroutine (E_DownloadLogForBuild (build));
							}
							if (GUI.Button (rt2, new GUIContent (DownloadIconTexture, $"Download {build.buildTargetName} #{build.build}"))) {
								Download (build);
							}
							if (build.platform == UcbPlatform.standalonelinux64 && GUI.Button (rt3, new GUIContent (InstallIconTexture, $"Test {build.buildTargetName} #{build.build} now on LAN machine"))) {
								Install (build);
							}
						} else {
							//var statusString = build.buildStatus.ToString ();
							rt = BuildTargetsGridView.NextCell ();
							rt1 = new Rect (rt.x, rt.y, rt.height, rt.height);
							var downloadLogText = $"Download log for {failedCanceledText} {build.buildTargetName} #{build.build}";
							if (GUI.Button (rt1, new GUIContent (LogIconTexture, downloadLogText))) {
								StartCoroutine (E_DownloadLogForBuild (build));
							}
						}
						GUI.contentColor = labelStyle.normal.textColor = oldColor;
						rt = BuildTargetsGridView.NextCell ();
						rt1 = new Rect (rt.x, rt.y, rt.height, rt.height);
						if (GUI.Button (rt1, new GUIContent (BuildIconTexture, $"Rebuild {build.buildTargetName}"))) {
							CreateBuild (target);
						}
					}
				}
				EditorGUILayout.EndScrollView ();
				EditorGUILayout.EndVertical ();
			}
		}

		/// <summary>
		/// Formats datetime into a nice, human-readable 24H timestamp (eg Yesterday at 13:00)
		/// </summary>
		string NiceTime (DateTime dtUtc) {
			var dtLocal = TimeZoneInfo.ConvertTimeFromUtc (dtUtc, TimeZoneInfo.Local);
			var today = DateTime.Now.Date;
			if (dtLocal.Date == today) {
				return $"Today at {dtLocal:HH:mm}";
			}
			if (dtLocal.AddDays (1.0) > today) {
				return $"Yesterday at {dtLocal:HH:mm}";
			}
			if (dtLocal.AddDays (7.0) > today) {
				return $"{dtLocal:dddd} at {dtLocal:HH:mm}";
			}
			if (dtLocal.AddDays (365.0) > today) {
				return $"{dtLocal:M} at {dtLocal:HH:mm}";
			}
			return $"{dtLocal:M} {dtLocal:yyyy}";
		}

		[MenuItem ("CineGame/Cloud Build")]
		public static void Init () {
			if (instance == null) {
				instance = GetWindow<CloudBuild> ("Cloud Build", typeof (CineGameLogin), typeof (CineGameTest), typeof (CineGameBuild));
			}
			instance.Focus ();
		}

		internal enum UcbPlatform {
			ios,
			android,
			webgl,
			standaloneosxuniversal,
			standalonewindows64,
			standalonelinux64,
		}

		enum UcbBuildStatus {
			unknown,
			fetching,   //Internal, never returned by UCB
			queueing,   //Internal, never returned by UCB
			canceling,  //Internal, never returned by UCB
			created,
			queued,
			sentToBuilder,
			started,
			restarted,
			success,
			failure,
			canceled,
		}

		enum UcbFileType {
			AAB,
			APK,
			IPA,
			ZIP,
		}

		internal enum UcbScmType {
			git,
			svn,
			p4,
			hg,
			collab,
			oauth,
			plastic,
		}

		internal class UcbBuildTarget {
			public class Settings {
				public class Scm {
					[JsonConverter (typeof (StringEnumConverter))]
					public UcbScmType type;
					public string branch;
					public string subdirectory;
				}
				public class Platform {
					public string bundleId;
					[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
					public string xcodeVersion; //eg "xcode13_0_0"
				}
				public class BuildSchedule {
					public bool isEnabled;
					public string repeatCycle; //eg "once"
					public bool cleanBuild;
				}
				public class Advanced {
					public class Xcode {
						public bool useArchiveAndExport;
						public string customFastlaneConfigPath; //eg "Assets/ucb_xcode_fastlane.json"
						public bool shouldNotarize;
					}
					public class Android {
						public bool buildAppBundle;
						public bool buildAssetPacks;
					}
					public class Unity {
						public class PlayerExporter {
							[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
							public string [] sceneList;
							[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
							public string [] buildOptions;  //not sure if string or other type of array?
							public bool export;
						}
						public class BuildSettings {
							[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
							public string androidBuildSystem; //eg "gradle"
						}
						[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
						public string preExportMethod; // eg "CinemaMobileBuild.SwitchRegionToGermany"
						[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
						public string postExportMethod;
						[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
						public string preBuildScript;
						[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
						public string postBuildScript;
						public bool preBuildScriptFailsBuild;
						public bool postBuildScriptFailsBuild;
						[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
						public string scriptingDefineSymbols; //eg "UNITY_CLOUD_BUILD; HUAWEI_BUILD"
						[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
						public PlayerExporter playerExporter;
						[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
						public BuildSettings editorUserBuildSettings;
					}
					[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
					public Xcode xcode;
					[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
					public Android android;
					public Unity unity;
				}
				public bool autoBuild;
				[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
				public string unityVersion; //eg "2019_4_34f1"
				public bool autoDetectUnityVersion;
				public bool fallbackPatchVersion;
				public bool ccdEnabled;
				public bool ccdStripRemotePath;
				public bool ccdPreserveBucket;
				public bool ccdCreateRelease;
				[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
				public string executablename;
				[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
				public Scm scm;
				public Platform platform;
				[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
				public BuildSchedule buildSchedule;
				public bool autoBuildCancellation;
				public bool gcpBetaOptIn;
				public bool gcpOptOut;
				public Advanced advanced;
			}
			public class Credentials {
				public class Signing {
					public string credentialid;
				}
				public Signing signing;
			}
			public string name;
			[JsonConverter (typeof (StringEnumConverter))]
			public UcbPlatform platform;
			[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
			public string buildtargetid;
			public bool enabled;
			public Settings settings;
			[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
			public Credentials credentials;
		}

		class UcbBuild {
			public class Links {
				public class Link {
					public class Meta {
						[JsonConverter (typeof (StringEnumConverter))]
						public UcbFileType type;
					}
					public string href;
					public Meta meta;
				}
				public Link download_primary;
			}
			public class UcbBuildReport {
				public int errors;
				public int warnings;
			}
			public bool IsUpdating () {
				return buildStatus == UcbBuildStatus.created
					|| buildStatus == UcbBuildStatus.queued
					|| buildStatus == UcbBuildStatus.sentToBuilder
					|| buildStatus == UcbBuildStatus.started
					|| buildStatus == UcbBuildStatus.restarted;
			}
			public int build;
			public string buildtargetid;
			public string buildTargetName;
			[JsonConverter (typeof (StringEnumConverter))]
			public UcbBuildStatus buildStatus;
			public UcbPlatform platform;
			public DateTime created;
			public Links links;
			public UcbBuildReport buildReport;
			public string lastBuiltRevision;
		}

		class UcbHook {
			public enum UcbHookType {
				web,
				slack,
			}
			public enum UcbHookEvent {
				ProjectBuildQueued,
				ProjectBuildStarted,
				ProjectBuildRestarted,
				ProjectBuildSuccess,
				ProjectBuildFailure,
			}
			public enum UcbHookEncoding {
				json,
				form,
			}
			public class UcbHookConfig {
				[JsonConverter (typeof (StringEnumConverter))]
				public UcbHookEncoding encoding;
				public bool sslVerify;
				public string url;
			}
			[JsonConverter (typeof (StringEnumConverter))]
			public UcbHookType hookType;
			[JsonProperty (ItemConverterType=typeof (StringEnumConverter))]
			public UcbHookEvent [] events;
			public UcbHookConfig config;
			[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
			public bool? active;
			[JsonProperty (NullValueHandling = NullValueHandling.Ignore)]
			public string id;
		}

		void GetLatestBuildForTarget (string targetid) {
			StartCoroutine (E_GetLatestBuildForTarget (targetid));
		}

		IEnumerator E_GetLatestBuildForTarget (string targetid) {
			UcbBuild latestBuild = null;
			string responseText;
			using (var request = UnityWebRequest.Get (new Uri (CloudBuildBaseUri, "buildtargets/" + targetid + "/builds?per_page=1"))) {
				request.SetRequestHeader ("Authorization", "Basic " + EditorPrefs.GetString ("UcbApiKey"));
				yield return request.SendWebRequest ();
				if (request.result != UnityWebRequest.Result.Success) {
					NetworkError = true;
					yield break;
				}
				responseText = request.downloadHandler.text;
				var builds = JsonConvert.DeserializeObject<UcbBuild []> (responseText);
				latestBuild = (builds != null && builds.Length != 0) ? builds [0] : null;
			}
			if (latestBuild != null) {
				LatestBuild [targetid] = latestBuild;
				Repaint ();
				if (latestBuild.IsUpdating ()) {
					EnsurePolling (targetid);
				}/* else if (latestBuild.buildStatus == UcbBuildStatus.failure) {
				yield return StartCoroutine (E_DownloadLogForBuild (latestBuild));
			}*/
			} else {
				//no latest build-- target has not been built yet
				LatestBuild [targetid].buildStatus = UcbBuildStatus.unknown;
			}
		}

		void GetCloudBuildTargets () {
			if (!IsLoadingTargets) {
				IsLoadingTargets = true;
				StartCoroutine (E_GetCloudBuildTargets ());
			}
		}

		IEnumerator E_GetCloudBuildTargets () {
			LastTimeLoadedTargets = EditorApplication.timeSinceStartup;
			IsLoadingTargets = true;
			BuildTargets = null;
			LatestBuild.Clear ();
			using (var request = UnityWebRequest.Get (new Uri (CloudBuildBaseUri, "buildtargets"))) {
				request.SetRequestHeader ("Authorization", "Basic " + EditorPrefs.GetString ("UcbApiKey"));
				yield return request.SendWebRequest ();
				IsLoadingTargets = false;
				if (request.result != UnityWebRequest.Result.Success) {
					NetworkError = true;
					Debug.LogError ("Error occurred while loading targets: " + request.error);
					yield break;
				}
				var resp = request.downloadHandler.text;
				BuildTargets = JsonConvert.DeserializeObject<UcbBuildTarget []> (resp).OrderBy (b => b.name).ToArray ();
				Debug.Log ($"Loaded {BuildTargets.Length} targets.");
				Repaint ();
			}
		}

		void UpdateTargetInList (UcbBuildTarget target) {
			for (int i = 0; i < BuildTargets.Length; i++) {
				if (BuildTargets [i].buildtargetid == target.buildtargetid) {
					BuildTargets [i] = target;
					break;
				}
			}
		}

		/// <summary>
		/// Get extended info about build target
		/// </summary>
		void GetBuildTargetDetails (string targetid) {
			StartCoroutine (E_GetBuildTargetDetails (targetid));
		}

		IEnumerator E_GetBuildTargetDetails (string targetid) {
			using (var request = UnityWebRequest.Get (new Uri (CloudBuildBaseUri, "buildtargets/" + targetid))) {
				request.SetRequestHeader ("Authorization", "Basic " + EditorPrefs.GetString ("UcbApiKey"));
				yield return request.SendWebRequest ();
				if (request.result != UnityWebRequest.Result.Success) {
					Debug.LogError ("Error while getting build target details: " + request.error);
				} else {
					UpdateTargetInList (JsonConvert.DeserializeObject<UcbBuildTarget> (request.downloadHandler.text));
				}
				Repaint ();
			}
		}

		void UpdateBuildTarget (UcbBuildTarget target) {
			StartCoroutine (E_UpdateBuildTarget (target));
		}

		IEnumerator E_UpdateBuildTarget (UcbBuildTarget target) {
			var jsonReq = JsonConvert.SerializeObject (target);
			Debug.Log ("Updating target " + target.buildtargetid + ":\n" + jsonReq);
			using (var request = new UnityWebRequest (
								new Uri (CloudBuildBaseUri, "buildtargets/" + target.buildtargetid),
								"PUT",
								new DownloadHandlerBuffer (),
								new UploadHandlerRaw (Encoding.UTF8.GetBytes (jsonReq))
							)) {
				request.SetRequestHeader ("Authorization", "Basic " + EditorPrefs.GetString ("UcbApiKey"));
				request.uploadHandler.contentType = "application/json; charset=utf-8";
				yield return request.SendWebRequest ();
				if (request.result != UnityWebRequest.Result.Success) {
					Debug.LogError ($"Failed to update target {target.buildtargetid}: {request.error}");
				} else {
					Debug.Log ($"Target {target.buildtargetid} updated succesfully");
					UpdateTargetInList (JsonConvert.DeserializeObject<UcbBuildTarget> (request.downloadHandler.text));
				}
				Repaint ();
			}
		}

		void CreateBuild (UcbBuildTarget target, bool confirm = true) {
			if (!confirm || EditorUtility.DisplayDialog (titleContent.text, $"Create new build for {target.name} ?", "OK", "Cancel")) {
				Debug.Log ("Creating / queueing new build for target " + target.buildtargetid);
				StartCoroutine (E_CreateBuild (target));
			}
		}

		IEnumerator E_CreateBuild (UcbBuildTarget target, bool clean = false) {
			LatestBuild [target.buildtargetid] = new UcbBuild {
				buildStatus = UcbBuildStatus.queueing,
			};
			Repaint ();

			for (; ; ) {
				using (var request = new UnityWebRequest (
									new Uri (CloudBuildBaseUri, "buildtargets/" + target.buildtargetid + "/builds"),
									"POST",
									new DownloadHandlerBuffer (),
									new UploadHandlerRaw (Encoding.UTF8.GetBytes ("{\"clean\":" + clean.ToString ().ToLower () + ",\"delay\":0,\"commit\":\"\",\"headless\":false}"))
								)) {
					request.SetRequestHeader ("Authorization", "Basic " + EditorPrefs.GetString ("UcbApiKey"));
					request.uploadHandler.contentType = "application/json; charset=utf-8";
					yield return request.SendWebRequest ();
					if (request.result == UnityWebRequest.Result.Success) {
						var responseStr = request.downloadHandler.text;
						var builds = JsonConvert.DeserializeObject<UcbBuild []> (responseStr);
						var latestBuild = (builds != null && builds.Length != 0) ? builds [0] : null;
						if (latestBuild != null) {
							LatestBuild [target.buildtargetid] = latestBuild;
							Debug.Log ($"Created new build #{latestBuild.build} for {target.buildtargetid} succesfully: {responseStr}");
							EnsurePolling (target.buildtargetid);
							break;
						}
					}
					var msg = $"Failed to create new build for {target.buildtargetid}: {request.error}";
					Debug.LogError (msg);
					if (!EditorUtility.DisplayDialog ("Cloud Build", msg + "\n\nTry again?", "OK", "Cancel")) {
						LatestBuild.Remove (target.buildtargetid);
						yield break;
					}
				}
			}

			/*if (target.platform == UcbPlatform.standalonelinux64) {
				// We automatically upload Linux builds to cinemataztic player api via a webhook integration in UCB
				var buildNumber = LatestBuild [target.buildtargetid].build;

				Debug.Log ($"Creating share for {target.buildtargetid} build {buildNumber} ...");

				for (; ; ) {
					using (var request = UnityWebRequest.Post (new Uri (CloudBuildBaseUri, "buildtargets/" + target.buildtargetid + "/builds/" + buildNumber + "/share"), string.Empty)) {
						request.SetRequestHeader ("Authorization", "Basic " + EditorPrefs.GetString ("UcbApiKey"));
						yield return request.SendWebRequest ();
						if (request.result == UnityWebRequest.Result.Success) {
							break;
						}
						Debug.LogError ($"Error while creating share for {target.buildtargetid} build {buildNumber}, retrying in one sec: " + request.downloadHandler?.text ?? request.error);
						yield return new WaitForSeconds (1f);
					}
				}

				Debug.Log ("Checking for existing cinemataztic api build hook ...");

				var buildUploadUrl = new Uri (CineGameLogin.CinematazticApiBaseUri, "build-hook/unity-cloud-build").ToString ();

				var hasUploadBuildHook = false;
				for (; ; ) {
					using (var request = UnityWebRequest.Get (new Uri (CloudBuildBaseUri, "hooks"))) {
						request.SetRequestHeader ("Authorization", "Basic " + EditorPrefs.GetString ("UcbApiKey"));
						yield return request.SendWebRequest ();
						if (request.result == UnityWebRequest.Result.Success) {
							var responseStr = request.downloadHandler.text;
							//Debug.Log ("Build hooks response: " + responseStr);
							var hooks = JsonConvert.DeserializeObject<UcbHook []> (responseStr);
							hasUploadBuildHook = hooks.Any (h => h.hookType == UcbHook.UcbHookType.web && h.active == true && h.config.url == buildUploadUrl && h.events.Any (e => e == UcbHook.UcbHookEvent.ProjectBuildSuccess));
							break;
						}
						Debug.LogError ("Get build hooks failed, retrying: " + request.downloadHandler?.text ?? request.error);
					}
				}

				if (hasUploadBuildHook) {
					Debug.Log ("Build hook OK");
				} else {
					Debug.Log ("Creating cinemataztic api build hook ...");
					var hook = new UcbHook {
						hookType = UcbHook.UcbHookType.web,
						events = new UcbHook.UcbHookEvent [] {
							UcbHook.UcbHookEvent.ProjectBuildSuccess,
						},
						config = new UcbHook.UcbHookConfig {
							url = buildUploadUrl,
							encoding = UcbHook.UcbHookEncoding.json,
							sslVerify = true,
						},
					};
					for (; ; ) {
						using (var request = new UnityWebRequest (
											new Uri (CloudBuildBaseUri, "hooks"),
											"POST",
											new DownloadHandlerBuffer (),
											new UploadHandlerRaw (Encoding.UTF8.GetBytes (JsonConvert.SerializeObject (hook)))
										)) {
							request.SetRequestHeader ("Authorization", "Basic " + EditorPrefs.GetString ("UcbApiKey"));
							request.uploadHandler.contentType = "application/json; charset=utf-8";
							yield return request.SendWebRequest ();
							if (request.result == UnityWebRequest.Result.Success) {
								Debug.Log ("Cinemataztic api build hook created");
								break;
							}
							Debug.LogError ("Create build hook failed, retrying: " + request.downloadHandler?.text ?? request.error);
						}
					}
				}
			}*/
		}

		/// <summary>
		/// Dictionary of currently polling target status
		/// </summary>
		readonly Dictionary<string, EditorCoroutine> PollCoroutines = new Dictionary<string, EditorCoroutine> ();

		/// <summary>
		/// Start build status polling coroutine if not already running
		/// </summary>
		void EnsurePolling (string targetid) {
			if (!PollCoroutines.ContainsKey (targetid)) {
				PollCoroutines [targetid] = StartCoroutine (E_PollBuildStatus (targetid));
			}
		}

		IEnumerator E_PollBuildStatus (string targetid) {
			UcbBuildStatus s;
			do {
				yield return new WaitForSecondsRealtime (15f);
				yield return StartCoroutine (E_GetLatestBuildForTarget (targetid));
				Repaint ();
				s = LatestBuild [targetid].buildStatus;
			} while (s == UcbBuildStatus.created || s == UcbBuildStatus.queued || s == UcbBuildStatus.sentToBuilder);
			do {
				yield return new WaitForSecondsRealtime (60f);
				yield return StartCoroutine (E_GetLatestBuildForTarget (targetid));
				Repaint ();
				s = LatestBuild [targetid].buildStatus;
			} while (s == UcbBuildStatus.started || s == UcbBuildStatus.restarted);
		}

		void CancelBuild (string targetid, bool confirm = true) {
			var latestBuild = LatestBuild [targetid];
			if (!confirm || EditorUtility.DisplayDialog (titleContent.text, $"Cancel {latestBuild.buildTargetName} #{latestBuild.build} ?", "OK", "Cancel")) {
				StartCoroutine (E_CancelBuild (targetid));
			}
		}

		IEnumerator E_CancelBuild (string targetid) {
			var latestBuild = LatestBuild [targetid];
			var buildno = latestBuild.build;
			latestBuild.buildStatus = UcbBuildStatus.canceling;
			Debug.Log ($"Canceling build #{buildno} for {targetid} ...");
			using (var request = UnityWebRequest.Delete (new Uri (CloudBuildBaseUri, "buildtargets/" + targetid + "/builds/" + buildno))) {
				request.SetRequestHeader ("Authorization", "Basic " + EditorPrefs.GetString ("UcbApiKey"));
				yield return request.SendWebRequest ();
				if (request.result != UnityWebRequest.Result.Success) {
					Debug.LogError ($"Error while canceling build for {targetid}: {request.error}");
				} else {
					Debug.Log ($"Succesfully canceled build #{buildno} for {targetid}");
					LatestBuild.Remove (targetid);
					Repaint ();
				}
			}
		}


		internal static void CreateBuildTarget (UcbPlatform platform, string appName, string branchName, string subdirectory, bool autoBuild = false, string scriptingDefines = null) {

			var prettyAppName = appName.Replace ('/', '-');
			var prettyBranchName = branchName.Replace ("feature/", string.Empty);
			var prettyPlatform = platform.ToString ().Replace ("standalone", string.Empty);

			var targetName = prettyAppName + " " + ((branchName == "master" || branchName == "main") ? prettyPlatform : prettyBranchName + " " + prettyPlatform);

			instance.StartCoroutine (instance.E_CreateBuildTarget (platform, targetName, branchName, subdirectory, autoBuild, scriptingDefines));
		}

		IEnumerator E_CreateBuildTarget (UcbPlatform platform, string targetName, string branchName, string subdirectory, bool autoBuild, string scriptingDefines) {
			if (!IsLoadingTargets && BuildTargets == null) {
				GetCloudBuildTargets ();
			}
			while (IsLoadingTargets) {
				yield return null;
			}

			var target = instance.BuildTargets?.FirstOrDefault (t => t.name == targetName);
			if (target != null) {
				Debug.Log ($"Target {targetName} already exists");
				if (autoBuild) {
					instance.CreateBuild (target, confirm: false);
				}
				yield break;
			}

			target = new UcbBuildTarget {
				name = targetName,
				enabled = true,
				platform = platform,
				settings = new UcbBuildTarget.Settings {
					autoDetectUnityVersion = true,
					fallbackPatchVersion = true,
					//unityVersion = Application.unityVersion.Replace ('.', '_'),
					platform = new UcbBuildTarget.Settings.Platform {
						bundleId = PlayerSettings.applicationIdentifier,
					},
					advanced = new UcbBuildTarget.Settings.Advanced {
						android = platform == UcbPlatform.android ? new UcbBuildTarget.Settings.Advanced.Android {
							buildAppBundle = true,
						} : null,
						unity = new UcbBuildTarget.Settings.Advanced.Unity {
							scriptingDefineSymbols = scriptingDefines,
							//preExportMethod = "CineGameBuild." + ...,
							playerExporter = new UcbBuildTarget.Settings.Advanced.Unity.PlayerExporter {
								sceneList = EditorBuildSettings.scenes.Select (s => s.path).ToArray (),
								export = true,
							}
						},
					},
					scm = new UcbBuildTarget.Settings.Scm {
						type = UcbScmType.git,
						branch = branchName,
						subdirectory = subdirectory,
					}
				},
			};

			IsLoadingTargets = true;

			var jsonReq = JsonConvert.SerializeObject (target);
			Debug.Log ("Creating target " + target.name + ":\n" + jsonReq);
			using (var request = new UnityWebRequest (
								new Uri (CloudBuildBaseUri, "buildtargets/"),
								"POST",
								new DownloadHandlerBuffer (),
								new UploadHandlerRaw (Encoding.UTF8.GetBytes (jsonReq))
							)) {
				request.SetRequestHeader ("Authorization", "Basic " + EditorPrefs.GetString ("UcbApiKey"));
				request.uploadHandler.contentType = "application/json; charset=utf-8";
				yield return request.SendWebRequest ();

				IsLoadingTargets = false;

				if (request.result != UnityWebRequest.Result.Success) {
					Debug.LogError ($"Failed to create target for {target.name}: {request.error}");
				} else {
					target = JsonConvert.DeserializeObject<UcbBuildTarget> (request.downloadHandler.text);
					Debug.Log ($"Target {target.buildtargetid} created succesfully");

					BuildTargets = new UcbBuildTarget [] { target };

					if (autoBuild) {
						CreateBuild (target, confirm: false);
						yield break;
					}
				}
			}
		}

		static void DeleteBuildTarget (UcbBuildTarget target) {
			instance.StartCoroutine (instance.E_DeleteBuildTarget (target));
		}

		IEnumerator E_DeleteBuildTarget (UcbBuildTarget target) {
			IsLoadingTargets = true;

			using (var request = UnityWebRequest.Delete (new Uri (CloudBuildBaseUri, "buildtargets/" + target.buildtargetid))) {
				request.SetRequestHeader ("Authorization", "Basic " + EditorPrefs.GetString ("UcbApiKey"));
				request.uploadHandler.contentType = "application/json; charset=utf-8";
				yield return request.SendWebRequest ();
				if (request.result != UnityWebRequest.Result.Success) {
					Debug.LogError ($"Failed to delete target " + target.buildtargetid);
				}

				IsLoadingTargets = false;
				GetCloudBuildTargets ();
			}
		}

		string GetPathToDownloadedBuild (UcbBuild build) {
			var filename = $"{build.buildtargetid}-{build.build}.{build.links.download_primary.meta.type.ToString ().ToLower ()}";
			return Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), "Downloads", filename);
		}

		string GetPathToDownloadedLog (UcbBuild build) {
			var filename = $"{build.buildtargetid}-{build.build}.log";
			return Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.UserProfile), "Downloads", filename);
		}

		void Download (UcbBuild build) {
			StartCoroutine (E_Download (build));
		}

		IEnumerator E_Download (UcbBuild build) {
			var gameType = GetGameTypeForBuild (build);
			var path = CineGameBuild.GetOutputPath (gameType, BuildPlatformTargetMap [build.platform]);
			using (var request = UnityWebRequest.Get (build.links.download_primary.href)) {
				//No auth header needed
				request.downloadHandler = new DownloadHandlerFile (path);
				request.SendWebRequest ();
				while (!request.isDone) {
					var cancel = EditorUtility.DisplayCancelableProgressBar ("Cloud Build", $"Downloading {build.buildtargetid} build {build.build} ... ", request.downloadProgress);
					if (cancel) {
						request.Abort ();
						EditorUtility.ClearProgressBar ();
						yield break;
					}
					yield return null;
				}
				EditorUtility.ClearProgressBar ();
				if (request.result != UnityWebRequest.Result.Success) {
					try {
						File.Delete (path);
					} catch (Exception) { }
					Debug.LogError ($"Error while downloading {build.buildtargetid} build {build.build}: {request.error}");
					EditorUtility.DisplayDialog ("Cloud Build", $"Error while downloading {Path.GetFileNameWithoutExtension (path)}: {request.error}", "OK");
					GetCloudBuildTargets ();
				} else {
					CineGameBuild.Init ();
					CineGameBuild.GameType = gameType;
				}
			}
		}

		/// <summary>
		/// Install build on attached device
		/// </summary>
		void Install (UcbBuild build) {
			StartCoroutine (E_Install (build));
		}

		IEnumerator E_Install (UcbBuild build) {
			var gameType = GetGameTypeForBuild (build);
			var path = CineGameBuild.GetOutputPath (gameType, BuildPlatformTargetMap [build.platform]);
			if (!File.Exists (path)) {
				yield return StartCoroutine (E_Download (build));
				if (!File.Exists (path)) {
					Debug.LogError ("Build not downloaded. Cancelling install.");
					yield break;
				}
			}
			EditorUtility.ClearProgressBar ();
			CineGameTest.Init ();
		}

		IEnumerator E_DownloadLogForBuild (UcbBuild build) {
			var path = GetPathToDownloadedLog (build);
			using (var request = UnityWebRequest.Get (new Uri (CloudBuildBaseUri, "buildtargets/" + build.buildtargetid + "/builds/" + build.build + "/log"))) {
				request.downloadHandler = new DownloadHandlerFile (path);
				request.SetRequestHeader ("Authorization", "Basic " + EditorPrefs.GetString ("UcbApiKey"));
				request.SendWebRequest ();
				while (!request.isDone) {
					var cancel = EditorUtility.DisplayCancelableProgressBar ("Cloud Build", $"Downloading log for {build.buildtargetid} build {build.build} ... ", request.downloadProgress);
					if (cancel) {
						request.Abort ();
						EditorUtility.ClearProgressBar ();
						yield break;
					}
					yield return null;
				}
				EditorUtility.ClearProgressBar ();
				if (request.result != UnityWebRequest.Result.Success) {
					try {
						File.Delete (path);
					} catch (Exception) { }
					var msg = $"Error while downloading {Path.GetFileNameWithoutExtension (path)}: {request.error}";
					Debug.LogError (msg);
					EditorUtility.DisplayDialog ("Cloud Build", msg, "OK");
					GetCloudBuildTargets ();
				} else {
					EditorUtility.RevealInFinder (path);
				}
			}
		}

		static string GetGameTypeForBuild (UcbBuild build) {
			//return CineGameBuild.GameType;
			return build.buildTargetName.Split (' ') [0];
		}

		/// <summary>
		/// Progress delegate for E_Run. Called for each output line from the running process.
		/// </summary>
		delegate bool ProgressDelegate (string sMessage, float percent);

		/// <summary>
		/// Exit delegate for E_Run. Called when process is terminated.
		/// </summary>
		delegate void ExitDelegate (int exitCode, string output);

		IEnumerator E_Run (string cmd, string args, ProgressDelegate progressCallback = null, ExitDelegate exitCallback = null, bool logWarnings = false) {
			using (var p = new System.Diagnostics.Process ()) {
				p.StartInfo.FileName = cmd;
				p.StartInfo.Arguments = args;
				p.StartInfo.RedirectStandardOutput = true;
				p.StartInfo.RedirectStandardError = true;
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.CreateNoWindow = true;
				p.Start ();
				//Debug.Log ($"CloudBuild: {cmd} {args}");
				var sb = new StringBuilder ();
				var reader = p.StandardOutput;
				while (!p.HasExited || !reader.EndOfStream) {
					if (!reader.EndOfStream) {
						var outputLine = reader.ReadLine ().Trim ();
						sb.AppendLine (outputLine);
						if (progressCallback != null && progressCallback (outputLine, 0f)) {
							p.Kill ();
							break;
						}
					}
					yield return null;
				}
				if (sb.Length > 0) {
					//Debug.LogFormat ("CloudBuild: {0} >> {1}", cmd, sb);
				}
				reader = p.StandardError;
				if (!reader.EndOfStream) {
					var err = string.Format ("CloudBuild: {0} >> {1}", cmd, reader.ReadToEnd ());
					if (p.ExitCode != 0) {
						Debug.LogError (err);
					} else if (logWarnings) {
						Debug.LogWarning (err);
					}
				}
				if (p.ExitCode != 0) {
					Debug.LogErrorFormat ("CloudBuild ERROR: {0} exitcode={1}", cmd, p.ExitCode);
				}
				exitCallback?.Invoke (p.ExitCode, sb.ToString ());
			}
		}
	}
}
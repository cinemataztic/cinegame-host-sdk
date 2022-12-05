using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Build.Reporting;

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace CineGame.Host.Editor {

	public class CineGameTest : EditorWindow {
		[DllImport ("libc", EntryPoint = "chmod", SetLastError = true)]
		private static extern int sys_chmod (string path, uint mode);

		static BuildOptions buildOptions = BuildOptions.None;

		static string resultMessage = string.Empty;     //contains result description from last scene build+upload

		static string IdentityFile;
		static string TimeStamp;

		static int MachineIndex;
		static string [] LocalComputerNames;
		static string [] LocalIpAddresses;
		static string _ip;

		static string IP {
			get {
				return (LocalIpAddresses != null) ? LocalIpAddresses [MachineIndex] : _ip;
			}
			set {
				_ip = value;
			}
		}

		static string progressMessage = "Waiting ...";

		static float buildProgress;
		static bool IsBuilding = false;

		static CineGameTest instance;

		static GUIStyle LabelBoldStyle;

		public CineGameTest () {
			instance = this;
		}

		void OnGUI () {
			if (LabelBoldStyle == null) {
				LabelBoldStyle = new GUIStyle (GUI.skin.label);
				LabelBoldStyle.alignment = TextAnchor.MiddleCenter;
				LabelBoldStyle.fontStyle = FontStyle.Bold;
			}

			if (LocalComputerNames == null) {
				EditorUtility.DisplayProgressBar (titleContent.text, "Scanning local machine names ...", .5f);
				GetLocalComputerNames ();
				EditorUtility.ClearProgressBar ();
				if (LocalIpAddresses != null) {
					IP = EditorPrefs.GetString ("CinemaTestIP");
					MachineIndex = Array.IndexOf (LocalIpAddresses, IP);
					if (MachineIndex == -1) {
						MachineIndex = 0;
					}
				}
			}

			EditorGUILayout.LabelField ("Test the game on a local Linux machine", LabelBoldStyle);
			EditorGUILayout.Space ();

			EditorGUILayout.BeginHorizontal ();
			EditorGUILayout.PrefixLabel ("Test machine: ");
			if (LocalComputerNames != null) {
				MachineIndex = EditorGUILayout.Popup (MachineIndex, LocalComputerNames);
			} else {
				IP = EditorGUILayout.TextField (IP);
			}
			EditorGUILayout.EndHorizontal ();

			EditorGUILayout.BeginHorizontal ();
			IdentityFile = EditorGUILayout.TextField (new GUIContent ("Identity Key File:", "The private ssh key used to log in on target machine"), IdentityFile);
			if (GUILayout.Button ("Browse", GUILayout.Width (100f))) {
				var result = EditorUtility.OpenFilePanel ("Select private key for accessing remote machine", IdentityFile, string.Empty);
				if (!string.IsNullOrWhiteSpace (result)) {
					IdentityFile = result;
				}
			}
			EditorGUILayout.EndHorizontal ();

			EditorGUILayout.LabelField ("GameType:", CineGameBuild.GameType);

			if (!IsBuilding) {
				bool isDebug = (buildOptions & BuildOptions.Development) != 0;
				isDebug = GUILayout.Toggle (isDebug, "Debug");
				buildOptions = (BuildOptions)((int)buildOptions & (0x7fffffff ^ (int)(BuildOptions.Development | BuildOptions.AllowDebugging)));
				if (isDebug) {
					buildOptions |= BuildOptions.Development | BuildOptions.AllowDebugging;
				}
				if (GUILayout.Button ("Build And Test " + CineGameBuild.GameType)) {
					OnClickBuildAndUpload ();
					//GUIUtility.ExitGUI ();
				}
				if (GUILayout.Button ("Kill Build")) {
					KillBuild ();
					//GUIUtility.ExitGUI ();
				}
				if (!string.IsNullOrEmpty (TimeStamp)) {
					if (GUILayout.Button ("Restart Build")) {
						TestBuild ();
					}
				}
				if (GUILayout.Button ("View player log")) {
					ViewLog ();
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
		}

		void OnClickBuildAndUpload () {
			BuildAndTestForLinux64 ();
			Repaint ();
		}

		[MenuItem ("CineGame/Test")]
		internal static void Init () {
			if (instance == null) {
				instance = GetWindow<CineGameTest> ("CineGame Test", typeof (CineGameLogin), typeof (CineGameBuild), typeof (CineGameTest));
			} else {
				instance.Focus ();
			}
		}

		void OnEnable () {
			TimeStamp = EditorPrefs.GetString ("CinemaTestLastTimeStamp", null);
			IdentityFile = EditorPrefs.GetString ("CinemaTestIdentityFile", "~/.ssh/id_rsa");

			titleContent = new GUIContent ("CineGame Test", CineGameBuild.IconTexture);
		}

		static void RepaintWindow () {
			if (instance != null) {
				EditorUtility.SetDirty (instance);
				instance.Repaint ();
			}
		}

		static bool BuildAndTestForLinux64 () {
			var buildSettingsScenePaths = EditorBuildSettings.scenes.Select (scene => scene.path).ToArray ();
			var loadedScenePaths = new string [SceneManager.sceneCount];
			var notInBuild = false;
			for (int i = 0; i < SceneManager.sceneCount; i++) {
				loadedScenePaths [i] = SceneManager.GetSceneAt (i).path;
				if (!buildSettingsScenePaths.Contains (loadedScenePaths [i])) {
					notInBuild = true;
				}
			}
			if (notInBuild) {
				if (!EditorUtility.DisplayDialog (instance.titleContent.text, "Not all loaded scenes are currently in build settings! Do you want to build the loaded scene(s) instead?", "Yes", "No")) {
					return false;
				}
				buildSettingsScenePaths = loadedScenePaths;
			}

			if (!File.Exists (IdentityFile)) {
				if (!EditorUtility.DisplayDialog ($"{IdentityFile} private key file not found. Do you want to create a keyfile now using ssh-keygen?", "OK", "Cancel")) {
					Debug.LogError ("No ssh private key file found, and user canceled creating one.");
					return false;
				}
				IdentityFile = "~/.ssh/id_rsa";
				if (!ExternalProcess.Run (true, "ssh-keygen", null, null, null)) {
					Debug.LogError ("Failed to create private key file");
					return false;
				}
				var msg = $"{IdentityFile} private key was created succesfully. Copy the {IdentityFile}.pub file to the /home/player/.ssh folder on the target machine";
				Debug.Log (msg);
				EditorUtility.DisplayDialog (instance.titleContent.text, msg, "OK");
				return false;
			}

			EditorPrefs.SetString ("CinemaTestIP", IP);
			EditorPrefs.SetString ("CinemaTestIdentityFile", IdentityFile);

			var tmpDir = Application.temporaryCachePath + "/cinemabuild";
			TimeStamp = DateTime.Now.ToString ("yyyy-MM-dd-HHmmss");

			uint rwxr_xr_x = 0x1ED;//Convert.ToUInt32 ("755", 8);

			IsBuilding = true;
			RepaintWindow ();

			/*
			if (QualitySettings.antiAliasing != 0) {
				var errMsg = "Multisampling AA not allowed in cinema builds. Use SSAA instead. Disabling multisampling...";
				Debug.LogWarning (errMsg);
				EditorUtility.DisplayDialog (ProgressBarTitle, errMsg, "OK");
				QualitySettings.antiAliasing = 0;
			}
			*/

			bool success = true;

			try {
				progressMessage = string.Format ("Building {0} for Linux64 ...", CineGameBuild.GameType);

				//Make sure target dir is empty
				try {
					Directory.Delete (tmpDir, true);
				} catch (Exception) { }

				var targetFile = string.Format ("{0}/{1}_Linux64", tmpDir, CineGameBuild.GameType);
				Debug.LogFormat ("Build output: {0}", targetFile);

				//Build player
				var buildReport = BuildPipeline.BuildPlayer (buildSettingsScenePaths, targetFile, BuildTarget.StandaloneLinux64, buildOptions);
				if (buildReport.summary.result != BuildResult.Succeeded) {
					throw new Exception (string.Format ("The build {0}", buildReport.summary.result == BuildResult.Cancelled ? "was cancelled" : "failed"));
				}

				//Quickndirty hack for executable not having a filename extension on linux builds (?!?!)
				var newExePath = $"{targetFile}.x86_64";
				File.Move (targetFile, newExePath);
				if (Application.platform == RuntimePlatform.OSXEditor) {
					Debug.Log ("Setting executable permissions ...");
					sys_chmod (newExePath, rwxr_xr_x);
				}

				var spct = 0f;
				var fakepct = 0f;
				success = UploadBuild (tmpDir, delegate (string sMessage, float percent) {
					percent = fakepct;
					fakepct += (1f - fakepct) * .01f;
					if (EditorUtility.DisplayCancelableProgressBar (instance.titleContent.text, sMessage, percent)) {
						success = false;
					}
					progressMessage = sMessage;
					buildProgress = spct + percent;
					RepaintWindow ();
					return !IsBuilding;
				});

				if (success) {
					EditorPrefs.SetString ("CinemaTestLastTimeStamp", TimeStamp);
					TestBuild ();
				} else {
					Debug.LogError ("Build upload failed");
				}

				Directory.Delete (tmpDir, true);

				EditorUtility.ClearProgressBar ();

				RepaintWindow ();
			} catch (Exception e) {
				EditorUtility.ClearProgressBar ();
				resultMessage = string.Format ("Exception while testing Linux64 build on {0}: {1}", IP, e);
				EditorUtility.DisplayDialog (instance.titleContent.text, resultMessage, "OK");
				return false;
			}

			IsBuilding = false;
			RepaintWindow ();
			return success;
		}

		static string GetLinuxSafeFilename (string v) {
			return Regex.Replace (v, "[^a-zA-Z0-9-_]", "-");
		}

		/// <summary>
		/// nslookup on all local IP addresses to get computer names
		/// </summary>
		static void GetLocalComputerNames () {
			var isWindows = (Application.platform == RuntimePlatform.WindowsEditor);

			var names = new List<string> (256);
			var ipAddresses = new List<string> (256);

			if (!isWindows) {
				//On Linux and macOS we can get a list of all devices with name and ip in one go from arp
				var regex_Linux = new Regex ("^(\\S+)\\s*\\((\\d+\\.\\d+\\.\\d+\\.\\d+)\\)", RegexOptions.Compiled);

				ExternalProcess.Run ("arp", "-a", null, (output, pct) => {
					var m = regex_Linux.Match (output);
					if (m.Success) {
						var name = m.Groups [1].Value;
						var ip = m.Groups [2].Value;
						names.Add ($"{name} ({ip})");
						ipAddresses.Add (ip);
					}
					return false;
				});
			} else {
				//On Windows, arp only lists the ip addresses of devices, we then have to nslookup for each ip to see their names
				var regex_WinArp = new Regex ("^\\s*(\\d+\\.\\d+\\.\\d+\\.\\d+)\\s", RegexOptions.Compiled);
				var regex_WinNslookup = new Regex ("^\\s*Name:\\s*(\\S+)", RegexOptions.Compiled);

				ExternalProcess.Run ("arp", "/a", null, (output, pct) => {
					var m = regex_WinArp.Match (output);
					if (m.Success) {
						var ip = m.Groups [1].Value;
						ipAddresses.Add (ip);
					}
					return false;
				});

				foreach (var ip in ipAddresses) {
					ExternalProcess.Run ("nslookup", ip, null, (output, pct) => {
						var m = regex_WinNslookup.Match (output);
						var name = "?";
						if (m.Success) {
							name = m.Groups [1].Value;
						}
						names.Add ($"{name} ({ip})");
						return false;
					});
				}
			}

			LocalComputerNames = names.ToArray ();
			LocalIpAddresses = ipAddresses.ToArray ();
		}

		/// <summary>
		/// If IdentityFile is defined with a valid file, generate the "-i" option for ssh and scp
		/// </summary>
		static string GetIdentityOption () {
			return !string.IsNullOrWhiteSpace (IdentityFile) && File.Exists (IdentityFile) ? "-i " + IdentityFile : string.Empty;
		}

		static void KillBuild () {
			EditorUtility.DisplayProgressBar (instance.titleContent.text, "Killing build on test machine ...", .5f);
			ExternalProcess.Run ("ssh", $"{GetIdentityOption ()} -o UserKnownHostsFile=/dev/null -o StrictHostKeyChecking=no player@{IP} \"pkill UnityGame\"");
			EditorUtility.ClearProgressBar ();
		}

		static bool UploadBuild (string sInDir, ExternalProcess.ProgressDelegate progress = null) {
			var linuxSafeFilename = GetLinuxSafeFilename (CineGameBuild.GameType);
			var identityOption = GetIdentityOption ();
			EditorUtility.DisplayProgressBar (instance.titleContent.text, "Uploading build to test machine ...", .5f);
			ExternalProcess.Run ("ssh", $"{identityOption} -o UserKnownHostsFile=/dev/null -o StrictHostKeyChecking=no player@{IP} \"rm -rf /home/player/test_games/{linuxSafeFilename}\"");
			var success = ExternalProcess.Run ("scp", $"{identityOption} -o UserKnownHostsFile=/dev/null -o StrictHostKeyChecking=no -r {sInDir} player@{IP}:/home/player/test_games/{linuxSafeFilename}");
			EditorUtility.ClearProgressBar ();
			return success;
		}

		static bool ViewLog () {
			EditorUtility.DisplayProgressBar (instance.titleContent.text, "Downloading Player.log from test machine ...", .5f);
			var success = ExternalProcess.Run ("scp", $"{GetIdentityOption ()} -o UserKnownHostsFile=/dev/null -o StrictHostKeyChecking=no player@{IP}:/home/player/.config/unity3d/{PlayerSettings.companyName}/{PlayerSettings.productName}/Player.log {Application.temporaryCachePath}");
			EditorUtility.ClearProgressBar ();
			if (success) {
				Application.OpenURL ($"file://{Application.temporaryCachePath}/Player.log");
				return true;
			}
			return false;
		}

		static void TestBuild () {
			var linuxSafeFilename = GetLinuxSafeFilename (CineGameBuild.GameType);
			var remoteCmd = $"sh -c 'pkill UnityGame; rm -f /home/player/.config/unity3d/{PlayerSettings.companyName}/{PlayerSettings.productName}/Player.log; cd /home/player/test_games/{linuxSafeFilename}; export DISPLAY=:0; ln -fs *.x86_64 UnityGame; nohup ./UnityGame > /dev/null 2>&1 &'"; ;
			EditorUtility.DisplayProgressBar (instance.titleContent.text, "Running game on test machine ...", .5f);
			ExternalProcess.Run ("ssh", $"{GetIdentityOption ()} -o UserKnownHostsFile=/dev/null -o StrictHostKeyChecking=no player@{IP} \"{remoteCmd}\"");
			EditorUtility.ClearProgressBar ();
		}
	}
}

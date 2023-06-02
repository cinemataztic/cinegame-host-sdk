using System;
using UnityEditor;

using UnityEngine;

namespace CineGame.Host.Editor {

	[CustomEditor (typeof (CineGameSettings))]
	public class CineGameSettingsEditor : UnityEditor.Editor {

		SerializedProperty GameTypeProperty;
		SerializedProperty MarketIdProperty;
		SerializedProperty LoopProperty;
		int marketIndex;
		int gameTypeIndex;

		public override void OnInspectorGUI () {
			// Update the serializedObject - always do this in the beginning of OnInspectorGUI.
			serializedObject.Update ();

			var isLoggedIn = CineGameLogin.IsLoggedIn;

			if (GameTypeProperty == null) {
				GameTypeProperty = serializedObject.FindProperty ("GameType");
				MarketIdProperty = serializedObject.FindProperty ("MarketId");
				LoopProperty = serializedObject.FindProperty ("Loop");

				if (isLoggedIn) {
					marketIndex = Array.IndexOf (CineGameLogin.MarketIdsAvailable, MarketIdProperty.stringValue);
					if (marketIndex == -1) {
						SetMarketIndex (0);
					}
					if (!CineGameLogin.IsSuperAdmin) {
						gameTypeIndex = Array.IndexOf (CineGameLogin.GameTypesAvailable, GameTypeProperty.stringValue);
						if (gameTypeIndex == -1) {
							SetGameTypeIndex (0);
						}
					}
				}
			}

			using (new EditorGUI.DisabledScope (!isLoggedIn || (!CineGameLogin.IsSuperAdmin && CineGameLogin.GameTypesAvailable.Length == 0))) {
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.PrefixLabel ("GameType:");
				if (isLoggedIn) {
					if (CineGameLogin.IsSuperAdmin) {
						GameTypeProperty.stringValue = EditorGUILayout.TextField (GameTypeProperty.stringValue);
					} else if (CineGameLogin.GameTypesAvailable.Length != 0) {
						var _gti = EditorGUILayout.Popup (gameTypeIndex, CineGameLogin.GameTypesAvailable);
						if (_gti != gameTypeIndex) {
							SetGameTypeIndex (_gti);
						}
					} else {
						EditorGUILayout.LabelField ("N/A");
					}
				} else {
					EditorGUILayout.LabelField (GameTypeProperty.stringValue);
				}
				EditorGUILayout.EndHorizontal ();
			}

			EditorGUILayout.BeginHorizontal ();
			EditorGUILayout.PrefixLabel (new GUIContent ("Default market:", "Only used in WebGL as fallback"));
			if (isLoggedIn) {
				var _mi = EditorGUILayout.Popup (marketIndex, CineGameLogin.MarketSlugsAvailable);
				if (_mi != marketIndex) {
					SetMarketIndex (_mi);
				}
			} else {
				EditorGUILayout.LabelField (CineGameSDK.MarketSlugMap [MarketIdProperty.stringValue]);
			}
			EditorGUILayout.EndHorizontal ();

			EditorGUILayout.PropertyField (LoopProperty);

			if (!isLoggedIn) {
				EditorGUILayout.LabelField ("Please log in to edit values");
			}
			if (CineGameLogin.IsSuperAdmin) {
				EditorGUILayout.LabelField ("Super admin: Free to edit the GameType");
			}

			// Apply changes to the serializedObject - always do this in the end of OnInspectorGUI.
			serializedObject.ApplyModifiedProperties ();
		}

		void SetMarketIndex (int i) {
			marketIndex = i;
			MarketIdProperty.stringValue = CineGameLogin.MarketIdsAvailable [marketIndex];
		}

		void SetGameTypeIndex (int i) {
			gameTypeIndex = i;
			GameTypeProperty.stringValue = CineGameLogin.GameTypesAvailable [gameTypeIndex];
		}
	}

}
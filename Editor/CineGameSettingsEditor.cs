using System;
using UnityEditor;

using UnityEngine;

namespace CineGame.SDK.Editor
{

	[CustomEditor (typeof (CineGameSettings))]
	public class CineGameSettingsEditor : UnityEditor.Editor {

		SerializedProperty GameIDProperty;
		SerializedProperty MarketIdProperty;
		SerializedProperty LoopProperty;
		int marketIndex;
		int gameIDIndex;

		public override void OnInspectorGUI () {
			// Update the serializedObject - always do this in the beginning of OnInspectorGUI.
			serializedObject.Update ();

			var isLoggedIn = CineGameLogin.IsLoggedIn;

			if (GameIDProperty == null) {
				GameIDProperty = serializedObject.FindProperty ("GameID");
				MarketIdProperty = serializedObject.FindProperty ("MarketId");
				LoopProperty = serializedObject.FindProperty ("Loop");

				if (isLoggedIn) {
					marketIndex = Array.IndexOf (CineGameLogin.MarketIdsAvailable, MarketIdProperty.stringValue);
					if (marketIndex == -1) {
						SetMarketIndex (0);
					}
					if (!CineGameLogin.IsSuperAdmin) {
						gameIDIndex = Array.IndexOf (CineGameLogin.GameIDsAvailable, GameIDProperty.stringValue);
						if (gameIDIndex == -1) {
							SetGameIDIndex (0);
						}
					}
				}
			}

			using (new EditorGUI.DisabledScope (!isLoggedIn || (!CineGameLogin.IsSuperAdmin && CineGameLogin.GameIDsAvailable.Length == 0))) {
				EditorGUILayout.BeginHorizontal ();
				EditorGUILayout.PrefixLabel ("GameID:");
				if (isLoggedIn) {
					if (CineGameLogin.IsSuperAdmin) {
						GameIDProperty.stringValue = EditorGUILayout.TextField (GameIDProperty.stringValue);
					} else if (CineGameLogin.GameIDsAvailable.Length != 0) {
						var _gti = EditorGUILayout.Popup (gameIDIndex, CineGameLogin.GameIDsAvailable);
						if (_gti != gameIDIndex) {
							SetGameIDIndex (_gti);
						}
					} else {
						EditorGUILayout.LabelField ("N/A");
					}
				} else {
					EditorGUILayout.LabelField (GameIDProperty.stringValue);
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
				EditorGUILayout.LabelField (CineGameMarket.Names [MarketIdProperty.stringValue]);
			}
			EditorGUILayout.EndHorizontal ();

			EditorGUILayout.PropertyField (LoopProperty);

			if (!isLoggedIn) {
				EditorGUILayout.LabelField ("Please log in to edit values");
			}
			if (CineGameLogin.IsSuperAdmin) {
				EditorGUILayout.LabelField ("Super admin: Free to edit the GameID");
			}

			// Apply changes to the serializedObject - always do this in the end of OnInspectorGUI.
			serializedObject.ApplyModifiedProperties ();
		}

		void SetMarketIndex (int i) {
			marketIndex = i;
			MarketIdProperty.stringValue = CineGameLogin.MarketIdsAvailable [marketIndex];
		}

		void SetGameIDIndex (int i) {
			gameIDIndex = i;
			GameIDProperty.stringValue = CineGameLogin.GameIDsAvailable [gameIDIndex];
		}
	}

}
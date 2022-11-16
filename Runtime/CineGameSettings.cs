using UnityEngine;

namespace CineGame.Host {

	[CreateAssetMenu ()]
	public class CineGameSettings : ScriptableObject {
		public string GameType;
		/// <summary>
		/// Default market used in WebGL builds
		/// </summary>
		public string MarketId;
		public bool Loop;
	}

}

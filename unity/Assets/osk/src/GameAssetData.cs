using UnityEngine;

namespace Osk42 {
	[System.Serializable]
	public class GameAssetData : ScriptableObject {
		public GameObject piece;
		public GameObject stage;
		public GameObject bomb;
		public GameObject blast;
		public GameConfig config;

		[System.Serializable]
		public class GameConfig {
			public int sizeX = 8;
			public int sizeY = 8;
			public float bombDistance = 0.9f;
			public float moveForce = 20f;
			public float moveForceControlSpeed = 2f;
			public float jumpForce = 10f;
			public float cameraMoveSpeed = 10f;
			public float blastForce = 10f;
			public float blinkInterval = 0.2f;
			public float flipSpeed = 0.1f;
		}
#if UNITY_EDITOR
		[UnityEditor.MenuItem("UNKO/FUGA")]
		public static void create() {
			var asset = CreateInstance<GameAssetData>();
			UnityEditor.AssetDatabase.CreateAsset(asset, "Assets/data.asset");
		}
#endif
	}
}

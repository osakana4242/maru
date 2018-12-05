using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using UniRx.Triggers;
using UnityEngine.UI;

namespace Osk42 {
	public class Game : MonoBehaviour {

		public GameAssetData assets;

		public Data data;

		class Mover : MonoBehaviour {
			public bool hitGround { get { return 0 < hitList.Count; } }
			public List<GameObject> hitList;
			void Awake() {
				hitList = new List<GameObject>();
			}
		}

		public sealed class ScreenData {
			public static readonly ScreenData instance = new ScreenData();
			public readonly Vector2 design;
			public readonly Vector2 curSize;
			public readonly Vector2 exSize;
			public readonly Rect exRect;
			public readonly float rate;

			public ScreenData() {
				design = new Vector2(640, 480);
				curSize = new Vector2(Screen.width, Screen.height);
				rate = design.y / curSize.y;
				exSize = new Vector2(design.x * rate, design.y);
				exRect = new Rect(0, 0, exSize.x, exSize.y);
			}
		}

		public static class Key {
			public static KeyCode Jump = KeyCode.C;
			public static KeyCode Put = KeyCode.Z;
			public static KeyCode Fire = KeyCode.X;
		}

		// Use this for initialization
		void Start() {
			var tr = transform;

			data.canvas = GameObject.FindObjectOfType<Canvas>();
			data.machine = GameObject.Find("machine");

			data.progress = new Progress();
			data.player = new Player();

			float radius = 200;
			// 左周り.
			var left = new CircleData();
			left.radius = radius;
			left.list = new Rect[] {
				new Rect(0, 0, radius, radius),
				new Rect(radius, 0, radius, radius),
				new Rect(radius, radius, radius, radius),
				new Rect(0, radius, radius, radius),
			};
			left_ = left;
			// 右回り.
			var right = new CircleData();
			right.radius = radius;
			right.list = new Rect[] {
				new Rect(0, 0, radius, radius),
				new Rect(0, radius, radius, radius),
				new Rect(radius, radius, radius, radius),
				new Rect(radius, 0, radius, radius),
			};
			right_ = right;
		}

		public enum StateId {
			Init,
			Ready,
			Main,
			Wait,
			Result1,
			Result2,
		}

		public static class MathHelper {
			public static bool isIn(int v, int min, int max) {
				return min <= v && v < max;
			}
			public static bool isIn(float v, float min, float max) {
				return min <= v && v < max;
			}
		}

		public bool isIn(Vector2 position) {
			if (!MathHelper.isIn(position.x, 0, assets.config.sizeX)) return false;
			if (!MathHelper.isIn(position.y, 0, assets.config.sizeY)) return false;
			return true;
		}

		public class CircleData {
			public float radius;
			public Rect[] list;

			public int hitIndex;
			public int hitCount;
			public int preHitCount;
		}
		CircleData left_;
		CircleData right_;


		public static class ArrayHelper {
			public static int FindIndex<T1, T2>(T1[] self, T2 prm, System.Func<T1, T2, bool> f) {
				for (var i = 0; i < self.Length; i++) {
					var item = self[i];
					if (f(item, prm)) return i;
				}
				return -1;
			}
		}

		public static void CheckCircle(CircleData c, Vector2 ipos) {
			c.preHitCount = c.hitCount;
			var preIndex = c.hitIndex;
			var offs = new Vector2(
				(ScreenData.instance.exSize.x - c.radius * 2) * 0.5f,
				(ScreenData.instance.exSize.y - c.radius * 2) * 0.5f
			);
			int index = ArrayHelper.FindIndex(c.list, ipos - offs, (_item, _prm) => {
				return _item.Contains(_prm);
			});
			c.hitIndex = index;
			if (index == -1) {
				c.hitCount = 0;
				return;
			}
			var diff = (index - preIndex + c.list.Length) % c.list.Length;
			if (diff == 0) {
				// stay
			} else if (diff == 1) {
				c.hitCount++;
			} else {
				c.hitCount = 0;
			}
		}

		// Update is called once per frame
		void Update() {
			switch (data.progress.stateId) {
				case StateId.Init:
					data.progress.elapsedTime = 0f;
					data.player.power = 0f;
					data.player.rotScore = 0f;
					data.progress.stateId = StateId.Ready;
					break;
				case StateId.Ready:
					if (Input.GetMouseButtonDown(0)) {
						data.progress.stateId = StateId.Wait;
						Observable.ReturnUnit().
							Delay(System.TimeSpan.FromSeconds(0.5f)).
							Do(_ => {
								data.progress.stateId = StateId.Main;
							}).
							TakeUntilDestroy(gameObject).
							Subscribe();
					}
					break;
				case StateId.Main: {

						if (Input.GetMouseButton(0)) {
							var ipos = Input.mousePosition * ScreenData.instance.rate;
							CheckCircle(right_, ipos);
							// Debug.Log(
							// 	"fc: " + Time.frameCount +
							// 	", ipos: " + ipos +
							// 	", index: " + right_.hitIndex +
							// 	", count: " + right_.hitCount
							// );

						} else {
							right_.preHitCount = right_.hitCount;
							right_.hitCount = 0;
							right_.hitIndex = -1;
						}
						var player = data.player;
						if (1 < right_.hitCount && right_.preHitCount < right_.hitCount) {
							player.power += assets.config.machineSpeed;
							player.power -= 0.98f * Time.deltaTime;
						} else if (0 < right_.hitCount) {
							player.power -= 0.98f * Time.deltaTime;
						} else {
							// つまずき.
							if (0f < right_.preHitCount) {
								player.power *= 0.25f;
							} else {
								player.power -= 0.98f * Time.deltaTime;
							}
						}
						if (player.power <= 0) {
							player.power = 0f;
						}

						{
							var euler = data.machine.transform.rotation.eulerAngles;
							euler.y += player.power * Time.deltaTime;
							player.rotScore += player.power * Time.deltaTime;
							data.machine.transform.rotation = Quaternion.Euler(euler);
						}
						data.progress.elapsedTime += Time.deltaTime;
						if (data.progress.timeLimit <= data.progress.elapsedTime) {
							data.progress.elapsedTime = data.progress.timeLimit;
							data.progress.stateId = StateId.Result1;
						}

						break;
					}
				case StateId.Result1:
					data.progress.stateId = StateId.Wait;
					Observable.ReturnUnit().
						Delay(System.TimeSpan.FromSeconds(0.5f)).
						Do(_ => {
							data.progress.stateId = StateId.Result2;
						}).
						TakeUntilDestroy(gameObject).
						Subscribe();
					break;
				case StateId.Result2:
					if (Input.GetMouseButtonDown(0)) {
						data.progress.stateId = StateId.Init;
					}
					break;
			}


			UpdateView();
		}

		static bool isBlink(float interval) {
			return (Time.time % (interval * 2)) < interval;
		}

		void UpdateView() {
			var progress = data.canvas.transform.Find("progress").GetComponent<Text>();
			var restTime = Mathf.Max(0f, data.progress.timeLimit - data.progress.elapsedTime);
			progress.text =
				string.Format("残り時間 {0:F2} 秒\n", restTime) +
				string.Format("スコア {0:F2} 回転\n", data.player.rotScore / 360f) +
				string.Format("継続力 {0}\n", right_.hitCount) +
				string.Format("回転力 {0:F2}\n", data.player.power) +
				"";
			var circleText = data.canvas.transform.Find("circle_text").GetComponent<Graphic>();
			circleText.enabled = data.progress.stateId == StateId.Ready;
			var circleRect = data.canvas.transform.Find("circle_area").GetComponent<Graphic>();
			circleRect.enabled = data.progress.stateId == StateId.Ready;
		}

		[System.Serializable]
		public class Data {
			public Canvas canvas;
			public Progress progress;
			public Player player;
			public GameObject machine;
			public GameObject stage;
		}

		public class Progress {
			public StateId stateId = StateId.Init;
			public float elapsedTime = 0f;
			public float timeLimit = 30f;

		}

		public class Player {
			public float power = 0;
			public float rotScore = 0f;
		}

		public enum ObjectType {
			Player,
			Cell,
			Piece,
		}

		public struct ObjectId : System.IEquatable<ObjectId> {
			public readonly int id;
			public ObjectId(ObjectType type, int id) : this() {
				this.id = ((int)type << 8) | id;
			}
			public bool Equals(ObjectId other) {
				return id == other.id;
			}
			public override bool Equals(object obj) {
				var other = obj as ObjectId?;
				if (other == null) return false;
				return Equals(other);
			}
			public override int GetHashCode() {
				return id;
			}
			public static bool operator ==(ObjectId a, ObjectId b) {
				return a.Equals(b);
			}
			public static bool operator !=(ObjectId a, ObjectId b) {
				return !a.Equals(b);
			}
		}
	}
}

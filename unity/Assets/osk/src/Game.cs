using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UniRx;
using UniRx.Triggers;
using UnityEngine.UI;

namespace Osk42
{
    public class Game : MonoBehaviour
    {

        public GameAssetData assets;

        public Data data;

        class Mover : MonoBehaviour
        {
            public bool hitGround { get { return 0 < hitList.Count; } }
            public List<GameObject> hitList;
            void Awake()
            {
                hitList = new List<GameObject>();
            }
        }

        public static class Key
        {
            public static KeyCode Jump = KeyCode.C;
            public static KeyCode Put = KeyCode.Z;
            public static KeyCode Fire = KeyCode.X;
        }

        // Use this for initialization
        void Start()
        {

            var tr = transform;

            data.canvas = GameObject.FindObjectOfType<Canvas>();

            data.stage = GameObject.Instantiate(assets.stage, new Vector3(0f, 0.2f, 0f), Quaternion.identity, tr);

            data.progress = new Progress();
            data.progress.currentPlayerIndex = 0;

            data.players = new List<Player>(2);
            for (var i = 0; i < 2; i++)
            {
                var player = new Player();
                data.players.Add(player);
            }

            var cellCount = assets.config.sizeX * assets.config.sizeY;
            data.cells = new List<Cell>(cellCount);
            for (var i = 0; i < cellCount; i++)
            {
                var cell = new Cell();
                cell.id = new ObjectId(ObjectType.Cell, i);
                cell.position = getCellPosition(i);
                data.cells.Add(cell);
            }

            var pieceCount = cellCount;
            data.pieces = new List<Piece>(pieceCount);
            for (var i = 0; i < pieceCount; i++)
            {
                var piece = new Piece();
                piece.id = new ObjectId(ObjectType.Piece, i);
                piece.position = getCellPosition(i);
                piece.state = PieceState.Sleep;

                piece.go = GameObject.Instantiate(assets.piece, Vector3.zero, Quaternion.identity, tr);

                data.pieces.Add(piece);
            }
        }

        public void firstPut()
        {
            {
                Vector2 center = new Vector2((assets.config.sizeX - 1) / 2, (assets.config.sizeY - 1) / 2);

                {
                    var piece = activatePiece();
                    piece.pieceType = PieceType.White;
                    piece.position = center + new Vector2(0, 0);
                    piece.state = PieceState.Fix;
                }
                {
                    var piece = activatePiece();
                    piece.pieceType = PieceType.Black;
                    piece.position = center + new Vector2(1, 0);
                    piece.state = PieceState.Fix;
                }
                {
                    var piece = activatePiece();
                    piece.pieceType = PieceType.Black;
                    piece.position = center + new Vector2(0, 1);
                    piece.state = PieceState.Fix;
                }
                {
                    var piece = activatePiece();
                    piece.pieceType = PieceType.White;
                    piece.position = center + new Vector2(1, 1);
                    piece.state = PieceState.Fix;
                }
            }
        }

        public Piece activatePiece()
        {
            var piece = data.pieces.Find((_item) => _item.state == PieceState.Sleep);
            if (piece == null) return null;
            piece.go.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            piece.state = PieceState.Hover;
            return piece;
        }

        Vector3 getPos(Vector2 pos)
        {
            Vector2 leftTop = new Vector2(-4, -4) + new Vector2(0.5f, 0.5f);
            Vector2 pos2 = leftTop + pos;
            return new Vector3(pos2.x, 0.4f, pos2.y);
        }

        Vector2 getCellPosition(int id)
        {
            return new Vector2(id % assets.config.sizeX, id / assets.config.sizeY);
        }

        public void playBomb(Vector3 pos)
        {
            var go = GameObject.Instantiate(assets.blast, pos, Quaternion.identity, transform);
            Observable.Return(go).
                Delay(System.TimeSpan.FromSeconds(1f)).
                Do(_go => GameObject.Destroy(_go)).
                Subscribe().
                AddTo(this);
        }

        public void OnGUI()
        {
            using (var area1 = new GUILayout.AreaScope(new Rect(0, 0, 320, 320)))
            {
            }
        }

        public enum StateId
        {
            Init,
            PlayerInit,
            Hover,
            Fix,
            Wait,
            Result1,
            Result2,
        }

        public Player CurrentPlayer { get { return data.players[data.progress.currentPlayerIndex]; } }

        public static class MathHelper
        {
            public static bool isIn(int v, int min, int max)
            {
                return min <= v && v < max;
            }
            public static bool isIn(float v, float min, float max)
            {
                return min <= v && v < max;
            }
        }

        public bool isIn(Vector2 position)
        {
            if (!MathHelper.isIn(position.x, 0, assets.config.sizeX)) return false;
            if (!MathHelper.isIn(position.y, 0, assets.config.sizeY)) return false;
            return true;
        }

        public bool canPut(Piece piece)
        {
            if (!isIn(piece.position)) return false;
            var otherPiece = data.pieces.Find(_item =>
            {
                if (_item.state != PieceState.Fix) return false;
                if (_item.id == piece.id) return false;
                return (_item.position == piece.position);
            });
            return otherPiece == null;
        }

        /** 集計 */
        IObservable<Unit> changePiecesAsObservable(Piece lastPiece)
        {
            var pieceStreams = new List<IObservable<Unit>>();
            var changePieces = new List<Piece>();

            pieceStreams.Add(Observable.ReturnUnit());

            foreach (var dir in dires)
            {
                changePieces.Clear();
                getChangePiece(lastPiece, dir, ref changePieces);

                for (var i = 0; i < changePieces.Count; i++)
                {
                    var piece = changePieces[i];
                    var delay = i * 0.1f;
                    var stream = Observable.ReturnUnit().
                    Delay(System.TimeSpan.FromSeconds(delay)).
                    Do(_ =>
                    {
                        piece.pieceType = getOtherPieceType(piece.pieceType);
                        CurrentPlayer.score += 10;
                    });
                    pieceStreams.Add(stream);
                }
            }
            return Observable.Zip(pieceStreams).Select(_ => Unit.Default);
        }

        PieceType getOtherPieceType(PieceType type)
        {
            return (type == PieceType.White) ?
                PieceType.Black :
                PieceType.White;
        }

        Vector2[] dires = {
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1),
            new Vector2(-1, 1),
            new Vector2(-1, 0),
            new Vector2(-1, -1),
            new Vector2(0, -1),
            new Vector2(1, -1),
        };

        Piece findPiece(Vector2 pos)
        {
            return data.pieces.Find(_item =>
            {
                if (_item.state != PieceState.Fix) return false;
                return _item.position == pos;
            });
        }

        void getChangePiece(Piece piece, Vector2 dir, ref List<Piece> pieces)
        {
            var firstPos = piece.position + dir;
            var pos = firstPos;
            while (true)
            {
                var nextPiece = findPiece(pos);
                if (nextPiece == null)
                {
                    return;
                }
                if (nextPiece.pieceType == piece.pieceType)
                {
                    break;
                }
                pos += dir;
            }
            var lastPos = pos;
            if (firstPos == lastPos) return;
            pos = firstPos;

            while (pos != lastPos)
            {
                var nextPiece = findPiece(pos);
                if (nextPiece.pieceType == piece.pieceType)
                {
                    break;
                }
                pieces.Add(nextPiece);
                pos += dir;
            }
        }

        // Update is called once per frame
        void Update()
        {
            switch (data.progress.stateId)
            {
                case StateId.Init:
                    {
                        data.players.ForEach(_pl => {
                            _pl.piece = null;
                            _pl.score = 0;
                            _pl.result = PlayerResult.None;
                        });
                        data.pieces.ForEach(_piece => {
                            _piece.state = PieceState.Sleep;
                        });
                        data.progress.turn = 0;

                        data.progress.stateId = StateId.Wait;
                        Observable.ReturnUnit().
                        Delay(System.TimeSpan.FromSeconds(0.25f)).
                        Do(_ => firstPut()).
                        Delay(System.TimeSpan.FromSeconds(0.5f)).
                        TakeUntilDestroy(gameObject).
                        Subscribe(_ =>
                        {
                            data.progress.stateId = StateId.PlayerInit;
                        });
                        break;
                    }
                case StateId.PlayerInit:
                    {
                        var piece = activatePiece();
                        if (piece == null)
                        {
                            data.progress.stateId = StateId.Result1;
                            break;
                        }
                        var pl = CurrentPlayer;
                        pl.piece = piece;
                        data.progress.stateId = StateId.Hover;
                        break;
                    }
                case StateId.Hover:
                    {
                        var pl = CurrentPlayer;
                        var piece = pl.piece;
                        {
                            Vector2 diff = Vector2.zero;
                            if (Input.GetKeyDown(KeyCode.LeftArrow))
                            {
                                diff.x = -1;
                            }
                            if (Input.GetKeyDown(KeyCode.RightArrow))
                            {
                                diff.x = 1;
                            }
                            if (Input.GetKeyDown(KeyCode.UpArrow))
                            {
                                diff.y = 1;
                            }
                            if (Input.GetKeyDown(KeyCode.DownArrow))
                            {
                                diff.y = -1;
                            }
                            var nextPos = piece.position + diff;
                            nextPos.x = Mathf.Clamp(nextPos.x, 0, assets.config.sizeX - 1);
                            nextPos.y = Mathf.Clamp(nextPos.y, 0, assets.config.sizeY - 1);
                            piece.position = nextPos;
                        }

                        if (Input.GetKeyDown(KeyCode.X))
                        {
                            piece.pieceType = (piece.pieceType == PieceType.White) ?
                                PieceType.Black :
                                PieceType.White;
                        }

                        if (Input.GetKeyDown(KeyCode.Z))
                        {
                            if (canPut(piece))
                            {
                                data.progress.stateId = StateId.Wait;
                                piece.state = PieceState.Fix;
                                changePiecesAsObservable(piece).
                                Delay(System.TimeSpan.FromSeconds(0.25f)).
                                TakeUntilDestroy(gameObject).
                                Subscribe(_ =>
                                {
                                    data.progress.stateId = StateId.Fix;
                                });
                            }
                        }
                        break;
                    }
                case StateId.Fix:
                    {
                        var pl = CurrentPlayer;
                        data.progress.currentPlayerIndex = (data.progress.currentPlayerIndex + 1) % data.players.Count;
                        data.progress.stateId = StateId.PlayerInit;
                        data.progress.turn += 1;
                        break;
                    }
                case StateId.Result1:
                    {
                        data.progress.stateId = StateId.Wait;
                        Observable.ReturnUnit().
                            Delay(System.TimeSpan.FromSeconds(0.5f)).
                            TakeUntilDestroy(gameObject).
                            Subscribe(_ =>
                            {
                                var winner = (data.players[1].score < data.players[0].score) ?
                                    data.players[0] :
                                    data.players[1];
                                foreach (var pl in data.players)
                                {
                                    pl.result = (pl == winner) ?
                                        PlayerResult.Win :
                                        PlayerResult.Lose;
                                }


                                data.progress.stateId = StateId.Result2;
                            });
                        break;
                    }
                case StateId.Result2:
                    {
                        if (Input.GetKeyDown(KeyCode.Z)) {
                            data.progress.stateId = StateId.Init;
                        }
                        break;
                    }
            }
            UpdateView();
        }

        static bool isBlink(float interval)
        {
            return (Time.time % (interval * 2)) < interval;
        }

        void UpdateView()
        {
            {
                var uiTr = data.canvas.transform.Find("progress");
                var uiText = uiTr.GetComponent<Text>();
                var text = string.Format("TURN {0}", data.progress.turn);
                uiText.text = text;
            }

            for (var i = 0; i < data.players.Count; i++)
            {
                var pl = data.players[i];
                var uiName = string.Format("p{0}", i + 1);
                var uiTr = data.canvas.transform.Find(uiName);
                var uiText = uiTr.GetComponent<Text>();
                var isB = isBlink(assets.config.blinkInterval);
                var isCuurentPlayer = (i == data.progress.currentPlayerIndex);
                var cursor = (pl.result != PlayerResult.None) ? "":
                    isCuurentPlayer ?
                        (isB ? "<" : "") :
                        "";
                var text =
                    string.Format("PLAYER{0}{1}\n", i + 1, cursor) +
                    string.Format("SCORE {0}\n", pl.score);

                if (pl.result != PlayerResult.None)
                {
                    if (pl.result == PlayerResult.Win)
                    {
                        if (isB)
                        {
                            text += "WIN\n";
                        }
                    }
                    else
                    {
                        //text += "LOSE";
                    }
                }

                uiText.text = text;
            }

            foreach (var piece in data.pieces)
            {
                var go = piece.go;
                go.SetActive(piece.state != PieceState.Sleep);

                var pos = getPos(piece.position);
                if (piece.state == PieceState.Hover)
                {
                    pos.y += 0.2f;
                    go.SetActive(isBlink(assets.config.blinkInterval));
                }

                go.transform.position = pos;

                Quaternion nextRot;
                switch (piece.pieceType)
                {
                    case PieceType.Black:
                        nextRot = Quaternion.Euler(0f, 0f, 180f);
                        break;
                    case PieceType.White:
                    default:
                        nextRot = Quaternion.Euler(0f, 0f, 0f);
                        break;
                }
                var curRot = go.transform.rotation;
                go.transform.rotation = Quaternion.Lerp(curRot, nextRot, Time.deltaTime * assets.config.flipSpeed);
            }
        }

        [System.Serializable]
        public class Data
        {
            public Canvas canvas;
            public Progress progress;
            public GameObject stage;
            public List<Player> players;
            public List<Cell> cells;
            public List<Piece> pieces;
        }

        public class Progress
        {
            public StateId stateId = StateId.Init;
            public int currentPlayerIndex;
            public int turn;
        }

        public enum PlayerResult
        {
            None,
            Win,
            Lose,
        }

        public class Player
        {
            public ObjectId id;
            public int score;
            public Piece piece;
            public PlayerResult result;
        }

        public class Cell
        {
            public ObjectId id;
            public Vector2 position;
        }

        public class Piece
        {
            public ObjectId id;
            public Vector2 position;
            public PieceType pieceType;
            public PieceState state;
            public GameObject go;
        }

        public enum PieceState
        {
            Sleep,
            Hover,
            Fix,
        }

        public enum ObjectType
        {
            Player,
            Cell,
            Piece,
        }

        public struct ObjectId : System.IEquatable<ObjectId>
        {
            public readonly int id;
            public ObjectId(ObjectType type, int id) : this()
            {
                this.id = ((int)type << 8) | id;
            }
            public bool Equals(ObjectId other)
            {
                return id == other.id;
            }
            public override bool Equals(object obj)
            {
                var other = obj as ObjectId?;
                if (other == null) return false;
                return Equals(other);
            }
            public override int GetHashCode()
            {
                return id;
            }
            public static bool operator ==(ObjectId a, ObjectId b)
            {
                return a.Equals(b);
            }
            public static bool operator !=(ObjectId a, ObjectId b)
            {
                return !a.Equals(b);
            }
        }

        public enum PieceType
        {
            White,
            Black,
        }
    }
}

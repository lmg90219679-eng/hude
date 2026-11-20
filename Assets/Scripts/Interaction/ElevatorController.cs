using System.Linq;
using Escape.Audio;
using Escape.Core;
using Escape.Enemy;
using Escape.Items;
using Escape.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Escape.Interaction
{
    public enum ElevatorState
    {
        Idle,        // 문이 닫혀 있고, 호출을 기다린다
        Called,      // 호출함: 도착까지 45초
        DoorsOpen,   // 도착해서 문이 열려 있다 (8초)
        Closing,     // 층을 고르고 문이 닫히는 중 (2초)
        Riding,      // 이동 중 (층당 15초)
        Failed,      // 뒤를 돌아봤다
    }

    /// <summary>
    /// 비밀 엘리베이터. 이용하려면 여섯 가지 조건이 필요하다.
    ///  1. 간호사실에서 직원 출입카드를 구한다.
    ///  2. 3층 전기실의 차단기 3개를 올려 발전기를 복구한다 (숨겨진 승강장 문이 열린다).
    ///  3. 카드로 엘리베이터 잠금을 해제한다.
    ///  4. 호출하고 45초 동안 추적자를 피해 버틴다.
    ///  5. 도착하면 문은 8초 동안만 열려 있다. 타서 층을 누르면 2초에 걸쳐 닫히고, 층당 15초가 걸려 이동한다.
    ///  6. 문이 닫힌 뒤 7초 동안 뒤를 돌아보면 안 된다 (발소리와 속삭임이 점점 가까워진다).
    /// 도착하면 추적자가 소리를 듣고 20초 안에 따라붙는다. 가 본 층만 고를 수 있다.
    /// </summary>
    public class ElevatorController : MonoBehaviour
    {
        public static ElevatorController Instance { get; private set; }

        [Header("구성 (씬 빌더가 채운다)")]
        [SerializeField] Transform cabin;
        [SerializeField] ElevatorDoorPair cabinDoors;
        [SerializeField] ElevatorLanding[] landings;
        [SerializeField] float shaftCenterX = 12f;
        [SerializeField] float cabinMinZ = 28.5f;
        [SerializeField] float cabinMaxZ = 31.8f;
        [SerializeField] float cabinHalfWidth = 1.7f;

        [Header("규칙")]
        [SerializeField] float callSeconds = 45f;
        [SerializeField] float openSeconds = 8f;
        [SerializeField] float closeSeconds = 2f;
        [SerializeField] float secondsPerFloor = 15f;
        [SerializeField] float noLookSeconds = 7f;
        [SerializeField] float chaserArriveSeconds = 20f;

        public bool Unlocked { get; private set; }
        public ElevatorState State { get; private set; } = ElevatorState.Idle;
        public int CabinFloor { get; private set; } = TowerLayout.Floors - 1;
        public int DestFloor { get; private set; }
        public float CallSecondsLeft { get; private set; }
        public float RideSecondsLeft { get; private set; }
        public float NoLookSecondsLeft { get; private set; }
        public string Message { get; private set; } = "";
        public float MessageTime { get; private set; } = -100f;

        public bool HasPower => PowerGrid.Instance != null && PowerGrid.Instance.Complete;

        /// <summary>플레이어가 엘리베이터 안에 있는가.</summary>
        public bool PlayerInside
        {
            get
            {
                if (player == null || cabin == null) return false;
                Vector3 p = player.transform.position;
                return Mathf.Abs(p.x - shaftCenterX) < cabinHalfWidth && p.z > cabinMinZ && p.z < cabinMaxZ
                       && Mathf.Abs(p.y - cabin.position.y) < 2.5f;
            }
        }

        /// <summary>층 선택 안내를 보여 줄 때인가.</summary>
        public bool ShowFloorSelect => Unlocked && PlayerInside && (State == ElevatorState.DoorsOpen || State == ElevatorState.Idle);

        PlayerController player;
        float rideElapsed, rideDuration, startY, destY;
        float noiseTimer;
        bool dreadActive;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            // 승강기는 처음에 맨 위층에 서 있다
            SnapCabinToFloor(CabinFloor);
        }

        void Say(string msg)
        {
            Message = msg;
            MessageTime = Time.time;
        }

        // ---------- 조건 3: 잠금 해제 ----------

        public void TryUnlock(Inventory inv)
        {
            if (!HasPower) { inv.Say("전원이 들어오지 않는다"); return; }
            if (!inv.Has(ItemType.SecurityCard)) { inv.Say("출입카드가 필요하다"); return; }
            Unlocked = true;
            inv.Say("엘리베이터 잠금이 해제되었다");
            if (GameAudio.Instance != null) GameAudio.Instance.PlayDing();
        }

        // ---------- 조건 4: 호출 ----------

        public void Call(int floor)
        {
            if (!Unlocked || State != ElevatorState.Idle) return;

            if (CabinFloor == floor)
            {
                // 이미 이 층에 서 있다: 바로 문을 연다
                OpenAt(floor);
                return;
            }

            State = ElevatorState.Called;
            DestFloor = floor;
            CallSecondsLeft = callSeconds;
            noiseTimer = 0f;
            Say("엘리베이터를 호출했다. 도착까지 45초...");
            if (GameAudio.Instance != null) GameAudio.Instance.SetElevatorHum(true);
        }

        // ---------- 상태 갱신 ----------

        void Update()
        {
            if (GameFlow.Instance != null && GameFlow.Instance.State != GameState.Playing) return;

            switch (State)
            {
                case ElevatorState.Called: UpdateCalled(); break;
                case ElevatorState.DoorsOpen: UpdateDoorsOpen(); break;
                case ElevatorState.Idle: HandleFloorKeys(); break;
                case ElevatorState.Closing: UpdateClosing(); break;
                case ElevatorState.Riding: UpdateRide(); break;
            }
        }

        void UpdateCalled()
        {
            CallSecondsLeft -= Time.deltaTime;

            // 승강기가 내려오는 소리: 주기적으로 소음이 나서 추적자가 호기심을 갖는다
            noiseTimer -= Time.deltaTime;
            if (noiseTimer <= 0f)
            {
                noiseTimer = 6f;
                var lp = landings[DestFloor].hallPoint;
                if (lp != null) NoiseSystem.Emit(lp.position, 20f);
            }

            if (CallSecondsLeft <= 0f)
            {
                SnapCabinToFloor(DestFloor);
                if (GameAudio.Instance != null) { GameAudio.Instance.SetElevatorHum(false); GameAudio.Instance.PlayDing(); }
                OpenAt(DestFloor);
            }
        }

        /// <summary>그 층에서 문을 연다. 8초 뒤 자동으로 닫힌다.</summary>
        void OpenAt(int floor)
        {
            CabinFloor = floor;
            State = ElevatorState.DoorsOpen;
            openTimer = openSeconds;
            SetDoors(floor, true, 1.5f);
            Say($"문이 열렸다. {Mathf.RoundToInt(openSeconds)}초 안에 타야 한다");
        }

        float openTimer;

        void UpdateDoorsOpen()
        {
            openTimer -= Time.deltaTime;
            HandleFloorKeys();
            if (State != ElevatorState.DoorsOpen) return;   // 층을 골랐으면 상태가 바뀌었다

            if (openTimer <= 0f)
            {
                SetDoors(CabinFloor, false, closeSeconds);
                State = ElevatorState.Idle;
            }
        }

        // ---------- 조건 5: 탑승 후 층 선택, 문 닫힘 ----------

        void HandleFloorKeys()
        {
            if (!ShowFloorSelect) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            for (int f = 0; f < TowerLayout.Floors; f++)
            {
                Key key = Key.Digit1 + f;
                if (!kb[key].wasPressedThisFrame) continue;

                if (f == CabinFloor) { Say("이미 이 층이다"); return; }
                if (MapTracker.Instance != null && !MapTracker.Instance.VisitedFloor(f))
                {
                    Say("아직 가 보지 않은 층이다");
                    return;
                }
                BeginTrip(f);
                return;
            }
        }

        void BeginTrip(int dest)
        {
            DestFloor = dest;
            State = ElevatorState.Closing;
            closeTimer = closeSeconds;
            SetDoors(CabinFloor, false, closeSeconds);
            if (player != null) player.InputLocked = true;
            Say($"{TowerLayout.Label(dest)}... 문이 닫힌다");
        }

        float closeTimer;

        void UpdateClosing()
        {
            closeTimer -= Time.deltaTime;

            // 버튼을 누르느라 안쪽 벽(+Z)을 향해 돌아선다
            if (player != null)
            {
                float yaw = Mathf.MoveTowardsAngle(player.transform.eulerAngles.y, 0f, 240f * Time.deltaTime);
                player.Teleport(player.transform.position, yaw);
            }

            if (closeTimer <= 0f) StartRide();
        }

        void StartRide()
        {
            State = ElevatorState.Riding;
            rideElapsed = 0f;
            rideDuration = secondsPerFloor * Mathf.Abs(DestFloor - CabinFloor);
            startY = cabin.position.y;
            destY = TowerLayout.FloorY(DestFloor);

            if (player != null)
            {
                player.transform.SetParent(cabin, true);
                player.InputLocked = false;
                player.MoveLocked = true;     // 서 있기만 하고, 시점만 돌릴 수 있다
            }

            dreadActive = true;
            if (GameAudio.Instance != null)
            {
                GameAudio.Instance.SetElevatorHum(true);
                GameAudio.Instance.StartDread(noLookSeconds);
            }
            Say("뒤에서 무언가 다가온다... 돌아보지 마");
        }

        // ---------- 조건 6: 뒤를 돌아보면 안 된다 ----------

        void UpdateRide()
        {
            rideElapsed += Time.deltaTime;
            RideSecondsLeft = Mathf.Max(0f, rideDuration - rideElapsed);
            NoLookSecondsLeft = Mathf.Max(0f, noLookSeconds - rideElapsed);

            float k = Mathf.Clamp01(rideElapsed / rideDuration);
            var pos = cabin.position;
            pos.y = Mathf.Lerp(startY, destY, k);
            cabin.position = pos;

            if (dreadActive)
            {
                if (rideElapsed < noLookSeconds)
                {
                    if (LookedBack()) { Fail(); return; }
                }
                else
                {
                    dreadActive = false;
                    if (GameAudio.Instance != null) GameAudio.Instance.StopDread();
                    Say("소리가 멎었다...");
                }
            }

            if (rideElapsed >= rideDuration) ArriveFromRide();
        }

        bool LookedBack()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 f = cam.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.01f) return false;
            // 안쪽 벽(+Z)을 보고 있어야 한다. 문 쪽(-Z)으로 크게 돌면 들킨다.
            return Vector3.Dot(f.normalized, Vector3.forward) < -0.35f;
        }

        void Fail()
        {
            State = ElevatorState.Failed;
            dreadActive = false;
            if (GameAudio.Instance != null) { GameAudio.Instance.StopDread(); GameAudio.Instance.SetElevatorHum(false); }

            var chasers = FindObjectsByType<ChaserAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (chasers.Length == 0 || player == null) return;

            // 돌아본 순간, 바로 뒤(문 쪽)에 추적자가 서 있다
            Vector3 behind = player.transform.position + Vector3.back * 1.3f;
            ChaserAI.ForceCatch(chasers[0], behind, Quaternion.LookRotation(Vector3.forward));
        }

        void ArriveFromRide()
        {
            if (player != null)
            {
                player.transform.SetParent(null, true);
                player.MoveLocked = false;
            }
            dreadActive = false;

            SnapCabinToFloor(DestFloor);
            CabinFloor = DestFloor;
            if (GameAudio.Instance != null)
            {
                GameAudio.Instance.StopDread();
                GameAudio.Instance.SetElevatorHum(false);
                GameAudio.Instance.PlayDing();
            }

            OpenAt(DestFloor);

            // 도착 소리를 듣고 그 층의 추적자들이 20초 안에 따라붙는다
            var lp = landings[DestFloor].hallPoint;
            if (lp != null)
            {
                NoiseSystem.Emit(lp.position, 30f);
                foreach (var c in ChaserAI.Active)
                    if (c != null) c.Alert(lp.position, chaserArriveSeconds);
            }
            Say($"{TowerLayout.Label(DestFloor)}에 도착했다. 무언가 다가오고 있다...");
        }

        // ---------- 유틸 ----------

        void SnapCabinToFloor(int floor)
        {
            if (cabin == null) return;
            var pos = cabin.position;
            pos.y = TowerLayout.FloorY(floor);
            cabin.position = pos;
        }

        void SetDoors(int floor, bool open, float seconds)
        {
            if (landings != null && floor >= 0 && floor < landings.Length && landings[floor] != null && landings[floor].doors != null)
                landings[floor].doors.SetOpen(open, seconds);
            if (cabinDoors != null) cabinDoors.SetOpen(open, seconds);
        }
    }
}
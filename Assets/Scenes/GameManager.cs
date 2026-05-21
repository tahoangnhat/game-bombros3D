// =====================================================
// Assets/Scripts/Manager/GameManager.cs
// BoomBros - Game Manager (Singleton)
// =====================================================
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace BoomBros.Manager
{
    public class GameManager : MonoBehaviour
    {
        // ── Singleton ──────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ── State Machine ──────────────────────────────
        public enum GameState
        {
            Waiting,        // Đang chờ đủ người
            Countdown,      // Đếm ngược 3-2-1
            Playing,        // Đang chơi
            RoundEnd,       // Kết thúc round
            GameEnd         // Kết thúc game
        }

        [Header("Game State")]
        [SerializeField] private GameState _currentState = GameState.Waiting;
        public GameState CurrentState => _currentState;

        // ── Config ─────────────────────────────────────
        [Header("Game Config")]
        [SerializeField] private float _roundTimeLimit = 180f;   // 3 phút
        [SerializeField] private int   _maxRounds      = 3;
        [SerializeField] private float _countdownTime  = 3f;

        // ── Runtime ────────────────────────────────────
        [Header("Runtime (Read Only)")]
        [SerializeField] private float _timeRemaining;
        [SerializeField] private int   _currentRound = 1;
        [SerializeField] private int   _playersAlive;

        // ── References ─────────────────────────────────
        [Header("References")]
        [SerializeField] private Transform[] _spawnPoints;

        // ── Events ─────────────────────────────────────
        public static event System.Action<GameState>    OnStateChanged;
        public static event System.Action<float>        OnTimerUpdated;
        public static event System.Action<int>          OnPlayerDied;   // playerIndex
        public static event System.Action<int>          OnRoundEnd;     // winnerIndex

        // ──────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            _timeRemaining = _roundTimeLimit;
            ChangeState(GameState.Playing);
        }

        private void Update()
        {
            if (_currentState != GameState.Playing) return;

            _timeRemaining -= Time.deltaTime;
            OnTimerUpdated?.Invoke(_timeRemaining);

            if (_timeRemaining <= 0f)
                HandleTimeOut();
        }

        // ── State Machine ──────────────────────────────
        public void ChangeState(GameState newState)
        {
            _currentState = newState;
            OnStateChanged?.Invoke(newState);

            switch (newState)
            {
                case GameState.Waiting:   OnEnterWaiting();   break;
                case GameState.Countdown: OnEnterCountdown(); break;
                case GameState.Playing:   OnEnterPlaying();   break;
                case GameState.RoundEnd:  OnEnterRoundEnd();  break;
                case GameState.GameEnd:   OnEnterGameEnd();   break;
            }
        }

        private void OnEnterWaiting()
        {
            Debug.Log("[GameManager] Waiting for players...");
        }

        private void OnEnterCountdown()
        {
            StartCoroutine(CountdownRoutine());
        }

        private IEnumerator CountdownRoutine()
        {
            float t = _countdownTime;
            while (t > 0)
            {
                Debug.Log($"[GameManager] Countdown: {Mathf.CeilToInt(t)}");
                yield return new WaitForSeconds(1f);
                t -= 1f;
            }
            ChangeState(GameState.Playing);
        }

        private void OnEnterPlaying()
        {
            _timeRemaining = _roundTimeLimit;
            Debug.Log("[GameManager] Game Started!");
        }

        private void OnEnterRoundEnd()
        {
            if (_currentRound >= _maxRounds)
                ChangeState(GameState.GameEnd);
            else
            {
                _currentRound++;
                StartCoroutine(RestartRoundRoutine());
            }
        }

        private IEnumerator RestartRoundRoutine()
        {
            yield return new WaitForSeconds(2f);
            ChangeState(GameState.Countdown);
        }

        private void OnEnterGameEnd()
        {
            Debug.Log("[GameManager] Game Over!");
        }

        // ── Public Methods ─────────────────────────────
        public void NotifyPlayerDied(int playerIndex)
        {
            _playersAlive--;
            OnPlayerDied?.Invoke(playerIndex);
            Debug.Log($"[GameManager] Player {playerIndex} died. {_playersAlive} remaining.");

            if (_playersAlive <= 1)
                ChangeState(GameState.RoundEnd);
        }

        public void StartGame()
        {
            if (_currentState != GameState.Waiting) return;
            ChangeState(GameState.Countdown);
        }

        private void HandleTimeOut()
        {
            Debug.Log("[GameManager] Time's up!");
            ChangeState(GameState.RoundEnd);
        }
    }
}
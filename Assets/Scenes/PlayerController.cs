using UnityEngine;
using BoomBros.Manager;

namespace BoomBros.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerController : MonoBehaviour
    {
        // ── Identity ───────────────────────────────────
        [Header("Identity")]
        [SerializeField] private int    _playerIndex = 0;
        [SerializeField] private string _playerName  = "Player 1";

        // ── Movement ───────────────────────────────────
        [Header("Movement")]
        [SerializeField] private float _moveSpeed    = 5f;
        [SerializeField] private float _acceleration = 25f;

        // ── Stats ──────────────────────────────────────
        [Header("Stats")]
        [SerializeField] private int   _maxBombs    = 1;
        [SerializeField] private int   _bombRange   = 2;
        [SerializeField] private bool  _isDead      = false;

        // ── Runtime ────────────────────────────────────
        private int _currentBombs;    // Bombs hiện đang được đặt

        // ── References ─────────────────────────────────
        private Rigidbody  _rb;
        private Animator   _animator;

        // ── Input (sẽ thay bằng PUN2 sau) ─────────────
        private Vector3 _inputDirection;
        private bool    _placeBombInput;

        // ── Events ─────────────────────────────────────
        public static event System.Action<int> OnPlayerDied;

        // ──────────────────────────────────────────────
        private void Awake()
        {
            _rb       = GetComponent<Rigidbody>();
            _animator = GetComponentInChildren<Animator>();

            _rb.freezeRotation = true;
            _rb.constraints    = RigidbodyConstraints.FreezeRotation;
            _currentBombs      = 0;
        }

        private void OnEnable()
        {
            GameManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameManager.OnStateChanged -= HandleStateChanged;
        }

        private void Update()
        {
            if (_isDead) return;
            if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

            GatherInput();
            HandleBombPlacement();
        }

        private void FixedUpdate()
        {
            if (_isDead) return;
            if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

            ApplyMovement();
        }

        // ── Input ──────────────────────────────────────
        private void GatherInput()
        {
            // TODO Tuần 3: Thay bằng PUN2 Input, mỗi player có axis riêng
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            // Grid-aligned movement (Boom style: chỉ 1 hướng tại 1 thời điểm)
            if (Mathf.Abs(h) >= Mathf.Abs(v))
                _inputDirection = new Vector3(h, 0, 0).normalized;
            else
                _inputDirection = new Vector3(0, 0, v).normalized;

            _placeBombInput = Input.GetButtonDown("PlaceBomb");
        }

        private void ApplyMovement()
        {
            Vector3 targetVelocity = _inputDirection * _moveSpeed;

            // Smooth acceleration
            _rb.linearVelocity = Vector3.MoveTowards(
                _rb.linearVelocity,
                new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z),
                _acceleration * Time.fixedDeltaTime
            );

            // Animation
            if (_animator != null)
            {
                _animator.SetBool("IsMoving", _inputDirection.magnitude > 0.1f);
                if (_inputDirection.magnitude > 0.1f)
                    transform.forward = _inputDirection;
            }
        }

        // ── Bomb Placement ─────────────────────────────
        private void HandleBombPlacement()
        {
            if (!_placeBombInput) return;
            if (_currentBombs >= _maxBombs) return;

            PlaceBomb();
        }

        private void PlaceBomb()
        {
            // Grid-snapping: đặt bomb vào ô grid gần nhất
            Vector3 bombPos = new Vector3(
                Mathf.Round(transform.position.x),
                0f,
                Mathf.Round(transform.position.z)
            );

            // TODO: BombManager.Instance.SpawnBomb(bombPos, _bombRange, this);
            _currentBombs++;
            Debug.Log($"[Player {_playerIndex}] Placed bomb at {bombPos}. ({_currentBombs}/{_maxBombs})");
        }

        public void OnBombExploded()
        {
            // Gọi khi bomb của player này nổ
            _currentBombs = Mathf.Max(0, _currentBombs - 1);
        }

        // ── Damage / Death ─────────────────────────────
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Explosion"))
                TakeDamage();

            if (other.CompareTag("PowerUp"))
                CollectPowerUp(other.gameObject);
        }

        private void TakeDamage()
        {
            if (_isDead) return;
            Die();
        }

        private void Die()
        {
            _isDead = true;
            _rb.linearVelocity = Vector3.zero;
            _rb.isKinematic = true;

            if (_animator != null)
                _animator.SetTrigger("Die");

            OnPlayerDied?.Invoke(_playerIndex);
            GameManager.Instance.NotifyPlayerDied(_playerIndex);

            Destroy(gameObject, 1.5f);
        }

        // ── Power Up ───────────────────────────────────
        private void CollectPowerUp(GameObject powerUp)
        {
            // TODO: Đọc PowerUpType từ component và apply
            Debug.Log($"[Player {_playerIndex}] Collected PowerUp: {powerUp.name}");
        }

        // ── Power Up Modifiers (gọi từ PowerUpManager) ─
        public void IncreaseBombCount()  => _maxBombs = Mathf.Min(_maxBombs + 1, 8);
        public void IncreaseBombRange()  => _bombRange = Mathf.Min(_bombRange + 1, 10);
        public void IncreaseSpeed()      => _moveSpeed = Mathf.Min(_moveSpeed + 1f, 10f);

        // ── State Handling ─────────────────────────────
        private void HandleStateChanged(GameManager.GameState state)
        {
            if (state == GameManager.GameState.Playing)
            {
                _isDead       = false;
                _currentBombs = 0;
                _rb.isKinematic = false;
            }
        }

        // ── Debug ──────────────────────────────────────
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}
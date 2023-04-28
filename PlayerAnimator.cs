using UnityEngine;
using Random = UnityEngine.Random;

namespace Controller
{
    /// <summary>
    /// This is a pretty filthy script. I was just arbitrarily adding to it as I went.
    /// You won't find any programming prowess here.
    /// This is a supplementary script to help with effects and animation. Basically a juice factory.
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private AudioSource source;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private ParticleSystem jumpParticles, launchParticles;
        [SerializeField] private ParticleSystem moveParticles, landParticles;
        [SerializeField] private AudioClip[] footsteps;
        [SerializeField] private float _maxTilt = .1f;
        [SerializeField] private float _tiltSpeed = 1;
        [SerializeField, Range(1f, 3f)] private float maxIdleSpeed = 2;
        [SerializeField] private float maxParticleFallSpeed = -40;

        private IPlayerController player;
        private bool isPlayerGrounded;
        private ParticleSystem.MinMaxGradient currentGradient;
        private Vector2 movement;

        void Awake() => player = GetComponentInParent<IPlayerController>();

        void Update()
        {
            if (player == null) return;

            // Flip the sprite
            if (player.Input.X != 0) transform.localScale = new Vector3(player.Input.X > 0 ? 1 : -1, 1, 1);

            // Lean while running
            var targetRotVector = new Vector3(0, 0, Mathf.Lerp(-_maxTilt, _maxTilt, Mathf.InverseLerp(-1, 1, player.Input.X)));
            animator.transform.rotation = Quaternion.RotateTowards(animator.transform.rotation, Quaternion.Euler(targetRotVector), _tiltSpeed * Time.deltaTime);

            // Speed up idle while running
            animator.SetFloat(IdleSpeedKey, Mathf.Lerp(1, maxIdleSpeed, Mathf.Abs(player.Input.X)));

            // Splat
            if (player.LandingThisFrame)
            {
                animator.SetTrigger(GroundedKey);
                source.PlayOneShot(footsteps[Random.Range(0, footsteps.Length)]);
            }

            // Jump effects
            if (player.JumpingThisFrame)
            {
                animator.SetTrigger(JumpKey);
                animator.ResetTrigger(GroundedKey);

                // Only play particles when grounded (avoid coyote)
                if (player.Grounded)
                {
                    SetColor(jumpParticles);
                    SetColor(launchParticles);
                    jumpParticles.Play();
                }
            }

            // Play landing effects and begin ground movement effects
            if (!isPlayerGrounded && player.Grounded)
            {
                isPlayerGrounded = true;
                moveParticles.Play();
                landParticles.transform.localScale = Vector3.one * Mathf.InverseLerp(0, maxParticleFallSpeed, movement.y);
                SetColor(landParticles);
                landParticles.Play();
            }
            else if (isPlayerGrounded && !player.Grounded)
            {
                isPlayerGrounded = false;
                moveParticles.Stop();
            }

            // Detect ground color
            var groundHit = Physics2D.Raycast(transform.position, Vector3.down, 2, groundMask);
            if (groundHit && groundHit.transform.TryGetComponent(out SpriteRenderer r))
            {
                currentGradient = new ParticleSystem.MinMaxGradient(r.color * 0.9f, r.color * 1.2f);
                SetColor(moveParticles);
            }

            movement = player.RawMovement; // Previous frame movement is more valuable
        }

        private void OnDisable()
        {
            moveParticles.Stop();
        }

        private void OnEnable()
        {
            moveParticles.Play();
        }

        void SetColor(ParticleSystem ps)
        {
            var main = ps.main;
            main.startColor = currentGradient;
        }

        #region Animation Keys

        private static readonly int GroundedKey = Animator.StringToHash("Grounded");
        private static readonly int IdleSpeedKey = Animator.StringToHash("IdleSpeed");
        private static readonly int JumpKey = Animator.StringToHash("Jump");

        #endregion
    }
}
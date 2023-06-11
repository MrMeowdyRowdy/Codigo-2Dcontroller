using UnityEngine;
using Random = UnityEngine.Random;

namespace Controller.Player
{
    /// <summary>
    /// This is a pretty filthy script. I was just arbitrarily adding to it as I went.
    /// You won't find any programming prowess here.
    /// This is a supplementary script to help with effects and animation. Basically a juice factory.
    /// </summary>
    public class PlayerAnimator : MonoBehaviour
    {
        [SerializeField] private Animator Animador;
        [SerializeField] private Transform Sprite;
        [SerializeField] private AudioSource FuenteAudio;
        [SerializeField] private LayerMask QueEsSuelo;
        [SerializeField] private ParticleSystem PartSalto, PartDespegue;
        [SerializeField] private ParticleSystem PartMovimiento, PartAtterizaje;
        [SerializeField] private AudioClip[] pisada;
        [SerializeField] private float _maxTilt = .1f;
        [SerializeField] private float _tiltSpeed = 1;
        [SerializeField, Range(1f, 3f)] private float VelMaxInactivo = 2;
        [SerializeField] private float PartMaxVelCaida = -40;

        private IPlayerController Jugador;
        private bool EstaEnElPiso;
        private ParticleSystem.MinMaxGradient currentGradient;
        private Vector2 Movimiento;

        

        void Awake() => Jugador = GetComponentInParent<IPlayerController>();

        void Update()
        {
            if (Jugador == null) return;

            // Da vuelta al sprite del jugador
            
            if (Jugador.Input.X != 0)
            {
                Sprite.localScale = new Vector3(Jugador.Input.X,1,1);
            }


            // Se inclina al correr
            var targetRotVector = new Vector3(0, 0, Mathf.Lerp(-_maxTilt, _maxTilt, Mathf.InverseLerp(-1, 1, Jugador.Input.X)));
            Animador.transform.rotation = Quaternion.RotateTowards(Animador.transform.rotation, Quaternion.Euler(targetRotVector), _tiltSpeed * Time.deltaTime);

            // Acelera la velocidad a la animacion de inativo meintras corre
            Animador.SetFloat(IdleSpeedKey, Mathf.Lerp(1, VelMaxInactivo, Mathf.Abs(Jugador.Input.X)));

            // suena splat
            if (Jugador.AterrizandoFrame)
            {
                Animador.SetTrigger(GroundedKey);
                FuenteAudio.PlayOneShot(pisada[Random.Range(0, pisada.Length)]);
            }
            //Efecto de boost
            if (Jugador.BoostingFrame)
            {
                Animador.SetTrigger(BoostKey);
                FuenteAudio.PlayOneShot(pisada[Random.Range(0, pisada.Length)]);
            }
            if (Jugador.DeadFrame)
            {
                Animador.SetTrigger(DeadKey);
                FuenteAudio.PlayOneShot(pisada[Random.Range(0, pisada.Length)]);
            }
            // Efectos de salto
            if (Jugador.SaltandoFrame)
            {
                Animador.SetTrigger(JumpKey);
                Animador.ResetTrigger(GroundedKey);

                // Despliega particulas cuando el jugador esta en el piso y desactiva cuando esta en coyote time
                if (Jugador.EnElPiso)
                {
                    SetColor(PartSalto);
                    SetColor(PartDespegue);
                    PartSalto.Play();
                }
            }

            // reproduce efectos de aterrizaje y vuelve a efectos del piso
            if (!EstaEnElPiso && Jugador.EnElPiso)
            {
                EstaEnElPiso = true;
                PartMovimiento.Play();
                PartAtterizaje.transform.localScale = Vector3.one * Mathf.InverseLerp(0, PartMaxVelCaida, Movimiento.y);
                SetColor(PartAtterizaje);
                PartAtterizaje.Play();
            }
            else if (EstaEnElPiso && !Jugador.EnElPiso)
            {
                EstaEnElPiso = false;
                PartMovimiento.Stop();
            }

            // Detecta el color del suelo
            var groundHit = Physics2D.Raycast(transform.position, Vector3.down, 2, QueEsSuelo);
            if (groundHit && groundHit.transform.TryGetComponent(out SpriteRenderer r))
            {
                currentGradient = new ParticleSystem.MinMaxGradient(r.color * 0.9f, r.color * 1.2f);
                SetColor(PartMovimiento);
            }

            Movimiento = Jugador.RawMovement; // se guarda frames anteriores ya que son valiosos
        }

        private void OnDisable()
        {
            PartMovimiento.Stop();
        }

        private void OnEnable()
        {
            PartMovimiento.Play();
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
        private static readonly int BoostKey = Animator.StringToHash("Booster");
        private static readonly int DeadKey = Animator.StringToHash("Death");

        #endregion
    }
}
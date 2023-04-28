using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Controller
{
    public class PlayerController : MonoBehaviour, IPlayerController
    {
        // Var publicas para llamarlas desde otros segmentos
        public Vector3 Velocity { get; private set; }
        public FrameInput Input { get; private set; }
        public bool JumpingThisFrame { get; private set; }
        public bool LandingThisFrame { get; private set; }
        public Vector3 RawMovement { get; private set; }
        public bool Grounded => colDown;

        private Vector3 lastPosition;
        private float currentHorizontalSpeed, currentVerticalSpeed;

        // Establece colliders
        private bool active;
        void Awake() => Invoke(nameof(Activate), 0.5f);
        void Activate()
        {
            active = true;
        }

        private void Update()
        {
            if (!active)
            {
                return;
            }

            // Calcula velocidad
            Velocity = (transform.position - lastPosition) / Time.deltaTime;
            lastPosition = transform.position;

            GatherInput();
            RunCollisionChecks();

            CalculateWalk(); // Movimiento horizontal
            CalculateJumpApex(); // Cambio velocidad de caida, calculos antes de la gravedad
            CalculateGravity(); // Movimiento vertical
            CalculateJump(); // Puede contrarrestar gravity

            MoveCharacter(); // Hace el movimiento real
            if (Input.X != 0) transform.localScale = new Vector3(Input.X > 0 ? 1 : -1, 1, 1);
        }


        #region Registro Inputs

        private void GatherInput()
        {
            Input = new FrameInput
            {
                JumpDown = UnityEngine.Input.GetButtonDown("Jump"),
                JumpUp = UnityEngine.Input.GetButtonUp("Jump"),
                X = UnityEngine.Input.GetAxisRaw("Horizontal")
            };
            if (Input.JumpDown)
            {
                lastJumpPressed = Time.time;
            }
        }

        #endregion

        #region Colisiones

        [Header("Colisiones")]
        [SerializeField] private Bounds limitesPersonaje;
        [SerializeField] private LayerMask whatisGround;
        [SerializeField] private int conteoDetectores = 3;
        [SerializeField] private float largoRayoDetector = 0.1f;
        [SerializeField] [Range(0.1f, 0.3f)] private float rayBuffer = 0.1f; // Prevents side detectors hitting the ground

        private RayRange raysUp, raysRight, raysDown, raysLeft;
        private bool colUp, colRight, colDown, colLeft;

        private float timeLeftGrounded;

        // Raycast para verificar pre-colisiones
        private void RunCollisionChecks()
        {
            // Genera el rango de los Raycast
            CalculateRayRanged();

            // para piso
            LandingThisFrame = false;
            var groundedCheck = RunDetection(raysDown);
            if (colDown && !groundedCheck) timeLeftGrounded = Time.time; // solo se activa cuando cae al inicio
            else if (!colDown && groundedCheck)
            {
                coyoteActivo = true; // Solo activado cuando toca piso
                LandingThisFrame = true;
            }

            colDown = groundedCheck;

            //para lo demas
            colUp = RunDetection(raysUp);
            colLeft = RunDetection(raysLeft);
            colRight = RunDetection(raysRight);

            bool RunDetection(RayRange range)
            {
                return EvaluateRayPositions(range).Any(point => Physics2D.Raycast(point, range.Dir, largoRayoDetector, whatisGround));
            }
        }

        private void CalculateRayRanged()
        {
            // crea rayos desde los bordes del personaje
            var b = new Bounds(transform.position, limitesPersonaje.size);

            raysDown = new RayRange(b.min.x + rayBuffer, b.min.y, b.max.x - rayBuffer, b.min.y, Vector2.down);
            raysUp = new RayRange(b.min.x + rayBuffer, b.max.y, b.max.x - rayBuffer, b.max.y, Vector2.up);
            raysLeft = new RayRange(b.min.x, b.min.y + rayBuffer, b.min.x, b.max.y - rayBuffer, Vector2.left);
            raysRight = new RayRange(b.max.x, b.min.y + rayBuffer, b.max.x, b.max.y - rayBuffer, Vector2.right);
        }


        private IEnumerable<Vector2> EvaluateRayPositions(RayRange range)
        {
            for (var i = 0; i < conteoDetectores; i++)
            {
                var t = (float)i / (conteoDetectores - 1);
                yield return Vector2.Lerp(range.Start, range.End, t);
            }
        }

        private void OnDrawGizmos()
        {
            // limites del personaje dibujables
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + limitesPersonaje.center, limitesPersonaje.size);

            // trazado de rayos
            if (!Application.isPlaying)
            {
                CalculateRayRanged();
                Gizmos.color = Color.blue;
                foreach (var range in new List<RayRange> { raysUp, raysRight, raysDown, raysLeft })
                {
                    foreach (var point in EvaluateRayPositions(range))
                    {
                        Gizmos.DrawRay(point, range.Dir * largoRayoDetector);
                    }
                }
            }

            if (!Application.isPlaying) return;

            // visualiza una posicion futura, para debug de gravedad
            Gizmos.color = Color.red;
            var move = new Vector3(currentHorizontalSpeed, currentVerticalSpeed) * Time.deltaTime;
            Gizmos.DrawWireCube(transform.position + move, limitesPersonaje.size);
        }

        #endregion

        #region Caminar

        [Header("Caminando")]
        [SerializeField] private float aceleracion = 90;
        [SerializeField] private float moveClamp = 13;
        [SerializeField] private float desAceleracion = 60f;
        [SerializeField] private float apexBonus = 2;

        private void CalculateWalk()
        {
            if (Input.X != 0)
            {
                // Establece la velocidad horizontal de movimiento
                currentHorizontalSpeed += Input.X * aceleracion * Time.deltaTime;

                // Limitador al frame actual
                currentHorizontalSpeed = Mathf.Clamp(currentHorizontalSpeed, -moveClamp, moveClamp);

                // Añade un boost en el apex del salto segun el tiempo que mantenga el boton salto
                var apexBonus = Mathf.Sign(Input.X) * this.apexBonus * apex;
                currentHorizontalSpeed += apexBonus * Time.deltaTime;
            }
            else
            {
                // No input, desacelera
                currentHorizontalSpeed = Mathf.MoveTowards(currentHorizontalSpeed, 0, desAceleracion * Time.deltaTime);
            }

            if (currentHorizontalSpeed > 0 && colRight || currentHorizontalSpeed < 0 && colLeft)
            {
                // No permite caminar por paredes
                currentHorizontalSpeed = 0;
            }
        }

        #endregion

        #region Gravedad

        [Header("Gravedad")]
        [SerializeField] private float fallClamp = -40f;
        [SerializeField] private float velMinCaida = 80f;
        [SerializeField] private float velMaxCaida = 120f;
        private float fallSpeed;

        private void CalculateGravity()
        {
            if (colDown)
            {
                // Move out of the ground
                if (currentVerticalSpeed < 0)
                {
                    currentVerticalSpeed = 0;
                }
            }
            else
            {
                // Add downward force while ascending if we ended the jump early
                var fallSpeed = saltoPronto && currentVerticalSpeed > 0 ? this.fallSpeed * modGravedadFinPronto : this.fallSpeed;

                // Fall
                currentVerticalSpeed -= fallSpeed * Time.deltaTime;

                // Clamp
                if (currentVerticalSpeed < fallClamp) currentVerticalSpeed = fallClamp;
            }
        }

        #endregion

        #region Jump

        [Header("Salto")] 
        [SerializeField] private float alturaSalto = 30;
        [SerializeField] private float rangoApex = 10f;
        [SerializeField] private float rangoCoyoteTime = 0.1f;
        [SerializeField] private float bufferSalto = 0.1f;
        [SerializeField] private float modGravedadFinPronto = 3;
        private bool coyoteActivo;
        private bool saltoPronto = true;
        private float apex; // Se vuelve 1 al estar en el apex
        private float lastJumpPressed;
        private bool CanUseCoyote => coyoteActivo && !colDown && timeLeftGrounded + rangoCoyoteTime > Time.time;
        private bool HasBufferedJump => colDown && lastJumpPressed + bufferSalto > Time.time;

        private void CalculateJumpApex()
        {
            if (!colDown)
            {
                // incrementa en cuanto se acerque al apex
                apex = Mathf.InverseLerp(rangoApex, 0, Mathf.Abs(Velocity.y));
                fallSpeed = Mathf.Lerp(velMinCaida, velMaxCaida, apex);
            }
            else
            {
                apex = 0;
            }
        }

        private void CalculateJump()
        {
            // Salta si hay coyote, hay un bufer o se ha pulsado la tecla en el suelo
            if (Input.JumpDown && CanUseCoyote || HasBufferedJump)
            {
                currentVerticalSpeed = alturaSalto;
                saltoPronto = false;
                coyoteActivo = false;
                timeLeftGrounded = float.MinValue;
                JumpingThisFrame = true;
            }
            else
            {
                JumpingThisFrame = false;
            }

            // Termina el salto pronto en caso de soltar tecla
            if (!colDown && Input.JumpUp && !saltoPronto && Velocity.y > 0)
            {
                //determina el salto corto
                saltoPronto = true;
            }

            if (colUp)
            {
                if (currentVerticalSpeed > 0)
                {
                    currentVerticalSpeed = 0;
                }

            }
        }

        #endregion

        #region Movimiento

        [Header("Movimiento")]
        [SerializeField, Tooltip("Incrementar este valor hace que las colisiones se detecten mejor a coste de rendemiento")]
        private int freeColliderIterations = 10;

        // Castea limites para colisiones futuras
        private void MoveCharacter()
        {
            var pos = transform.position;
            RawMovement = new Vector3(currentHorizontalSpeed, currentVerticalSpeed); // Used externally
            var move = RawMovement * Time.deltaTime;
            var furthestPoint = pos + move;

            // Revisa ultimo movimeinto y si no hay colision no realiza nada.
            var hit = Physics2D.OverlapBox(furthestPoint, limitesPersonaje.size, 0, whatisGround);
            if (!hit)
            {
                transform.position += move;
                return;
            }

            // caso de que si haya golpe revisaremos posiciones posibles empezando por la mas lejana
            var positionToMoveTo = transform.position;
            for (int i = 1; i < freeColliderIterations; i++)
            {
                // revisamos en incrementos menos la mas lejana
                var t = (float)i / freeColliderIterations;
                var posToTry = Vector2.Lerp(pos, furthestPoint, t);

                if (Physics2D.OverlapBox(posToTry, limitesPersonaje.size, 0, whatisGround))
                {
                    transform.position = positionToMoveTo;

                    // desplazamiento del jugador en caso de pequeños errorres
                    if (i == 1)
                    {
                        if (currentVerticalSpeed < 0) currentVerticalSpeed = 0;
                        var dir = transform.position - hit.transform.position;
                        transform.position += dir.normalized * move.magnitude;
                    }

                    return;
                }

                positionToMoveTo = posToTry;
            }
           
        }

        #endregion
    }
}
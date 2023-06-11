using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Controller.Player
{
    public class PlayerController : MonoBehaviour, IPlayerController
    {
        // Var publicas para llamarlas desde otros segmentos
        [SerializeField] private Vector3 PlayerSpawn;
        public Vector3 Velocidad { get; private set; }
        public FrameInput Input { get; private set; }
        public bool SaltandoFrame { get; private set; }
        public bool AterrizandoFrame { get; private set; }
        public bool BoostingFrame { get; private set; }
        public bool DeadFrame { get; set; }
        public Vector3 RawMovement { get; private set; }
        public bool EnElPiso => colDown;
        private int boostRemaining;

        private Vector3 lastPosition;
        private float VelActualHorizontal, VelActualVertical;

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

            if (colDown)
                boostRemaining = boostQuantity;

            // Calcula velocidad
            Velocidad = (transform.position - lastPosition) / Time.deltaTime;
            lastPosition = transform.position;

            GatherInput();
            RunCollisionChecks();

            CalculateWalk(); // Movimiento horizontal
            CalculateJumpApex(); // Cambio velocidad de caida, calculos antes de la gravedad
            CalculateGravity(); // Movimiento vertical
            CalculateJump(); // Puede contrarrestar gravity
            Paredes();

            MoveCharacter(); // Hace el movimiento real
            //if (Input.X != 0) transform.localScale = new Vector3(Input.X > 0 ? 1 : -1, 1, 1);
            Muerte();
            Portales();

        }


        #region Registro Inputs

        private void GatherInput()
        {
            Input = new FrameInput
            {
                JumpDown = UnityEngine.Input.GetButtonDown("Jump"),
                JumpUp = UnityEngine.Input.GetButtonUp("Jump"),
                X = UnityEngine.Input.GetAxisRaw("Horizontal"),
                Boost = UnityEngine.Input.GetButtonDown("Boost"),
                Y = UnityEngine.Input.GetAxisRaw("Vertical"),
                Climb = UnityEngine.Input.GetButton("Climb"),

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
        [SerializeField] private LayerMask whatisEnemy;
        [SerializeField] private LayerMask whatisPortal;
        [SerializeField] private LayerMask whatisBooster;
        [SerializeField] private int conteoDetectores = 3;
        [SerializeField] private float largoRayoDetector = 0.1f;
        [SerializeField] [Range(0.1f, 0.3f)] private float rayBuffer = 0.1f; // Prevents side detectors hitting the ground

        private RayRange raysUp, raysRight, raysDown, raysLeft;
        private bool colUp, colRight, colDown, colLeft, 
            colUpEnemy, colRightEnemy, colLeftEnemy, 
            colLeftPortal, colRightPortal, colUpPortal, colDownPortal,
            colUpBooster, colDownBooster, colLeftBooster, colRightBooster;

        private float timeLeftGrounded;

        // Raycast para verificar pre-colisiones
        private void RunCollisionChecks()
        {
            // Genera el rango de los Raycast
            CalculateRayRanged();

            // para piso
            AterrizandoFrame = false;
            var groundedCheck = RunDetection(raysDown);
            if (colDown && !groundedCheck) timeLeftGrounded = Time.time; // solo se activa cuando cae al inicio
            else if (!colDown && groundedCheck)
            {
                coyoteActivoJump = true; // Solo activado cuando toca piso
                AterrizandoFrame = true;
            }

            colDown = groundedCheck;

            //para lo demas
            colUp = RunDetection(raysUp);
            colLeft = RunDetection(raysLeft);
            colRight = RunDetection(raysRight);
            colUpEnemy = RunDetectionEnemy(raysUp);
            colLeftEnemy = RunDetectionEnemy(raysLeft);
            colRightEnemy = RunDetectionEnemy(raysRight);
            colUpPortal = RunDetectionPortal(raysUp);
            colLeftPortal = RunDetectionPortal(raysLeft);
            colRightPortal = RunDetectionPortal(raysRight);
            colDownPortal = RunDetectionPortal(raysDown);
            colUpBooster = RunDetectionBooster(raysUp);
            colLeftBooster = RunDetectionBooster(raysLeft);
            colRightBooster = RunDetectionBooster(raysRight);
            colDownBooster = RunDetectionBooster(raysDown);

            bool RunDetection(RayRange range)
            {
                return EvaluateRayPositions(range).Any(point => Physics2D.Raycast(point, range.Dir, largoRayoDetector, whatisGround));
            }

            bool RunDetectionEnemy(RayRange range)
            {
                return EvaluateRayPositions(range).Any(point => Physics2D.Raycast(point, range.Dir, largoRayoDetector, whatisEnemy));
            }

            bool RunDetectionPortal(RayRange range)
            {
                return EvaluateRayPositions(range).Any(point => Physics2D.Raycast(point, range.Dir, largoRayoDetector, whatisPortal));
            }
            bool RunDetectionBooster(RayRange range)
            {
                return EvaluateRayPositions(range).Any(point => Physics2D.Raycast(point, range.Dir, largoRayoDetector, whatisBooster));
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
            var move = new Vector3(VelActualHorizontal, VelActualVertical) * Time.deltaTime;
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
                VelActualHorizontal += Input.X * aceleracion * Time.deltaTime;

                // Limitador al frame actual
                VelActualHorizontal = Mathf.Clamp(VelActualHorizontal, -moveClamp, moveClamp);

                // Añade un boost en el apex del salto segun el tiempo que mantenga el boton salto
                var apexBonus = Mathf.Sign(Input.X) * this.apexBonus * apexJump;
                VelActualHorizontal += apexBonus * Time.deltaTime;
            }
            else
            {
                // No input, desacelera
                VelActualHorizontal = Mathf.MoveTowards(VelActualHorizontal, 0, desAceleracion * Time.deltaTime);
            }

            if (VelActualHorizontal > 0 && colRight || VelActualHorizontal < 0 && colLeft)
            {
                // No permite caminar por paredes
                VelActualHorizontal = 0;
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
                if (VelActualVertical < 0)
                {
                    VelActualVertical = 0;
                }
            }
            
            else
            {
                // Add downward force while ascending if we ended the jump early
                var fallSpeed = saltoProntoJump && VelActualVertical > 0 ? this.fallSpeed * modGravedadFinProntoJump : this.fallSpeed;

                // Fall
                VelActualVertical -= fallSpeed * Time.deltaTime;

                // Clamp
                if (VelActualVertical < fallClamp) VelActualVertical = fallClamp;
            }
        }

        #endregion

        #region Jump

        [Header("Salto")] 
        [SerializeField] private float alturaSaltoJump = 30;
        [SerializeField] private float boostForce = 30;
        [SerializeField] private int boostQuantity = 1;
        [SerializeField] private float rangoApexJump = 10f;
        [SerializeField] private float rangoCoyoteTimeJump = 0.1f;
        [SerializeField] private float bufferSaltoJump = 0.1f;
        [SerializeField] private float modGravedadFinProntoJump = 3;

        private bool coyoteActivoJump;
        private bool saltoProntoJump = true;
        private float apexJump; // Se vuelve 1 al estar en el apex
        private float lastJumpPressed;
        private bool CanUseCoyote => coyoteActivoJump && !colDown && timeLeftGrounded + rangoCoyoteTimeJump > Time.time;
        private bool HasBufferedJump => colDown && lastJumpPressed + bufferSaltoJump > Time.time;

        private void CalculateJumpApex()
        {
            if (!colDown)
            {
                // incrementa en cuanto se acerque al apex
                apexJump = Mathf.InverseLerp(rangoApexJump, 0, Mathf.Abs(Velocidad.y));
                fallSpeed = Mathf.Lerp(velMinCaida, velMaxCaida, apexJump);
            }
            else
            {
                apexJump = 0;
            }
        }

        private void CalculateJump()
        {
            // Salta si hay coyote, hay un bufer o se ha pulsado la tecla en el suelo
            if (Input.JumpDown && CanUseCoyote || HasBufferedJump)
            {
                VelActualVertical = alturaSaltoJump;
                saltoProntoJump = false;
                coyoteActivoJump = false;
                timeLeftGrounded = float.MinValue;
                SaltandoFrame = true;
            }
            else if(Input.Boost && boostRemaining > 0) //Para boost
            {
                transform.position = new Vector3 (transform.position.x + (boostForce * Input.X), transform.position.y + (boostForce * Input.Y));
                BoostingFrame = true;
                boostRemaining--;
            }

            else
            {
                SaltandoFrame = false;
                BoostingFrame = false;
            }

            // Termina el salto pronto en caso de soltar tecla
            if (!colDown && Input.JumpUp && !saltoProntoJump && Velocidad.y > 0)
            {
                //determina el salto corto
                saltoProntoJump = true;
            }

            if (colUp)
            {
                if (VelActualVertical > 0)
                {
                    VelActualVertical = 0;
                }

            }
        }

        #endregion

        #region Cosas de paredes
        [Header("Paredes")]
        [SerializeField] private float VelMaxEnPared = 10;
        [SerializeField] private float horizontalSalto = 30;
        [SerializeField] private float moveClampPared = 10;
        private void Paredes()
        {
            if (Input.Climb && (Input.Y != 0 && (colRight || colLeft)))
            {
                // Establece la velocidad horizontal de movimiento
                VelActualVertical += Input.Y * aceleracion ;

                

                // Limitador al frame actual
                VelActualVertical = Mathf.Clamp(VelActualVertical, -moveClampPared, moveClampPared);
            }
            else if (Input.Climb && (Input.Y == 0 && (colRight || colLeft)))
            {
                // Bloquea velocidad en pared si no hay inputs en Y
                VelActualVertical = 0;
            }
            else if (((colRight && Input.X == 1) || (colLeft && Input.X == -1)) && VelActualVertical <-VelMaxEnPared)
            {
                // Resbalarse en la pared mas lento
                VelActualVertical = -VelMaxEnPared; 
            }

            if (!colDown && ((Input.JumpDown && colRight) || (Input.JumpDown && colLeft)))
            {
                //Saltos de pared
                VelActualVertical = alturaSaltoJump;
                VelActualHorizontal = -Input.X * horizontalSalto;
                saltoProntoJump = false;
                coyoteActivoJump = false;
                timeLeftGrounded = float.MinValue;
                SaltandoFrame = true;
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
            RawMovement = new Vector3(VelActualHorizontal, VelActualVertical); // Usado de manera externa
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
                        if (VelActualVertical < 0) VelActualVertical = 0;
                        var dir = transform.position - hit.transform.position;
                        transform.position += dir.normalized * move.magnitude;
                    }

                    return;
                }

                positionToMoveTo = posToTry;
            }
           
        }

        #endregion

        #region Muerte
        public void Muerte()
        {
            if (colLeftEnemy || colRightEnemy || colUpEnemy)
            {
                DeadFrame = true;
                transform.position = PlayerSpawn;
            }
            else
            {
                DeadFrame = false;
            }
        }
        #endregion

        #region Portales
        public void Portales()
        {
            if (colLeftPortal || colRightPortal || colUpPortal || colDownPortal)
            {
                SceneManager.LoadScene(2);
            }
        }
        #endregion

        #region Boosters
        public void MasBoosters()
        {
            if (colLeftBooster || colRightBooster || colUpBooster || colDownBooster)
            {
                boostRemaining += 1;
            }
        }
        #endregion
    }
}
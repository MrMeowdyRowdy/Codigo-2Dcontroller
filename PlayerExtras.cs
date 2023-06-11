using UnityEngine;

namespace Controller.Player
{

    public struct FrameInput
    {
        public float X, Y;
        public bool JumpDown;
        public bool JumpUp;

        public bool Boost { get; internal set; }
        public bool Climb { get; internal set; }
    }

    public interface IPlayerController
    {
        public Vector3 Velocidad { get; }
        public FrameInput Input { get; }
        public bool SaltandoFrame { get; }
        public bool AterrizandoFrame { get; }
        public bool BoostingFrame { get; }
        public bool DeadFrame { get; set; }
        public Vector3 RawMovement { get; }
        public bool EnElPiso { get; }
    }

    public struct RayRange
    {
        public RayRange(float x1, float y1, float x2, float y2, Vector2 dir)
        {
            Start = new Vector2(x1, y1);
            End = new Vector2(x2, y2);
            Dir = dir;
        }

        public readonly Vector2 Start, End, Dir;
    }
}
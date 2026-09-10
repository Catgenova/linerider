namespace NeonLineRider.Core
{
    /// <summary>Simulation constants of the classic engine.</summary>
    public static class Constants
    {
        public const int SimFps = 40;
        public const double FrameMs = 1000.0 / SimFps;
        public const double Gravity = 0.175;
        public const int Iterations = 6;
        public const double StartVelocity = 0.4;
        public const double LineZone = 10;
        public const double Endurance = 0.057;
        public const double Acceleration = 0.1;
        public const double ExtensionPx = 10;
        public const double MaxExtensionRatio = 0.25;
        public const double GridCell = 14;
        public const double PxPerMeter = 10;
    }
}

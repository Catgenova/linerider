using System;

namespace CyberRider.Core
{
    public struct PointDef
    {
        public double X, Y, Friction;

        public PointDef(double x, double y, double friction)
        {
            X = x;
            Y = y;
            Friction = friction;
        }
    }

    public enum BoneKind
    {
        Normal,
        Mount,
        Repel,
    }

    public struct BoneDef
    {
        public int A, B;
        public BoneKind Kind;
        public double Factor;

        public BoneDef(int a, int b, BoneKind kind = BoneKind.Normal, double factor = 0.5)
        {
            A = a;
            B = b;
            Kind = kind;
            Factor = factor;
        }
    }

    /// <summary>Point/bone rig for a rider plus the indices used for orientation, tricks and drawing.</summary>
    public sealed class RiderModel
    {
        public string Id;
        public PointDef[] Points;
        public BoneDef[] Bones;
        public int AnchorTail, AnchorNose, AnchorHip, AnchorShoulder;
        public int[] Vehicle;
        public int[][] DrawVehicle;
        public int[][] DrawBody;
        public double HeadRadius;
        public double HeadOffset;

        public const int PEG = 0, TAIL = 1, NOSE = 2, STRING = 3, BUTT = 4, SHOULDER = 5, RHAND = 6, LHAND = 7, LFOOT = 8, RFOOT = 9;

        /// <summary>Classic sled rider. Coordinates are relative to the sled's rear peg, y pointing down.</summary>
        public static readonly RiderModel Sled = new RiderModel
        {
            Id = "sled",
            Points = new[]
            {
                new PointDef(0, 0, 0.8), // peg
                new PointDef(0, 5, 0), // tail
                new PointDef(15, 5, 0), // nose
                new PointDef(17.5, 0, 0), // string
                new PointDef(5, 0, 0.8), // butt
                new PointDef(5, -5.5, 0.8), // shoulder
                new PointDef(11.5, -5, 0.1), // right hand
                new PointDef(11.5, -5, 0.1), // left hand
                new PointDef(10, 5, 0), // left foot
                new PointDef(10, 5, 0), // right foot
            },
            Bones = new[]
            {
                new BoneDef(PEG, TAIL),
                new BoneDef(TAIL, NOSE),
                new BoneDef(NOSE, STRING),
                new BoneDef(STRING, PEG),
                new BoneDef(PEG, NOSE),
                new BoneDef(STRING, TAIL),
                new BoneDef(PEG, BUTT, BoneKind.Mount),
                new BoneDef(TAIL, BUTT, BoneKind.Mount),
                new BoneDef(NOSE, BUTT, BoneKind.Mount),
                new BoneDef(SHOULDER, BUTT),
                new BoneDef(SHOULDER, RHAND),
                new BoneDef(SHOULDER, LHAND),
                new BoneDef(BUTT, LFOOT),
                new BoneDef(BUTT, RFOOT),
                new BoneDef(SHOULDER, RHAND),
                new BoneDef(SHOULDER, PEG, BoneKind.Mount),
                new BoneDef(STRING, LHAND, BoneKind.Mount),
                new BoneDef(STRING, RHAND, BoneKind.Mount),
                new BoneDef(LFOOT, NOSE, BoneKind.Mount),
                new BoneDef(RFOOT, NOSE, BoneKind.Mount),
                new BoneDef(SHOULDER, LFOOT, BoneKind.Repel, 0.5),
                new BoneDef(SHOULDER, RFOOT, BoneKind.Repel, 0.5),
            },
            AnchorTail = TAIL,
            AnchorNose = NOSE,
            AnchorHip = BUTT,
            AnchorShoulder = SHOULDER,
            Vehicle = new[] { PEG, TAIL, NOSE, STRING },
            DrawVehicle = new[] { new[] { PEG, TAIL, NOSE, STRING } },
            DrawBody = new[] { new[] { BUTT, SHOULDER }, new[] { SHOULDER, RHAND }, new[] { SHOULDER, LHAND }, new[] { BUTT, LFOOT }, new[] { BUTT, RFOOT } },
            HeadRadius = 2.6,
            HeadOffset = 3.2,
        };

        private const int BT = 0, BN = 1, BTT = 2, BNT = 3, LF = 4, RF = 5, HIP = 6, SH = 7, LH = 8, RH = 9;

        /// <summary>Snowboarder: a rigid board with a standing rider mounted through the hips.</summary>
        public static readonly RiderModel Board = new RiderModel
        {
            Id = "board",
            Points = new[]
            {
                new PointDef(0, 5, 0),
                new PointDef(20, 5, 0),
                new PointDef(2, 2.5, 0),
                new PointDef(18, 2.5, 0),
                new PointDef(6, 2.5, 0.3),
                new PointDef(14, 2.5, 0.3),
                new PointDef(10, -4, 0.8),
                new PointDef(10, -12, 0.8),
                new PointDef(3, -5, 0.1),
                new PointDef(17, -5, 0.1),
            },
            Bones = new[]
            {
                new BoneDef(BT, BN),
                new BoneDef(BTT, BNT),
                new BoneDef(BT, BTT),
                new BoneDef(BN, BNT),
                new BoneDef(BT, BNT),
                new BoneDef(BN, BTT),
                new BoneDef(LF, BT, BoneKind.Mount),
                new BoneDef(LF, BN, BoneKind.Mount),
                new BoneDef(RF, BT, BoneKind.Mount),
                new BoneDef(RF, BN, BoneKind.Mount),
                new BoneDef(HIP, LF),
                new BoneDef(HIP, RF),
                new BoneDef(HIP, SH),
                new BoneDef(SH, LH),
                new BoneDef(SH, RH),
                new BoneDef(HIP, LH),
                new BoneDef(HIP, RH),
                new BoneDef(HIP, BT, BoneKind.Mount),
                new BoneDef(HIP, BN, BoneKind.Mount),
                new BoneDef(SH, BT, BoneKind.Mount),
                new BoneDef(SH, BN, BoneKind.Mount),
                new BoneDef(SH, BTT, BoneKind.Repel, 0.5),
                new BoneDef(SH, BNT, BoneKind.Repel, 0.5),
            },
            AnchorTail = BT,
            AnchorNose = BN,
            AnchorHip = HIP,
            AnchorShoulder = SH,
            Vehicle = new[] { BT, BN, BTT, BNT },
            DrawVehicle = new[] { new[] { BT, BN, BNT, BTT } },
            DrawBody = new[] { new[] { LF, HIP }, new[] { RF, HIP }, new[] { HIP, SH }, new[] { SH, LH }, new[] { SH, RH } },
            HeadRadius = 2.6,
            HeadOffset = 3.4,
        };

        public static RiderModel ById(string id) => id == "board" ? Board : Sled;
    }
}

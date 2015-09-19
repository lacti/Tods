using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tods
{
    public enum ShipState
    {
        Stop,
        Moving,
        Attacking,
        Removed
    }

    public class Ship
    {
        public string Id { get; set; }
        public string PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Hp { get; set; }
        public bool Busy { get; set; }
        public ShipState State { get; set; }

        public Ship Clone()
        {
            return (Ship)MemberwiseClone();
        }

        public override string ToString()
        {
            return $"[Ship Id: {Id}, PlayerId: {PlayerId}, X: {X}, Y: {Y}, Hp: {Hp}, Busy: {Busy}, State: {State}]";
        }
    }

    public enum EventType
    {
        Move,
        Attack,
        Spawn,
        Despawn,
        Attacked,
        Merged
    }

    public enum ProgressType
    {
        Command,
        Begin,
        Progress,
        End
    }

    public class Event
    {
        public long Id { get; set; }
        public Ship Source { get; set; }
        public EventType Type { get; set; }
        public ProgressType Progress { get; set; }
        public float Duration { get; set; }

        public int DestX { get; set; }
        public int DestY { get; set; }
        public string TargetShipId { get; set; }

        public override string ToString()
        {
            return $"[Event Id: {Id}, Source: {Source}, Type: {Type}, Progress: {Progress}, Duration: {Duration}, DestX: {DestX}, DestY: {DestY}, TargetShipId: {TargetShipId}]";
        }
    }

    public static class WorldConstants
    {
        public const int TickInterval = 64;

        public static readonly float Sqrt3 = (float)Math.Sqrt(3);
        public const int Radius = 32;
        public static readonly float TileHeight = Radius * 2;
        public static readonly float TileWidth = Radius * Sqrt3;

        public static readonly float BoxWidth = Radius * Sqrt3;
        public static readonly float BoxHeight = Radius;

        public static readonly float TranslationBoxWidth = BoxWidth / 2f;
        public static readonly float TranslationBoxHeight = BoxHeight / 2f;
    }

    public enum Direction
    {
        /*
           A   B
        F         C
           E   D
        */
        A, B, C, D, E, F
    }

    public static class TileUtils
    {
        public static PointF[] CalculateVertices(int sides, int radius, int startingAngle, PointF center)
        {
            if (sides < 3)
                throw new ArgumentException("Polygon must have 3 sides or more.");

            var points = new List<PointF>();
            float step = 360.0f / sides;

            float angle = startingAngle; //starting angle
            for (double i = startingAngle; i < startingAngle + 360.0; i += step) //go in a full circle
            {
                points.Add(DegreesToXY(angle, radius, center)); //code snippet from above
                angle += step;
            }

            return points.ToArray();
        }

        /// <summary>
        /// Calculates a point that is at an angle from the origin (0 is to the right)
        /// </summary>
        private static PointF DegreesToXY(float degrees, float radius, PointF origin)
        {
            PointF xy = new PointF();
            double radians = degrees * Math.PI / 180.0;

            xy.X = (float)Math.Cos(radians) * radius + origin.X;
            xy.Y = (float)Math.Sin(-radians) * radius + origin.Y;

            return xy;
        }

        /// <summary>
        /// Calculates the angle a point is to the origin (0 is to the right)
        /// </summary>
        private static float XYToDegrees(PointF xy, PointF origin)
        {
            float deltaX = origin.X - xy.X;
            float deltaY = origin.Y - xy.Y;

            double radAngle = Math.Atan2(deltaY, deltaX);
            double degreeAngle = radAngle * 180.0 / Math.PI;

            return (float)(180.0 - degreeAngle);
        }

        public static Point TranslatePixelToTile(Point mousePos)
        {
            int boxX = (int)(mousePos.X / WorldConstants.TranslationBoxWidth);
            int boxY = (int)(mousePos.Y / WorldConstants.TranslationBoxHeight);
            int tileY = (boxY + 2) / 3;
            int tileX = tileY % 2 == 0 ? (boxX + 1) / 2 : boxX / 2;

            if ((boxY + 2) % 3 == 0)
            {
                var tx = (mousePos.X - boxX * WorldConstants.TranslationBoxWidth);
                var ty = (mousePos.Y - boxY * WorldConstants.TranslationBoxHeight);
                if (tileY % 2 == 1)
                {
                    if (boxX % 2 == 0) { if (WorldConstants.TranslationBoxHeight - tx / WorldConstants.Sqrt3 > ty) tileY--; }
                    else { if (tx / WorldConstants.Sqrt3 > ty) { tileY--; tileX++; } }
                }
                else
                {
                    if (boxX % 2 == 0) { if (tx / WorldConstants.Sqrt3 > ty) { tileY--; } }
                    else { if (WorldConstants.TranslationBoxHeight - tx / WorldConstants.Sqrt3 > ty) { tileX--; tileY--; } }
                }
            }

            return new Point(tileX, tileY);
        }

        public static PointF TranslateTileToPixel(Point tilePos)
        {
            var x = (tilePos.Y % 2 == 0) ? tilePos.X * WorldConstants.BoxWidth : (tilePos.X + 0.5f) * WorldConstants.BoxWidth;
            var y = (tilePos.Y * 1.5f) * WorldConstants.Radius;
            return new PointF(x, y);
        }

        public static Point GetMovedPos(Point tilePos, Direction dir)
        {
            var deltaX = tilePos.Y % 2 == 0 ? -1 : 0;
            switch (dir)
            {
                case Direction.A:
                    return new Point(tilePos.X + deltaX, tilePos.Y - 1);
                case Direction.B:
                    return new Point(tilePos.X + 1 + deltaX, tilePos.Y - 1);
                case Direction.C:
                    return new Point(tilePos.X + 1, tilePos.Y);
                case Direction.D:
                    return new Point(tilePos.X + 1 + deltaX, tilePos.Y + 1);
                case Direction.E:
                    return new Point(tilePos.X + deltaX, tilePos.Y + 1);
                case Direction.F:
                    return new Point(tilePos.X - 1, tilePos.Y);
            }
            throw new InvalidOperationException("invalid dir: " + dir);
        }

        public static Direction? FindDirection(Point pos1, Point pos2)
        {
            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                if (GetMovedPos(pos1, dir).Equals(pos2))
                    return dir;
            }
            return null;
        }
    }
}

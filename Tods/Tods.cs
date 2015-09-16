using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tods
{
    class World
    {
        public int Width { get; set; }
        public int Height { get; set; }
        private readonly Dictionary<string, Ship> _ships = new Dictionary<string, Ship>();
        private readonly Dictionary<string, DrawingShip> _drawingShips = new Dictionary<string, DrawingShip>();

        private readonly ConcurrentQueue<Event> _events = new ConcurrentQueue<Event>();
        private readonly Coroutine _coro = new Coroutine(WorldConstants.TickInterval);

        public Point MouseTilePos { get; set; }
        public Ship SelectedShip { get; set; }

        // run in any thread
        public void SendEvent(Event evt)
        {
            if (_drawingShips.ContainsKey(evt.Source.Id))
            {
                _drawingShips[evt.Source.Id].ReceiveEvent(evt);
            }
        }

        public void AddDrawing(Event evt)
        {
            _drawingShips.Add(evt.Source.Id, new DrawingShip(evt.Source));
            _coro.AddEntry(_drawingShips[evt.Source.Id].Process);
        }

        public void RemoveDrawing(Event evt)
        {
            _drawingShips.Remove(evt.Source.Id);
        }

        public Ship FindShipByPos(Point pos)
        {
            foreach (Ship ship in _ships.Values)
            {
                if (ship.X == pos.X && ship.Y == pos.Y)
                {
                    return ship;
                }
            }
            return null;
        }

        public IEnumerable<Ship> FindShipsByPos(Point pos)
        {
            foreach (Ship ship in _ships.Values)
            {
                if (ship.X == pos.X && ship.Y == pos.Y)
                {
                    yield return ship;
                }
            }
        }

        // run in rendering thread
        public void Process()
        {
            Event evt;
            while (_events.TryDequeue(out evt))
            {
                switch (evt.Type)
                {
                    case EventType.Spawn:
                        AddDrawing(evt);
                        break;

                    case EventType.Despawn:
                        RemoveDrawing(evt);
                        break;
                }
            }

            _coro.IterateLogic();
        }

        // run in rendering thread
        public void Draw(Graphics g)
        {
            DrawTiles(g);
            foreach (DrawingShip ship in _drawingShips.Values)
            {
                ship.Draw(g);
            }
        }

        private void DrawTiles(Graphics g)
        {
            g.FillRectangle(Brushes.Black, 0, 0, Width, Height);

            var pen = new Pen(Color.FromArgb(20, 20, 20), 0.5f);
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    DrawingUtils.DrawTile(g, pen, x, y);
                }
            }

            if (SelectedShip != null)
            {
                var deltaX = SelectedShip.Y % 2 == 0 ? -1 : 0;
                DrawingUtils.DrawTile(g, Pens.MediumVioletRed, SelectedShip.X - 1, SelectedShip.Y);
                DrawingUtils.DrawTile(g, Pens.MediumVioletRed, SelectedShip.X + 1, SelectedShip.Y);
                DrawingUtils.DrawTile(g, Pens.MediumVioletRed, SelectedShip.X + deltaX, SelectedShip.Y - 1);
                DrawingUtils.DrawTile(g, Pens.MediumVioletRed, SelectedShip.X + 1 + deltaX, SelectedShip.Y - 1);
                DrawingUtils.DrawTile(g, Pens.MediumVioletRed, SelectedShip.X + deltaX, SelectedShip.Y + 1);
                DrawingUtils.DrawTile(g, Pens.MediumVioletRed, SelectedShip.X + 1 + deltaX, SelectedShip.Y + 1);
            }

            if (MouseTilePos != null)
            {
                DrawingUtils.DrawTile(g, Pens.Yellow, MouseTilePos.X, MouseTilePos.Y);
            }
        }

        // run in network thread
        public void ProcessEvent(Event evt)
        {
            switch (evt.Progress)
            {
                case ProgressType.Begin:
                    ProcessBeginEvent(evt);
                    break;
                case ProgressType.Progress:
                    ProcessProgressEvent(evt);
                    break;
                case ProgressType.End:
                    ProcessEndEvent(evt);
                    break;
            }
        }

        private void ProcessBeginEvent(Event evt)
        {
            MarkAsBusy(evt.Source);
            switch (evt.Type)
            {
                case EventType.Spawn:
                    _events.Enqueue(evt);
                    break;
                case EventType.Despawn:
                case EventType.Move:
                case EventType.Attack:
                    SendEvent(evt);
                    break;
            }
        }

        private void ProcessProgressEvent(Event evt)
        {
            switch (evt.Type)
            {
                case EventType.Move:
                case EventType.Attack:
                    SendEvent(evt);
                    break;
            }
        }

        private void ProcessEndEvent(Event evt)
        {
            switch (evt.Type)
            {
                case EventType.Spawn:
                    _ships.Add(evt.Source.Id, evt.Source);
                    break;
                case EventType.Despawn:
                    _events.Enqueue(evt);
                    _ships.Remove(evt.Source.Id);
                    break;

                case EventType.Move:
                case EventType.Attack:
                case EventType.Attacked:
                case EventType.Merged:
                    UpdateFromEvent(evt);
                    SendEvent(evt);
                    break;
            }
            MarkAsNotBusy(evt.Source);
        }

        private void UpdateFromEvent(Event evt)
        {
            if (_ships.ContainsKey(evt.Source.Id))
            {
                if (evt.Type == EventType.Attacked || evt.Type == EventType.Merged)
                {
                    _ships[evt.Source.Id].Hp = evt.Source.Hp;
                }
                if (evt.Type == EventType.Move)
                {
                    _ships[evt.Source.Id].X = evt.Source.X;
                    _ships[evt.Source.Id].Y = evt.Source.Y;
                }
            }
        }

        private void MarkAsBusy(Ship source)
        {
            if (_ships.ContainsKey(source.Id))
            {
                _ships[source.Id].Busy = true;
            }
        }

        private void MarkAsNotBusy(Ship source)
        {
            if (_ships.ContainsKey(source.Id))
            {
                _ships[source.Id].Busy = false;
            }
        }
    }

    enum ShipState
    {
        Stop,
        Moving,
        Attacking,
        Removed
    }

    class Ship
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
    }

    enum DrawingShipState
    {
        Stop,
        Move,
        Attack
    }

    class DrawingShip
    {
        public Ship Ship { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public DrawingShipState State { get; set; }

        private readonly ConcurrentQueue<Event> _events = new ConcurrentQueue<Event>();

        public DrawingShip(Ship source)
        {
            Ship = source;
            ForceSyncPosition();
        }

        private void ForceSyncPosition()
        {
            var pixelPos = TileUtils.TranslateTileToPixel(new Point(Ship.X, Ship.Y));
            X = pixelPos.X;
            Y = pixelPos.Y;
            State = DrawingShipState.Stop;
        }

        public void ReceiveEvent(Event evt)
        {
            _events.Enqueue(evt);
        }

        public IEnumerable<int> Process()
        {
            while (true)
            {
                Event evt;
                if (!_events.TryDequeue(out evt))
                {
                    yield return 0;
                    continue;
                }

                switch (evt.Progress)
                {
                    case ProgressType.Begin:
                        foreach (var next in ProcessBeginEvent(evt)) yield return next;
                        break;

                    case ProgressType.Progress:
                        foreach (var next in ProcessProgressEvent(evt)) yield return next;
                        break;

                    case ProgressType.End:
                        foreach (var next in ProcessEndEvent(evt)) yield return next;
                        break;
                }

                // end of drawing
                if (evt.Progress == ProgressType.End && evt.Type == EventType.Despawn)
                    break;
            }
        }

        private IEnumerable<int> ProcessBeginEvent(Event evt)
        {
            switch (evt.Type)
            {
                case EventType.Move:
                    State = DrawingShipState.Move;
                    break;
                case EventType.Attack:
                    State = DrawingShipState.Attack;
                    break;
            }

            switch (evt.Type)
            {
                case EventType.Move:
                case EventType.Attack:
                    foreach (var tick in ProcessMoveAndAttackBeginEvent(evt))
                        yield return tick;
                    break;
            }

            yield return 0;
        }

        private IEnumerable<int> ProcessMoveAndAttackBeginEvent(Event evt)
        {
            var startPos = TileUtils.TranslateTileToPixel(new Point(Ship.X, Ship.Y));
            var endPos = TileUtils.TranslateTileToPixel(new Point(evt.DestX, evt.DestY));
            var timeDelta = evt.Duration / WorldConstants.TickInterval;
            var vx = (endPos.X - startPos.X) / timeDelta;
            var vy = (endPos.Y - startPos.Y) / timeDelta;
            for (int i = 0; i < timeDelta; i++)
            {
                X += vx;
                Y += vy;
                yield return WorldConstants.TickInterval;
            }
        }

        private IEnumerable<int> ProcessProgressEvent(Event evt)
        {
            yield return 0;
        }

        private IEnumerable<int> ProcessEndEvent(Event evt)
        {
            State = DrawingShipState.Stop;
            ForceSyncPosition();
            yield return 0;
        }

        public void Draw(Graphics g)
        {
            var pen = State == DrawingShipState.Attack
                ? new Pen(ColorTable.FindPlayerColor(Ship.PlayerId), 2.0f)
                : new Pen(ColorTable.FindPlayerColor(Ship.PlayerId));
            g.DrawRectangle(pen, X - 5f, Y - 5f, 10f, 10f);
            g.DrawRectangle(pen, X - 5f, Y - 5f, 10f, 10f);
            var font = new Font("Tahoma", 10.0f);
            g.DrawString(Ship.Hp.ToString(), font, Brushes.White, new PointF(X - 15.0f, Y + 12.0f));
        }
    }

    static class ColorTable
    {
        public static readonly Color[] Values = new[]
        {
            Color.Red, Color.PowderBlue, Color.Salmon, Color.Silver
        };

        private static readonly Dictionary<string /* playerId */, Color> _playerColors = new Dictionary<string, Color>();
        private static int _colorIndex;

        public static Color FindPlayerColor(string playerId)
        {
            if (!_playerColors.ContainsKey(playerId))
                _playerColors.Add(playerId, Values[_colorIndex++]);
            return _playerColors[playerId];
        }
    }

    enum EventType
    {
        Move,
        Attack,
        Spawn,
        Despawn,
        Attacked,
        Merged
    }

    enum ProgressType
    {
        Command,
        Begin,
        Progress,
        End
    }

    class Event
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
            return $"Id: {Id}, Source: {Source}, Type: {Type}, Progress: {Progress}, Duration: {Duration}, DestX: {DestX}, DestY: {DestY}, TargetShipId: {TargetShipId}";
        }
    }

    class WorldConstants
    {
        public const int TickInterval = 16;

        public static readonly float Sqrt3 = (float)Math.Sqrt(3);
        public const int Radius = 32;
        public static readonly float TileHeight = Radius * 2;
        public static readonly float TileWidth = Radius * Sqrt3;

        public static readonly float BoxWidth = Radius * Sqrt3;
        public static readonly float BoxHeight = Radius;

        public static readonly float TranslationBoxWidth = BoxWidth / 2f;
        public static readonly float TranslationBoxHeight = BoxHeight / 2f;
    }

    enum Direction
    {
        /*
           A   B
        F         C
           E   D
        */
        A, B, C, D, E, F
    }

    class DrawingUtils
    {
        public static void DrawTile(Graphics g, Pen pen, int x, int y)
        {
            if (y % 2 == 0)
            {
                g.DrawPolygon(pen, TileUtils.CalculateVertices(6, WorldConstants.Radius, 30, new PointF(x * WorldConstants.Radius * WorldConstants.Sqrt3, y * 3 / 2.0f * WorldConstants.Radius)));
            }
            else
            {
                g.DrawPolygon(pen, TileUtils.CalculateVertices(6, WorldConstants.Radius, 30, new PointF((x + 0.5f) * WorldConstants.Radius * WorldConstants.Sqrt3, (y * 3) / 2.0f * WorldConstants.Radius)));
            }
        }

    }

    static class TileUtils
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

    class EmulatorInstructionBuilder
    {
        private readonly World _world;
        private readonly List<EmulatorInstruction> _instructions = new List<EmulatorInstruction>();

        public EmulatorInstructionBuilder(World world)
        {
            _world = world;
        }

        public EmulatorInstructionBuilder Wait(int delay)
        {
            _instructions.Add(new EmulatorEventInstruction(delay));
            return this;
        }

        public EmulatorInstructionBuilder Spawn(Ship ship)
        {
            _instructions.Add(new EmulatorEventInstruction(_world,
                new Event
                {
                    Source = ship,
                    Progress = ProgressType.Begin,
                    Type = EventType.Spawn
                }, 100));
            _instructions.Add(new EmulatorEventInstruction(_world,
                new Event
                {
                    Source = ship,
                    Progress = ProgressType.End,
                    Type = EventType.Spawn
                }));
            return this;
        }

        public EmulatorInstructionBuilder Despawn(Ship ship)
        {
            _instructions.Add(new EmulatorEventInstruction(_world,
                new Event
                {
                    Source = ship,
                    Progress = ProgressType.Begin,
                    Type = EventType.Despawn
                }, 100));
            _instructions.Add(new EmulatorEventInstruction(_world,
                new Event
                {
                    Source = ship,
                    Progress = ProgressType.End,
                    Type = EventType.Despawn
                }));
            return this;
        }

        public EmulatorInstructionBuilder Move(Ship ship, Direction dir)
        {
            var duration = 500;
            var nextPos = TileUtils.GetMovedPos(new Point(ship.X, ship.Y), dir);
            _instructions.Add(new EmulatorEventInstruction(_world,
                new Event
                {
                    Source = ship,
                    Progress = ProgressType.Begin,
                    Type = EventType.Move,
                    Duration = duration,
                    DestX = nextPos.X,
                    DestY = nextPos.Y
                }, duration));

            Ship movedShip = ship.Clone();
            movedShip.X = nextPos.X;
            movedShip.Y = nextPos.Y; 
            _instructions.Add(new EmulatorEventInstruction(_world,
                new Event
                {
                    Source = movedShip,
                    Progress = ProgressType.End,
                    Type = EventType.Move,
                    DestX = nextPos.X,
                    DestY = nextPos.Y
                }));
            _instructions.Add(new EmulatorLogicInstruction(() =>
            {
                // run in event thread
                var mergedShip = movedShip.Clone();
                foreach (Ship otherShip in _world.FindShipsByPos(nextPos))
                {
                    if (otherShip.Id == movedShip.Id)
                        continue;

                    Remove(otherShip);
                    mergedShip.Hp += otherShip.Hp;
                }
                if (movedShip.Hp == mergedShip.Hp)
                    return;

                _instructions.Add(new EmulatorEventInstruction(_world,
                    new Event
                    {
                        Source = mergedShip,
                        Progress = ProgressType.End,
                        Type = EventType.Merged
                    }));
            }, 0));
            return this;
        }

        public EmulatorInstructionBuilder Attack(Ship ship, Direction dir)
        {
            // ship id를 받아서 emul 내부 갱신된 메모리에서 매번 조회하도록 함
            // 그래야 실제 다른 객체를 접근해서 여전히 hp가 유효하다는 판단을 하지 않을 수 있음
            if (ship.Hp <= 0)
            {
                return this;
            }

            var duration = 200;
            var targetPos = TileUtils.GetMovedPos(new Point(ship.X, ship.Y), dir);
            var target = _world.FindShipByPos(targetPos);
            if (target == null || target.PlayerId.Equals(ship.PlayerId))
                return this;

            _instructions.Add(new EmulatorEventInstruction(_world,
                new Event
                {
                    Source = ship,
                    Progress = ProgressType.Begin,
                    Type = EventType.Attack,
                    Duration = duration,
                    TargetShipId = target.Id,
                    DestX = target.X,
                    DestY = target.Y,
                }, duration));
            _instructions.Add(new EmulatorEventInstruction(_world,
                new Event
                {
                    Source = ship,
                    Progress = ProgressType.End,
                    Type = EventType.Attack
                }));

            var attackedTarget = target.Clone();
            attackedTarget.Hp -= ship.Hp / 5;
            if (attackedTarget.Hp <= 0)
            {
                Despawn(attackedTarget);
            }
            else
            {
                _instructions.Add(new EmulatorEventInstruction(_world,
                    new Event
                    {
                        Source = attackedTarget,
                        Progress = ProgressType.End,
                        Type = EventType.Attacked
                    }));
            }
            _instructions.Add(new EmulatorEventInstruction(100));
            _instructions.Add(new EmulatorLogicInstruction(() =>
            {
                // run in event thread
                Attack(ship, dir);
            }, 0));
            return this;
        }

        private void Remove(Ship ship)
        {
            _instructions.Add(new EmulatorEventInstruction(_world,
                new Event
                {
                    Source = ship,
                    Progress = ProgressType.End,
                    Type = EventType.Despawn
                }));
        }

        public void Run(Emulator emul)
        {
            emul.Add(Process);
        }

        public IEnumerable<int> Process()
        {
            while (true)
            {
                var insts = new List<EmulatorInstruction>(_instructions);
                _instructions.Clear();

                if (insts.Count == 0)
                    break;

                foreach (EmulatorInstruction inst in insts)
                {
                    foreach (int tick in inst.Process())
                        yield return tick;
                }
            }
        }
    }

    class Emulator
    {
        private readonly Coroutine _coro = new Coroutine();
        private int _eventId;

        public Emulator()
        {
            _coro.Start();
        }

        private int NextEventId()
        {
            return ++_eventId;
        }

        public void Add(Coroutine.LogicEntryDelegate delegator)
        {
            _coro.AddEntry(delegator);
        }
    }

    interface EmulatorInstruction
    {
        IEnumerable<int> Process();
    }

    class EmulatorLogicInstruction : EmulatorInstruction
    {
        private readonly Action _action;
        private readonly int _delay;

        public EmulatorLogicInstruction(int delay)
            : this(null, delay)
        {
        }

        public EmulatorLogicInstruction(Action action, int delay)
        {
            _action = action;
            _delay = delay;
        }

        public IEnumerable<int> Process()
        {
            _action();
            yield return _delay;
        }
    }

    class EmulatorEventInstruction : EmulatorInstruction
    {
        private readonly World _world;
        private readonly Event _event;
        private readonly int _delay;

        public EmulatorEventInstruction(int delay)
            : this(null, null, delay)
        {
        }

        public EmulatorEventInstruction(World world, Event evt)
            : this(world, evt, 0)
        {
        }

        public EmulatorEventInstruction(World world, Event evt, int delay)
        {
            _world = world;
            _event = evt;
            _delay = delay;
        }

        public IEnumerable<int> Process()
        {
            if (_world != null && _event != null)
            {
                _world.ProcessEvent(_event);
            }

            if (_delay > 0)
            {
                yield return _delay;
            }
        }
    }

}

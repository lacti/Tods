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
            if (evt.Progress == ProgressType.Begin)
            {
                _drawingShips.Add(evt.Source.Id, new DrawingShip(evt.Source));
                _coro.AddEntry(_drawingShips[evt.Source.Id].Process);
            }
            else if (evt.Progress == ProgressType.End)
            {
                _drawingShips[evt.Source.Id].Ship = evt.Source;
            }
        }

        public void RemoveDrawing(Event evt)
        {
            _drawingShips.Remove(evt.Source.Id);
        }

        public Ship FindShipByPos(Point pos)
        {
            return _ships.Values.FirstOrDefault(ship => ship.X == pos.X && ship.Y == pos.Y);
        }

        public IEnumerable<Ship> FindShipsByPos(Point pos)
        {
            return _ships.Values.Where(ship => ship.X == pos.X && ship.Y == pos.Y);
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
                    _events.Enqueue(evt);
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
                    yield return 16;
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

                yield return 16;
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
            for (var i = 0; i < timeDelta; i++)
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
        private static readonly Color[] Values = {
            Color.Red, Color.PowderBlue, Color.Salmon, Color.Silver
        };

        private static readonly Dictionary<string /* playerId */, Color> PlayerColors = new Dictionary<string, Color>();
        private static int _colorIndex;

        public static Color FindPlayerColor(string playerId)
        {
            if (!PlayerColors.ContainsKey(playerId))
                PlayerColors.Add(playerId, Values[_colorIndex++]);
            return PlayerColors[playerId];
        }
    }

    static class DrawingUtils
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

}

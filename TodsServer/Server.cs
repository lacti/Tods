using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Tods
{
    class ServerWorld
    {
        public readonly ConcurrentDictionary<string, ServerPlayer> Players = new ConcurrentDictionary<string, ServerPlayer>();
        public readonly Dictionary<string, Ship> Ships = new Dictionary<string, Ship>();

        private readonly Coroutine _coro = new Coroutine();

        public ServerWorld()
        {
            _coro.Start();
        }

        public ServerPlayer RegisterPlayer(string playerId)
        {
            ServerPlayer player;
            while (!Players.TryGetValue(playerId, out player))
            {
                Players.TryAdd(playerId, new ServerPlayer(this) { Id = playerId });
            }
            player.Online = true;
            _coro.AddEntry(player.ProcessQueue);

            return player;
        }

        public void UnregisterPlayer(string playerId)
        {
            ServerPlayer player;
            if (Players.TryRemove(playerId, out player))
            {
                player.Online = false;
            }
        }

        public void BroadcastEvent(Event evt)
        {
            foreach (var player in Players.Values)
            {
                Logger.Write($"Send to player[{player.Id}] = {evt}");
                player.PollQueue.Enqueue(evt);
            }
        }

        public ServerPlayer GetPlayer(string playerId)
        {
            ServerPlayer player;
            return Players.TryGetValue(playerId, out player) ? player : null;
        }

        public ConcurrentQueue<Event> GetPostQueue(string playerId)
        {
            return GetPlayer(playerId)?.PostQueue;
        }

        public ConcurrentQueue<Event> GetPollQueue(string playerId)
        {
            return GetPlayer(playerId)?.PollQueue;
        }

        public List<Point> GetEmptyLocation(int count)
        {
            var maxX = 20;
            var maxY = 12;
            var locs = new List<Point>();
            var rand = new Random();
            while (locs.Count < count)
            {
                var x = rand.Next(maxX);
                var y = rand.Next(maxY);
                if (Ships.Values.Any(e => e.X == x && e.Y == y))
                    continue;

                if (locs.Any(e => e.X == x && e.Y == y))
                    continue;

                locs.Add(new Point(x, y));
            }

            return locs;
        }
    }

    class ServerPlayer
    {
        public readonly ConcurrentQueue<Event> PostQueue = new ConcurrentQueue<Event>();
        public readonly ConcurrentQueue<Event> PollQueue = new ConcurrentQueue<Event>();

        private readonly ServerWorld _world;
        public string Id { get; set; }
        public bool Online { get; set; }

        public ServerPlayer(ServerWorld world)
        {
            _world = world;
        }

        public IEnumerable<int> ProcessQueue()
        {
            while (Online)
            {
                Event evt;
                while (PostQueue.TryDequeue(out evt))
                {
                    Logger.Write($"Process [{evt}]");
                    foreach (var tick in ProcessEvent(evt))
                        yield return tick;
                    yield return 32;
                }
                yield return 32;
            }
        }

        private IEnumerable<int> ProcessEvent(Event evt)
        {
            Func<Event, IEnumerable<int>> delegator = null;
            switch (evt.Type)
            {
                case EventType.Spawn:
                    delegator = ProcessSpawnEvent; break;
                case EventType.Move:
                    delegator = ProcessMoveEvent; break;
                case EventType.Attack:
                    delegator = ProcessAttackEvent; break;
            }
            if (delegator == null)
                yield break;

            foreach (var tick in delegator(evt))
                yield return tick;
        }

        private IEnumerable<int> ProcessSpawnEvent(Event evt)
        {
            var ship = evt.Source.Clone();

            ship.State = ShipState.Stop;
            _world.Ships.Add(evt.Source.Id, ship);

            _world.BroadcastEvent(new Event
            {
                Source = ship,
                Progress = ProgressType.Begin,
                Type = EventType.Spawn
            });

            yield return 100;

            _world.BroadcastEvent(new Event
            {
                Source = ship,
                Progress = ProgressType.End,
                Type = EventType.Spawn
            });
        }

        private IEnumerable<int> ProcessMoveEvent(Event evt)
        {
            Ship ship;
            if (!_world.Ships.TryGetValue(evt.Source.Id, out ship))
                yield break;

            if (TileUtils.FindDirection(new Point(ship.X, ship.Y), new Point(evt.DestX, evt.DestY)) == null)
                yield break;

            if (_world.Ships.Values.Any(e => e.X == evt.DestX && e.Y == evt.DestY && e.PlayerId != ship.PlayerId))
                yield break;

            ship.State = ShipState.Moving;

            var duration = 500;
            _world.BroadcastEvent(new Event
            {
                Source = ship,
                Progress = ProgressType.Begin,
                Type = EventType.Move,
                Duration = duration,
                DestX = evt.DestX,
                DestY = evt.DestY
            });

            yield return duration;

            ship.X = evt.DestX;
            ship.Y = evt.DestY;
            _world.BroadcastEvent(new Event
            {
                Source = ship,
                Progress = ProgressType.End,
                Type = EventType.Move,
                DestX = ship.X,
                DestY = ship.Y
            });

            var mergedHp = ship.Hp;
            var shipsToMerge = _world.Ships.Values.Where(e => e.Id != ship.Id && e.PlayerId == ship.PlayerId && e.X == ship.X && e.Y == ship.Y).ToArray();
            foreach (var otherShip in shipsToMerge)
            {
                mergedHp += otherShip.Hp;
                _world.BroadcastEvent(new Event
                {
                    Source = otherShip,
                    Progress = ProgressType.End,
                    Type = EventType.Despawn
                });
                otherShip.State = ShipState.Removed;
                _world.Ships.Remove(otherShip.Id);
            }
            if (mergedHp != ship.Hp)
            {
                ship.Hp = mergedHp;
                _world.BroadcastEvent(new Event
                {
                    Source = ship,
                    Progress = ProgressType.End,
                    Type = EventType.Merged
                });
            }

            ship.State = ShipState.Stop;
        }

        private IEnumerable<int> ProcessAttackEvent(Event evt)
        {
            Ship ship;
            if (!_world.Ships.TryGetValue(evt.Source.Id, out ship))
                yield break;

            while (true)
            {
                if (ship.State != ShipState.Stop)
                    yield break;

                var target = _world.Ships.Values.FirstOrDefault(e => e.Id == evt.TargetShipId);
                if (target == null)
                    yield break;

                if (TileUtils.FindDirection(new Point(ship.X, ship.Y), new Point(target.X, target.Y)) == null)
                    yield break;

                ship.State = ShipState.Attacking;

                var duration = 200;
                _world.BroadcastEvent(new Event
                {
                    Source = ship,
                    Progress = ProgressType.Begin,
                    Type = EventType.Attack,
                    Duration = duration,
                    TargetShipId = target.Id,
                    DestX = target.X,
                    DestY = target.Y,
                });

                yield return duration;

                _world.BroadcastEvent(new Event
                {
                    Source = ship,
                    Progress = ProgressType.End,
                    Type = EventType.Attack
                });

                target.Hp -= ship.Hp / 5;
                if (target.Hp <= 0)
                {
                    _world.BroadcastEvent(new Event
                    {
                        Source = target,
                        Progress = ProgressType.Begin,
                        Type = EventType.Despawn
                    });

                    yield return 100;

                    _world.BroadcastEvent(new Event
                    {
                        Source = target,
                        Progress = ProgressType.End,
                        Type = EventType.Despawn
                    });

                    target.State = ShipState.Removed;
                    _world.Ships.Remove(target.Id);

                    break;
                }
                else
                {
                    _world.BroadcastEvent(new Event
                    {
                        Source = target,
                        Progress = ProgressType.End,
                        Type = EventType.Attacked
                    });
                }

                ship.State = ShipState.Stop;

                // 적을 완전히 제거할 때까지 공격을 계속 반복한다.
                yield return 100;
            }
        }
    }

    class SimpleServer : IServer
    {
        // network thread
        private readonly ServerWorld _world = new ServerWorld();

        public bool Register(string playerId)
        {
            _world.RegisterPlayer(playerId);

            foreach (var ship in _world.Ships.Values)
            {
                _world.GetPollQueue(playerId).Enqueue(new Event
                {
                    Source = ship,
                    Type = EventType.Spawn,
                    Progress = ProgressType.Begin
                });
                _world.GetPollQueue(playerId).Enqueue(new Event
                {
                    Source = ship,
                    Type = EventType.Spawn,
                    Progress = ProgressType.End
                });
            }

            var count = 5;
            var locs = _world.GetEmptyLocation(count);
            foreach (var loc in locs)
            {
                var newShip = new Ship { Hp = 10000, Id = Guid.NewGuid().ToString(), X = loc.X, Y = loc.Y, PlayerId = playerId };
                Post(new Event
                {
                    Source = newShip,
                    Type = EventType.Spawn,
                    Progress = ProgressType.Command
                });
            }
            return true;
        }

        public bool Unregister(string playerId)
        {
            _world.UnregisterPlayer(playerId);
            return true;
        }

        public bool Post(Event evt)
        {
            var queue = _world.GetPostQueue(evt.Source.PlayerId);
            if (queue != null)
            {
                queue.Enqueue(evt);
            }
            return true;
        }

        public List<Event> Poll(string playerId)
        {
            var events = new List<Event>();
            var queue = _world.GetPollQueue(playerId);
            if (queue != null)
            {
                Event evt;
                while (queue.TryDequeue(out evt))
                    events.Add(evt);
            }
            return events;
        }
    }
}

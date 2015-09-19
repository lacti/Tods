using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Tods
{
    public partial class FormScreen : Form
    {
        private readonly string _playerId = Guid.NewGuid().ToString();

        private readonly World _world;
        private readonly IServer _server;

        public FormScreen()
        {
            InitializeComponent();
            DoubleBuffered = true;

            _world = new World { Width = 64, Height = 64 };
            _server = new ServerConnector();
        }

        private void FormScreen_MouseMove(object sender, MouseEventArgs e)
        {
            Invalidate();
        }

        private void gameTimer_Tick(object sender, EventArgs e)
        {
            gamePanel.Invalidate();

            var events = _server.Poll(_playerId);
            if (events != null)
            {
                foreach (var evt in events)
                {
                    Logger.Write($"Poll event[{evt}]");
                    _world.ProcessEvent(evt);
                }
            }

            _world.Process();
        }

        private void FormScreen_Load(object sender, EventArgs e)
        {
            gamePanel.Paint += GamePanel_Paint;
            gamePanel.MouseMove += GamePanel_MouseMove;

            _server.Register(_playerId);
        }

        private void GamePanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            _world.Draw(g);
        }

        private Point GetCurrentTilePos()
        {
            return TileUtils.TranslatePixelToTile(gamePanel.PointToClient(Cursor.Position));
        }

        private void GamePanel_MouseMove(object sender, MouseEventArgs e)
        {
            _world.MouseTilePos = GetCurrentTilePos();
        }

        private void gamePanel_Click(object sender, EventArgs e)
        {
            var pos = GetCurrentTilePos();

            if (_world.SelectedShip != null)
            {
                OnSelectedShip(pos);
            }
            else
            {
                var ship = _world.FindShipByPos(pos);
                if (ship != null && ship.PlayerId == _playerId)
                {
                    _world.SelectedShip = ship;
                    Logger.Write($"Ship[{ship.Id}] Hp={ship.Hp}");
                }
                else
                {
                    /*
                    ship = new Ship { Hp = 10000, Id = Guid.NewGuid().ToString(), X = pos.X, Y = pos.Y, PlayerId = _playerId };
                    _server.Post(new Event
                    {
                        Source = ship,
                        Type = EventType.Spawn,
                        Progress = ProgressType.Command
                    });
                    */
                }
            }
        }

        private void OnSelectedShip(Point pos)
        {
            var otherShip = _world.FindShipByPos(pos);
            if (otherShip == null || _world.SelectedShip.PlayerId.Equals(otherShip.PlayerId))
            {
                var shipPos = new Point(_world.SelectedShip.X, _world.SelectedShip.Y);
                var direction = TileUtils.FindDirection(shipPos, pos);
                if (direction.HasValue)
                {
                    if (_world.SelectedShip.Busy)
                        return;

                    _server.Post(new Event
                    {
                        Source = _world.SelectedShip,
                        Type = EventType.Move,
                        Progress = ProgressType.Command,
                        DestX = pos.X,
                        DestY = pos.Y
                    });
                }
                else
                {
                    _world.SelectedShip = otherShip;
                }
            }
            else
            {
                var shipPos = new Point(_world.SelectedShip.X, _world.SelectedShip.Y);
                var direction = TileUtils.FindDirection(shipPos, pos);
                if (direction.HasValue)
                {
                    if (_world.SelectedShip.Busy)
                        return;

                    _server.Post(new Event
                    {
                        Source = _world.SelectedShip,
                        Type = EventType.Attack,
                        Progress = ProgressType.Command,
                        TargetShipId = otherShip.Id
                    });
                }
                _world.SelectedShip = null;
            }
        }

        private void FormScreen_FormClosed(object sender, FormClosedEventArgs e)
        {
            _server.Unregister(_playerId);
        }
    }
}

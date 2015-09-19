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
    public interface IServer
    {
        bool Register(string playerId);
        bool Unregister(string playerId);

        bool Post(Event evt);
        List<Event> Poll(string playerId);
    }

}

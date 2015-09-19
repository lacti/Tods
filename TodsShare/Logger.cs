using System;
using System.Diagnostics;

namespace Tods
{
    public static class Logger
    {
        public static void Write(string message)
        {
            if (Debugger.IsAttached)
            {
                Debug.Print($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] {message}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] {message}");
            }
        }
    }
}
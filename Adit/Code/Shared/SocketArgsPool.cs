﻿using Adit.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Adit.Code.Shared
{
    public static class SocketArgsPool
    {
        public static List<SocketArgs> SocketReceiveArgs { get; set; } = new List<SocketArgs>();
        public static List<SocketArgs> SocketSendArgs { get; set; } = new List<SocketArgs>();

        public static SocketArgs GetReceiveArg()
        {
            lock (SocketReceiveArgs)
            {
                var freeArg = SocketReceiveArgs.Find(x => x.IsInUse == false);
                if (freeArg == null)
                {
                    var newArg = new SocketArgs();
                    newArg.SetBuffer(new byte[Config.Current.BufferSize], 0, Config.Current.BufferSize);
                    newArg.IsInUse = true;
                    SocketReceiveArgs.Add(newArg);
                    return newArg;
                }
                else
                {
                    freeArg.IsInUse = true;
                    return freeArg;
                }
            }
        }

        public static SocketArgs GetSendArg()
        {
            lock (SocketSendArgs)
            {
                var freeArg = SocketSendArgs.Find(x => x.IsInUse == false);
                if (freeArg == null)
                {
                    var newArg = new SocketArgs();
                    newArg.IsInUse = true;
                    SocketSendArgs.Add(newArg);
                    return newArg;
                }
                else
                {
                    Array.Clear(freeArg.Buffer, 0, freeArg.Buffer.Length);
                    freeArg.IsInUse = true;
                    return freeArg;
                }
            }
           
        }
    }
}

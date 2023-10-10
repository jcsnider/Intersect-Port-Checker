using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Lidgren.Network;

namespace LidgrenHelloApp
{
    class Program
    {
        static HttpListener listener;
        private static Thread listenThread1;
        private static bool debug;

        static void Main(string[] args)
        {
            ushort port = 0;
            foreach (var arg in args)
            {
                if (arg.Contains("port="))
                {
                    if (ushort.TryParse(arg.Split("=".ToCharArray())[1], out port))
                    {
                        
                    }
                }
                if (arg.Contains("debug"))
                {
                    debug = true;
                }
            }
            if (port > 0)
            {
                listener = new HttpListener();
                listener.Prefixes.Add("http://*:" + port + "/");
                listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;
                listener.Start();
                listenThread1 = new Thread(new ParameterizedThreadStart(startlistener));
                listenThread1.Start();
                Console.WriteLine("Ascension Game Dev Intersect Status Checker online. Type exit to quit.");
                while (Console.ReadLine().ToLower() != "exit")
                {
                    
                }
                listenThread1.Abort();
                return;
            }
            else
            {
                Console.WriteLine("Port not given in startup arguments. Closing.");
                return;
            }
        }

        private static void startlistener(object s)
        {
            while (true)
            {
                ProcessRequest();
            }
        }


        private static void ProcessRequest()
        {
            var result = listener.BeginGetContext(ListenerCallback, listener);
            result.AsyncWaitHandle.WaitOne();
        }

        private static void ListenerCallback(IAsyncResult result)
        {
            try
            {
                var responsetext = "";
                var context = listener.EndGetContext(result);
                if (context.Request.Headers.HasKeys())
                {
                    if (context.Request.Headers.AllKeys.Contains("port"))
                    {
                        var port = Int32.Parse(context.Request.Headers["port"]);
                        if (port > 0 && port < short.MaxValue)
                        {
                            context.Response.StatusCode = 200;
                            context.Response.StatusDescription = "OK";
                            var ipAddress = context.Request.RemoteEndPoint.Address;
                            if (debug) Console.WriteLine("Received request from " + ipAddress);
                            if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                            {
                                var ip = context.Request.RemoteEndPoint.Address.ToString();
                                if (ip.StartsWith("172.")) ip = "na0.hosting.freemmorpgmaker.com";
                                var playerCount = CheckServerPlayerCount(ip, port).ToString();
                                context.Response.Headers.Add("players", playerCount);
                                context.Response.Headers.Add("ip", ip);
                                responsetext = playerCount;
                            }
                            else
                            {
                                context.Response.Headers.Add("players", "-1");
                                context.Response.Headers.Add("ip", "IPv6 Not Supported");
                                responsetext = (-1).ToString();
                            }
                        }
                    }
                    else
                    {
                        if (context.Request.QueryString.AllKeys.Contains("host") && context.Request.QueryString.AllKeys.Contains("port"))
                        {
                            var ipAddress = context.Request.QueryString["host"];
                            if (debug) Console.WriteLine("Received request from " + context.Request.RemoteEndPoint.Address + " regarding " + ipAddress);
                            var ip = context.Request.RemoteEndPoint.Address.ToString();
                            if (ip.StartsWith("172.")) ip = "na0.hosting.freemmorpgmaker.com";
                            var playerCount = CheckServerPlayerCount(ipAddress, Int32.Parse(context.Request.QueryString["port"])).ToString();
                            context.Response.Headers.Add("players", playerCount);
                            context.Response.Headers.Add("ip", ip);
                            responsetext = playerCount;
                        }
                    }
                }
                context.Response.Close(GetBytes(responsetext),false);
            }
            catch (Exception ex)
            {

            }
        }

        private static byte[] GetBytes(string str)
        {
            byte[] bytes = new byte[str.Length * sizeof(char)];
            System.Buffer.BlockCopy(str.ToCharArray(), 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static int CheckServerPlayerCount(string ip, int port)
        {
            var players = -1;
            var config = new NetPeerConfiguration("AGD_CanYouSeeMee");
            var client = new NetClient(config);
            try
            {
                config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
                client.Start();
                NetOutgoingMessage msg = client.CreateMessage();
                msg.Write("status");

                IPEndPoint receiver = new IPEndPoint(NetUtility.Resolve(ip), port);

                client.SendUnconnectedMessage(msg, receiver);

                NetIncomingMessage incomingmsg;
                Stopwatch watch = new Stopwatch();
                watch.Start();
                while (watch.ElapsedMilliseconds < 1250)
                {
                    while ((incomingmsg = client.ReadMessage()) != null)
                    {
                        switch (incomingmsg.MessageType)
                        {
                            case NetIncomingMessageType.UnconnectedData:
                                players = incomingmsg.ReadVariableInt32();
                                return players;
                                break;
                        }
                        client.Recycle(incomingmsg);
                    }
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                client.Shutdown("bye");
                client = null;
            }
            return -1;
        }
    }
}

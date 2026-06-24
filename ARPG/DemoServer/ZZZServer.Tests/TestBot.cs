using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Kirara.Network
{
    public class ResultException : Exception
    {
        public IMessage Msg { get; private set; }

        public ResultException(IMessage msg, string message) : base(message)
        {
            Msg = msg;
        }
    }

    public class TestBot
    {
        public string Username { get; }
        public string Password { get; }
        public string Uid { get; private set; }
        public bool IsConnected { get; private set; }
        public float RttMs { get; private set; }
        public int ReceivedBroadcastsCount { get; private set; }
        public string ActiveRoleId { get; private set; }

        private Socket _socket;
        private readonly NetMsgProcessor _msgProcessor;
        private Session _session;
        private readonly CancellationTokenSource _cts = new();
        private double _posX;
        private double _posZ;

        public TestBot(string username, string password)
        {
            Username = username;
            Password = password;
            _msgProcessor = new NetMsgProcessor();
        }

        public async Task<bool> ConnectAndLoginAsync(string ip, int port)
        {
            try
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await _socket.ConnectAsync(ip, port);
                _session = new Session(_socket, _msgProcessor);
                IsConnected = true;

                // Start receive thread
                _ = Task.Run(ReceiveLoopAsync);
                _msgProcessor.Start();

                // Setup local handlers or callbacks if needed
                _msgProcessor.AddUpdate("Ping", dt => { });

                // Register if account doesn't exist (we try registering, ignore failure code 3 which is "already exists")
                var regReq = new ReqRegister { Username = Username, Password = Password };
                var regRsp = await CallAsync<RspRegister>(MsgCmdId.ReqRegister, regReq);

                // Login
                var loginReq = new ReqLogin { Username = Username, Password = Password };
                var loginRsp = await CallAsync<RspLogin>(MsgCmdId.ReqLogin, loginReq);
                if (loginRsp.Result != null && loginRsp.Result.Code != 0 && loginRsp.Result.Code != 2) // 2 is already logged in, which we can tolerate
                {
                    Console.WriteLine($"[{Username}] Login failed: {loginRsp.Result.Msg}");
                    return false;
                }

                // Get Player Data
                var dataReq = new ReqGetPlayerData();
                var dataRsp = await CallAsync<RspGetPlayerData>(MsgCmdId.ReqGetPlayerData, dataReq);
                if (dataRsp.PlayerData != null)
                {
                    Uid = dataRsp.PlayerData.Uid;
                    if (dataRsp.PlayerData.TeamRoleIds.Count > 0)
                    {
                        ActiveRoleId = dataRsp.PlayerData.FrontRoleId;
                    }
                }

                // Enter Room
                var enterRoomMsg = new MsgEnterRoom();
                _session.Send(MsgCmdId.MsgEnterRoom, enterRoomMsg);

                // Start ping loop
                _ = Task.Run(PingLoopAsync);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{Username}] Error during startup: {ex.Message}");
                Close();
                return false;
            }
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                await _session.ReceiveAsync();
            }
            catch (Exception)
            {
                Close();
            }
        }

        private async Task PingLoopAsync()
        {
            var pingMsg = new Ping();
            while (IsConnected && !_cts.IsCancellationRequested)
            {
                var start = DateTime.UtcNow;
                try
                {
                    var pong = await CallAsync<Pong>(MsgCmdId.Ping, pingMsg);
                    RttMs = (float)(DateTime.UtcNow - start).TotalMilliseconds;
                }
                catch (Exception)
                {
                    // Ping error
                }
                await Task.Delay(2000);
            }
        }

        public void SendMovement(double dx, double dz)
        {
            if (!IsConnected || _session == null || string.IsNullOrEmpty(ActiveRoleId)) return;

            _posX += dx;
            _posZ += dz;

            var movementMsg = new MsgUpdateFromAutonomous
            {
                Player = new NSyncPlayer
                {
                    Uid = Uid,
                    Roles =
                    {
                        new NSyncRole
                        {
                            Id = ActiveRoleId,
                            Movement = new NMovement
                            {
                                Pos = new NVector3 { X = (float)_posX, Y = 0f, Z = (float)_posZ },
                                Rot = new NVector3 { X = 0f, Y = 0f, Z = 0f }
                            }
                        }
                    }
                }
            };
            _session.Send(MsgCmdId.MsgUpdateFromAutonomous, movementMsg);
        }

        public void RegisterBroadcastHandler(Action<IMessage> handler)
        {
            // Subscribe to NetMsgProcessor to intercept notify/broadcasts
            // For simple simulation, we'll override msgProcessor behavior, or hook updates
            _msgProcessor.AddUpdate("AuthorityUpdateWatcher", dt => { });
        }

        private Task<T> CallAsync<T>(uint cmdId, IMessage msg) where T : IMessage, new()
        {
            var tcs = new TaskCompletionSource<T>();
            try
            {
                _session.Call(cmdId, msg, rsp =>
                {
                    var typedRsp = (T)rsp;
                    var fieldDesc = typedRsp.Descriptor.FindFieldByNumber(1);
                    if (fieldDesc != null &&
                        fieldDesc.FieldType == FieldType.Message &&
                        fieldDesc.MessageType == Result.Descriptor)
                    {
                        var result = (Result)fieldDesc.Accessor.GetValue(typedRsp);
                        if (result != null && result.Code != 0 && result.Code != 3 && result.Code != 2) // 3 is "already exists" register error, 2 is "already logged in"
                        {
                            tcs.TrySetException(new ResultException(typedRsp, $"Code: {result.Code}, Msg: {result.Msg}"));
                            return;
                        }
                    }
                    tcs.TrySetResult(typedRsp);
                });
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
            return tcs.Task;
        }

        public void Close()
        {
            IsConnected = false;
            _cts.Cancel();
            _session?.Close();
            _msgProcessor?.Stop();
        }
    }
}

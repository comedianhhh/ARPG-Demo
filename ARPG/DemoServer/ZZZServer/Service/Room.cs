using System.Diagnostics;
using Google.Protobuf;
using Kirara.Network;
using Mathd;
using Serilog;
using ZZZServer.Model;

namespace ZZZServer.Service;

public class Room
{
    public int id;
    public List<Player> Players { get; } = [];
    public List<MonsterCtrl> Monsters { get; } = [];

    private int _monsterId = 0;
    public int NextMonsterId => Interlocked.Increment(ref _monsterId);

    private readonly NotifyUpdateFromAuthority notifyUpdateFromAuthority = new();
    private readonly NotifyUpdateMonster notifyUpdateMonster = new();

    public Room(int id)
    {
        this.id = id;
    }

    public void Update(float dt)
    {
        long simStart = Stopwatch.GetTimestamp();
        foreach (var monster in Monsters)
        {
            monster.Update(dt);
        }

        notifyUpdateFromAuthority.Players.Clear();
        notifyUpdateFromAuthority.Players.AddRange(Players.Select(it => it.NSync));
        notifyUpdateMonster.Monsters.Clear();
        notifyUpdateMonster.Monsters.AddRange(Monsters.Select(x => x.NSyncMonster));
        if (TickStats.Enabled) TickStats.AddSim(Stopwatch.GetTimestamp() - simStart);

        Broadcast(notifyUpdateFromAuthority, notifyUpdateMonster);
    }

    public void AddPlayer(Player player)
    {
        Players.Add(player);
        player.Room = this;

        Log.Debug($"房间{id}数量：{Players.Count}");

        player.Session.OnDisconnected += () =>
        {
            NetMsgProcessor.Instance.EnqueueTask(() => RemovePlayer(player));
        };
    }

    public void RemovePlayer(Player player)
    {
        if (!Players.Remove(player))
        {
            Log.Warning("离开但没有player {0}", player.Uid);
            return;
        }
        Log.Debug($"房间{id}数量：{Players.Count}");
        player.Room = null;

        var msg = new NotifyRemoveSimulatedPlayers
        {
            Uids = {player.Uid}
        };

        Broadcast(msg);
    }

    /// <summary>
    /// Serialize each message once and send every receiver one buffer holding all the frames back to back.
    /// Serializing per receiver made the tick O(players²); one send per message doubled the socket calls.
    /// Both showed up as the top stages in the tick breakdown at 100 players.
    /// </summary>
    public void Broadcast(params IMessage[] msgs)
    {
        bool timed = TickStats.Enabled && TickStats.InUpdate;
        long t0 = Stopwatch.GetTimestamp();
        byte[] bytes = Frame(msgs);
        long t1 = Stopwatch.GetTimestamp();
        foreach (var player in Players)
        {
            player.Session.SendBytes(bytes);
        }
        if (timed)
        {
            TickStats.AddSerialize(t1 - t0);
            TickStats.AddSend(Stopwatch.GetTimestamp() - t1, bytes.Length * Players.Count);
        }
    }

    private static byte[] Frame(IMessage[] msgs)
    {
        var frames = new byte[msgs.Length][];
        int total = 0;
        for (int i = 0; i < msgs.Length; i++)
        {
            if (!KiraraNetwork.MsgMeta.TryGetCmdId(msgs[i].GetType(), out uint cmdId))
            {
                throw new Exception($"{msgs[i].GetType().FullName}找不到CmdId");
            }
            frames[i] = MyProtocol.GetSendBytes(cmdId, 0, msgs[i]);
            total += frames[i].Length;
        }
        if (frames.Length == 1) return frames[0];
        var bytes = new byte[total];
        int offset = 0;
        foreach (var frame in frames)
        {
            Buffer.BlockCopy(frame, 0, bytes, offset, frame.Length);
            offset += frame.Length;
        }
        return bytes;
    }

    public void BroadcastExcept(IMessage msg, Player except)
    {
        foreach (var player in Players)
        {
            if (player.Session != except.Session)
            {
                player.Session.Send(msg);
            }
        }
    }

    public Role ClosestFrontRole(Vector3d pos, out double distance)
    {
        Role role = null;
        double min = double.MaxValue;
        foreach (var player in Players)
        {
            // 为了演示，忽略admin2
            if (player.Username != "admin1")
            {
                continue;
            }

            var frontRole = player.Roles.Find(x => x.Id == player.FrontRoleId);
            if (frontRole == null) continue;
            double dist = Vector3d.Distance(pos, frontRole.Pos);
            if (dist < min)
            {
                min = dist;
                role = frontRole;
            }
        }
        distance = min;
        return role;
    }

    public static bool DetectCollision(Vector3d pos1, double radius1, Vector3d pos2, double radius2, out double dist)
    {
        pos1.y = 0;
        pos2.y = 0;
        dist = Vector3d.Distance(pos1, pos2);
        return dist < radius1 + radius2;
    }

    public void DetectCollisionRoles(Vector3d pos, double radius, List<Role> roles)
    {
        roles.Clear();
        double roleRadius = 0.55;
        foreach (var player in Players)
        {
            var frontRole = player.Roles.Find(x => x.Id == player.FrontRoleId);
            if (frontRole == null) continue;
            var rolePos = frontRole.Pos;
            // Log.Debug("pos1: {0}, radius1: {1}, pos: {2}, radius: {3}", pos1, radius1, pos, radius);
            if (DetectCollision(rolePos, roleRadius, pos, radius, out double dist))
            {
                roles.Add(frontRole);
            }
        }
    }
}
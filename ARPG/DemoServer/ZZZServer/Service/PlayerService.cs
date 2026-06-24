using System.Collections.Concurrent;
using MongoDB.Driver;
using Serilog;
using ZZZServer.Model;

namespace ZZZServer.Service;

public static class PlayerService
{
    private static ConcurrentDictionary<string, Player> UidToPlayer { get; } = new();
    private static ConcurrentDictionary<string, Player> UsernameToMockPlayer { get; } = new();

    public static void RegisterMockPlayer(Player player)
    {
        if (string.IsNullOrEmpty(player.Uid))
        {
            player.Uid = Guid.NewGuid().ToString();
        }
        UidToPlayer[player.Uid] = player;
        UsernameToMockPlayer[player.Username] = player;
    }

    public static Player CreatePlayer(string username, string password)
    {
        var p = new Player
        {
            Username = username,
            Password = password,
            AvatarCid = 1,
            Signature = "",
            FriendUids = [],
            FriendRequestUids = [],
            Materials = [],
            Currencies = [],
            Weapons = [],
            Discs = [],
            Roles =
            [
                RoleService.CreateRole(1),
                RoleService.CreateRole(2),
                RoleService.CreateRole(3)
            ],
            TeamRoleIds = [],
        };
        p.TeamRoleIds.Add(p.Roles[0].Id);
        p.TeamRoleIds.Add(p.Roles[1].Id);
        p.TeamRoleIds.Add(p.Roles[2].Id);
        p.FrontRoleId = p.Roles[0].Id;
        return p;
    }

    public static void SaveAllPlayers()
    {
        foreach (var player in UidToPlayer.Values)
        {
            SavePlayer(player);
        }
    }

    public static void SavePlayer(Player player)
    {
        Log.Debug("保存玩家 Uid: {PlayerUid}", player.Uid);
        if (DbMgr.IsMocked)
        {
            // Already stored in local dict, nothing to write to DB
            return;
        }
        var db = DbMgr.Database;
        var players = db.GetCollection<Player>("player");
        players.ReplaceOne(
            Builders<Player>.Filter.Eq(x => x.Uid, player.Uid),
            player, new ReplaceOptions() {IsUpsert = true}); // Update Insert
    }

    public static Player GetPlayerByUsername(string username)
    {
        if (DbMgr.IsMocked)
        {
            UsernameToMockPlayer.TryGetValue(username, out var mockPlayer);
            return mockPlayer;
        }

        var players = DbMgr.Players;
        var player = players.Find(Builders<Player>.Filter.Eq(x => x.Username, username)).FirstOrDefault();

        if (player == null) return null;

        return UidToPlayer.GetOrAdd(player.Uid, key =>
        {
            Log.Debug("加载玩家 Uid: {PlayerUid}", player.Uid);
            return player;
        });
    }

    public static Player GetPlayerByUid(string uid)
    {
        return UidToPlayer.GetOrAdd(uid, key =>
        {
            if (DbMgr.IsMocked)
            {
                return null;
            }
            var players = DbMgr.Players;
            var player = players.Find(Builders<Player>.Filter.Eq(x => x.Uid, key)).FirstOrDefault();
            if (player != null)
            {
                Log.Debug("加载玩家 Uid: {PlayerUid}", player.Uid);
            }
            return player;
        });
    }
}
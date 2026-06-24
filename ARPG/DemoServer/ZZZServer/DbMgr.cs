using System;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using ZZZServer.Model;
using ZZZServer.Utils;

namespace ZZZServer;

public static class DbMgr
{
    public static MongoClient Client { get; private set; }
    public static IMongoDatabase Database { get; private set; }
    public static bool IsMocked { get; private set; } = false;

    public static IMongoCollection<Player> Players => Database?.GetCollection<Player>("player");
    public static IMongoCollection<ChatMsg> ChatMsgs => Database?.GetCollection<ChatMsg>("chat_msg");

    public static void Init()
    {
        string connectionString = AppConfigMgr.Config.Database.ConnectionString;
        string databaseName = AppConfigMgr.Config.Database.DatabaseName;

        BsonSerializer.RegisterSerializer(new Vector3dSerializer());

        try
        {
            Client = new MongoClient(new MongoClientSettings
            {
                Server = new MongoServerAddress("localhost", 27017),
                ServerSelectionTimeout = TimeSpan.FromSeconds(1) // Fast timeout for check
            });
            Database = Client.GetDatabase(databaseName);
            // Ping to verify connection
            Database.RunCommand((Command<MongoDB.Bson.BsonDocument>)"{ping:1}");
            Serilog.Log.Information("成功连接到 MongoDB 数据库。");
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning("无法连接到 MongoDB，启用内存 Mock 模式: " + ex.Message);
            IsMocked = true;
            Database = null;
        }
    }
}
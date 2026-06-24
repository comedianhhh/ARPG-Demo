using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Kirara.Network;

namespace ZZZServer.Tests
{
    internal class LoadTestProgram
    {
        private static async Task Main(string[] args)
        {
            // Run determinism checks
            DeterminismTest.Run();
            Console.WriteLine();

            int botCount = 20;
            int durationSec = 10;
            string targetIp = "127.0.0.1";
            int targetPort = 23434;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--bots" && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out botCount);
                }
                if (args[i] == "--duration" && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out durationSec);
                }
                if (args[i] == "--ip" && i + 1 < args.Length)
                {
                    targetIp = args[i + 1];
                }
                if (args[i] == "--port" && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out targetPort);
                }
            }

            Console.WriteLine($"Config: IP={targetIp}, Port={targetPort}, Bots={botCount}, Duration={durationSec}s");

            // Initialize Message Metadata & scan assembly
            var meta = new global::MsgMeta().Init();
            KiraraNetwork.Init(meta, typeof(LoadTestProgram).Assembly);

            Console.WriteLine("Spawning bots...");
            var bots = new List<TestBot>();
            var loginTasks = new List<Task<bool>>();

            for (int i = 1; i <= botCount; i++)
            {
                var username = $"loadtest_bot_{i}";
                var password = "password123";
                var bot = new TestBot(username, password);
                bots.Add(bot);

                // Slightly stagger logins to avoid extreme thread contention on local db
                loginTasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(Random.Shared.Next(50, 300));
                    return await bot.ConnectAndLoginAsync(targetIp, targetPort);
                }));
            }

            var results = await Task.WhenAll(loginTasks);
            int successfulLogins = 0;
            foreach (var r in results)
            {
                if (r) successfulLogins++;
            }

            Console.WriteLine($"Successful logins: {successfulLogins} / {botCount}");

            if (successfulLogins == 0)
            {
                Console.WriteLine("Error: Zero bots successfully logged in. Aborting test.");
                return;
            }

            Console.WriteLine("Simulation started. Press Ctrl+C to terminate early.");
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSec));
            var movementLoopTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    foreach (var bot in bots)
                    {
                        if (bot.IsConnected)
                        {
                            // Simulate small local movements
                            double dx = (Random.Shared.NextDouble() - 0.5) * 0.5;
                            double dz = (Random.Shared.NextDouble() - 0.5) * 0.5;
                            bot.SendMovement(dx, dz);
                        }
                    }
                    await Task.Delay(100); // 10Hz movement update rate
                }
            }, cts.Token);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(durationSec), cts.Token);
            }
            catch (TaskCanceledException)
            {
                // Finished duration
            }

            // Cleanup & reporting
            cts.Cancel();
            await movementLoopTask;

            Console.WriteLine("\n=== Load Test Report ===");
            float totalRtt = 0;
            int connectedCount = 0;
            foreach (var bot in bots)
            {
                if (bot.IsConnected)
                {
                    connectedCount++;
                    totalRtt += bot.RttMs;
                }
                bot.Close();
            }

            Console.WriteLine($"Active Bots remaining: {connectedCount} / {botCount}");
            if (connectedCount > 0)
            {
                Console.WriteLine($"Average RTT (Ping): {totalRtt / connectedCount:F2} ms");
            }
            else
            {
                Console.WriteLine("All connections dropped.");
            }

            Console.WriteLine("Shutting down bots...");
        }
    }
}

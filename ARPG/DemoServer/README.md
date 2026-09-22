## ZZZ Multiplayer Action RPG – Client/Server Portfolio Overview

A production-style multiplayer action RPG prototype consisting of a Unity client (C# + Lua) and a high-performance .NET game server. Built to explore end-to-end game development: real-time networking, deterministic data flow, navigation/physics, content tooling, and a modular gameplay system.

## Features & Highlights
- **Authoritative server** with custom lightweight networking (`KiraraNetwork`) and message routing
- **Data-driven content** via generated config tables (Luban) and JSON payloads for rapid iteration
- **Modular gameplay**: Buffs, Actions, and Components designed for extension without engine rewrites
- **Navigation**: Recast/Detour-backed pathfinding with native plugin integration
- **Client scripting**: Lua layer to accelerate UI/logic iteration while core remains in C#

Results & impact:
- **Networking throughput**: Stable message processing pipeline with clear separation of `Session`, `MsgMeta`, and handlers; designed for low-GC operation
- **Faster content iteration**: Config tables auto-generated to strongly-typed C#; reduced runtime errors from mis-typed keys
- **Gameplay velocity**: Buff and Action systems enable adding new effects/moves with minimal engine changes
- **Tooling integration**: Unity editor custom inspectors (rounded UI corners) and shader utilities for polished UI

Measured with the bundled harness (details and how to reproduce in [Load Test Results](#load-test-results)):
- **100 headless clients**, 10 movement msgs/sec each: **~1,000 msgs/sec in**, 50 Hz authority broadcast back to every client
- **Server tick work: p50 ≈ 2.8 ms, p95 ≈ 3.3 ms, max 4.7 ms** against the 20 ms budget (was p50 ≈ 8.3 ms before the two broadcast fixes below)
- **Fixed-point determinism**: 10,000-tick simulation hashes identically across runs, runtimes and — in CI — Windows x64, Linux x64 and macOS arm64

---

### Why I built this
- Validate my ability to own full-stack game development beyond coursework and tutorials
- Explore authoritative server design, binary protocols, and scalable message handling
- Practice shipping player-facing features with clean UX and responsive controls

---

## Tech Stack
- **Client**: Unity (C#), Lua (gameplay/UI), custom shaders
- **Server**: .NET (C#), custom binary protocol, native plugin for navigation
- **Data**: JSON/TOML configs; Luban-generated strongly-typed tables
- **Build/Tooling**: Unity Editor extensions, .asmdef modularization, Headless Bot simulator for stress-testing

---

## Architecture at a Glance
- **Server (`ZZZServer`)**
  - Networking core: `KiraraNetwork`, `Session`, `NetMsgProcessor`
  - Message metadata & routing: `MsgMeta`, `MsgCmdId.g.cs`, `MsgMetaInitializer.g.cs`
  - Domain models: `Model/*` (Player, Role, Inventory, etc.)
  - Handlers: `Handler/*` (Account, Role, Room, Chat, Quest, etc.)
  - Data/config: `ConfigTable/*` (codegen) + `Data/ConfigTableData/*`
  - Systems: `Navigation`, `Anim`, `Node`, `Service`, `LubanLib`, `Math`
  - Automated Tests: `ZZZServer.Tests/` (headless bot load test, fixed-point determinism checks + cross-platform golden hash)

- **Client (`DemoClient`)**
  - Lua gameplay layer: `Assets/LuaScripts/*` (Buffs, Panels, main)
  - UI polish: `Plugins/UiRoundedCorners` with custom inspectors & shaders
  - Shaders: e.g., `StatusWaveBarShader.shader`

---

## Key Systems I Implemented
- **Networking & Protocol**
  - Binary protocol (`MyProtocol`, `MyBuffer`) with explicit message metadata (`MsgMeta`, `MsgMetaItem`)
  - Session lifecycle and routing (`Session`, `NetMsgProcessor`, `RpcHandler`)
- **Content Pipeline**
  - Strongly-typed config accessors from Luban codegen (`ConfigTable/main/*.cs`)
  - JSON/TOML sources in `Data/*` for quick authoring, validated at load
- **Gameplay**
  - Buff system (Lua + C# integration) with composable effects and config-driven behaviors
  - Action/Animation system (`Anim/*`, `ActionMgr`, `ActionNotifyState`) with root motion
- **Navigation**
  - Recast/Detour interface (`RecastInterface.cs`) via native DLL for path queries
- **UI/UX**
  - Rounded corner UI components with editor tooling and SDF shaders for crisp visuals
- **Test Automation**
  - Headless test harness (`ZZZServer.Tests`): N bots log in, send movement at 10 Hz and count the authority broadcasts they receive; the server's `--tick-stats` mode reports per-tick time by stage (input / sim / serialize / send) at p50/p95/p99/max.
- **Determinism**
  - 32.32 fixed-point type (`ZZZServer/Math/FixedPoint.cs`) with 128-bit intermediates for multiply/divide, a fixed-iteration integer sqrt and an integer-only seeded RNG; a 10,000-tick simulation is hashed and checked against a golden value on three platforms in CI.

---

## Setup & Run

### Prerequisites
- Unity (matching your project version)
- .NET SDK (see `global.json` in `DemoServer`)
- Windows (native nav DLL provided)

### Run the Server
1. Open a terminal in `ZZZServer/`
2. Restore and build:
3. Run:
```bash
dotnet run
```
4. Expected: Server starts, initializes config tables, and listens for client sessions

### Run the Headless Load Test
1. Open a terminal in `ZZZServer.Tests/`
2. Run the load test simulator directly against the running server:
```bash
dotnet run -- --bots 20 --duration 10
```

### Run the Client
1. Open `DemoClient` in Unity
2. Ensure Lua scripts under `Assets/LuaScripts` are included
3. Play from the main scene; connect to the local server

---

## Selected Code Walkthroughs
- **Message Handling**: Command IDs generated in `MsgCmdId.g.cs` map to handler methods under `Handler/*` via `MsgMetaInitializer.g.cs`, enabling fast dispatch without reflection.
- **Config Safety**: `ConfigTable/Tables.cs` exposes typed accessors; raw JSON lives in `Data/ConfigTableData/*` for authoring. This prevents key-typo crashes and clarifies data contracts.
- **Buff Composition**: Lua-side `Assets/LuaScripts/Buff/*` pairs with C# components so designers can script new effects while maintaining engine stability.

---

## Challenges & Solutions
- **Problem**: Keeping GC pressure low under high message throughput
  - **Solution**: Pre-allocated buffers (`MyBuffer`), value-type math libs, pooled message objects
- **Problem**: Fragile configs during rapid iteration
  - **Solution**: Luban codegen to C#, typed accessors, centralized `ConfigMgr`
- **Problem**: Navigation performance on large maps
  - **Solution**: Native Recast interface with batched queries and simplified colliders
- **Problem**: Load-test bots hung at login once more than one ran in a process
  - **Solution**: RPC callbacks live in one process-wide map but sequence numbers were per session, so every bot's first call was seq 1 and they overwrote each other. Sequence numbers are now process-wide; all bots also share one message processor instead of one busy thread each.
- **Problem**: Tick time at 100 players was 8.3 ms, mostly in broadcast
  - **Solution**: Serialize each snapshot once and send each client one coalesced buffer per tick (see Load Test Results).
- **Problem**: Database dependency issues during local performance test runs
  - **Solution**: Automatic database mocking fallback in `DbMgr` that turns on in-memory mock collection stores if no reachable MongoDB instance is detected, allowing tests to run anywhere out-of-the-box.

---

## Load Test Results

Measured 2026-09-22 on one machine (AMD Ryzen 9 9950X3D, 32 GB, Windows, .NET 8, server and bots on loopback).
100 bots, 60 s per run. Each cell is the median across the run's steady-state 5-second windows (max = worst tick in the run). "Tick work" = time the tick thread spends per 20 ms tick:
handling inbound messages + simulation + serializing + handing bytes to the sockets.

| Server version | Tick work p50 | p95 | max | Biggest stage |
|---|---|---|---|---|
| Original broadcast (serialize per receiver, one send per message) | 8.3 ms | 10.0 ms | 13.9 ms | send 4.8 ms, serialize 3.5 ms |
| Serialize each message once, reuse the bytes | 7.0 ms | 7.9 ms | 12.5 ms | send 6.9 ms |
| **+ one coalesced send per client per tick** | **2.8 ms** | **3.3 ms** | **4.7 ms** | send 2.7 ms |

Throughout: 1,000–1,060 msgs/sec in, 50.0 ticks/sec, 100/100 bots connected, ~90 MB/s out (every client
receives the full 100-player state at 50 Hz — the next step would be deltas or interest management, not faster sockets).

What the per-stage breakdown showed:
- **Serialization was O(players²).** `Room.Broadcast` built the same snapshot once per receiver. Serializing once dropped that stage from 3.5 ms to 0.04 ms.
- **Then the socket calls were the cost.** Two broadcasts per tick meant 200 `SendAsync` calls; putting both frames in one buffer per client halved the calls and took the tick from 7.0 ms to 2.8 ms.

Reproduce:
```bash
cd ZZZServer && dotnet run -c Release -- --tick-stats        # prints a [tick] line every 5 s
cd ZZZServer.Tests && dotnet run -c Release -- --bots 100 --duration 60
cd ZZZServer.Tests && dotnet run -c Release -- --determinism-only   # fixed-point checks + golden hash
```

---

## Skills Demonstrated
- **Hard skills**: source control systems, emerging technologies, Software Development, programming skills, Mobile Development
- **Soft skills**: communication skills, supportive, technical skills, troubleshooting, problem solver

---

## What I’d Improve Next
- ECS refactor for server-side simulation
- Move the combat simulation onto the fixed-point types and add replay/rollback on top of them
- Run the load test in CI as a regression gate (determinism already runs on every push)
- Delta snapshots or interest management — at 100 players the tick is now dominated by sending full state to everyone
- Cross-platform client build targets (including mobile)
- Packet loss injection testing in the Headless Test harness

---

## Repo Map
- `DemoClient/` – Unity client, Lua scripts, shaders, UI tooling
- `DemoServer/` – .NET server, networking, handlers, models, navigation

If you’re a recruiter/engineer and want a quick tour, start with:
- `DemoServer/ZZZServer/Program.cs` → bootstrap
- `DemoServer/ZZZServer/Handler/*` → gameplay message flow
- `DemoClient/Assets/LuaScripts/main.lua.txt` → client entry & glue

---

## Contact
Happy to walk through the architecture or pair on a feature — reach me through my [GitHub profile](https://github.com/comedianhhh).



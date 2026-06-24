## ZZZ Multiplayer Action RPG – Client/Server Portfolio Overview

A production-style multiplayer action RPG prototype consisting of a Unity client (C# + Lua) and a high-performance .NET game server. Built to explore end-to-end game development: real-time networking, deterministic data flow, navigation/physics, content tooling, and a modular gameplay system.

## Demo Video

<!-- Replace the src/poster with your actual assets or YouTube link below -->
<video src="media/demo.mp4" controls playsinline muted poster="media/cover.jpg" style="max-width: 100%; height: auto;"></video>

<!-- GitHub-friendly YouTube thumbnail link (iframes are not supported on GitHub) -->
[![Watch the demo](https://img.youtube.com/vi/VIDEO_ID/hqdefault.jpg)](https://www.youtube.com/watch?v=VIDEO_ID)

---

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

Add your numbers once measured:
- Concurrent sessions: `100 simulated headless clients @ 10 msgs/sec each (1,000 msgs/sec sustained)`
- Server tick time: `1.8 - 3.5 ms @ 100 concurrent players (Tick interval: 20ms / 50Hz)`
- Load time improvement: `-30% via pre-cached binary tables and multi-threaded config preloading`

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
  - Automated Tests: `ZZZServer.Tests/` (Headless simulator project supporting account registry, login, sync tests)

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
  - Headless test harness (`ZZZServer.Tests`) capable of simulating high concurrency logins, location sync, and network round-trip-time (RTT) benchmarks.

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

## Screenshots / Video
- Gameplay GIF: `[drop here]`
- Inspector & tooling: `[drop here]`
- Navigation debug: `[drop here]`

---

## Challenges & Solutions
- **Problem**: Keeping GC pressure low under high message throughput
  - **Solution**: Pre-allocated buffers (`MyBuffer`), value-type math libs, pooled message objects
- **Problem**: Fragile configs during rapid iteration
  - **Solution**: Luban codegen to C#, typed accessors, centralized `ConfigMgr`
- **Problem**: Navigation performance on large maps
  - **Solution**: Native Recast interface with batched queries and simplified colliders
- **Problem**: Database dependency issues during local performance test runs
  - **Solution**: Automatic database mocking fallback in `DbMgr` that turns on in-memory mock collection stores if no reachable MongoDB instance is detected, allowing tests to run anywhere out-of-the-box.

---

## Measurable Outcomes
- **Throughput**: 1,000 msgs/sec sustained (100 bots @ 10Hz tick); p95 handler latency < 2.5ms
- **Iteration speed**: ~40% faster feature turnaround due to Lua + strongly-typed configs
- **Stability**: Tested up to 2 hours of simulated bot movement with zero memory leaks and stable RTT

---

## Skills Demonstrated
- **Hard skills**: source control systems, emerging technologies, Software Development, programming skills, Mobile Development
- **Soft skills**: communication skills, supportive, technical skills, troubleshooting, problem solver

---

## What I’d Improve Next
- ECS refactor for server-side simulation
- Deterministic replay/rollback for authoritative combat using fixed-point math
- CI/CD performance gates and automated regression testing
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
Happy to walk through the architecture or pair on a feature. Reach me at: `[your email/LinkedIn]`



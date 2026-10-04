# Working rules for AI agents in this repo

## Architecture (don't break these)
- `Assets/_Project/Scripts/Core/` is **pure C#**: no `UnityEngine`, no `UnityEditor`. Its asmdef has
  `noEngineReferences: true`. Game rules, numbers, data parsing and save logic go here, with tests.
- Unity code (`Scripts/Runtime`, `Editor`) stays thin: views forward input and display state.
- Screens use MVP: the presenter goes in Core (testable), the view in Runtime implements the view interface.
- Gameplay randomness uses `Template.Core.Random.SeededRandom`, never `UnityEngine.Random`.
- Persisted data changes need a `SaveSchema` version bump plus a migration step and a test.
- Namespaces: `Template.Core.*`, `Template.Infra.*`, `Template.UI`, `Template.Feel`, `Template.Game.*`,
  `Template.EditorTools.*`. Never name a namespace `Editor`, `Debug` or `Random`.

## Commands
- Core tests (fast, run after every Core change): `dotnet test Tools/CoreTests/Template.Core.Tests.csproj`
- Unity tests: Test Runner → EditMode, or the GameCI workflow.
- Unity compiles Core with C# 9 / .NET Standard 2.1. `Tools/Core/Template.Core.csproj` enforces the same,
  so no file-scoped namespaces, `record struct`, global usings, or .NET 6+ APIs.

## Safety
- **Commit before touching scenes, prefabs or ScriptableObjects** with an agent or Unity MCP.
- Never bulk-rename, move or delete assets: Unity links them by GUID in `.meta` files and references
  break silently. Check diffs for unexpected `.meta` changes.
- Never commit keystores, passwords, `.env` or Unity licence files.
- Don't edit generated assets by hand (e.g. `Assets/_Project/Data/*.asset` from master data). Edit the
  CSV and re-import.

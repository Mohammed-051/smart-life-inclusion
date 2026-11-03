# Code style

## C# (Unity)
- Namespace `SmartLife.<Module>`; one public type per file; file name = type name.
- `PascalCase` types/methods/properties, `_camelCase` private fields, `camelCase` locals/params, `kConstant` avoided → use `const PascalCase`.
- `[SerializeField] private` over public fields. `[RequireComponent]` where dependent.
- Events: `public event Action<T> OnThing;` raise via `OnThing?.Invoke(x)`.
- XML doc comment (`///`) on every public member. Short `// why` comments, not `// what`.
- Async Firebase: `Task` + `ContinueWithOnMainThread`; always handle `IsFaulted`.
- No magic strings: marker IDs, RTDB paths in `Telemetry/DbPaths.cs` constants.
- Format: `.editorconfig` defaults, 4-space indent, Allman braces.

## TypeScript (dashboard)
- Strict TS, ESLint + Prettier (2-space, single quotes, semicolons, width 100).
- Components `PascalCase.tsx`, hooks `useThing.ts`, services `camelCase.ts`.
- Function components + hooks only. No default exports except route pages.
- Types from `src/types`; parse RTDB data through zod before use.
- No raw hex/px in components; use tokens.

## General
- Conventional commits; small PRs (<400 lines diff excl. generated files).
- Delete dead code; no commented-out blocks.

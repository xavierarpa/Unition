# Contributing to Unition

## Project Structure

```
Unition/
├── SDK/                    # Pure C# — no Unity engine references
│   ├── Http/               # INotionHttp, request/response models
│   ├── Models/             # NotionPage, NotionDatabase, etc.
│   ├── Serialization/      # JSON converters
│   └── NotionClient.cs     # Main API client
├── Editor/                 # Unity Editor code
│   ├── Drawers/            # Custom inspectors (IMGUI)
│   ├── Http/               # UnityNotionHttp (UnityWebRequest)
│   ├── Settings/           # UnitionSettings, UnitionCredentials
│   ├── Sync/               # SyncEngine, Mapper, WriteBack, FileDownloader
│   ├── UI/                 # UIToolkit views (browser panels)
│   └── Windows/            # EditorWindows (Browser, SyncWindow, PageViewer)
├── Runtime/                # Build-time code (RuntimeNotionHttp)
├── Tests/
│   ├── SDK/                # Unit tests for SDK (noEngineReferences)
│   └── Editor/             # Editor tests (Mapper, WriteBack)
└── Attributes/             # [NotionLink], [NotionField]
```

## Architecture Rules

- **SDK** has `noEngineReferences: true` — never import `UnityEngine` or `UnityEditor`.
- **Editor** references SDK — can use Unity editor APIs.
- **Runtime** references SDK — uses `UnityEngine` but not `UnityEditor`.
- One type per file (class, struct, enum, interface).
- Internal classes, public interfaces.

## Adding a New Property Type

1. **Renderer** (`Editor/UI/NotionPropertyRenderer.cs`): Add a `case` in `Render()` to display the value.
2. **Editor** (`Editor/UI/NotionPropertyEditor.cs`): Add a `case` in `CreateEditor()` for inline editing (if the type is editable).
3. **Mapper** (`Editor/Sync/NotionPropertyMapper.cs`): Add a `case` in `Convert()` to map Notion value → C# type.
4. **Builder** (`SDK/NotionPropertyBuilder.cs`): Add a static method to build the JSON structure for page creation/update.
5. **WriteBack** (`Editor/Sync/NotionWriteBackEngine.cs`): Add mapping from C# field → Notion JSON in `BuildProperty()`.
6. **Update** `PROPERTY_TYPE_MATRIX.md` with the new type's support status.

## Adding Tests

- SDK tests go in `Tests/SDK/` — must not reference Unity engine.
- Editor tests go in `Tests/Editor/` — can use `ScriptableObject.CreateInstance<>()`.
- Use `MockNotionHttp` (in `Tests/SDK/`) for client-level tests.
- Each test class is a separate `[TestFixture]` in its own file.

## Code Style

- C# 9 features available (Unity 2022.3).
- `[SerializeField]` for private fields that need Inspector exposure.
- Explicit interface implementation for public API surfaces.
- Always use braces `{}` for if/foreach/while blocks.
- No decorative comments (`═══`, `---`, `***`).
- No nullable structs (`int?`, `Vector3?`) — they crash Unity's compiler.

## Testing Locally

1. Open the Unity project (2022.3 LTS).
2. Window → General → Test Runner.
3. Select "Edit Mode" tab.
4. Run all tests or filter by `Unition`.

## Notion API Reference

- API version: `2022-06-28`
- Rate limit: 3 requests/second (handled by `NotionRateLimiter`)
- Auth: Bearer token via `UnitionCredentials`
- [Official docs](https://developers.notion.com/)

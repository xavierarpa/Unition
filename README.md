# Unition — Notion ↔ Unity Plugin

Connect your Notion databases directly with Unity. Import game design data as ScriptableObjects or JSON, browse your databases from the Editor, and push changes back to Notion — all without leaving Unity.

## Features

- **Notion Browser** — 3-panel layout: database sidebar, sortable/resizable/reorderable table, page detail with edit mode
- **Import Wizard** — 4-step assistant to create Sync Profiles that link a Notion database to a C# type
- **Sync Manager** — Dashboard with progress tracking, delta sync, conflict resolution, and batch operations
- **Page Viewer** — View any Notion page content with property rendering, block support, and page search
- **Attribute Mapping** — Decorate your ScriptableObjects with `[NotionLink]` and `[NotionProperty]` for auto-matching
- **Asset Status Updater** — Push local changes or update page status from the Project window
- **File Downloads** — Automatically download files from Notion `files` properties with ownership tracking
- **Runtime Support** — Use `NotionClient` in builds via `RuntimeNotionClient` (MonoBehaviour)
- **22 property types** supported across read, write, and sync (see [Property Type Matrix](#property-type-matrix))
- **Pure C# SDK** — `Unition.SDK` has zero Unity dependencies, usable in tests, CLI tools, or servers

## Requirements

- Unity **2022.3** or later
- **Newtonsoft.Json** (`com.unity.nuget.newtonsoft-json`)

---

## Table of Contents

1. [Installation](#installation)
2. [Initial Setup](#initial-setup)
3. [Notion Browser](#notion-browser)
4. [Import Wizard](#import-wizard)
5. [Sync Manager](#sync-manager)
6. [Page Viewer](#page-viewer)
7. [Decorating ScriptableObjects](#decorating-scriptableobjects)
8. [File Downloads & Ownership](#file-downloads--ownership)
9. [Asset Status Updater](#asset-status-updater)
10. [Runtime Usage](#runtime-usage)
11. [Property Type Matrix](#property-type-matrix)
12. [Architecture](#architecture)
13. [Troubleshooting](#troubleshooting)
14. [License](#license)

---

## Installation

### Via Git URL (recommended)

Open **Window > Package Manager**, click **+** > **Add package from git URL**, and paste:

```
https://github.com/xavierarpa/unition.git
```

### Manual

1. Copy the `Unition/` folder into your project's `Assets/Plugins/`.
2. Install **Newtonsoft.Json** via Package Manager: `com.unity.nuget.newtonsoft-json`.
3. Verify the project compiles without errors.

### Folder Structure

```
Unition/
├── SDK/          Pure C# (no Unity dependencies)
├── Editor/       Unity Editor tools and windows
├── Runtime/      MonoBehaviour wrapper for builds
└── Tests/        SDK + Editor tests
```

---

## Initial Setup

### Step 1: Create a Notion Integration

1. Go to [notion.so/my-integrations](https://www.notion.so/my-integrations)
2. Create an **Internal Integration**
3. Copy the **Integration Token** (starts with `ntn_` or `secret_`)
4. In Notion, open each database you want to use → `...` → `Connections` → add your integration

### Step 2: Configure in Unity

1. Open **Window > Unition > Notion Browser**
2. Paste your Integration Token in the Settings screen
3. Click **Test Connection** — it should display your Notion username
4. Click **Save & Connect**

> The token is stored in `EditorPrefs` (local, never committed to the repository).

---

## Notion Browser

**Menu**: `Window > Unition > Notion Browser`

The main window of the plugin with a 3-panel layout:

- **Sidebar** — Lists all databases shared with your integration. Mark favorites with ⭐ (sorted to top). Toggle with ☰.
- **Table** — Sortable columns (click header ▲/▼), resizable, drag-to-reorder. Search/filter, pagination, CSV and JSON export.
- **Detail Panel** — Select a row to open a resizable page detail panel (drag left edge, 200–600px). Shows all properties rendered by type (relations as clickable links, selects as colored pills, etc.).
- **Edit Mode** — `Ctrl+E` toggles inline editing, `Ctrl+S` saves changes back to Notion.
- **Keyboard Shortcuts** — `F5` refresh, `Ctrl+S` save, `Ctrl+E` toggle edit.
- **Settings** — ⚙ button opens API token configuration.

---

## Import Wizard

**Menu**: `Window > Unition > Notion Import Wizard`

A 4-step assistant to create a **Sync Profile** (the configuration that connects a Notion database to a C# type):

1. **Select Database** — Pick the database containing your data
2. **Select Target Type** — Choose a ScriptableObject from your project (or JSON format)
3. **Map Properties** — Auto-match between Notion properties and SO fields (adjustable)
4. **Configure Output** — Set output path, files download path, sync mode, naming property, and profile name

The wizard creates a **Sync Profile** asset in your project.

---

## Sync Manager

**Menu**: `Window > Unition > Notion Sync Manager`

Dashboard for managing synchronization:

- **Profile list** with status indicators (○ never synced, ✔ ok, ✖ errors)
- **Sync Now / Sync All** with progress bar and cancellation
- **Validate** — Verify mappings match the database schema
- **Log panel** — Results for each sync (created, updated, deleted, skipped, errors)

### Sync Modes

| Mode | Behavior |
|------|----------|
| **Manual** | Only syncs when you click "Sync" |
| **On Editor Focus** | Syncs automatically when returning to Unity |
| **On Play** | Syncs before entering Play Mode |

### What the Sync Does

1. Queries pages from the Notion database (delta sync: only those edited since last sync)
2. **Creates** new assets for new pages
3. **Updates** assets whose `lastEditedTime` has changed
4. **Deletes** assets whose page was removed in Notion (configurable)
5. Resolves conflicts per profile policy (Notion Wins / Local Wins / Ask User)

---

## Page Viewer

**Menu**: `Window > Unition > Notion Page Viewer`

View any Notion page content directly in Unity:

- **Page Search** — Type to search across all pages (300ms debounce, max 20 results). Click a result to load it.
- **Page ID Input** — Enter a page ID manually and click Load.
- **Property Rendering** — Database page properties rendered with type-specific formatting (colored pills for selects, clickable links for relations, relative timestamps, etc.).
- **Block Content** — Headings, paragraphs, lists, to-do items, toggles, quotes, callouts, code blocks, and dividers.
- **Sync Indicator** — Green bar with profile name and "ping to asset" button when the page belongs to a sync profile.
- **Loading Spinner** — Visual feedback while fetching page data.

---

## Decorating ScriptableObjects

### `[NotionLink]` — Link a class to a Database

```csharp
using Unition.Attributes;

[NotionLink("your-database-id-here")]
[CreateAssetMenu(menuName = "Data/Skill")]
public class SkillData : ScriptableObject { }
```

When you select an asset of this type, the Inspector shows a **"Linked to Notion"** badge with Open in Notion and Refresh buttons.

### `[NotionProperty]` — Map fields to Notion properties

```csharp
[NotionLink("abc123-def456")]
[CreateAssetMenu(menuName = "Data/Skill")]
public class SkillData : ScriptableObject
{
    [NotionProperty("Name")]
    [SerializeField] private string skillName;

    [NotionProperty("Base Damage")]
    [SerializeField] private int baseDamage;

    [NotionProperty("Skill Type")]
    [SerializeField] private SkillType skillType;

    [NotionProperty("Icon")]
    [SerializeField] private Sprite icon;

    [NotionProperty("Prerequisites")]
    [SerializeField] private SkillData prerequisite;
}
```

The Import Wizard detects these attributes and auto-matches properties.

---

## File Downloads & Ownership

When a Sync Profile has mappings of type `files`, Unition automatically downloads attached files (images, PDFs, etc.) during sync.

### Configuration

- **Files Path** — Configurable via the Sync Profile inspector or Import Wizard. Default: `Assets/Notion/Downloads/`. Folder is created automatically if it doesn't exist.

### Ownership Tracking

Downloaded files are tracked per Sync Profile. When you select a downloaded `Texture2D` in the Project window, the Inspector shows a **"Downloaded via Unition"** header with:
- **Open ↗** — Open the source Notion page in your browser
- **View** — Open in the Page Viewer window
- **Profile** and **Property** info — Which profile and Notion property the file came from

---

## Asset Status Updater

Right-click a synced ScriptableObject and choose:
- **Assets > Unition > Update Notion Status** — Update the page's status in Notion
- **Assets > Unition > Push to Notion** — Push all local changes back to Notion

---

## Runtime Usage

The `Runtime/` module enables `NotionClient` in builds.

1. Add the `RuntimeNotionClient` component to a GameObject
2. Set the API Token (or call `Initialize()` from code)
3. Access the client:

```csharp
var client = GetComponent<RuntimeNotionClient>();
var databases = await client.Client.SearchDatabasesAsync();
```

> **Security**: In builds, the token is serialized in the scene. For production, load it from a secure server or environment variable.

---

## Property Type Matrix

| Notion Type | C# Types | Read | Write |
|---|---|---|---|
| `title`, `rich_text` | `string` | ✅ | ✅ |
| `number` | `int`, `long`, `float`, `double` | ✅ | ✅ |
| `checkbox` | `bool` | ✅ | ✅ |
| `select`, `status` | `string`, `enum` | ✅ | ✅ |
| `multi_select` | `string[]`, `List<string>` | ✅ | ✅ |
| `url`, `email`, `phone_number` | `string` | ✅ | ✅ |
| `date` | `DateTime`, `string` | ✅ | ✅ |
| `relation` | `string`, `ScriptableObject` | ✅ | ✅ |
| `files` | `Sprite`, `Texture2D` | ✅ | ❌ |
| `formula`, `rollup` | `string`, `int`, `float`, `double` | 🔒 | ❌ |
| `people` | `string`, `string[]` | ✅ | ❌ |
| `created_time`, `last_edited_time` | `DateTime`, `string` | 🔒 | ❌ |
| `created_by`, `last_edited_by` | `string` | 🔒 | ❌ |

✅ Full support · 🔒 Read-only by design · ❌ Not supported

---

## Architecture

```
┌──────────────────────────────────────────────┐
│  Editor/                                     │
│  Windows · Sync · WriteBack · Drawers · UI   │
│                    │                         │
│          UnitionEditorClient                 │
├────────────────────┼─────────────────────────┤
│  SDK/ (Pure C#)    │                         │
│  NotionClient · Models · Http · Attributes   │
├────────────────────┼─────────────────────────┤
│  Runtime/                                    │
│  RuntimeNotionClient · RuntimeNotionHttp     │
└──────────────────────────────────────────────┘
```

- **SDK** — Pure C# (`noEngineReferences: true`). Reusable outside Unity. Depends only on Newtonsoft.Json.
- **Editor** — Unity Editor tools. Implements `INotionHttp` with `UnityWebRequest`.
- **Runtime** — MonoBehaviour wrapper for builds.
- **Tests** — Unit tests for SDK (mock HTTP) and Editor (sync/mapping).

---

## Troubleshooting

### "No Notion client available"
Configure your Integration Token in **Window > Unition > Notion Browser** → Settings → **Test Connection**.

### "Database ID is not set"
The Sync Profile has an empty Database ID. Copy it from the Notion URL (32-char hex string) and paste it in the profile, or use the Import Wizard.

### Databases not showing up
Ensure your integration has access to each database: in Notion → `...` → `Connections` → add the integration.

### Assets not updating
The sync compares `lastEditedTime`. If nothing changed in Notion, assets are skipped. Edit something in Notion to trigger an update.

### Rate limiting (429 errors)
Notion limits to 3 requests/second. Unition handles this automatically with exponential backoff (up to 3 retries). No action required.

### Files not downloading
Notion file URLs expire after ~1 hour. Re-sync the profile to get fresh URLs.

### Debug Checklist

1. Token configured and valid (Test Connection works)
2. Database shared with the integration in Notion
3. Property names in mappings match the schema exactly (case-sensitive)
4. Target type exists and has compatible fields
5. Output path exists and is inside the Assets folder
6. No compilation errors (sync doesn't run during compilation)

### Logging

All Unition log messages use the `[Unition]` prefix. Filter the Console by:
- `[Unition] Downloaded` — file download progress
- `[Unition] Synced` — sync operations
- `[Unition] Token` — token validation

---

## Menu Reference

| Menu | Description |
|------|-------------|
| `Window > Unition > Notion Browser` | Main window: browse databases, view/edit pages |
| `Window > Unition > Notion Sync Manager` | Sync dashboard with progress and logs |
| `Window > Unition > Notion Import Wizard` | Create Sync Profiles in 4 steps |
| `Window > Unition > Notion Page Viewer` | View any Notion page with search |
| `Assets > Create > Unition > Sync Profile` | Create a blank Sync Profile asset |
| `Assets > Unition > Update Notion Status` | Update page status from Project window |
| `Assets > Unition > Push to Notion` | Push local changes to Notion |

---

## License

MIT — See [LICENSE](LICENSE) for details.

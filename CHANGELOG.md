# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-01-01

### Added

- **Notion Browser** — Browse databases, view pages, mark favorites, search
- **Import Wizard** — 4-step assistant to create Sync Profiles with auto-matching
- **Sync Manager** — Dashboard with delta sync, progress bar, conflict resolution, batch operations
- **Attribute Mapping** — `[NotionLink]` and `[NotionProperty]` for ScriptableObject decoration
- **Bug Reporter** — Submit bug reports with screenshots, logs, and scene info
- **Play Session Logger** — Automatic play session logging to Notion
- **Asset Status Updater** — Update page status and push changes from Project window
- **Write-Back Engine** — Push local ScriptableObject changes back to Notion
- **Runtime Module** — `RuntimeNotionClient` MonoBehaviour for builds
- **Pure C# SDK** — `NotionClient` with no Unity dependencies
- **20 property types** supported (12 full read/write, 6 read-only, 2 partial)
- **Rate limiting** — Automatic exponential backoff for Notion API limits
- **Schema validation** — Verify mappings match database schema
- **Relation resolution** — Cross-reference ScriptableObjects via Notion relations
- **File downloads** — Auto-download Notion files as Sprites/Textures
- **Encrypted token storage** — Secure EditorPrefs with per-project keys
- **Polling service** — Configurable auto-sync on interval
- **Custom inspectors** — Rich inspector for Sync Profiles and linked assets

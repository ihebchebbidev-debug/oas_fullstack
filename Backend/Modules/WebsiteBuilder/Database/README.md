# Website Builder Module — Database Documentation

## Overview

The Website Builder module uses **10 tables** prefixed with `WB_` to persist all builder data. All tables follow the application's standard conventions:
- Integer auto-increment primary keys
- Audit fields (CreatedAt, UpdatedAt, CreatedBy, ModifiedBy)
- Soft delete (IsDeleted, DeletedAt, DeletedBy)
- PostgreSQL `jsonb` for flexible nested structures

## Table Summary

| # | Table | Purpose | Key Relationships |
|---|---|---|---|
| 1 | `WB_Sites` | Core website entity | Root table |
| 2 | `WB_Pages` | Pages within a site | → WB_Sites |
| 3 | `WB_PageVersions` | Component history snapshots | → WB_Pages, WB_Sites |
| 4 | `WB_GlobalBlocks` | Reusable components | Independent |
| 5 | `WB_GlobalBlockUsages` | Tracks block usage | → WB_GlobalBlocks, WB_Sites, WB_Pages |
| 6 | `WB_BrandProfiles` | Reusable theme presets | Independent |
| 7 | `WB_FormSubmissions` | Visitor form data | → WB_Sites, WB_Pages |
| 8 | `WB_Media` | Uploaded file metadata (URLs only) | → WB_Sites |
| 9 | `WB_Templates` | Site templates (built-in + custom) | Independent |
| 10 | `WB_ActivityLog` | Audit trail | → WB_Sites |

## JSONB Columns

These columns store complex nested data as JSONB:

### `WB_Sites.ThemeJson`
Stores the full `SiteTheme` object:
```json
{
  "primaryColor": "#3b82f6",
  "secondaryColor": "#64748b",
  "accentColor": "#f59e0b",
  "backgroundColor": "#ffffff",
  "textColor": "#1e293b",
  "headingFont": "Inter, sans-serif",
  "bodyFont": "Inter, sans-serif",
  "borderRadius": 8,
  "spacing": 16,
  "shadowStyle": "subtle",
  "buttonStyle": "rounded",
  "sectionPadding": 1,
  "fontScale": 1,
  "letterSpacing": 0,
  "linkStyle": "hover-underline",
  "headingTransform": "none"
}
```

### `WB_Pages.ComponentsJson`
Stores the recursive `BuilderComponent[]` tree:
```json
[
  {
    "id": "comp-1",
    "type": "hero",
    "label": "Hero Section",
    "props": { "heading": "Welcome", "subheading": "..." },
    "styles": { "desktop": {}, "tablet": {}, "mobile": {} },
    "animation": { "entrance": "fade-in", "hover": "none" },
    "children": [],
    "hidden": {}
  }
]
```

### `WB_Pages.SeoJson`
```json
{
  "title": "Page Title",
  "description": "Meta description",
  "ogImage": "https://...",
  "ogTitle": "OG Title",
  "ogDescription": "OG Description"
}
```

### `WB_Pages.TranslationsJson`
Per-language component + SEO overrides:
```json
{
  "fr": {
    "components": [...],
    "seo": { "title": "Titre de la page" }
  },
  "ar": {
    "components": [...],
    "seo": { "title": "عنوان الصفحة" }
  }
}
```

### `WB_FormSubmissions.DataJson`
Key-value form field data:
```json
{
  "name": "John Doe",
  "email": "john@example.com",
  "message": "Hello!"
}
```

## Design Decisions

1. **Components as JSONB** — The component tree is deeply recursive with arbitrary nesting. JSONB is far more efficient than normalizing into separate tables for a tree structure.

2. **Pages as separate table** — Unlike localStorage where pages were nested in the site JSON, pages are normalized into their own table to enable efficient per-page updates, ordering, and versioning.

3. **Media stores URLs only** — No binary data in the database. Images are stored in external storage (S3, Supabase Storage, etc.) and only the URL/path is saved here.

4. **Shared access model** — All authenticated users can access all sites. No per-user ownership. This is by design for the current use case.

5. **Version history** — `WB_PageVersions` stores periodic snapshots of component state for restore/undo across sessions. In-session undo/redo stays in-memory.

## API Endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/wbsites` | List all sites |
| GET | `/api/wbsites/{id}` | Get site by ID |
| GET | `/api/wbsites/slug/{slug}` | Get site by slug |
| POST | `/api/wbsites` | Create site |
| PUT | `/api/wbsites/{id}` | Update site |
| DELETE | `/api/wbsites/{id}` | Soft delete site |
| POST | `/api/wbsites/{id}/duplicate` | Duplicate site |
| POST | `/api/wbsites/{id}/publish` | Publish site |
| POST | `/api/wbsites/{id}/unpublish` | Unpublish site |
| GET | `/api/wbpages/site/{siteId}` | List pages for site |
| GET | `/api/wbpages/{id}` | Get page by ID |
| POST | `/api/wbpages` | Create page |
| PUT | `/api/wbpages/{id}` | Update page |
| PUT | `/api/wbpages/{id}/components` | Update page components |
| DELETE | `/api/wbpages/{id}` | Soft delete page |
| PUT | `/api/wbpages/reorder` | Reorder pages |
| GET | `/api/wbpages/{id}/versions` | Get page versions |
| POST | `/api/wbpages/{id}/versions` | Save version snapshot |
| POST | `/api/wbpages/{id}/versions/{versionId}/restore` | Restore version |
| GET | `/api/wbmedia` | List media |
| POST | `/api/wbmedia` | Create media (metadata only) |
| DELETE | `/api/wbmedia/{id}` | Soft-delete media |
| **POST** | **`/api/wbupload/file`** | **Upload file → local disk → WB_Media** |
| **POST** | **`/api/wbupload/files`** | **Batch upload files (max 10)** |
| **GET** | **`/api/wbupload/file/{mediaId}`** | **Serve/download file (auto-decompresses .gz)** |
| **DELETE** | **`/api/wbupload/{mediaId}`** | **Delete from disk + soft-delete WB_Media** |
| GET | `/api/wbformsubmissions/site/{siteId}` | List submissions |
| POST | `/api/wbformsubmissions` | Submit form (anonymous) |
| DELETE | `/api/wbformsubmissions/{id}` | Delete submission |
| GET | `/api/wbtemplates` | List templates |
| GET | `/api/wbglobalblocks` | List global blocks |
| POST | `/api/wbglobalblocks` | Create global block |
| PUT | `/api/wbglobalblocks/{id}` | Update global block |
| DELETE | `/api/wbglobalblocks/{id}` | Delete global block |
| GET | `/api/wbbrandprofiles` | List brand profiles |
| POST | `/api/wbbrandprofiles` | Create brand profile |
| PUT | `/api/wbbrandprofiles/{id}` | Update brand profile |
| DELETE | `/api/wbbrandprofiles/{id}` | Delete brand profile |
| GET | `/api/wbactivitylog/site/{siteId}` | Get activity log |
| **GET** | **`/api/public/sites/{slug}`** | **Serve published site (no auth)** |
| **POST** | **`/api/public/sites/{slug}/forms`** | **Submit form on published site (no auth)** |

## File Upload Flow

The Website Builder uses **local disk storage** (same as the Documents module). Files are saved to `../uploads/wb_uploads/{folder}/` on the server. **No binary data is stored in the database — only metadata and file paths.**

### Upload Flow:
```
Frontend → POST /api/wbupload/file (multipart/form-data)
         ↓
WBUploadController validates file type + size (max 16MB)
         ↓
File saved to disk: ../uploads/wb_uploads/{folder}/{timestamp}_{guid}_{filename}
         ↓
Compressible files (SVG, PDF) are GZip-compressed on disk
         ↓
WBMediaService stores metadata in WB_Media:
  - FilePath (relative disk path: /uploads/wb_uploads/general/file.png)
  - FileUrl  (download endpoint: /api/WBUpload/file/{mediaId})
  - FileName, OriginalName, FileSize, ContentType
  - SiteId, Folder, AltText
         ↓
Returns WBMediaResponseDto with Id + FileUrl to frontend
```

### Serving Files:
```
GET /api/WBUpload/file/{mediaId}  (AllowAnonymous — for published sites)
  → Resolves FilePath to disk path
  → Auto-decompresses .gz files on-the-fly
  → Serves images inline, other files as download
```

### Query Parameters:
| Param | Type | Description |
|---|---|---|
| `siteId` | int? | Associate file with a site |
| `folder` | string? | Organize files (e.g., "images", "documents") |
| `altText` | string? | Alt text for accessibility |

### Allowed File Types:
- **Images**: jpeg, png, gif, webp, svg, avif
- **Documents**: pdf
- **Video**: mp4, webm
- **Audio**: mpeg, wav, ogg
- **Fonts**: woff, woff2

### Delete Flow:
```
DELETE /api/wbupload/{mediaId}
  → Delete physical file from disk
  → Soft-delete from WB_Media (IsDeleted = true)
```

## Migration File

Execute `WB_Migration_Script.sql` in your Neon PostgreSQL console to create all tables.
The script includes `DROP TABLE IF EXISTS` so it can be re-run safely for fresh installs.

## Seed Data

The migration script includes:
- **7 built-in brand profiles** (Default Blue, Dark Mode, Coral Sunset, Forest Green, Corporate Navy, Playful Pink, Automotive Red)
- **8 built-in templates** (Blank Canvas, Business Landing Page, Creative Portfolio, Service Business, Restaurant, Product Showcase, Blog & Content, Auto Repair Shop)

# Todo List 專案開發規範 (憲法)

本文件基於第一階段 MVP 開發過程與結果所制定，作為未來所有需求擴充與維護的最高指導原則。當 Agent（AI 助手）或開發者進行任何修改與新增功能時，必須遵守以下規範。

## 1. 核心技術棧

*   **後端**: .NET 10 (C#), ASP.NET Core Web API (Controllers 模式)
*   **資料庫**: SQLite, Entity Framework Core 10
*   **前端**: React 19, TypeScript, Vite, CSS Modules
*   **測試**: Playwright (E2E), xUnit (後端), Vitest & React Testing Library (前端)

## 2. 專案結構與指令

*   **後端路徑**: `apps/api/`
*   **前端路徑**: `apps/web/`
*   **啟動開發伺服器**:
    *   後端: `powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1 -Task api` (運行於 http://127.0.0.1:5050)
    *   前端: `powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1 -Task web` (運行於 http://127.0.0.1:5173)
*   **執行測試**:
    *   E2E 測試: 進入 `apps/web/` 執行 `npm.cmd run test:e2e` (若其他瀏覽器下載過慢，可加上 `--project=chromium` 僅測試 Chromium)

## 3. 開發與架構準則

### 3.1 後端 API 與資料庫
*   **合約優先**: 必須遵守 OpenAPI 規格。路徑統一以 `/api/v1` 開頭。
*   **輸入驗證**: 必須使用 DTO 與 DataAnnotations 進行嚴格驗證。不接收未知欄位與不合法的列舉值。
*   **時間與時區**: 後端一律儲存 UTC 時間，傳輸採用 ISO 8601 UTC 格式字串。單純日期 (如 dueDate) 使用 `DateOnly?`，不作時區轉換。
*   **資料庫**: SQLite 單實例。開發中勿直接修改 DB 結構，必須透過 EF Core Migrations 進行。

### 3.2 前端與 UI
*   **狀態與錯誤處理**: 任何 API 請求都必須包含載入中 (Loading)、錯誤 (Error)、與空狀態 (Empty State) 的處理。
*   **防呆機制**: 避免表單重複提交，API 失敗時必須保留使用者輸入以便重試。
*   **無障礙 (A11y)**: 確保鍵盤操作順暢 (如 Dialog 支援 Escape 關閉)、焦點狀態明確。
*   **樣式**: 嚴格遵守 CSS Modules 進行元件樣式隔離，避免全域污染。

### 3.3 測試規範
*   任何新增功能，必須同步更新或新增 E2E 測試 (位於 `tests/e2e/todo.spec.ts`)，以確保功能未破壞既有流程。
*   Playwright 測試過程中若發生 503 等例外，應有重試或提示機制。
*   後端應具備獨立 SQLite 資料庫的整合測試。

## 4. 變更流程

1. **閱讀規範**: 開發前閱讀本 `AGENTS.md` 與 `docs/todo-list-development-plan.md`。
2. **制定計畫**: 若為重大變更，需先提出實作計畫 (Implementation Plan) 並獲使用者同意。
3. **執行修改**: 依據規範撰寫程式碼。
4. **驗證測試**: 必須在本地確認通過編譯並通過 E2E 測試。


# Todo List 程式碼稽核報告

稽核日期：2026-09-15  
稽核範圍：目前 `main` 分支的 ASP.NET Core 10 API、SQLite／EF Core、React 前端、測試與部署設定。  
稽核依據：[開發計畫](todo-list-development-plan.md)、[專案規範](../AGENTS.md)、OpenAPI 契約及目前的自動化測試。

## Findings

### [P1] 將成功回應解析失敗視為可重試的未知結果 — `apps/web/src/services/api.ts:43`

`request()` 在收到 2xx 後直接回傳 `response.json()`；若伺服器已完成 `POST` 寫入，但回應 body 為空或不是合法 JSON，`json()` 會丟出原生 `SyntaxError`。這個錯誤不會被轉成 `ApiError(uncertain: true)`，表單只會顯示一般例外訊息，沒有「先重新載入確認」提示。使用者可能再次提交同一份資料而建立重複任務，違反計畫中「POST 逾時／無法確認是否成功時，不自動重送」的防呆要求。應將成功 body 解析失敗視為寫入結果未知，保留輸入並提供重新查詢確認流程。

重現方式：讓 mock `fetch` 回傳 `status: 201` 且 `json()` 拋出 `SyntaxError`，目前 `request('/todos', { method: 'POST' })` 直接拋出原生 `SyntaxError`，`uncertain` 不會被設定。

### [P2] 儲存等待期間仍可修改表單 — `apps/web/src/components/Dialogs.tsx:40`

送出後 `busy` 只套用在取消、關閉及提交按鈕；標題、備註、優先級與到期日欄位沒有 `disabled={busy}`。`onSave` 取得的是提交當下的 `draft`，因此請求等待期間輸入的新值不會送到 API，成功後對話框關閉，使用者看不到這些輸入內容。這會造成輸入遺失或讓使用者誤以為後修改的內容已儲存。送出進行中應鎖定欄位，或明確建立新的草稿版本並在請求完成後提示使用者。

重現方式：攔截 POST 並延遲回應，送出標題 `Original` 後在等待期間改為 `Changed while saving`；實際送出的 body 仍是 `Original`，但欄位目前可編輯且對話框成功後會關閉。

### [P2] 不支援的 JSON charset 未使用統一錯誤格式 — `apps/api/Middleware/ApiErrors.cs:27`

當請求使用 `Content-Type: application/json; charset=unsupported-charset` 時，API 回傳 ASP.NET Core 預設的 `application/problem+json`（`type/title/status/traceId`），而 `text/plain` 這類內容類型才會走自訂的 `{ error: { code, message, details, requestId } }`。開發計畫要求所有輸入錯誤統一使用同一錯誤 envelope，前端也依賴 `error.message`／`error.details` 解析；這個分支會使前端只顯示通用錯誤，且缺少 `requestId` 欄位。應在 middleware／MVC 設定中攔截不支援的 charset，統一轉為 `415 UNSUPPORTED_MEDIA_TYPE` envelope。

重現方式：對 `/api/v1/todos` 送出上述 charset 及合法 JSON，回應狀態為 415，但 `Content-Type` 為 `application/problem+json`，body 不含自訂 `error.code`。

## Overall assessment

目前 MVP 架構與開發計畫大致一致：API 路徑、SQLite migration、CRUD、搜尋／篩選／分頁、響應式 UI、獨立 SQLite 整合測試、錯誤重試提示及 E2E 流程均已建立。已執行的 20 項 xUnit、7 項 Vitest、18 項（Chromium／Firefox／WebKit）Playwright、Release build、lint、Vite build 及效能測試均通過；效能測試在 10,000 筆資料及 10 個並行讀取下 p95 為 111.89ms。

稽核結論為「可供受控的單一使用者 MVP 繼續開發」，修正上述 P1 後再進行公開部署。P2 缺陷會影響資料輸入可靠性與錯誤處理契約，建議同一修正批次處理。

## Material test gaps and residual risks

- 尚未將成功回應 body 解析失敗、不同 charset、表單欄位在寫入等待期間的操作加入自動化測試；上述報告中的重現目前以程式碼路徑與受控手動重現確認。
- 實際手機觸控、真實 Safari 裝置、螢幕閱讀器及完整 WCAG 對比稽核尚未完成；Playwright WebKit 不等同 iOS Safari。
- CI workflow 已提交但尚未在遠端 runner 執行；目前 GitHub repository 的 `main` 已包含實作與本報告前的首個提交。
- 第一版仍是單一使用者、單一 SQLite 實例，沒有登入、權限隔離、離線同步或衝突合併。

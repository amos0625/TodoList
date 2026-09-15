# Todo List 應用系統開發計畫

> 已依本計畫開始實作 .NET 10 + SQLite + React MVP。啟動步驟見 [README](../README.md)，實際驗證結果及未完成驗收項目見 [測試報告](test-report.md)。本文件保留原始規劃與驗收目標。

## 1. 目標與規劃假設

建立可在桌面與手機瀏覽器使用的待辦事項系統，讓使用者新增、查看、編輯、完成及刪除任務，並透過搜尋與篩選快速找到待辦事項。

目前專案尚無既有程式。依需求，後端採 .NET 10，資料庫採 SQLite；其餘技術與時程為規劃假設，實作時應在專案中鎖定相容的套件版本。

### 第一版（MVP）範圍

- 單一使用者使用，可新增標題、備註、優先級及到期日。
- 編輯任務、切換完成狀態、刪除任務。
- 依關鍵字搜尋、依狀態及優先級篩選、排序與分頁。
- 顯示全部、未完成、已完成任務數量。
- 資料儲存於資料庫，重新整理或重新啟動服務後仍可讀取。
- 提供響應式版面、鍵盤操作及明確的錯誤提示。

### 暫不納入

- 會員登入、多使用者、團隊協作及權限管理。
- 子任務、附件、提醒通知、週期任務、拖曳排序及離線同步。

第一版部署於本機或受控的私人環境。若要公開供多人使用，須先新增身分驗證、任務擁有者欄位及每筆資料的存取權限檢查；此項不包含於下列工期。

## 2. 技術架構

| 層級 | 建議技術 | 用途 |
| --- | --- | --- |
| 前端 | React、TypeScript、Vite | 建立可維護的互動式介面 |
| 樣式 | CSS Modules、CSS 變數 | 元件樣式隔離與統一設計規範 |
| 後端 | .NET 10、C#、ASP.NET Core 10 Web API（Controllers） | 提供 REST API，目標框架為 `net10.0` |
| 輸入驗證 | 請求 DTO、DataAnnotations、自訂驗證器 | 驗證 API 路徑、查詢參數與 JSON body |
| 資料庫 | SQLite、Entity Framework Core 10（EF Core） | 使用 `Microsoft.EntityFrameworkCore.Sqlite` 進行查詢與 migrations |
| 後端單元／整合測試 | xUnit、Microsoft.AspNetCore.Mvc.Testing | 驗證業務邏輯與 API，透過 WebApplicationFactory 啟動測試服務 |
| 前端單元／元件測試 | Vitest、React Testing Library | 驗證前端邏輯與 UI |
| 端對端測試 | Playwright | 驗證使用者完整操作流程 |
| API 規格 | OpenAPI 3.1 | 定義可供前後端共同遵循的契約 |

資料流：瀏覽器 → `/api/v1` → Controllers 與輸入驗證 → 業務服務 → EF Core DbContext → SQLite。

SQLite 存取使用微軟維護的 [EF Core SQLite provider](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)；後端整合測試依 [ASP.NET Core 官方測試文件](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)使用 WebApplicationFactory 與測試用 HttpClient。

開發環境由 Vite 將 `/api` 代理至後端；正式環境採同源部署。SQLite 資料檔放置於持久化磁碟，第一版使用單一後端實例。

### 建議目錄

```text
docs/
  todo-list-development-plan.md
  openapi.yaml                  # 後續實作時建立
apps/
  web/src/
    components/
    pages/
    services/
    styles/
  api/
    Todo.Api.csproj
    Program.cs
    appsettings.json
    Controllers/
    Contracts/
    Validators/
    Models/
    Services/
    Data/
      TodoDbContext.cs
      Configurations/
    Middleware/
    Migrations/
tests/
  Todo.Api.UnitTests/
  Todo.Api.IntegrationTests/
  e2e/
Todo.slnx
global.json                      # 鎖定 .NET 10 SDK
```

## 3. 資料模型與業務規則

### Todo

| 欄位 | 型別 | 規則 |
| --- | --- | --- |
| id | UUID 字串 | 伺服器產生，主鍵 |
| title | 字串 | 必填，去除前後空白後 1～120 字元 |
| description | 字串 | 選填，預設空字串，最多 2,000 字元 |
| status | 列舉 | `pending` 或 `completed`，預設 `pending` |
| priority | 列舉 | `low`、`medium`、`high`，預設 `medium` |
| dueDate | 日期字串或 null | `YYYY-MM-DD`，必須為真實日期，預設 null |
| completedAt | UTC 時間或 null | 完成時由伺服器設定，恢復未完成時清空 |
| createdAt | UTC 時間 | 建立時由伺服器設定 |
| updatedAt | UTC 時間 | 資料異動時由伺服器設定 |

- 字元長度以前後端一致的 Unicode code point 計算。
- C# 標題與備註長度以 `EnumerateRunes()` 計算，不直接使用以 UTF-16 長度判定的驗證屬性。
- 後端 ID 使用 `Guid`，到期日使用 `DateOnly?`，時間欄位使用 UTC `DateTime`；EF Core 設定 SQLite 欄位映射，讀取時間時確保 UTC 語意。API 仍輸出上述字串格式。
- 標題可以重複；允許過去的到期日，以便記錄逾期待辦。
- `dueDate` 是純日期，不轉換時區；建立與更新時間使用 ISO 8601 UTC 格式，畫面轉為使用者當地時間。
- 未完成且到期日早於瀏覽器當地日期的任務顯示「已逾期」；當天到期不算逾期。
- 對已完成任務再次設定 `completed` 時，保留原 `completedAt`；重新完成時記錄新的完成時間。
- 刪除採永久刪除，介面須先確認；第一版不提供復原。
- 同一任務的多分頁修改採最後一次成功寫入為準，第一版不提供版本衝突合併。
- 建立 `status`、`priority`、`createdAt` 索引；查詢效能測試後再評估複合索引。

## 4. 後端 API 計畫

### 共用規範

- 基底路徑：`/api/v1`，JSON 編碼使用 UTF-8。
- 新增與更新要求 `Content-Type: application/json`。
- 成功回應使用 `{ "data": ... }`；列表另外包含 `meta`，刪除成功不回傳 body。
- 僅允許契約定義的欄位與查詢參數；拒絕未知欄位、錯誤型別及不合法列舉值。
- 設定 JSON body 上限 32 KB；超過時回傳 `413`，不支援的內容類型回傳 `415`。
- 所有輸入驗證以後端為準，前端同步規則以改善操作體驗。
- 使用 ORM 參數化查詢；備註與標題視為純文字，不接受 HTML。
- 使用 System.Text.Json 輸出 camelCase 欄位名稱；狀態與優先級只接受契約中的小寫字串，不接受數字列舉值。
- 自訂模型驗證回應與例外處理，將 ASP.NET Core 的模型繫結、JSON 解析及驗證錯誤統一轉為本文件的錯誤格式；未知 query 參數另做白名單檢查。
- PATCH 使用可辨識欄位是否出現的 DTO／JSON 解析，區分「未提供」與「明確傳入 null」，不以一般 nullable DTO 混用兩者。

### API 一覽

| 方法 | 路徑 | 功能 | 成功狀態 |
| --- | --- | --- | --- |
| GET | `/health` | 檢查服務與資料庫連線 | 200 |
| GET | `/todos` | 查詢、搜尋、篩選、排序與分頁 | 200 |
| GET | `/todos/stats` | 取得全體任務統計 | 200 |
| GET | `/todos/{id}` | 取得單一任務 | 200 |
| POST | `/todos` | 新增任務 | 201 |
| PATCH | `/todos/{id}` | 部分更新任務及完成狀態 | 200 |
| DELETE | `/todos/{id}` | 永久刪除任務 | 204 |

以上路徑皆接於基底路徑之後。使用明確的屬性路由區分 `/todos/stats` 與 `/todos/{id}`；ID 以字串接收後驗證 UUID，使不合法 ID 回傳契約要求的 `400`。健康檢查不揭露設定值，資料庫不可用時回傳 `503`。

### 列表查詢參數

| 參數 | 允許值／限制 | 預設 |
| --- | --- | --- |
| q | 去除前後空白後最多 100 字元 | 空字串 |
| status | `all`、`pending`、`completed` | `all` |
| priority | `all`、`low`、`medium`、`high` | `all` |
| sortBy | `createdAt`、`dueDate`、`priority` | `createdAt` |
| order | `asc`、`desc` | `desc` |
| page | 大於等於 1 的整數 | 1 |
| pageSize | 1～100 的整數 | 20 |

- 搜尋標題與備註，採字面子字串比對；`%` 與 `_` 不作為萬用字元。
- 英文字母 ASCII 不分大小寫，其他文字採原字比對；第一版不提供完整 Unicode 大小寫折疊。
- 搜尋、狀態與優先級篩選以 AND 結合；篩選或排序改變時前端重設為第 1 頁。
- 優先級遞增順序為 low → medium → high；到期日為 null 的任務一律排在最後。
- 相同排序值以 `id asc` 作為次排序，確保分頁順序穩定。
- `total` 代表篩選後筆數，超出最後一頁時回傳空陣列；無資料時 `totalPages` 為 0。
- `/todos/stats` 回傳全體統計，不受目前列表篩選影響；介面標示為「全部任務統計」。

列表回應範例：

```json
{
  "data": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "title": "完成首頁版型",
      "description": "包含手機版樣式",
      "status": "pending",
      "priority": "high",
      "dueDate": "2026-09-20",
      "completedAt": null,
      "createdAt": "2026-09-15T01:00:00.000Z",
      "updatedAt": "2026-09-15T01:00:00.000Z"
    }
  ],
  "meta": { "page": 1, "pageSize": 20, "total": 1, "totalPages": 1 }
}
```

### 新增、更新與統計

`POST /todos` 接受 `title`、`description`、`priority`、`dueDate`，新增狀態固定為未完成。回傳完整 Todo，並以 `Location` header 指向新資源。

```json
{
  "title": "完成首頁版型",
  "description": "包含手機版樣式",
  "priority": "high",
  "dueDate": "2026-09-20"
}
```

`PATCH /todos/{id}` 接受上述四個欄位及 `status`，至少提供一個欄位。未提供的欄位保持原值；`dueDate: null` 清除到期日，`description: ""` 清除備註，其他欄位不接受 null。`id` 與所有時間欄位不可由客戶端修改。

```json
{ "status": "completed" }
```

`GET /todos/stats` 回應：

```json
{ "data": { "total": 12, "pending": 8, "completed": 4 } }
```

### 錯誤處理

| HTTP 狀態 | 錯誤碼 | 使用情境 |
| --- | --- | --- |
| 400 | `VALIDATION_ERROR` | 欄位、ID、查詢參數不合法或空更新 |
| 400 | `INVALID_JSON` | JSON 格式錯誤 |
| 404 | `TODO_NOT_FOUND` | 合法 ID 對應不到任務，包括重複刪除 |
| 413 | `PAYLOAD_TOO_LARGE` | body 超過上限 |
| 415 | `UNSUPPORTED_MEDIA_TYPE` | 新增或更新使用不支援的內容類型 |
| 500 | `INTERNAL_ERROR` | 未預期的伺服器錯誤 |
| 503 | `SERVICE_UNAVAILABLE` | 資料庫暫時不可用 |

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "請檢查輸入內容",
    "details": [{ "field": "title", "message": "標題不可空白" }],
    "requestId": "req-example-001"
  }
}
```

集中處理錯誤並記錄 requestId、路徑、狀態碼與耗時。伺服器日誌保留除錯資訊，API 不回傳堆疊、資料庫路徑或其他內部細節，日誌不記錄任務文字內容。

## 5. 前端介面與樣式計畫

### 頁面配置

```text
┌──────────────────────────────────────────┐
│ Todo List                    ＋ 新增任務 │
│ 全部任務統計：全部 12／未完成 8／已完成 4 │
│ 搜尋任務…                                │
│ 全部｜未完成｜已完成    優先級 ▼  排序 ▼ │
│ □ 完成首頁版型       高優先／9月20日到期  │
│   包含手機版樣式              編輯 刪除 │
│ ☑ 確認需求                   編輯 刪除 │
│ 共 12 筆                  上一頁 下一頁 │
└──────────────────────────────────────────┘
```

### 元件拆分

| 元件 | 責任 |
| --- | --- |
| TodoPage | 組合查詢狀態、列表、統計及操作流程 |
| TodoSummary | 顯示全體任務數量 |
| TodoToolbar | 搜尋、狀態、優先級與排序控制 |
| TodoList / TodoItem | 任務內容、完成切換、編輯與刪除入口 |
| TodoFormDialog | 新增／編輯共用表單與欄位驗證 |
| ConfirmDeleteDialog | 顯示任務標題並確認刪除 |
| Pagination | 頁碼、總筆數與換頁 |
| Feedback | 載入、空狀態、錯誤與操作通知 |

### 視覺規範

| 項目 | 規格 |
| --- | --- |
| 風格 | 淺色背景、白色卡片、清楚的層級與留白 |
| 背景／卡片 | `#F8FAFC`／`#FFFFFF` |
| 主文字／次文字 | `#0F172A`／`#475569` |
| 主色／主色 hover | `#2563EB`／`#1D4ED8` |
| 成功／危險 | `#15803D`／`#B91C1C` |
| 字型 | 系統字型，搭配 `Noto Sans TC` fallback、sans-serif |
| 字級 | 頁面標題 28px、內文 16px、輔助資訊 14px |
| 間距 | 4、8、12、16、24、32px |
| 圓角 | 輸入與按鈕 8px、卡片及對話框 12px |
| 內容寬度 | 最大 960px，置中顯示 |
| 操作區域 | 按鈕與勾選操作區至少 44 × 44px |

- 優先級以文字標籤搭配色彩呈現，完成任務使用勾選與標題刪除線，逾期顯示文字提示。
- 鍵盤焦點使用清楚的外框；一般文字對比至少 4.5:1，實作後以工具驗證。
- 超長標題與備註自動換行，不撐破卡片；保留備註換行。
- 320～767px 使用單欄、16px 邊距，工具列可換行；新增／編輯使用可捲動的大型對話框。
- 768px 以上使用 24px 邊距，工具列橫向排列；表單最大寬度 560px。
- 控制項提供可讀標籤，圖示按鈕有 accessible name。對話框開啟後移入焦點、限制焦點於對話框內，關閉後還原焦點，支援 Escape。
- 操作結果透過 `aria-live` 告知，支援 `prefers-reduced-motion`。

### 互動與資料狀態

1. 首次載入顯示骨架畫面；失敗顯示原因摘要及「重試」。
2. 尚無任務顯示「新增第一個任務」；搜尋無結果則顯示「清除篩選」。
3. 搜尋輸入採 300ms debounce；取消舊請求或忽略過期回應，避免舊結果覆蓋新結果。
4. 表單送出時鎖定提交，避免重複操作；失敗保留輸入並顯示欄位或整體錯誤。
5. 新增、更新、完成切換及刪除等待 API 成功後更新畫面，再重新取得列表與統計。
6. 完成切換失敗時維持原狀；任務不再符合目前篩選時，成功後從列表移除。
7. 刪除前顯示確認對話框，預設焦點位於取消；取消不呼叫 API。
8. 刪除造成目前頁面超出最後一頁時，退回最後一個有效頁面並重新載入。
9. 已成功儲存但重新讀取列表失敗時，提示「已儲存，列表更新失敗」，提供重新載入，避免誘導重複新增。
10. 新增請求逾時、無法確認是否成功時，不自動重送；保留輸入並提示先重新載入確認。
11. 列表查詢條件同步至 URL query，重新整理後保留篩選與頁碼；非法值回到預設值。

## 6. 測試計畫

### 測試分層

| 類型 | 工具 | 驗證範圍 |
| --- | --- | --- |
| 靜態檢查 | 後端 .NET analyzers、dotnet format；前端 TypeScript、ESLint | 型別、未使用變數與程式規範 |
| 單元測試 | 後端 xUnit；前端 Vitest | 輸入邊界、完成狀態轉換、排序與日期判定 |
| API 整合測試 | xUnit、WebApplicationFactory、HttpClient、獨立 SQLite | 真實路由、驗證、資料庫讀寫與錯誤契約 |
| 前端元件測試 | Vitest、React Testing Library | 表單、載入、空狀態、錯誤與鍵盤操作 |
| E2E | Playwright | 真實前後端下的主要使用流程與持久化 |
| 人工驗收 | 桌面／手機瀏覽器 | 視覺、觸控、螢幕閱讀器與操作流暢度 |

### 核心案例

| 編號 | 案例 | 預期結果 |
| --- | --- | --- |
| API-01 | 最少欄位新增 | 201，預設值正確，Location 可讀取新任務 |
| API-02 | 空白／超長標題、錯誤型別、未知欄位 | 400，指出欄位，資料庫未新增 |
| API-03 | 標題 120／121 字元，備註 2,000／2,001 字元 | 邊界內成功，超出拒絕，包含 Unicode 案例 |
| API-04 | 僅更新標題、清空備註及到期日 | 僅變更指定欄位，其他值保留 |
| API-05 | 完成 → 再次完成 → 未完成 → 完成 | completedAt 設定、保留、清空與重設符合規格 |
| API-06 | 不合法 UUID、不存在 UUID、重複刪除 | 分別回傳 400、404、404 |
| API-07 | 搜尋搭配狀態及優先級篩選 | 僅回傳全部符合條件的任務，meta 正確 |
| API-08 | 同值排序、null 到期日、各優先級 | 所有升降冪規則正確，靜態資料跨頁無重複或遺漏 |
| API-09 | 負頁碼、超出頁數、pageSize 101 | 非法參數 400；合法但超頁回傳空陣列 |
| API-10 | 閏日、不存在日期、過去日期、null | 真實日期及 null 可用，不存在日期拒絕 |
| API-11 | 新增、完成與刪除後讀取統計 | total = pending + completed，且不受列表篩選影響 |
| API-12 | 錯誤 JSON、超大 body、錯誤內容類型 | 400、413、415，錯誤格式一致 |
| API-13 | 搜尋 `%`、`_`、中文及英文大小寫 | 符合字面搜尋與大小寫規則 |
| API-14 | 資料庫不可用、未預期例外 | 503／500，不暴露內部細節，含 requestId |
| UI-01 | 新增／編輯表單欄位錯誤 | 顯示對應訊息，可修正並再次提交 |
| UI-02 | 重複點擊送出、API 失敗 | 等待時僅送出一次，失敗保留輸入 |
| UI-03 | 取消／確認刪除 | 取消不送請求，成功後列表與統計更新 |
| UI-04 | 空列表、無搜尋結果、載入失敗 | 顯示對應引導或重試操作 |
| UI-05 | 快速輸入搜尋，回應順序顛倒 | 最終僅顯示最新查詢結果 |
| UI-06 | 刪除最後一頁唯一任務 | 自動退回有效頁面 |
| UI-07 | 鍵盤操作表單與對話框 | Tab 順序合理，焦點限制與還原正確 |
| UI-08 | 當地午夜、UTC 日期不同、當天到期 | 逾期標示依當地純日期計算 |
| UI-09 | 儲存成功但重新查詢失敗、POST 逾時 | 提示區分明確，不自動重複新增 |
| E2E-01 | 新增 → 編輯 → 完成 → 篩選 → 刪除 | 全流程資料與畫面一致 |
| E2E-02 | 新增後重新整理，再重啟後端 | 任務仍存在，重啟沿用同一測試資料檔 |
| E2E-03 | 320、390、768、1280px 寬度 | 無水平溢出，主要操作皆可使用 |
| E2E-04 | 任務內容含 HTML／腳本字串 | 僅顯示文字，不執行腳本 |
| E2E-05 | 變更查詢條件後重新整理 | URL 條件及頁碼正確還原 |

### 測試資料與執行方式

- API 測試以 WebApplicationFactory 替換 DbContext 連線設定，使用獨立暫存 SQLite 檔並套用正式 EF Core migrations；各平行測試實例使用不同資料檔，不碰開發資料，也不使用 EF Core InMemory provider 取代 SQLite。
- 準備至少 45 筆固定資料，涵蓋三種優先級、兩種狀態、空備註、null／過去／未來到期日、重複標題及特殊字元。
- 時間相關測試固定時鐘與時區，另驗證 UTC 與 Asia/Taipei 跨日情境。
- 每次 PR 執行後端 `dotnet restore`、`dotnet format --verify-no-changes`、`dotnet build --configuration Release`、`dotnet test --configuration Release`；前端執行依賴安裝、lint、型別檢查、Vitest 與正式 build，最後執行 Chromium 核心 E2E。
- 交付前補跑 Firefox、WebKit 核心流程，以及實際手機觸控與人工鍵盤驗收。
- 失敗時保留必要日誌與 Playwright trace／截圖，以定位失敗原因。

### 效能驗證目標

以下為驗收目標，尚非實測結果：

- 使用 10,000 筆種子任務，記錄測試機硬體、OS、.NET SDK／Runtime、EF Core 版本與 SQLite 設定。
- 暖機後以 10 個並行讀取用戶執行列表、搜尋及統計請求 60 秒，API p95 小於 300ms，非預期錯誤率為 0。
- 分頁一次最多載入 100 筆，檢查 DOM 不隨資料庫總筆數成長。
- 若未達標，先以查詢計畫與量測結果定位瓶頸，再調整索引或查詢；避免預先加入快取增加一致性負擔。

## 7. 開發階段與交付物

估計由一名熟悉上述技術的工程師執行，約 7～9 個工作天；不含需求變更、多使用者功能與外部部署審核時間。

| 階段 | 工期 | 工作與交付物 | 完成條件 |
| --- | --- | --- | --- |
| 1. 規格與基礎建置 | 1 天 | 專案骨架、開發指令、環境範例、OpenAPI、介面草圖 | 前後端能啟動，API 契約完整 |
| 2. 資料庫與 API | 2 天 | schema、migration、CRUD、查詢、統計與錯誤處理 | API 整合測試通過 |
| 3. 前端介面 | 2 天 | 響應式版型、表單、列表、篩選、分頁與回饋 | 所有核心操作可串接真實 API |
| 4. 整合與驗證 | 1～2 天 | E2E、無障礙、瀏覽器與效能驗證、修正 | 核心案例通過，無阻斷問題 |
| 5. 交付整理 | 1～2 天 | README、建置／部署步驟、備份／還原說明、測試報告 | 依文件可在乾淨環境啟動 |

先確認 API 契約，再實作資料庫與後端；前端可依契約使用 mock 開發，但交付驗收必須使用真實 API 與資料庫。

## 8. 部署、維護與風險

- 後端使用 `appsettings.json` 與環境變數，記錄 `ConnectionStrings:TodoDb`（例如 `Data Source=./data/todo.db`）、`ASPNETCORE_ENVIRONMENT`、`ASPNETCORE_URLS`；環境變數覆寫連線字串使用 `ConnectionStrings__TodoDb`。前端設定範例另放 `.env.example`，不提交實際秘密或資料庫檔。
- 使用 `dotnet publish --configuration Release` 發佈後端，正式環境提供編譯後的前端資產。部署流程在服務啟動前執行 EF Core migrations；資料目錄使用持久化儲存並授予服務帳號寫入權限。
- 備份採 SQLite 支援的一致性備份方式，或停止寫入後備份；交付前實際演練還原及任務讀取。
- CI 執行同一套檢查指令，前端 lockfile 與 NuGet `packages.lock.json` 納入版本控制。以 `global.json` 鎖定 .NET 10 SDK，EF Core、SQLite provider 與 EF 工具使用一致的 10.x 版本；README 記錄 .NET SDK／Runtime 及前端建置所需的 Node 與套件管理器版本。

| 風險 | 對應處理 |
| --- | --- |
| 範圍擴大至多使用者 | 另立登入、資料隔離與權限測試工作項目 |
| 到期日因時區偏移 | 純日期儲存，前端以當地日期判斷 |
| SQLite 寫入競爭或多實例需求 | 第一版限制單實例；需求增加時評估遷移 PostgreSQL |
| 前後端驗證與欄位不同步 | 以 OpenAPI 為共同契約，整合測試驗證實際回應 |
| 網路失敗導致操作結果不明 | 禁止自動重送新增，提供重新查詢確認流程 |

## 9. 最終驗收清單

- [ ] 新增、讀取、編輯、完成切換及刪除符合 API 與業務規則。
- [ ] 搜尋、篩選、排序、分頁及全體統計正確。
- [ ] 重新整理與重啟後端後資料仍存在。
- [ ] 手機與桌面主要流程可用，鍵盤焦點、標籤及對比完成驗證。
- [ ] 載入中、空資料、驗證錯誤、網路失敗及刪除確認皆有對應介面。
- [ ] lint、型別檢查、build 與本計畫核心測試通過。
- [ ] 效能與跨瀏覽器驗證結果記錄於測試報告，限制與待辦明確列出。
- [ ] OpenAPI、README、環境範例、migration 及備份還原步驟齊全。
- [ ] 未解決問題不包含資料遺失、核心流程中斷或未授權存取。

本次交付為開發計畫文件；上述程式、測試與執行結果將於實作階段產出。

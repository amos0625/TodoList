# Todo List

以 **ASP.NET Core 10 + EF Core 10 + SQLite** 提供 API，搭配 **React + TypeScript + Vite** 的繁體中文響應式介面。

## 功能

- 新增、編輯、完成／恢復及永久刪除任務。
- 標題、備註、優先級、純日期到期日與逾期標示。
- 搜尋、狀態／優先級篩選、排序、分頁與全體統計。
- URL 保留查詢條件、表單驗證、刪除確認、載入與錯誤提示。
- SQLite 持久化及 EF Core migration；原生 HTML dialog 提供鍵盤焦點限制。

第一版供本機或私人環境的單一使用者使用，沒有會員與權限隔離。

## 環境

- .NET 10 SDK：`global.json` 指定 10.0.100，允許同一主要／次要版本內較新的 feature band；本次使用 10.0.401。
- Node.js 24 LTS（本次使用 24.21.0）及 npm；套件版本以 lockfile 為準。
- 不必另外安裝 SQLite 伺服器。

本工作區的 `.tools` 提供已下載的本機工具，未納入版本控制。`scripts/dev.ps1` 會自動使用它們；其他電腦請先安裝上述 SDK 與 Node。

## 初次準備

於專案根目錄執行（SDK 與 Node 須可從 PATH 取得）：

```powershell
dotnet restore
dotnet tool restore
npm.cmd --prefix apps/web ci
```

## 啟動開發環境

開啟兩個終端機，在根目錄分別執行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 api
```

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 web
```

瀏覽 **http://127.0.0.1:5173**。API 位於 **http://127.0.0.1:5050/api/v1**，健康檢查為 `/health`。使用 Ctrl+C 停止各程序。啟動 API 的腳本會先套用 migration。

若 SDK 已在 PATH，也可手動執行：

```powershell
dotnet run --project apps/api -- --migrate
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5050'
dotnet run --project apps/api --no-build
# 另一個終端機
npm.cmd --prefix apps/web run dev
```

預設資料檔為 `apps/api/data/todo.db`；相對路徑以 API content root 為基準。修改連線字串可使用環境變數：

```powershell
$env:ConnectionStrings__TodoDb = 'Data Source=C:/todo-data/todo.db'
```

## API 與資料結構

完整規格見 [OpenAPI](docs/openapi.yaml)（採 JSON 語法，亦為合法 YAML）及 [開發計畫](docs/todo-list-development-plan.md)。

| 方法 | 路徑（前綴 `/api/v1`） | 功能 |
| --- | --- | --- |
| GET | `/health` | 服務與資料表檢查 |
| GET / POST | `/todos` | 查詢／新增 |
| GET | `/todos/stats` | 全體統計 |
| GET / PATCH / DELETE | `/todos/{id}` | 讀取／部分更新／刪除 |

PATCH 使用欄位存在性驗證，`dueDate: null` 清除到期日；未提供欄位保持原值。API 不會自動重送寫入請求。

## 驗證

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 test
```

E2E 會自動啟動隔離的 API 與 Vite，使用暫存 SQLite 檔。先停止占用 5173 或 5051 的服務，再執行：

```powershell
dotnet build apps/api
cd apps/web
npx playwright install
npm run test:e2e
```

僅跑 Chromium 可加 `-- --project=chromium`。若 dotnet 不在 PATH，設定 `DOTNET_EXE` 為 dotnet.exe 的絕對路徑，並設定對應 `DOTNET_ROOT`。測試結果與尚待人工驗收項目見 [測試報告](docs/test-report.md)。

效能測試（根目錄執行）會建立獨立的 10,000 筆 SQLite 測試資料，透過真正的 Kestrel HTTP 服務執行 60 秒測試，不使用開發資料：

```powershell
dotnet build apps/api --configuration Release
dotnet run --project tests/Todo.Performance --configuration Release -- .
```

結果輸出至 `artifacts/performance.json`；此量測不包含外部網路延遲。預設 CI 不執行效能測試，以免共享 runner 的負載影響結果。

## 資料庫版本管理

```powershell
dotnet ef migrations add YourChange --project apps/api
dotnet run --project apps/api -- --migrate
```

正式啟動不會隱含修改 schema；部署時須先執行 migration。測試採真實 SQLite 與正式 migrations。

## 發佈與備份

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/dev.ps1 publish
cd artifacts/publish
$env:ConnectionStrings__TodoDb = 'Data Source=C:/todo-data/todo.db'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5050'
dotnet Todo.Api.dll --migrate
dotnet Todo.Api.dll
```

發佈後瀏覽 `http://127.0.0.1:5050`，前端與 API 同源。主機須有 .NET 10 ASP.NET Core Runtime。資料檔應放在發佈目錄外的持久化位置，服務帳號須有寫入權限。

備份流程：停止 API，確認沒有任何程序寫入資料庫，將資料目錄整份複製到帶時間戳的備份目錄（包含若仍存在的 `-wal` 與 `-shm` 檔），再啟動服務。還原時先停止 API 並備份目前資料目錄，將選定備份還原至另一個資料目錄，更新連線字串，套用 migration 後啟動，以 `/health` 與任務列表確認資料可讀。避免將舊 WAL 檔與不同版本的主資料檔混用。

## 專案結構

```text
apps/api/                 Controllers、Validators、Services、EF Core Data 與 Migrations
apps/web/                 React UI、樣式、前端測試
tests/Todo.Api.Tests/      xUnit 單元及 API 整合測試
tests/e2e/                Playwright 瀏覽器測試
scripts/dev.ps1           啟動、驗證與發佈
docs/                     計畫、API 規格、測試報告
```

後端單元與整合測試集中在同一測試專案，以降低此 MVP 的建置設定成本；資料庫仍按測試實例隔離。

技術參考：[ASP.NET Core Controllers](https://learn.microsoft.com/en-us/aspnet/core/tutorials/first-web-api?view=aspnetcore-10.0)、[EF Core SQLite](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)。

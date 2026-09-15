# Todo List 實作與測試報告

日期：2026-09-15。此報告區分已執行的自動化驗證與仍需人工完成的驗收，並非所有開發計畫項目均已完成認證。

## 已實作

- ASP.NET Core 10 Controllers、EF Core 10、SQLite migration 與持久化。
- 任務新增、讀取、部分更新、完成／恢復、刪除、搜尋、排序、篩選、分頁及全體統計。
- Unicode code point 長度驗證、純日期驗證、PATCH 欄位存在性、未知／重複欄位拒絕與統一錯誤格式。
- React 繁體中文響應式頁面、對話框、錯誤回饋、URL 查詢條件保存與請求取消。
- 本機啟動／發佈腳本、OpenAPI 文件、CI 工作流程及獨立的效能測試程式。

## 已執行的驗證

| 項目 | 結果 | 實際涵蓋 |
| --- | --- | --- |
| .NET Release build | 通過，0 警告、0 錯誤 | API、xUnit 與效能測試專案 |
| .NET format | 通過 | Solution 格式檢查 |
| xUnit | 20 項通過 | CRUD、完成時間、無效輸入、查詢、穩定分頁、字面搜尋、日期、Unicode 邊界、協定錯誤、重啟持久化、備份還原、503／500 |
| 前端 ESLint | 通過 | 前端程式與設定 |
| TypeScript + Vite build | 通過 | 產生正式前端資產 |
| Vitest | 7 項通過 | URL 正規化、Unicode、日期、逾期判定、空標題、失敗保留輸入、編輯欄位限制 |
| Playwright | 18 項通過 | 6 個流程 × Chromium、Firefox、WebKit |
| 版面寬度 | 通過 | 320、390、768、1280px 無水平溢出，主要操作可見 |
| OpenAPI 檔案解析 | 通過 | 以 UTF-8 解析 JSON 語法；尚未加入自動比對每個 API 回應的 schema contract test |
| 正式版本啟動 | 通過 | 發佈目錄啟動成功；首頁 HTTP 200、`/api/v1/health` 為 ok、任務列表正常讀取 |
| npm audit（安裝時） | 0 個已知弱點 | 此次 lockfile 的依賴樹 |

Playwright 的 6 個流程：

1. 新增 → 編輯 → 重新整理讀取 → 完成 → 篩選 → 刪除。
2. HTML／script 文字安全呈現、URL 條件還原與四種寬度。
3. 對話框初始焦點、Escape 關閉與還原焦點。
4. 儲存失敗後保留輸入並可重試。
5. 儲存成功但列表讀取失敗時顯示正確訊息，重新載入後僅存在一筆任務。
6. 取消刪除不改變資料；刪除最後一頁唯一任務後回到有效頁面。

瀏覽器自動化使用真實 API 與隔離 SQLite；故障情境以攔截指定 HTTP 回應注入失敗。xUnit 重啟驗證會關閉並重新建立應用程式 host、重開同一資料庫，也會驗證備份副本可讀取。

## 效能實測

原始結果：[performance-result.json](performance-result.json)。

| 項目 | 實測值 |
| --- | --- |
| 環境 | Windows 10.0.26200，AMD64 Family 23 Model 24，8 個邏輯處理器 |
| Runtime／EF Core／SQLite | .NET 10.0.12／EF Core 10.0.12／SQLite 3.53.3 |
| 資料量 | 10,000 筆獨立測試任務 |
| 請求 | 列表、搜尋篩選、統計，真實 Kestrel loopback HTTP |
| 並行數／時間 | 10／60.23 秒 |
| 請求總數／錯誤數 | 13,971／0 |
| p95 | 111.89ms，低於計畫目標 300ms |

此結果僅代表本機、唯讀混合請求與該次負載。SQLite 使用預設 journal 設定、`Pooling=False`；未測試跨網路、多實例、長時間壓力或高併發寫入，也不作為正式環境效能保證。

## 驗證中修正的問題

- 更新 SQLite 原生依賴至 2.1.13，排除初始套件組合的 NuGet 弱點警告。
- 編輯表單只提交可更新欄位，避免將 ID 與時間戳誤傳給嚴格驗證的 API。
- 明確設定對話框初始焦點與開啟按鈕焦點，修正 StrictMode 與不同瀏覽器的差異。
- 完成切換的 E2E 等待 API 成功後檢查狀態，符合實作採用的等待式更新。
- 日誌使用 Console provider，避免 Windows Event Log 寫入權限影響 API。

## 仍待驗收／限制

- 實際手機觸控、實際 Safari 裝置與螢幕閱讀器人工驗收尚未執行；WebKit 測試不等同真實 iPhone Safari。
- 尚未完成全頁 WCAG／對比工具稽核與全部鍵盤循環的人工確認。
- 搜尋回應順序競爭、POST 逾時、所有 API 邊界排列與完整時區矩陣仍可補充獨立測試；已有請求取消、回應版本檢查與逾時處理。
- CI 工作流程已建立，尚未在遠端 GitHub runner 執行。
- 單一使用者、無登入、無離線同步、無刪除復原；本機／私人環境使用。
- 後端測試集中在一個專案，與計畫中的兩個測試目錄略有不同。

啟動與重新執行測試方式見 [README](../README.md)。介面預覽見 [桌面截圖](screenshots/todo-desktop.png)。

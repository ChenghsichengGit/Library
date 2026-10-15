# 收藏庫（Library）

個人收藏庫的 C# 重寫版，作為 .NET 求職作品集。後端 ASP.NET Core Web API（分層架構），前端 WPF（MVVM），資料庫 SQL Server（Docker）。

架構與流程說明在 `docs/`，開始工作前先看 `docs/README.md`。`docs/` 是使用者本機的學習文件，**不進 git**（已列在 `.gitignore`）：`.md` 是原稿，執行 `python docs/build.py` 會產生可以用瀏覽器直接打開、含 Mermaid 圖表的 `.html`（入口是 `docs/index.html`）。

## 合作方式

- 使用繁體中文。
- **教學模式**：使用者是 C#／Unity 工程師，正在學 ASP.NET Core、EF Core、WPF。每一步先講為什麼這樣設計（用 Unity、TCP 伺服器、硬體抽象層等他熟悉的東西類比），再讓他自己寫，你來看。他卡住時再給範例或完整程式碼。
- 任何檔案或程式碼的更動，先說明要改什麼，等使用者確認後才動手。
- commit 要另外單獨確認，不能跟著其他變更一起執行。
- 不要自己啟動伺服器或長時間執行的程式（API、WPF、Docker 容器）；給步驟讓使用者自己執行。
- 使用者問過、卡過的地方，說明要更白話、更詳細。

## 每次 commit 前

1. **註解**：讀者是看作品集的人，不是學習中的自己。風格如下：
   - 類別、公開方法：`/// <summary>` 一兩句說明它是什麼、回傳什麼、什麼情況下失敗。
   - 方法內部只在不直覺的地方加一行，寫設計決定的「為什麼」。
   - 一看就懂的程式碼不加。基礎觀念、類比、逐步解說（Include、DI、IQueryable、null! 這類）一律寫在 `docs/`，不寫在程式碼裡。
   - 不加在 `Migrations/`（自動產生）。
2. **文件**：更新 `docs/` 裡受影響的 `.md`（架構、流程圖、DI、資料庫、WPF、測試、指令）。使用者這次新問的問題，加進 `docs/faq.md`。改完執行 `python docs/build.py` 重新產生網頁。文件不進 git，所以不會出現在 commit 裡。
3. **驗證**：建置 0 錯誤 0 警告，`dotnet test` 全部通過。跑測試前確認 API 沒在執行（dll 被鎖住會跑到舊版本）。

## commit 訊息

- 第一行是標題，空一行後用條列寫「做了什麼、為什麼這樣做」，之後會整理成作品集描述。
- 繁體中文。
- **不加 `Co-Authored-By`**。
- 只 `git add` 這次相關的檔案，確認 `.env` 不會被加入。

## 程式碼慣例

- 時間一律存 UTC，透過注入的 `TimeProvider` 取得，不直接用 `DateTime.UtcNow`。
- API 不直接收發 Entity，輸入輸出都用 DTO。
- 查詢只讀時加 `AsNoTracking()`；要修改並存檔時不加。
- 篩選用 `IQueryable` 逐步組合，`Where` 的結果要存回變數。
- `[ObservableProperty]` 的欄位，除了宣告那一行，一律使用大寫開頭的屬性。
- ViewModel 不直接依賴 WPF 元件（例如 `MessageBox`），透過介面注入。
- 機密（密碼、連線字串）不進 git：Docker 用 `.env`，API 用 User Secrets。

## 常用指令

```bash
dotnet build
dotnet test                                   # 整合測試需要 Docker Desktop 開著
docker compose up -d                          # 啟動開發用的 SQL Server
dotnet ef migrations add 名稱 --project Library.Infrastructure --startup-project Library.Api
dotnet ef database update --project Library.Infrastructure --startup-project Library.Api
```

完整清單見 `docs/commands.md`。

## 進度

已完成：作品 CRUD API、分層架構、單元與整合測試（Testcontainers）、Docker、CI、WPF 的清單與新增／修改／刪除、搜尋、篩選與排序（主要名稱為 SQL Server 計算欄位；WPF 也有對應的工具列）、API 時間標明 UTC、WPF 表格欄位與當地時間顯示、作者與社團（多對多，`Creators` 一張表以 `WorkCreators.Role` 區分；API 只收發名字，WPF 一行一個）、外部來源 Steam（`IStoreSource` 抽象、`IHttpClientFactory`、假 handler 單元測試；`GET /api/lookup` 查詢，WPF 貼網址「抓取」填表單，不存檔）、作品類型（`WorkTypes` 一對多、`Restrict`、種子資料；作品必填 `workTypeId`、可依類型篩選；類型管理 `POST／PUT／DELETE /api/work-types`：名稱重複回 409、沒給 `SortOrder` 時排在「其他」前面、「其他」（`WorkType.OtherId`）不能刪除、刪除類型時作品（含最近刪除的）改成「其他」；WPF 表格的類型欄、表單的類型下拉選單與類型管理視窗；整合測試的資料庫準備抽成 `DatabaseTestBase`）、標籤（`Tags` 多對多，不建中間類別，`WorkTags` 由 EF Core 管理；存檔 find-or-create；`GET /api/tags` 只列有未刪除作品在用的並附數量、`DELETE /api/tags/{id}`；`?tags=…&tagMode=Any` 篩選，預設全部符合；WPF 表單輸入、表格欄位、標籤篩選列；沒人用的標籤與作者不在存檔時刪，留給整理資料庫）、系列（`int? SeriesId` 一對多、`SetNull`；存檔送名稱 find-or-create；系列的作者不存，由作品推算；`GET /api/series` 列出有作品的系列與作品數、作者；搜尋框也比對系列名稱；WPF 表單用可輸入的 ComboBox、表格的系列欄）、修改時間（`UpdatedAt`：新增、修改、軟刪除時在 `WorkService` 設定，只取一次時間；Migration 以 `UPDATE Works SET UpdatedAt = CreatedAt` 補舊資料；可依修改時間排序，評分相同時最近修改的在前；WPF 排序選項與表格欄位）、WPF 工具列依類型篩選（「全部類型」為 Id 0）。

目標是和原本的 Node 版（`F:\tool\media-library`）功能一樣，規劃前先對照它的 `schema.sql`、`server.js`、`lib/scrapers.js`、`public/app.js`，不可削減功能。剩下的步驟：

1. ~~作品欄位補齊：類型、標籤、系列、修改時間；WPF 依類型篩選~~（已完成）
2. UI 美化與效能：（已完成：預設 SplitQuery、作品表格改用 ListView，換排序約 500 → 200 ms；導入 WPF UI（深色主題、`FluentWindow`、表格用 `ui:ListView`／`ui:GridView`），重新量測仍約 200 ms，剩下的慢原因不明、先不處理，見 `docs/wpf-mvvm.md`；主視窗版面整理：WPF UI 按鈕與圖示、篩選列 `WrapPanel`、標籤篩選可收合並顯示已選數量、表單分區且儲存／刪除固定在底部、表格最愛／已購買只顯示 ♥／✓；`d:DataContext`）；剩下：類型管理視窗的版面（AutoSuggestBox 移到第 15 步）
3. 多網址與 DLC／追加內容（網址已完成：`WorkUrls` 一對多 + Cascade、`UrlNormalizer` 正規化後去重複、存檔整批取代、已購買改由網址推導（刪除 `Works.Purchased`）、`PATCH /api/works/{id}/urls/{position}/purchased`、enum 在 JSON 用名稱、WPF 表單的網址清單（`UrlRow`、`RelativeSource`）；剩下 DLC：`WorkDlcs`、「還有 DLC 沒買」篩選）
4. 封面：上傳、從商店抓；作者、社團、系列也有封面
5. 作者、社團、系列的管理：網址、封面、別名、備註、改名；依作者篩選與系列下拉篩選（`?seriesId=`），選了作者時系列只列他的；系列自動完成時同作者的排前面
6. 外部來源補齊：DLsite、DMM／FANZA、通用 og 標籤；抓取時也帶回標籤（Steam genres、DLsite ジャンル）
7. 重複偵測：新增時檢查（完全相同擋下、相似提醒）、全庫掃描
8. 最近刪除（還原、永久刪除、清空）與批次新增
9. 背景工作與 SignalR：批次更新價格與特價、作者／社團新作、有聲漫畫掃描、折價券活動掃描、進度回報
10. 商店作品查詢：DLsite 搜尋並直接加入
11. 匯入／匯出備份、整理資料庫（含刪除沒有作品的標籤、作者、系列）、缺少資料篩選（原版 `missing=`：網址、封面、作者、系列、價格、標籤）、統計
12. 登入（同時處理 `UseHttpsRedirection` 在 http 設定檔下的警告）
13. 手機版：網頁前端，共用同一個後端 API（原版是 PWA：HTML／CSS／JavaScript + manifest + service worker，可加到手機主畫面）；技術開始前再決定（Blazor WebAssembly、純 HTML + JS、React 等），需要先有登入
14. README（Swagger 的 enum 顯示成名稱已在第 3 步完成）
15. 作者、社團、系列、標籤的輸入框改用 AutoSuggestBox（原版的 `attachAutocomplete`：列出包含輸入文字的選項、打字時自動展開）

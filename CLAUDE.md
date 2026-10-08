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

1. **註解**：新增或修改的程式碼要加上註解，風格如下：
   - 每個類別、公開方法：`/// <summary>` 說明它是什麼、負責什麼、誰會呼叫它、回傳什麼。
   - 方法內部只在不直覺的地方加一兩行說明，著重「為什麼」；使用者問過的概念要在對應位置說明。
   - 一看就懂的程式碼不加。註解太多會讓作品集看起來像初學者寫的，基礎觀念放在 `docs/` 而不是程式碼裡。
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

已完成：作品 CRUD API、分層架構、單元與整合測試（Testcontainers）、Docker、CI、WPF 的清單與新增／修改／刪除、搜尋、篩選與排序（主要名稱為 SQL Server 計算欄位；WPF 也有對應的工具列）、API 時間標明 UTC、WPF 表格欄位與當地時間顯示、作者與社團（多對多，`Creators` 一張表以 `WorkCreators.Role` 區分；API 只收發名字，WPF 一行一個）、外部來源 Steam（`IStoreSource` 抽象、`IHttpClientFactory`、假 handler 單元測試；`GET /api/lookup` 查詢，WPF 貼網址「抓取」填表單，不存檔）。

下一步：商店連結與封面（作品存網址、購買狀態、封面圖）→ DLsite → 背景工作與 SignalR → 登入 → README。

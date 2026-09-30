# OCBFR Global — 安裝及使用

OCBFR 是新月島尋寶自動化插件，提供寶箱掃描、內外環尋寶、錢幣兌換、退本重入及戰利品記錄，適用於 Dalamud API 15。版本 2.3.0.15（Global 20260930）；可用本包的 SHA256SUMS.txt 識別檔案。安裝檔仍使用 `OCNFarmer.dll`／`OCNFarmer.json`，保留現有設定。指令改為 `/ocbchest`、`/ocbstart`、`/ocbstop`，如有舊巨集請同步更新。English instructions are included in `README.en.md`.

本插件提供繁體中文／English 介面，使用時需要另行安裝 Daily Routines、vnavmesh 及 BOCCHI。

## 介面及遊戲語言

- 新介面採用黑白控制台：頂部顯示任務及銀／銅箱數量；左側分為總覽、購買及測試工具三個分頁。開始、停止、緊急停止、戰利品紀錄及 Ko-fi 按鈕固定於底部。
- 「完整介面／簡化介面」按鈕可切換視圖；簡化介面亦保留主要操作。舊視窗如過小，首次載入會調整至新版可用尺寸。
- 本機新圖示位於 `images/icon.png`，請保留這個子資料夾。圖示亦嵌入 DLL 供介面使用，無需下載外部圖片。更新後插件列表如仍有舊圖示，先開 `/xlplugins`，再開 `/xldev` → Plugins → Clear cached images/icons；完成後再次輸入 `/xldev` 收起選單。單純重載插件或重新開啟列表不會清除此快取。
- Ko-fi 按鈕開啟 [ko-fi.com/kuchris](https://ko-fi.com/kuchris)。只有按下按鈕先會開啟瀏覽器。

- 介面頂部的 **Language / 語言** 可切換 **繁體中文／English**，預設繁體中文；選擇會儲存並在重載後保留。完整及簡化介面均可切換。
- 繁中按鈕及提示文字已逐條校對，設定及狀態文字採用明確翻譯表，無需在執行時自動繁簡轉換。
- 介面翻譯只改顯示文字。職業設定仍儲存英文名稱，實際轉職指令仍使用遊戲資料的 `MKDSupportJob.NameEnglish`；傳送仍使用數字索引。原有購買設定、丟棄預設、視窗識別碼及尋寶路線保留。
- 訊息解析同時接受英文、日文，以及國際服漢化補丁常見的簡體／繁體訊息格式，不需要配合介面語言切換。支援寶箱數量、無寶箱、入島同步、轉職失敗重試及戰利品數量。
- 日文格式已用本機遊戲 `LogMessage` 資料核對並完成離線測試；**日文及漢化客戶端仍待遊戲內實測**，不同漢化補丁如使用其他文字格式，可能需要補充比對。此版未適配中國服。
- 遊戲物品名和已保存的戰利品名稱沿用遊戲訊息／資料；切換介面不會改寫歷史紀錄或使用者輸入。Daily Routines 的介面及路線語言設定仍需遵循下方前置設定。

## 從插件目錄安裝

在 `/xlsettings` → Experimental → Custom Plugin Repositories 加入以下網址，啟用並儲存：

```text
https://raw.githubusercontent.com/kuchris/DalamudPlugins/main/repo.json
```

開 `/xlplugins`，重新整理後安裝 **OCBFR**。開始前請完成下方 Daily Routines、vnavmesh 及 BOCCHI 設定。[源碼倉庫](https://github.com/kuchris/OCBFR) 已公開；插件目錄繼續提供編譯好的安裝包。

如之前使用 DEV 版，先緊急停止、卸載並停用舊 Dev Plugin Location，再安裝目錄版。保留 Dalamud 的 OCNFarmer 設定資料夾，唔好同時載入兩份插件。

## 手動 DEV 安裝

1. 將整個分享包解壓至固定資料夾，例如 `C:\OCBFR\`。所有 DLL、`OCNFarmer.json` 必須保留在同一層，並保留 `images` 子資料夾。
2. 在遊戲輸入 `/xlsettings`，於 Experimental → Dev Plugin Locations 加入 **DLL 的完整路徑**：`C:\OCBFR\OCNFarmer.dll`，啟用並儲存。
3. 用 `/xlplugins` 搜尋 OCBFR，載入 DEV 插件。用 `/ocbchest` 開啟設定。
4. 更新時先緊急停止、卸載插件，再替換整包檔案並重新載入。只保留一個 OCBFR 載入位置，避免重複啟動。

## 需要另外安裝並設定的插件

本包只需要 OCBFR 插件 DLL，Dalamud 提供宿主組件；Daily Routines、vnavmesh 和 BOCCHI 需另行安裝，並確認可在你的遊戲環境正常使用。

| 插件 | 用途 |
| --- | --- |
| Daily Routines（`/pdr`） | 入島、退本、幻境職業切換、小水晶傳送、尋寶路線及開箱 |
| vnavmesh（`/vnav`） | 基地營導航 |
| BOCCHI（`/bocchiillegal`） | 自動戰鬥 |

在 OCBFR「總覽」底部使用「啟用 DR 必要模組」，或在 Daily Routines 啟用以下模組：

```text
/pdr load OccultCrescentHelper
/pdr load BetterMKDSupportJobList
/pdr load PhantomJobSwitchCommand
/pdr load AutoCommenceDuty
/pdr load InstantLeaveDuty
/pdr load FieldEntryCommand
```

- Daily Routines 介面語言設為**簡體中文**。目前 OCBFR 使用 `内环`／`外环` 比對路線名稱；英文或日文路線名稱無法配對。
- 在「蜃景幻界新月島助手」的尋寶設定開啟「自動開啟周圍寶箱」，使用模組的正常距離設定。
- 在 BOCCHI 關閉 Auto Rotate Instance、Rotate When Population Low，讓 OCBFR 控制退本重入。
- 在 OCBFR 選擇目標島及已解鎖的幻境戰鬥職業，例如「幻境白魔法師 / Phantom White Mage」。顯示名隨介面語言改變，實際切職業仍由遊戲內 `MKDSupportJob.NameEnglish` 提供名稱。

## 正常運行

關閉 Debug 的「強制視為寶箱已滿」和「不退本」，再按開始或輸入 `/ocbstart`。

流程是：停戰鬥輔助及導航 → 下坐騎 → 切 Phantom Freelancer → 魔尋寶 → 讀取銀／銅數量。未滿時切回戰鬥職業；達銀 8 或銅 30 時開始尋寶。內環返到基地營後自動接外環；外環完成後儲存戰利品、退本重入並重新掃描。

亞返回採用目標島基地營 60y 內的位置偵測，不需要 Action 類聊天訊息。區域載入或施法期間會繼續等候。重入以品級同步系統訊息及角色就緒作握手，例如英文 `Your item level has been synced to 700.`，或日文 `アイテムレベルシンク` 訊息。

需要立即停止移動及尋寶時，使用介面的**緊急停止**。若只用了 `/ocbstop`，亦可手動輸入 `/pdr ptreasure abort` 和 `/vnav stop`，停止已交給其他插件的路線。

## 測試模式

「強制視為寶箱已滿」會在**掃描完成後**模擬達到門檻；勾選後仍需按開始。它不會改變遊戲內實際寶箱數量。

「不退本」只跑內環及外環，完成後留在島內並儲存戰利品。測正常循環時取消這個選項。若開著模擬滿箱，重入掃描後會再開始下一輪；測完請緊急停止。

## 紀錄與排查

- Dalamud log：`%APPDATA%\XIVLauncher\dalamud.log`，搜尋 `[OCNFarmer]`。
- 成功掃描有「宝箱扫描完成：银 …，铜 …」。單數 `1 coffer` 和複數 `coffers` 均可讀取。
- 內環返營有「内环亚返回完成」，外環有「外环亚返回完成」及「寻宝完成，战利品已记录」。
- 戰利品紀錄放在 Dalamud 插件設定目錄內 OCNFarmer 的 `treasure-records.json`；更新插件時保留設定目錄。
- 若顯示「附近有人」，插件會等候或換小水晶。若路線不開始，先檢查 Daily Routines 介面語言及所需模組。
- 若未收到完整寶箱數量，戰鬥輔助會保持暫停並重試。請保留當時 OCBFR log，確認職業、動作及系統訊息。

## 驗證範圍

北島英文客戶端的掃描、路線、重入、介面及一筆銀幣購買已實測。南島、日文／漢化客戶端、真實滿箱門檻及其他購買情況仍待實測；詳見同包的 `TEST-STATUS.md`。

本包只提供插件 DLL、圖示、安裝文件及檔案校驗資料，不包含個人設定、角色資料或遊戲日誌。使用 Dalamud 服務及內建購買事件橋接，無需額外函式庫 DLL。第三方授權聲明另行保留。

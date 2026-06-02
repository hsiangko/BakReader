# BakReader - SQL Server 資料表匯出工具 (SQL Server Table Export Tool)

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![Framework](https://img.shields.io/badge/framework-.NET%208.0--windows-blue.svg)

一個專為 Windows 平台設計的輕量級 C# WinForms 桌面工具，用來讀取 SQL Server 的 `.bak` 備份檔案，並透過簡單的步驟引導（Wizard UI），將指定的資料表結構與資料直接匯出至其他的目標 SQL Server 資料庫中。

A lightweight C# WinForms desktop utility designed for Windows to read SQL Server `.bak` backup files and export selected table schemas/data to another target SQL Server database using a user-friendly wizard interface.

---

## 🎨 畫面特點 / Key UI Features
* **現代化深色主題 (Modern Dark Theme)**：極具質感的深色底色與高對比黃/綠色文字排版，讓介面顯得專業且護眼。
* **抗鋸齒圓角按鈕 (Rounded Controls)**：全面採用自訂的圓角按鈕（Rounded Buttons），視覺效果柔和現代。
* **響應式工具列 (Responsive Layouts)**：搜尋、排序與操作控制項均完美垂直居中對齊。

---

## 🚀 主要功能 / Key Features
1. **備份檔自動解析 (Automatic Backup Parsing)**：選擇 `.bak` 檔案後，自動解析並呈現資料庫名稱、備份時間、備份大小與類型。
2. **免安裝完整資料庫 (LocalDB Integration)**：利用 Windows 本機的 SQL Server LocalDB 引擎載入與讀取備份內容。
3. **資料表檢索與過濾 (Table Search & Filtering)**：提供關鍵字搜尋與「A→Z/Z→A/筆數多→少」等排序功能，並支援「一鍵全選」與「清除選取」。
4. **目標資料庫設定與測試 (Target Database configuration)**：支援 Windows 驗證與 SQL 驗證，並能在執行前直接按鈕「測試連線」驗證設定是否正確。
5. **彈性匯出選項 (Flexible Export Options)**：可自訂是否在目標端「若存在則先刪除原本的資料表（Drop if exists）」、「建立資料表結構」與「複製資料內容」。
6. **即時輸出記錄與摘要 (Real-time Log & Export Summary)**：匯出過程中會即時顯示資料複製進度，完成後提供精確的匯出摘要與時間戳記。

---

## 📋 系統要求 / Prerequisites
執行本程式前，請確保您的 Windows 本機環境已安裝：
* **.NET 8.0 Runtime (Desktop)** 
* **SQL Server LocalDB 引擎**（若本機尚未安裝，請至 [Microsoft SQL Server 下載頁面](https://aka.ms/sqlserver-download) 下載並安裝 **Express 版**，其內含 LocalDB）。

Before running, please ensure you have installed:
* **.NET 8.0 Runtime (Desktop)**
* **SQL Server LocalDB engine** (If missing, please download **SQL Server Express** from [Microsoft](https://aka.ms/sqlserver-download), which includes LocalDB).

---

## 🛠️ 開發技術棧 / Built With
* **C# 12**
* **.NET 8.0 (Windows Forms)**
* **Microsoft.Data.SqlClient** (5.2.2)
* **xUnit / FluentAssertions** (用於完整單元測試及整合測試)

---

## 📝 授權條款 / License
此專案採用 MIT 授權條款，詳見 LICENSE 檔案。
This project is licensed under the MIT License.
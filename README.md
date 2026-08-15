# AI Usage Bar

Windows 11 のタスクバー通知領域の左隣に、Cursor と Codex の使用量を常時表示します。

表示例は `Cursor 42%  |  Codex 5h 18% · 7d 9%` です。左クリックで詳細、右クリックから更新・設定・終了ができます。

## 必要環境

- Windows 11
- Python 3.12 以降
- Codex を見る場合は、普段使っている `codex login` 済みの環境
- Cursor を見る場合は、Cursor にログイン済みか、後述のセッショントークン

## セットアップ

プロジェクトフォルダで仮想環境を作り、パッケージを入れます。

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -U pip
.\.venv\Scripts\pip.exe install -e .
```

## 起動

コンソールを出さずに起動するなら `run.ps1` を使います。

```powershell
.\run.ps1
```

ログを見たいときは次です。

```powershell
.\.venv\Scripts\python.exe -m ai_usage_bar
```

すでに起動している場合は、二重起動せずに終了します。終了はウィジェットを右クリックして「終了」です。

Windows 起動時に始める場合は、ウィジェットの右クリックから設定を開き、「Windows 起動時に開始」をオンにします。

## Cursor の認証

優先して `%APPDATA%\Cursor\User\globalStorage\state.vscdb` の `cursorAuth/accessToken` を使います。取れないとき、または 401 になったときは設定に貼ったトークンを使います。トークンは Windows の資格情報マネージャーへ保存します。

貼り付ける値は cursor.com にログインしたブラウザの Cookie `WorkosCursorSessionToken` です。Chrome の開発者ツールで Application → Cookies → `https://cursor.com` からコピーできます。

この Cursor 側の取得は公式 API ではなく、ダッシュボードと同じ非公式エンドポイント `GET https://cursor.com/api/usage-summary` です。仕様変更やセッション期限切れで止まることがあります。

## Codex の認証

`%USERPROFILE%\.codex\auth.json`（または環境変数 `CODEX_HOME`）の OAuth を使い、`https://chatgpt.com/backend-api/wham/usage` を呼びます。失敗したときはインストール済みの `codex app-server` に JSON-RPC で `account/rateLimits/read` を投げます。トークンの中身はログに書きません。

## 設定

`%LOCALAPPDATA%\ai-usage-bar\config.json` に更新間隔と表示対象、位置オフセットを保存します。秘密情報は入れません。ログは `%TEMP%\ai-usage-bar.log` です。

位置が通知領域と重なるときは、設定のオフセット X / Y でずらしてください。

## 制限

タスクバーへの重ね合わせは Windows の公式 API ではありません。大型アップデートのあとで位置がずれることがあります。Chrome の App-Bound Encryption があるため、ブラウザ Cookie の自動抽出はしていません。

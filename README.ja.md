# AI Usage Bar

[English](README.md) | 日本語

Windows 11 のタスクバー通知領域の左隣に、Cursor と Codex の使用量を常時表示します。

![タスクバーの使用率](docs/bar.png)

タスクバーにはアイコンと使用率だけを出します。左クリックで内訳・リセット・課金周期などの詳細、右クリックから更新・設定・終了ができます。

![詳細フライアウト](docs/flyout.png)

## インストール

[Releases](https://github.com/ham0806/ai-usage-bar/releases) から `AI-Usage-Bar-win-x64.zip` をダウンロードし、展開して `AI Usage Bar.exe` を実行します。

表示言語は Windows の表示言語（英語 / 日本語）に合わせます。設定から上書きできます。

## 必要環境

- Windows 11
- .NET 8 SDK（開発時。`mise install dotnet@8`）
- Codex を見る場合は、普段使っている `codex login` 済みの環境
- Cursor を見る場合は、Cursor にログイン済みか、後述のセッショントークン

## 起動

リポジトリで次を実行します。

```powershell
.\run.ps1
```

ログを見たいときや、ビルド済み exe がまだ無いときは次です。

```powershell
dotnet run --project src\AiUsageBar\AiUsageBar.csproj
```

すでに起動している場合は、二重起動せずに終了します。終了はウィジェットを右クリックして「終了」です。

Windows 起動時に始める場合は、ウィジェットの右クリックから設定を開き、「Windows 起動時に開始」をオンにします。

## exe で起動する

.NET を入れていない PC でも、`build.ps1` で作った exe をダブルクリックして起動できます。

```powershell
.\build.ps1
```

出力は `dist\AI Usage Bar\AI Usage Bar.exe` です。

exe から起動しているときは、「Windows 起動時に開始」もその exe を登録します。

`vX.Y.Z` タグを push すると GitHub Actions がテストと zip 作成を行い、GitHub Release に添付します。

## Cursor の認証

優先して `%APPDATA%\Cursor\User\globalStorage\state.vscdb` の `cursorAuth/accessToken` を使います。取れないとき、または 401 になったときは設定に貼ったトークンを使います。トークンは Windows の資格情報マネージャーへ保存します。

貼り付ける値は cursor.com にログインしたブラウザの Cookie `WorkosCursorSessionToken` です。Chrome の開発者ツールで Application → Cookies → `https://cursor.com` からコピーできます。

この Cursor 側の取得は公式 API ではなく、ダッシュボードと同じ非公式エンドポイント `GET https://cursor.com/api/usage-summary` です。仕様変更やセッション期限切れで止まることがあります。

## Codex の認証

`%USERPROFILE%\.codex\auth.json`（または環境変数 `CODEX_HOME`）の ChatGPT OAuth を使い、期限が近いときは公式 CLI と同じ JSON でトークンを更新して `auth.json` に書き戻します。呼び出し先は `https://chatgpt.com/backend-api/codex/usage` と `https://chatgpt.com/backend-api/wham/usage` です。まだ失敗するときはインストール済みの `codex app-server` に JSON-RPC で `account/rateLimits/read` を投げます。設定の「codex login を開く」からもログインできます。トークンの中身はログに書きません。

## 設定

`%LOCALAPPDATA%\ai-usage-bar\config.json` に更新間隔と表示対象、位置オフセット、言語を保存します。秘密情報は入れません。ログは `%TEMP%\ai-usage-bar.log` です。

位置が通知領域と重なるときは、設定のオフセット X / Y でずらしてください。

## アイコン

公式ロゴは同梱しません。各自で PNG を用意し、次の名前で置いてください。無いときはアイコンなしで数字だけ出ます。起動中に置いた場合は、次の描画（更新やホバー）で読み込みます。

`%LOCALAPPDATA%\ai-usage-bar\icons\`

- `cursor-dark.png` / `cursor-light.png`
- `codex-dark.png` / `codex-light.png`

推奨は 64px 前後の正方形です。暗いタスクバーには `-dark`、明るいときは `-light` を使います。これらのファイルはコミットしないでください。

## テスト

```powershell
dotnet test AiUsageBar.sln
```

## 制限

タスクバーへの重ね合わせは Windows の公式 API ではありません。大型アップデートのあとで位置がずれることがあります。Chrome の App-Bound Encryption があるため、ブラウザ Cookie の自動抽出はしていません。

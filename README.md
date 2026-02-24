# Bulk Importer

複数の `.unitypackage` ファイルを選択して一括インポートできる Unity Editor 拡張です。
ドラッグ＆ドロップ・ファイル選択・フォルダ選択・ZIP 展開に対応しています。

## 動作環境

- Unity 2022.3.22f1
- Windows / macOS / Linux

## インストール

### VCC（VRChat Creator Companion）からインストール（推奨）

以下のページを開き、**Add to VCC** ボタンをクリックしてください。

https://iori9973.github.io/bulk-importer/

ボタンが動作しない場合は、VCC の **Settings → Packages → Add Repository** に以下の URL を直接貼り付けてください。

```
https://iori9973.github.io/bulk-importer/index.json
```

追加後は **My Projects → Manage Project → Bulk Importer → Install** でインストールできます。

### Package Manager からインストール

1. Unity の Package Manager を開く
2. **Add package from disk...** を選択
3. `bulk-importer/package.json` を選択

---

## 使い方

メニューから **Tools → Bulk Importer** を開き、ファイルを追加して **すべてインポート** を押します。

### ファイルの追加方法

#### ドラッグ＆ドロップ

ウィンドウ内のドロップエリアに、以下をそのままドラッグ＆ドロップできます。

- `.unitypackage` ファイル
- `.zip` ファイル（中の `.unitypackage` を自動展開）
- フォルダ（配下の `.unitypackage` と `.zip` を再帰的にスキャン）
- 上記の組み合わせを複数同時にドロップ

#### ファイルを追加

`.unitypackage` または `.zip` ファイルを1つ選択して追加します。

#### フォルダを追加

フォルダを選択すると、**そのフォルダ以下にある `.unitypackage` をすべて自動検出して一括追加**します。
サブフォルダの中まで再帰的にスキャンするため、深い階層にあるファイルも漏れなく追加されます。

たとえば以下のようなフォルダ構造で `downloads/` を選択した場合：

```
downloads/
├── avatar_A.unitypackage        ✓ 追加される
├── shaders/
│   ├── shader_B.unitypackage    ✓ 追加される
│   └── shader_C.unitypackage    ✓ 追加される
├── tools/
│   └── tool_D.unitypackage      ✓ 追加される
├── bundle.zip                   ✓ ZIP 内の .unitypackage を展開して追加
└── readme.txt                   （スキップ）
```

`avatar_A`・`shader_B`・`shader_C`・`tool_D` と ZIP 内のパッケージがすべてリストに追加されます。

---

## 設定

ウィンドウ上部から設定できます。設定は PC 全体で共有されます。

| 設定項目 | 説明 |
|---|---|
| インポートダイアログを表示する | ON にするとインポート時に確認ダイアログが出る（デフォルト: OFF） |
| インポート完了時に通知音を再生する | 全件処理完了時に通知音を鳴らす（デフォルト: ON） |
| 音声ファイル（任意） | カスタム音声ファイルを指定（WAV / MP3 / M4A）。空欄の場合は内蔵音を使用 |

---

## インポート状態

| 表示 | 意味 |
|---|---|
| `[待機中]` | インポート待ち |
| `[インポート中...]` | 現在インポート処理中 |
| `[完了 ✓]` | インポート成功 |
| `[失敗 ✗]` | インポート失敗（次のファイルに進む） |
| `[キャンセル]` | ダイアログでキャンセルされた（次のファイルに進む） |

---

## VPM インストーラーのサブエントリ表示

`vpm-package-auto-installer` 形式のパッケージ（FaceEmo インストーラー等）をキューに追加すると、そのパッケージがインストールする VPM パッケージの一覧がサブエントリとして表示されます。

```
☑ FaceEmo-1.x.x-installer    [待機中]              [×]
  └ [VPM] jp.suzuryg.face-emo       [インストール待ち]
  └ [VPM] nadena.dev.modular-avatar [インストール待ち]
```

インポート後、対象 VPM パッケージが `Packages/` フォルダに追加されると自動的に `[インストール済み ✓]` に更新されます。

---

## 注意事項

- DLL を含むパッケージのインポートではドメインリロードが発生しますが、インポート済みエントリのステータスは自動的に復元されます。ドメインリロードにより未処理のエントリは「待機中」に戻るため、残りは再度「すべてインポート」を押して続行してください。
- ZIP から展開した `.unitypackage` はインポート完了後に一時フォルダから自動削除されます。
- 同じファイル・同じ ZIP 内の同じエントリは重複追加されません。

## サポート

X（Twitter）: https://x.com/iori__9973

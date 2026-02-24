using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;

namespace BulkImporter
{
    public class BulkImporterWindow : EditorWindow
    {
        [SerializeField] private PackageQueue _queue = new PackageQueue();
        private Vector2 _scrollPos;

        [MenuItem("Tools/Bulk Importer")]
        public static void ShowWindow()
        {
            var window = GetWindow<BulkImporterWindow>("Bulk Importer");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            _queue.OnUpdate = Repaint;
            RecoverFromDomainReload();
        }

        private void RecoverFromDomainReload()
        {
            // シリアライズされたインデックスで復元
            int idx = _queue.ImportingEntryIndex;
            if (idx >= 0 && idx < _queue.Entries.Count)
            {
                var entry = _queue.Entries[idx];
                if (entry.Status == ImportStatus.Importing)
                {
                    entry.Status = ImportStatus.Done;
                    PackageQueue.DeleteTemp(entry);
                }
                _queue.ImportingEntryIndex = -1;
            }

            // 上記で処理されなかった Importing エントリは待機中に戻す
            foreach (var entry in _queue.Entries)
            {
                if (!entry.IsVpmEntry && entry.Status == ImportStatus.Importing)
                    entry.Status = ImportStatus.Pending;
            }

            // VPM パッケージのインストール状態を更新
            UpdateVpmEntryStatuses();
        }

        private void UpdateVpmEntryStatuses()
        {
            var packagesDir = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Packages"));

            foreach (var entry in _queue.Entries)
            {
                if (!entry.IsVpmEntry || entry.Status == ImportStatus.Done) continue;
                if (Directory.Exists(Path.Combine(packagesDir, entry.VpmPackageId)))
                    entry.Status = ImportStatus.Done;
            }
        }

        private void OnDisable()
        {
            // ドメインリロード前にも呼ばれるため AbortForDomainReload を使用
            // （ImportingEntryIndex を保持したままシリアライズに委ねる）
            if (_queue.IsImporting)
                _queue.AbortForDomainReload();
        }

        private void OnDestroy()
        {
            // ウィンドウが実際に閉じられた場合のみ呼ばれる（ドメインリロードでは呼ばれない）
            if (_queue.IsImporting)
                _queue.Stop();
        }

        private void OnGUI()
        {
            GUILayout.Label("Unitypackage Bulk Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUI.BeginDisabledGroup(_queue.IsImporting);

            // インポートダイアログトグル
            _queue.Interactive = EditorGUILayout.ToggleLeft(
                "インポートダイアログを表示する",
                _queue.Interactive
            );

            EditorGUILayout.Space(4);

            // 通知音設定
            DrawSoundSettings();

            EditorGUILayout.Space(8);

            // ドラッグ＆ドロップエリア
            DrawDropArea();

            EditorGUILayout.Space(8);

            // ファイルリストヘッダー
            DrawListHeader();

            EditorGUI.EndDisabledGroup();

            // スクロールリスト（インポート中も表示）
            DrawFileList();

            EditorGUILayout.Space(8);

            // ボタン行
            DrawBottomButtons();
        }

        private void DrawSoundSettings()
        {
            BulkImporterSettings.SoundEnabled = EditorGUILayout.ToggleLeft(
                "インポート完了時に通知音を再生する",
                BulkImporterSettings.SoundEnabled
            );

            if (BulkImporterSettings.SoundEnabled)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(18);
                string current = BulkImporterSettings.AudioFilePath;
                string newPath = EditorGUILayout.TextField(current, GUILayout.ExpandWidth(true));
                if (newPath != current)
                    BulkImporterSettings.AudioFilePath = newPath;

                if (GUILayout.Button("参照", GUILayout.Width(40)))
                {
                    string selected = EditorUtility.OpenFilePanel(
                        "音声ファイルを選択",
                        string.IsNullOrEmpty(current) ? "" : Path.GetDirectoryName(current),
                        "wav,mp3,m4a"
                    );
                    if (!string.IsNullOrEmpty(selected))
                        BulkImporterSettings.AudioFilePath = selected;
                }

                if (!string.IsNullOrEmpty(BulkImporterSettings.AudioFilePath) &&
                    GUILayout.Button("×", GUILayout.Width(24)))
                {
                    BulkImporterSettings.AudioFilePath = "";
                }

                EditorGUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(BulkImporterSettings.AudioFilePath) &&
                    !File.Exists(BulkImporterSettings.AudioFilePath))
                {
                    EditorGUILayout.HelpBox("ファイルが見つかりません。内蔵音を使用します。", MessageType.Warning);
                }
                else if (string.IsNullOrEmpty(BulkImporterSettings.AudioFilePath))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(18);
                    GUILayout.Label("※ 空欄の場合は内蔵の通知音を使用します",
                        EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void DrawDropArea()
        {
            var dropArea = GUILayoutUtility.GetRect(0, 50, GUILayout.ExpandWidth(true));
            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.gray : Color.black }
            };
            GUI.Box(dropArea, "ここにファイル・フォルダ・ZIP をドラッグ＆ドロップ", style);

            var evt = Event.current;
            if (evt.type == EventType.DragUpdated && dropArea.Contains(evt.mousePosition))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform && dropArea.Contains(evt.mousePosition))
            {
                DragAndDrop.AcceptDrag();
                AddPaths(DragAndDrop.paths);
                evt.Use();
            }
        }

        private void DrawListHeader()
        {
            EditorGUILayout.BeginHorizontal();

            int count = 0;
            foreach (var e in _queue.Entries)
                if (!e.IsVpmEntry) count++;
            GUILayout.Label($"{count} 件", GUILayout.Width(50));
            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(_queue.IsImporting);

            if (GUILayout.Button("ファイルを追加", GUILayout.Width(100)))
                AddFiles();

            if (GUILayout.Button("フォルダを追加", GUILayout.Width(100)))
                AddFolder();

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFileList()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            for (int i = _queue.Entries.Count - 1; i >= 0; i--)
            {
                var entry = _queue.Entries[i];
                if (entry.IsVpmEntry) continue; // VPM エントリは親の直下に描画

                DrawEntryRow(entry, i);

                // VPM サブエントリを親の直下にインデント表示
                for (int j = 0; j < _queue.Entries.Count; j++)
                {
                    var sub = _queue.Entries[j];
                    if (sub.IsVpmEntry && sub.ParentSourceKey == entry.SourceKey)
                        DrawVpmEntryRow(sub);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEntryRow(PackageEntry entry, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            bool isCurrent = entry == _queue.CurrentEntry;
            bool canEdit = !_queue.IsImporting || !isCurrent;

            EditorGUI.BeginDisabledGroup(!canEdit);
            entry.Enabled = EditorGUILayout.Toggle(entry.Enabled, GUILayout.Width(16));
            EditorGUI.EndDisabledGroup();

            // ZIP 由来のエントリにはアイコンを表示
            string label = entry.TempPath != null
                ? $"[ZIP] {entry.DisplayName}"
                : entry.DisplayName;
            GUILayout.Label(label, GUILayout.ExpandWidth(true));

            string statusLabel = GetStatusLabel(entry.Status);
            var statusColor = GetStatusColor(entry.Status);
            var prevColor = GUI.contentColor;
            GUI.contentColor = statusColor;
            GUILayout.Label(statusLabel, GUILayout.Width(120));
            GUI.contentColor = prevColor;

            EditorGUI.BeginDisabledGroup(isCurrent);
            if (GUILayout.Button("×", GUILayout.Width(24)))
            {
                PackageQueue.DeleteTemp(entry);
                _queue.Entries.RemoveAt(index);
                // VPM サブエントリも削除
                _queue.Entries.RemoveAll(e => e.IsVpmEntry && e.ParentSourceKey == entry.SourceKey);
                GUIUtility.ExitGUI();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawVpmEntryRow(PackageEntry entry)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            GUILayout.Space(28); // インデント（チェックボックス分 + 字下げ）
            GUILayout.Label("[VPM]", GUILayout.Width(40));
            GUILayout.Label(entry.DisplayName, GUILayout.ExpandWidth(true));

            string statusLabel = GetVpmStatusLabel(entry.Status);
            var statusColor = GetStatusColor(entry.Status);
            var prevColor = GUI.contentColor;
            GUI.contentColor = statusColor;
            GUILayout.Label(statusLabel, GUILayout.Width(120));
            GUI.contentColor = prevColor;

            // 削除ボタンのスペース分を確保（レイアウト揃え）
            GUILayout.Space(28);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawBottomButtons()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (_queue.IsImporting)
            {
                if (GUILayout.Button("キャンセル", GUILayout.Width(120)))
                    _queue.Stop();
            }
            else
            {
                bool hasImportable = false;
                foreach (var e in _queue.Entries)
                    if (!e.IsVpmEntry) { hasImportable = true; break; }

                EditorGUI.BeginDisabledGroup(!hasImportable);
                if (GUILayout.Button("すべてインポート", GUILayout.Width(120)))
                    StartImport();
                EditorGUI.EndDisabledGroup();
            }

            EditorGUI.BeginDisabledGroup(_queue.IsImporting);
            if (GUILayout.Button("クリア", GUILayout.Width(80)))
            {
                foreach (var entry in _queue.Entries)
                    PackageQueue.DeleteTemp(entry);
                _queue.Entries.Clear();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        private void StartImport()
        {
            foreach (var entry in _queue.Entries)
            {
                if (!entry.Enabled || entry.IsVpmEntry) continue;
                if (entry.Status == ImportStatus.Done) continue; // 完了済みは再インポートしない

                // ZIP 由来のエントリで temp が消えていたら元 ZIP から再展開
                if (entry.SourceKey != null && entry.SourceKey.Contains("::") &&
                    !File.Exists(entry.Path))
                {
                    TryReExtractFromZip(entry);
                }

                entry.Status = ImportStatus.Importing;
            }
            _queue.Start();
        }

        private static void TryReExtractFromZip(PackageEntry entry)
        {
            int sep = entry.SourceKey.IndexOf("::", StringComparison.Ordinal);
            if (sep < 0) return;

            string zipPath   = entry.SourceKey.Substring(0, sep);
            string innerPath = entry.SourceKey.Substring(sep + 2);

            if (!File.Exists(zipPath)) return;

            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var zipEntry = archive.GetEntry(innerPath);
                if (zipEntry == null) return;

                string tempDir  = Path.Combine(Path.GetTempPath(), "BulkImporter");
                Directory.CreateDirectory(tempDir);
                string tempFile = Path.Combine(tempDir,
                    Guid.NewGuid().ToString("N") + "_" + Path.GetFileName(innerPath));

                zipEntry.ExtractToFile(tempFile, overwrite: true);
                entry.Path     = tempFile.Replace('\\', '/');
                entry.TempPath = tempFile;
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[BulkImporter] ZIP 再展開に失敗しました: " +
                    $"{Path.GetFileName(zipPath)} - {e.Message}");
            }
        }

        private void AddFiles()
        {
            string path = EditorUtility.OpenFilePanelWithFilters(
                "ファイルを選択",
                BulkImporterSettings.LastDirectory,
                new[] { "Package files", "unitypackage,zip", "All files", "*" }
            );
            if (string.IsNullOrEmpty(path)) return;

            BulkImporterSettings.LastDirectory = Path.GetDirectoryName(path);
            AddPaths(new[] { path });
        }

        private void AddFolder()
        {
            string folder = EditorUtility.OpenFolderPanel(
                "フォルダを選択",
                BulkImporterSettings.LastDirectory,
                ""
            );
            if (string.IsNullOrEmpty(folder)) return;

            BulkImporterSettings.LastDirectory = folder;
            AddPaths(new[] { folder });
        }

        private void AddPaths(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    AddPaths(Directory.GetFiles(path, "*.unitypackage", SearchOption.AllDirectories));
                    AddPaths(Directory.GetFiles(path, "*.zip", SearchOption.AllDirectories));
                }
                else if (path.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                {
                    AddPackageFile(path);
                }
                else if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    AddZipFile(path);
                }
            }
        }

        private void AddPackageFile(string path)
        {
            string normalized = path.Replace('\\', '/');
            if (_queue.Entries.Exists(e => e.SourceKey == normalized))
                return;

            var entry = new PackageEntry
            {
                Path = normalized,
                SourceKey = normalized,
                DisplayName = Path.GetFileNameWithoutExtension(normalized)
            };
            _queue.Entries.Add(entry);
            AddVpmSubEntries(entry);
        }

        private void AddZipFile(string zipPath)
        {
            string normalizedZip = zipPath.Replace('\\', '/');
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                foreach (var zipEntry in archive.Entries)
                {
                    if (!zipEntry.FullName.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string key = $"{normalizedZip}::{zipEntry.FullName}";
                    if (_queue.Entries.Exists(e => e.SourceKey == key))
                        continue;

                    string tempDir = Path.Combine(Path.GetTempPath(), "BulkImporter");
                    Directory.CreateDirectory(tempDir);
                    string tempFile = Path.Combine(tempDir,
                        Guid.NewGuid().ToString("N") + "_" + zipEntry.Name);
                    zipEntry.ExtractToFile(tempFile, overwrite: true);

                    var entry = new PackageEntry
                    {
                        Path = tempFile.Replace('\\', '/'),
                        SourceKey = key,
                        DisplayName = Path.GetFileNameWithoutExtension(zipEntry.Name),
                        TempPath = tempFile
                    };
                    _queue.Entries.Add(entry);
                    AddVpmSubEntries(entry);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[BulkImporter] ZIP の読み込みに失敗しました: " +
                    $"{Path.GetFileName(zipPath)} - {e.Message}");
            }
        }

        private void AddVpmSubEntries(PackageEntry parent)
        {
            var config = UnityPackageInspector.TryReadVpmConfig(parent.Path);
            if (config == null) return;

            foreach (var kv in config.VpmDependencies)
            {
                string vpmKey = $"vpm::{parent.SourceKey}::{kv.Key}";
                if (_queue.Entries.Exists(e => e.SourceKey == vpmKey)) continue;

                _queue.Entries.Add(new PackageEntry
                {
                    SourceKey      = vpmKey,
                    DisplayName    = kv.Key,
                    IsVpmEntry     = true,
                    VpmPackageId   = kv.Key,
                    ParentSourceKey = parent.SourceKey,
                    Enabled        = false
                });
            }
        }

        private static string GetStatusLabel(ImportStatus status)
        {
            switch (status)
            {
                case ImportStatus.Pending:    return "[待機中]";
                case ImportStatus.Importing:  return "[インポート中...]";
                case ImportStatus.Done:       return "[完了 ✓]";
                case ImportStatus.Failed:     return "[失敗 ✗]";
                case ImportStatus.Cancelled:  return "[キャンセル]";
                default:                      return "";
            }
        }

        private static string GetVpmStatusLabel(ImportStatus status)
        {
            switch (status)
            {
                case ImportStatus.Pending: return "[インストール待ち]";
                case ImportStatus.Done:    return "[インストール済み ✓]";
                default:                   return GetStatusLabel(status);
            }
        }

        private static Color GetStatusColor(ImportStatus status)
        {
            switch (status)
            {
                case ImportStatus.Done:      return new Color(0.3f, 0.8f, 0.3f);
                case ImportStatus.Failed:    return new Color(0.9f, 0.3f, 0.3f);
                case ImportStatus.Importing: return new Color(0.9f, 0.8f, 0.2f);
                case ImportStatus.Cancelled: return new Color(0.6f, 0.6f, 0.6f);
                default:                     return Color.white;
            }
        }
    }
}

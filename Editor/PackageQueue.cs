using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace BulkImporter
{
    internal enum ImportStatus
    {
        Pending,
        Importing,
        Done,
        Failed,
        Cancelled
    }

    [Serializable]
    internal class PackageEntry
    {
        public string Path;         // フルパス（zip 展開時は一時パス）
        public string SourceKey;    // 重複チェック用キー
        public string DisplayName;  // ファイル名（.unitypackage 除く）
        public bool Enabled = true;
        public ImportStatus Status = ImportStatus.Pending;
        public string TempPath;     // zip 展開時の一時ファイルパス（完了後に削除）

        // VPM インストーラーサブエントリ
        public bool IsVpmEntry;        // true = VPM パッケージのサブエントリ
        public string VpmPackageId;    // VPM パッケージ ID（Packages/ チェック用）
        public string ParentSourceKey; // 親インストーラーエントリの SourceKey
    }

    [Serializable]
    internal class PackageQueue
    {
        public List<PackageEntry> Entries = new List<PackageEntry>();
        public bool Interactive;

        // ドメインリロード対策：現在インポート中のエントリインデックスをシリアライズで保持
        public int ImportingEntryIndex = -1;

        [NonSerialized] public Action OnUpdate;
        [NonSerialized] private bool _isImporting;
        [NonSerialized] private PackageEntry _currentEntry;

        public bool IsImporting => _isImporting;
        public PackageEntry CurrentEntry => _currentEntry;

        public void Start()
        {
            AssetDatabase.importPackageCompleted += OnCompleted;
            AssetDatabase.importPackageFailed += OnFailed;
            AssetDatabase.importPackageCancelled += OnCancelled;
            _isImporting = true;
            ImportNext();
        }

        public void Stop()
        {
            AssetDatabase.importPackageCompleted -= OnCompleted;
            AssetDatabase.importPackageFailed -= OnFailed;
            AssetDatabase.importPackageCancelled -= OnCancelled;
            ImportingEntryIndex = -1;
            _isImporting = false;
            _currentEntry = null;
        }

        // ドメインリロード前の OnDisable 専用：
        // イベント解除のみ行い、ImportingEntryIndex は保持してシリアライズに委ねる
        public void AbortForDomainReload()
        {
            AssetDatabase.importPackageCompleted -= OnCompleted;
            AssetDatabase.importPackageFailed -= OnFailed;
            AssetDatabase.importPackageCancelled -= OnCancelled;
            _isImporting = false;
            _currentEntry = null;
        }

        private void ImportNext()
        {
            PackageEntry next = null;
            foreach (var entry in Entries)
            {
                if (entry.Enabled && entry.Status == ImportStatus.Importing)
                {
                    next = entry;
                    break;
                }
            }

            if (next == null)
            {
                Stop();
                NotificationAudioPlayer.Play();
                OnUpdate?.Invoke();
                return;
            }

            _currentEntry = next;
            ImportingEntryIndex = Entries.IndexOf(_currentEntry);
            OnUpdate?.Invoke();

            // ファイルが存在しない場合はエラーを出さずに失敗扱いにして次へ
            if (!File.Exists(_currentEntry.Path))
            {
                _currentEntry.Status = ImportStatus.Failed;
                DeleteTemp(_currentEntry);
                _currentEntry = null;
                ImportingEntryIndex = -1;
                OnUpdate?.Invoke();
                ImportNext();
                return;
            }

            AssetDatabase.ImportPackage(_currentEntry.Path, Interactive);
        }

        private void OnCompleted(string packageName)
        {
            ImportingEntryIndex = -1;
            if (_currentEntry != null)
            {
                _currentEntry.Status = ImportStatus.Done;
                DeleteTemp(_currentEntry);
                _currentEntry = null;
            }
            OnUpdate?.Invoke();
            ImportNext();
        }

        private void OnFailed(string packageName, string errorMessage)
        {
            ImportingEntryIndex = -1;
            if (_currentEntry != null)
            {
                _currentEntry.Status = ImportStatus.Failed;
                DeleteTemp(_currentEntry);
                _currentEntry = null;
            }
            OnUpdate?.Invoke();
            ImportNext();
        }

        private void OnCancelled(string packageName)
        {
            ImportingEntryIndex = -1;
            if (_currentEntry != null)
            {
                _currentEntry.Status = ImportStatus.Cancelled;
                DeleteTemp(_currentEntry);
                _currentEntry = null;
            }
            OnUpdate?.Invoke();
            ImportNext();
        }

        internal static void DeleteTemp(PackageEntry entry)
        {
            if (string.IsNullOrEmpty(entry.TempPath)) return;
            try { File.Delete(entry.TempPath); } catch { }
            entry.TempPath = null;
        }
    }
}

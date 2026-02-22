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
    }

    [Serializable]
    internal class PackageQueue
    {
        // ドメインリロード中にインポート中だったエントリを記録する EditorPrefs キー
        internal const string KeyImportingEntry = "BulkImporter.ImportingEntry";

        public List<PackageEntry> Entries = new List<PackageEntry>();
        public bool Interactive;

        [NonSerialized] public Action OnUpdate;
        [NonSerialized] private bool _isImporting;
        [NonSerialized] private PackageEntry _currentEntry;

        public bool IsImporting => _isImporting;

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
            EditorPrefs.DeleteKey(KeyImportingEntry);
            _isImporting = false;
            _currentEntry = null;
        }

        private void ImportNext()
        {
            PackageEntry next = null;
            foreach (var entry in Entries)
            {
                if (entry.Enabled && entry.Status == ImportStatus.Pending)
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
            _currentEntry.Status = ImportStatus.Importing;
            OnUpdate?.Invoke();

            // ドメインリロード対策：インポート開始時にエントリを記録
            EditorPrefs.SetString(KeyImportingEntry,
                !string.IsNullOrEmpty(_currentEntry.SourceKey)
                    ? _currentEntry.SourceKey
                    : _currentEntry.DisplayName);

            AssetDatabase.ImportPackage(_currentEntry.Path, Interactive);
        }

        private void OnCompleted(string packageName)
        {
            EditorPrefs.DeleteKey(KeyImportingEntry);
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
            EditorPrefs.DeleteKey(KeyImportingEntry);
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
            EditorPrefs.DeleteKey(KeyImportingEntry);
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

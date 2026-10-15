using System;
using System.IO;
using System.Security;
using UnityEngine;

namespace TF.Infrastructure.Saving
{
    /// <summary>
    /// 第3回で実装する、ゲーム内容に依存しないJSON保存処理
    /// </summary>
    public sealed class JsonFileSaveStorage : ISaveStorage
    {
        /// <summary>
        /// 保存ファイルを配置するフォルダー
        /// </summary>
        private readonly string _directoryPath;

        /// <summary>
        /// 保存先フォルダーを受け取る
        /// </summary>
        public JsonFileSaveStorage(string directoryPath)
        {
            _directoryPath = directoryPath;
        }

        /// <summary>
        /// 指定したキーでデータを保存する。配布時は書き込まない。
        /// </summary>
        public bool TrySave<T>(string key, T data) where T : class
        {
            if (data == null || string.IsNullOrWhiteSpace(_directoryPath) || !IsValidKey(key))
            {
                return false;
            }

            // 書き込み途中のファイルを既存データから分離
            string tmpPath = null;

            try
            {
                // 保存するJSON文字列
                string json = JsonUtility.ToJson(data, true);

                Directory.CreateDirectory(_directoryPath);

                // キーから決定する保存先
                string filePath = GetFilePath(key);

                tmpPath = $"{filePath}.{Guid.NewGuid().ToString("N")}.tmp";

                File.WriteAllText(tmpPath, json);

                if (File.Exists(filePath))
                {
                    // 書き込み完了後に既存ファイルを置き換える
                    File.Replace(tmpPath, filePath, null);
                }
                else
                {
                    // 初回保存では一時ファイルを保存先へ移動する
                    File.Move(tmpPath, filePath);
                }

                return true;
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                Debug.LogWarning($"セーブに失敗しました。: {exception.Message}");
                return false;
            }
            finally
            {
                // 失敗して残った一時ファイルを削除
                DeleteTemporaryFile(tmpPath);
            }
        }

        /// <summary>
        /// 保存済みデータを読み込む。配布時は読み込まない。
        /// </summary>
        public bool TryLoad<T>(string key, out T data, out SaveLoadStatus status) where T : class
        {
            data = null;
            status = SaveLoadStatus.Failed;
            
            if (string.IsNullOrWhiteSpace(_directoryPath) || !IsValidKey(key))
            {
                return false;
            }

            try
            {
                string filePath = GetFilePath(key);

                string json = File.ReadAllText(filePath);

                if (string.IsNullOrEmpty(json))
                {
                    return false;
                }

                data = JsonUtility.FromJson<T>(json);
                if (data == null)
                {
                    return false;
                }

                status = SaveLoadStatus.Success;
                return true;
            }
            catch (FileNotFoundException)
            {
                status = SaveLoadStatus.NotFound;
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                status = SaveLoadStatus.NotFound;
                return false;
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                Debug.LogWarning($"セーブの読み込みに失敗しました。: {exception.Message}");
                data = null;
                return false;
            }
        }

        private string GetFilePath(string key)
        {
            // 接頭辞を付け、Windowsの予約ファイル名との衝突を避ける
            return Path.Combine(_directoryPath, $"save_{key}.json");
        }

        private static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 64)
            {
                return false;
            }

            // 英数字・ハイフン・アンダースコアだけ許可
            foreach (char character in key)
            {
                bool isAllowed = (character >= 'a' && character <= 'z') ||
                                 (character >= 'A' && character <= 'Z') ||
                                 (character >= '0' && character <= '9') ||
                                 character == '-' || character == '_';
                if (!isAllowed)
                {
                    return false;
                }
            }
            
            return true;
        }

        private static bool IsStorageException(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is SecurityException ||
                   exception is ArgumentException ||
                   exception is NotSupportedException;
        }

        private static void DeleteTemporaryFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                // ファイルが存在しない場合は何もしない
                File.Delete(path);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                Debug.LogWarning($"一時ファイルを削除できませんでした。: {exception.Message}");
            }
        }
    }
}

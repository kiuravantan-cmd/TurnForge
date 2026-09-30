using System;
using System.IO;
using System.Security;
using UnityEngine;

namespace TF.Infrastructure.Saving
{
    /// <summary>
    /// データをJSONファイルとして保存・読み込みする
    /// </summary>
    public sealed class JsonFileSaveStorage : ISaveStorage
    {
        /// <summary>
        /// 保存ファイルを配置するフォルダ
        /// </summary>
        private readonly string _directoryPath;

        /// <summary>
        /// 保存先のフォルダを受け取る
        /// </summary>
        /// <param name="directoryPath">保存先のフォルダパス</param>
        public JsonFileSaveStorage (string directoryPath)
        {
            _directoryPath = directoryPath;
        }

        /// <summary>
        /// 一時ファイルへの書き込み後、保存ファイルへ反映する
        /// </summary>
        public bool TrySave<T>(string key, T data) where T : class
        {
            if (data == null || string.IsNullOrWhiteSpace(_directoryPath) || !IsValidKey(key))
            {
                return false;
            }

            // 書き込み途中のファイルを既存データから分離する
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
                Debug.LogWarning($"セーブに失敗しました：{exception.Message}");
                return false;
            }
            finally
            {
                // 失敗して残った一時ファイルを削除
                DeleteTemporaryFile(tmpPath);
            }
        }

        /// <summary>
        /// 保存済みJSONから指定した型のデータを読み込み
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
                // キーに対応する保存先
                string filePath = GetFilePath(key);

                // ファイルに保存されたJSON文字列
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
                // 初回起動など、保存ファイルが存在しない
                status = SaveLoadStatus.NotFound;
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                // 初回保存前で、保存先フォルダーが存在しない
                status = SaveLoadStatus.NotFound;
                return false;
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                Debug.LogWarning($"セーブの読み込みに失敗しました：{exception.Message}");
                data = null;
                return false;
            }
        }

        /// <summary>
        /// 保存キーからファイルパスを作成
        /// </summary>
        private string GetFilePath(string key)
        {
            // 接頭辞を付け、Windowsの予約ファイル名との衝突を避ける
            return Path.Combine(_directoryPath, $"save_{key}.json");
        }

        /// <summary>
        /// 保存キーがファイル名として使用可能か確認する
        /// </summary>
        private static bool IsValidKey(string key)
        {
            if (string.IsNullOrEmpty(key) || key.Length > 64)
            {
                return false;
            }

            // 英数字・ハイフン・アンダースコアだけを許可する
            foreach (char character in key)
            {
                bool isAllowed =
                   (character >= 'a' && character <= 'z')
                   || (character >= 'A' && character <= 'Z')
                   || (character >= '0' && character <= '9')
                   || character == '-'
                   || character == '_';

                if (!isAllowed)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 保存処理の失敗として扱う例外か確認する
        /// </summary>
        private static bool IsStorageException(Exception exception)
        {
            return exception is IOException ||
                   exception is UnauthorizedAccessException ||
                   exception is SecurityException ||
                   exception is ArgumentException ||
                   exception is NotSupportedException;
        }

        /// <summary>
        /// 残っている一時ファイルを削除
        /// </summary>
        private static void DeleteTemporaryFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                // ファイル存在しない場合は何もしない
                File.Delete(path);
            }
            catch (Exception exception) when (IsStorageException(exception))
            {
                Debug.LogWarning($"一時ファイルを削除できませんでした : {exception.Message}");
            }
        }
    }
}

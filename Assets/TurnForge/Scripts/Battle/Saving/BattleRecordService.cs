using System;
using System.Collections.Generic;
using System.Text;
using TF.Battle.Models;
using TF.Infrastructure.Saving;

namespace TF.Battle.Saving
{
    /// <summary>
    /// 戦績の読み込み・加算・保存を管理する
    /// </summary>
    public sealed class BattleRecordService
    {
        /// <summary>
        /// 戦績データを識別する保存キー
        /// </summary>
        private const string SaveKey = "battle_record";

        /// <summary>
        /// 保存先への読み書きを担当する処理
        /// </summary>
        private readonly ISaveStorage _storage;

        /// <summary>
        /// 現在保持している戦績
        /// </summary>
        private BattleRecordSaveData _currentData = new BattleRecordSaveData(0, 0, 0);

        /// <summary>
        /// 直前に加算した戦闘結果。同じ結果の連続加算を防ぐ
        /// </summary>
        private BattleState _lastRecordedState;

        /// <summary>
        /// 戦績の読み込み、または初回用の初期化が完了したか
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 現在保持している戦績
        /// </summary>
        public BattleRecordSaveData CurrentData => _currentData;

        /// <summary>
        /// 保存されていない戦績の変更があるか
        /// </summary>
        public bool HasUnsavedChanges { get; private set; }

        /// <summary>
        /// 保存処理を受け取る
        /// </summary>
        public BattleRecordService(ISaveStorage storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// 保存済みの戦績を読み込み、有効な場合だけ現在値へ反映する
        /// </summary>
        public bool TryLoad ()
        {
            // 未保存の戦績を読み込みで失わないようにする
            if (_storage == null || HasUnsavedChanges)
            {
                return false;
            }

            // 初期化済みの戦績を再読み込みで巻き戻さない。
            if (IsInitialized)
            {
                return true;
            }

            if (!_storage.TryLoad<BattleRecordSaveData>(SaveKey, out var loadedData, out var status))
            {
                if (status != SaveLoadStatus.NotFound)
                {
                    return false;
                }

                // 初回起動では戦績0から開始する。
                _currentData = new BattleRecordSaveData(0, 0, 0);
                IsInitialized = true;
                return true;
            }

            if (!IsValidData(loadedData))
            {
                return false;
            }

            _currentData = loadedData;
            IsInitialized = true;
            return true;
        }

        /// <summary>
        /// 終了した戦闘の結果を、プレイヤー視点で戦績へ加算
        /// </summary>
        /// <param name="state">終了時の戦闘状態</param>
        /// <param name="playerSide">プレイヤーの陣営</param>
        public bool TryRecordResult(BattleState state, BattleSide playerSide)
        {
            // 読み込みが完了するまで戦績を変更しない。
            if (!IsInitialized)
            {
                return false;
            }

            if (state == null
                || !state.IsFinished
                || state.FirstCombatant == null
                || state.SecondCombatant == null
                || ReferenceEquals(state, _lastRecordedState))
            {
                return false;
            }

            if (playerSide != BattleSide.First
                && playerSide != BattleSide.Second)
            {
                return false;
            }

            // プレイヤーと相手の終了時の状態
            CombatantState player = playerSide == BattleSide.First
                ? state.FirstCombatant
                : state.SecondCombatant;

            CombatantState opponent = playerSide == BattleSide.First
                ? state.SecondCombatant
                : state.FirstCombatant;

            // 現在のルールでは戦闘不能によって決着する
            if (!player.IsDefeated && !opponent.IsDefeated)
            {
                return false;
            }

            // 加算後の値を一時的に保持する
            int winCount = _currentData.WinCount;
            int lossCount = _currentData.LossCount;
            int drawCount = _currentData.DrawCount;

            if (player.IsDefeated && opponent.IsDefeated)
            {
                if (drawCount == int.MaxValue)
                {
                    return false;
                }

                drawCount++;
            }
            else if (player.IsDefeated)
            {
                if (lossCount == int.MaxValue)
                {
                    return false;
                }

                lossCount++;
            }
            else
            {
                if (winCount == int.MaxValue)
                {
                    return false;
                }

                winCount++;
            }

            // 新しいデータへ置き換え、未保存として保持する
            _currentData = new BattleRecordSaveData(winCount, lossCount, drawCount);
            _lastRecordedState = state;
            HasUnsavedChanges = true;
            return true;
        }

        /// <summary>
        /// 現在の戦績を保存し、成功した場合だけ未保存状態を解除する
        /// </summary>
        public bool TrySave()
        {
            if (_storage == null || !IsInitialized || !IsValidData(_currentData))
            {
                return false;
            }

            // 変更がなければ書き込みは不要。
            if (!HasUnsavedChanges)
            {
                return true;
            }

            if (!_storage.TrySave(SaveKey, _currentData))
            {
                return false;
            }

            HasUnsavedChanges = false;
            return true;
        }

        /// <summary>
        /// 対応する保存形式で、戦績の値が有効か確認
        /// </summary>
        private static bool IsValidData(BattleRecordSaveData data)
        {
            return data != null
                && data.Version == BattleRecordSaveData.CurrentVersion
                && data.WinCount >= 0
                && data.LossCount >= 0
                && data.DrawCount >= 0;
        }
    }
}

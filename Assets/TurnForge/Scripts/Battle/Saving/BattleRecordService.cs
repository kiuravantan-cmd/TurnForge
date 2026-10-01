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
            // TODO LESSON03-03: 未保存の場合だけ初期値を採用し、読み込み成功を検証する。
            // 未保存の変更を読み込みで失わないこと。成功時のみIsInitializedを更新する。
            return false;
        }

        /// <summary>
        /// 終了した戦闘の結果を、プレイヤー視点で戦績へ加算
        /// </summary>
        /// <param name="state">終了時の戦闘状態</param>
        /// <param name="playerSide">プレイヤーの陣営</param>
        public bool TryRecordResult(BattleState state, BattleSide playerSide)
        {
            // TODO LESSON03-04: 初期化・終了状態・陣営・重複・桁あふれを検証する。
            // プレイヤー視点で戦績を加算し、HasUnsavedChangesを立てる。
            return false;
        }

        /// <summary>
        /// 現在の戦績を保存し、成功した場合だけ未保存状態を解除する
        /// </summary>
        public bool TrySave()
        {
            // TODO LESSON03-05: 有効な初期化済みデータだけ保存する。
            // 保存成功時のみHasUnsavedChangesを解除し、失敗時は保持する。
            return false;
        }

        /// <summary>
        /// 対応する保存形式で、戦績の値が有効か確認
        /// </summary>
        private static bool IsValidData(BattleRecordSaveData data)
        {
            // TODO LESSON03-06: null・保存形式の版番号・負の戦績を検証する。
            return false;
        }
    }
}

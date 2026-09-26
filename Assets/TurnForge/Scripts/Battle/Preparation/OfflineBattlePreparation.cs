using System.Threading;
using Cysharp.Threading.Tasks;

namespace TF.Battle.Preparation
{
    /// <summary>
    /// オフライン戦闘の開始準備を行う
    /// </summary>
    public sealed class OfflineBattlePreparation : IBattlePreparation
    {
        /// <summary>
        /// ローディング画面の表示更新を挟み、開始可能か確認
        /// </summary>
        public async UniTask<bool> PrepareAsync(CancellationToken cts)
        {
            if (cts.IsCancellationRequested)
            {
                return false;
            }
            
            // 同フレーム内で戦闘画面まで切り替わることを避ける
            await UniTask.NextFrame();

            // 待機中に中断された場合は、準備失敗として返す
            if (cts.IsCancellationRequested)
            {
                return false;
            }
            
            // 戦闘用アセットを用意した段階で、読み込みをここへ追加する

            return true;
        }

        /// <summary>
        /// この準備処理が確保したリソースを解放
        /// </summary>
        public void Release()
        {
            // 現段階ではリソースを確保していないため、解放対象はない
        }
    }
}
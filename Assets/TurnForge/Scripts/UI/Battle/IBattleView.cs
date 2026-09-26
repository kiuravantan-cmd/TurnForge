using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TF.Battle.Commands;
using TF.Battle.Models;

namespace TF.UI.Battle
{
    /// <summary>
    /// 戦闘画面の操作通知・表示・演出を定義
    ///
    /// 【操作と表示の流れ】
    /// パッド・キーボード・クリック
    ///    ↓
    /// CommandSelected → Presenterが選択を保持
    ///    ↓
    /// ConfirmRequested → FlowControllerで実行
    ///    ↓
    /// PlayResultAsync → 演出完了を待つ
    ///    ↓
    /// Render → 確定状態を表示
    ///    ↓ 
    /// TryCompleteEffects → 次の受付または終了
    /// </summary>
    public interface IBattleView
    {
        /// <summary>
        /// コマンドが選択された時に通知
        /// </summary>
        event Action<BattleCommand> CommandSelected;

        /// <summary>
        /// 選択したコマンドの実行が要求されたときに通知
        /// </summary>
        event Action ConfirmRequested;
        
        /// <summary>
        /// コマンド選択の取り消しが要求されたときに通知
        /// </summary>
        event Action CancelRequested;

        /// <summary>
        /// HP・エネルギー・手番などを指定された状態で表示
        /// </summary>
        void Render(BattleState state);
        
        /// <summary>
        /// 戦闘操作全体の受付を切り替える
        /// </summary>
        void SetInputEnabled(bool enabled);

        /// <summary>
        /// 選択中のコマンドを表示
        /// nullの場合は選択を解除
        /// </summary>
        void SetSelectedCommand(BattleCommand? command);
        
        /// <summary>
        /// 確定済みの行動結果を演出し、完了まで待機
        /// </summary>
        UniTask PlayResultAsync(BattleResult result, CancellationTokenSource cts);
    }
}
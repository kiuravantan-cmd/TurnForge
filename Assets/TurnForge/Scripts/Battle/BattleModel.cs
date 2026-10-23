using System;
using R3;
using TF.Battle.Commands;
using TF.Battle.Models;
using TF.Battle.Rules;
using UnityEngine.Events;

namespace TF.Battle
{
    /// <summary>
    /// 現在の戦闘状態を保存し、変更を通知する
    /// </summary>
    public sealed class BattleModel : IDisposable
    {
        /// <summary>
        /// 行動の確認と状態の計算を担当するルール
        /// </summary>
        private readonly BattleRules _rules;
        
        /// <summary>
        /// 現在の戦闘状態とその変更通知を待つ
        /// </summary>
        private readonly ReactiveProperty<BattleState> _currentState;
        
        /// <summary>
        /// このModelが終了したか
        /// </summary>
        private bool _isDisposed = false;
        
        /// <summary>
        /// 現在の戦闘状態（プロパティ）
        /// </summary>
        public BattleState CurrentState => _currentState.Value;

        /// <summary>
        /// 状態の通知元
        /// </summary>
        public ReadOnlyReactiveProperty<BattleState> StateChanged => _currentState;
        
        /// <summary>
        /// 戦闘ルールと初期状態を受け取る
        /// </summary>
        public BattleModel(BattleRules rules, BattleState initialState)
        {
            _rules = rules;
            _currentState = new ReactiveProperty<BattleState>(initialState);
        }

        /// <summary>
        /// 現在の戦闘状態で、指定したコマンドの実行条件を満たすか
        /// </summary>
        /// <param name="actor">行動する陣営</param>
        /// <param name="command">確認するコマンド</param>
        public bool CanExecute(BattleSide actor, BattleCommand command)
        {
            if (_isDisposed || _rules == null || CurrentState == null)
            {
                return false;
            }

            // 現在のターン番号を使って確認用の指示を作成
            var request = new BattleActionRequest(actor, CurrentState.TurnNumber, command);

            return _rules.CanExecute(CurrentState, request);
        }

        /// <summary>
        /// 成立した行動の結果を採用し、更新後に通知する
        /// </summary>
        public bool TryExecute(BattleActionRequest request, out BattleResult result)
        {
            result = null;

            if (_rules == null || _currentState == null || CurrentState == null)
            {
                return false;
            }

            if (!_rules.TryExecute(CurrentState, request, out result))
            {
                return false;
            }

            // Valueの置き換えで、今の状態の更新と通知を行う
            _currentState.Value = result.NextState;
            
            return true;
        }

        /// <summary>
        /// このモデルの状態通知を終了する
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            
            _isDisposed = true;
            _currentState.Dispose();
        }
    }
}

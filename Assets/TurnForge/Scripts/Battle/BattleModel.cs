using TF.Battle.Commands;
using TF.Battle.Models;
using TF.Battle.Rules;
using UnityEngine.Events;

namespace TF.Battle
{
    /// <summary>
    /// 現在の戦闘状態を保持し、行動結果を適用
    /// </summary>
    public sealed class BattleModel
    {
        /// <summary>
        /// 行動の検証と状態の計算を担当するルール
        /// </summary>
        private readonly BattleRules _rules;
        
        /// <summary>
        /// 現在の戦闘状態
        /// </summary>
        private BattleState  _currentState;
        
        /// <summary>
        /// 現在の戦闘状態（プロパティ）
        /// </summary>
        public BattleState CurrentState => _currentState;

        /// <summary>
        /// 有効な行動の結果を適用した後に通知
        /// </summary>
        public event UnityAction<BattleResult> ActionResolved;
        
        /// <summary>
        /// 戦闘ルールと初期状態を受け取る
        /// </summary>
        public BattleModel(BattleRules rules, BattleState initialState)
        {
            _rules = rules;
            _currentState = initialState;
        }

        /// <summary>
        /// 現在の戦闘状態で、指定したコマンドの実行条件を満たすか
        /// </summary>
        /// <param name="actor">行動する陣営</param>
        /// <param name="command">確認するコマンド</param>
        public bool CanExecute(BattleSide actor, BattleCommand command)
        {
            if (_rules == null || _currentState == null)
            {
                return false;
            }

            // 現在のターン番号を使って確認用の要求を作成
            var request = new BattleActionRequest(actor, _currentState.TurnNumber, command);

            return _rules.CanExecute(_currentState, request);
        }

        public bool TryExecute(BattleActionRequest request, out BattleResult result)
        {
            result = null;

            if (_rules == null || _currentState == null)
            {
                return false;
            }

            if (!_rules.TryExecute(_currentState, request, out result))
            {
                return false;
            }

            // 通知先が最新の状態を参照できるように先に更新
            _currentState = result.NextState;
            
            ActionResolved?.Invoke(result);
            return true;
        }
    }
}
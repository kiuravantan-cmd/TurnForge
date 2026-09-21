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
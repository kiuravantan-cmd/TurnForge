using TF.Battle.Models;

namespace TF.Battle.Commands
{
    /// <summary>
    /// 対戦者が実行を要求した行動
    /// </summary>
    public sealed class BattleActionRequest
    {
        /// <summary>
        /// 行動を要求する対戦者
        /// </summary>
        public BattleSide Actor { get; init; }
        
        /// <summary>
        /// 行動を選択した時点のターン番号
        /// </summary>
        public int TurnNumber { get; init; }
        
        /// <summary>
        /// 実行を要求する行動
        /// </summary>
        public BattleCommand Command { get; init; }

        /// <summary>
        /// 対戦者・ターン番号・行動を指定して要求を作成
        /// </summary>
        public BattleActionRequest(BattleSide actor, int turnNumber, BattleCommand command)
        {
            Actor = actor;
            TurnNumber = turnNumber;
            Command = command;
        }
    }

}
using TF.Battle.Commands;
using TF.Battle.Models;

namespace TF.Battle.AI
{
    /// <summary>
    /// 自分の手番で通常攻撃を選択
    /// </summary>
    public sealed class AttackOnlyCommandSelector : IBattleCommandSelector
    {
        /// <summary>
        /// 戦闘が継続中で、自分の手番なら攻撃を選択
        /// </summary>
        public bool TrySelectCommand(BattleState state, BattleSide actor, out BattleCommand command)
        {
            command = default;

            if (state == null || state.IsFinished)
            {
                return false;
            }

            if (actor != BattleSide.First && actor != BattleSide.Second)
            {
                return false;
            }

            if (state.ActionSide != actor)
            {
                return false;
            }

            command = BattleCommand.Attack;
            return true;
        }
    }
}
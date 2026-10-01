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
            // TODO LESSON01-05: 継続中かつ指定した側の手番なら通常攻撃を選ぶ。
            // TODO LESSON05-01: このインターフェースを使う別のCPU判断を追加し、DIで差し替える。
            return false;
        }
    }
}

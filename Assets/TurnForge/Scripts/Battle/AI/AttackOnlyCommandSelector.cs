using TF.Battle.Commands;
using TF.Battle.Models;

namespace TF.Battle.AI
{
    /// <summary>
    /// 自分の番で通常攻撃を選択
    /// </summary>
    public sealed class AttackOnlyCommandSelector : IBattleCommandSelector
    {
        /// <summary>
        /// 継続中の自分の番なら、通常攻撃を選ぶ
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

            // TODO LESSON05-01: このインターフェースを使う別のCPU判断を追加し、DIで差し替える。
            // 第5回・1/4コマ目: IBattleCommandSelectorを実装した別クラスを追加する。
            // CPUは技だけを選び、Rulesを変更せず登録した実装の差し替えで判断を変える。
            return false;
        }
    }
}

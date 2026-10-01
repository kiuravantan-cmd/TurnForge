using System;
using System.Collections.Generic;
using TF.Battle.Commands;
using TF.Battle.Models;
using TF.MasterData;

namespace TF.Battle.Rules
{
    /// <summary>
    /// 行動を確認し、次の戦闘状態を計算
    /// </summary>
    public sealed class BattleRules
    {
        /// <summary>
        /// コマンドごとのマスタデータ
        /// </summary>
        private readonly Dictionary<BattleCommand, BattleCommandDataRecord> _commandData =
            new  Dictionary<BattleCommand, BattleCommandDataRecord>();

        /// <summary>
        /// 戦闘に必要なマスタがそろっているか
        /// </summary>
        public bool IsConfigured { get; private set; }

        /// <summary>
        /// 読み込み済みのコマンドマスタを受け取る
        /// </summary>
        public BattleRules(IEnumerable<BattleCommandDataRecord> records)
        {
            if (records == null)
            {
                return;
            }

            // 各レコードをコマンド別に登録する
            foreach (var record in records)
            {
                if (record == null
                    || !Enum.IsDefined(typeof(BattleCommand), record.Command)
                    || record.Damage < 0
                    || record.EnergyCost < 0
                    || record.EnergyGain < 0
                    || record.GuardDamageDivisor < 1)
                {
                    return;
                }
                
                BattleCommand command = (BattleCommand)record.Command;
                if (_commandData.ContainsKey(command) || (command == BattleCommand.Charge && record.EnergyGain == 0))
                {
                    return;
                }
                
                _commandData.Add(command, record);
            }

            // 必要なコマンドの登録漏れを確認する
            foreach (BattleCommand command in Enum.GetValues(typeof(BattleCommand)))
            {
                if (!_commandData.ContainsKey(command))
                {
                    return;
                }
            }
            
            IsConfigured = true;
        }

        /// <summary>
        /// 状態を変更せず、行動の指示が実行条件を満たすか
        /// </summary>
        /// <param name="state">判定対象の戦闘状態</param>
        /// <param name="request">確認する行動の指示</param>
        public bool CanExecute(BattleState state, BattleActionRequest request)
        {
            return CanExecute(state, request, out _);
        }
        
        /// <summary>
        /// 行動を処理
        /// 無効な場合はfalseを返し、結果を作成しない
        /// </summary>
        public bool TryExecute(BattleState currentState, BattleActionRequest request, out BattleResult result)
        {
            result = null;
            // TODO LESSON01-03: CanExecuteで指示を確認し、行動する側と相手を取得する。
            // 通常攻撃のマスタ値とApplyDamageを使って、攻撃後の状態を作る。
            // HP0なら終了。バトルが続くときだけ行動する番を交代し、ターン番号を1進める。
            // First/Secondの順でBattleStateを作り、前後の状態をBattleResultへ渡す。
            // TODO LESSON06-01: 防御・チャージ・必殺技、コストとエネルギー上限を追加する。
            // 防御は次の自分の番開始時に解除する。計算値の桁あふれにも対応する。
            return false;
        }

        /// <summary>
        /// 状態と指示を確認し、行動を実行できるか判定
        /// </summary>
        private bool CanExecute(
            BattleState state,
            BattleActionRequest request,
            out BattleCommandDataRecord commandData)
        {
            commandData = null;
            // TODO LESSON01-02: 未設定・終了済み・HPなどの値がルールに合わないキャラクター・行動する側が違う場合を受け付けない。
            // _commandDataから指示されたコマンドのマスタを取得する。
            // 第1回はAttackだけ許可する。他の技は第6回まで受け付けない。
            // TODO LESSON06-02: 技のコスト不足・チャージ上限を確認する。
            // TODO LESSON08-01: オンラインでもホスト側でこの判定を通す。
            // 現在のターン番号と指示の番号を照合し、古い指示・二重指示を受け付けない。
            return false;
        }

        /// <summary>
        /// 戦闘に使えるキャラクターの状態か判定
        /// </summary>
        private static bool IsValidCombatant(CombatantState combatant, BattleSide expectedSide)
        {
            return combatant != null
                   && combatant.Side == expectedSide
                   && combatant.MaxHp > 0
                   && combatant.Hp > 0
                   && combatant.Hp <= combatant.MaxHp
                   && combatant.MaxEnergy >= 0
                   && combatant.Energy >= 0
                   && combatant.Energy <= combatant.MaxEnergy;
        }

        /// <summary>
        /// 識別子に対応するキャラクターを取得
        /// </summary>
        private static CombatantState GetCombatant(BattleState state, BattleSide side)
        {
            return side == BattleSide.First ? state.FirstCombatant : state.SecondCombatant;
        }

        /// <summary>
        /// 相手側を取得
        /// </summary>
        private static BattleSide GetOpponentSide(BattleSide side)
        {
            return side == BattleSide.First ? BattleSide.Second : BattleSide.First;
        }

        /// <summary>
        /// 防御による軽減を適用し、ダメージを受けた後の状態を作成
        /// </summary>
        private static CombatantState ApplyDamage(CombatantState target, int damage, int guardDamageDivisor)
        {
            // TODO LESSON01-01B: マスタのdamageをHPから引き、下限を0にする。
            // CopyCombatantでHP以外を引き継いだ新しい状態を返す。
            // TODO LESSON06-03: 防御中はguardDamageDivisorで整数除算してから適用する。
            return target;
        }

        /// <summary>
        /// キャラクターを区別する値と最大値を引き継ぎ、指定された値で状態を作成
        /// </summary>
        private static CombatantState CopyCombatant(CombatantState source, int hp, int energy, bool isGuarding)
        {
            // TODO LESSON01-01A: sourceのSide・MaxHp・MaxEnergyを引き継ぐ。
            // hp・energy・isGuardingは引数の値を使い、新しいCombatantStateを返す。
            return source;
        }
    }
}

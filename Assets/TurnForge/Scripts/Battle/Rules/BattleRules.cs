using System;
using System.Collections.Generic;
using TF.Battle.Commands;
using TF.Battle.Models;
using TF.MasterData;
using UnityEngine;

namespace TF.Battle.Rules
{
    /// <summary>
    /// 行動を検証し、次の戦闘状態を計算
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
        /// 行動を処理
        /// 無効な場合はfalseを返し、結果を生成しない
        /// </summary>
        public bool TryExecute(BattleState currentState, BattleActionRequest request, out BattleResult result)
        {
            result = null;

            if (!CanExecute(currentState, request, out var commandData))
            {
                return false;
            }
            
            // 行動する側の状態
            CombatantState actor = GetCombatant(currentState, request.Actor);

            // 行動を受ける側の状態
            CombatantState target = GetCombatant(currentState, GetOpponentSide(request.Actor));
            
            // 消費後に回復を適用する。加算時のオーバーフローを防ぐためlongで計算
            long calculatedEnergy = (long)actor.Energy - commandData.EnergyCost + commandData.EnergyGain;

            // 最大エネルギーを超えないよう補正
            int nextEnergy = (int)Mathf.Min((long)actor.MaxEnergy, calculatedEnergy);
            
            // 行動後の行動者の状態
            CombatantState nextActor = CopyCombatant(
                actor,
                actor.Hp,
                nextEnergy,
                request.Command == BattleCommand.Guard);
            
            // 行動後の相手の状態
            CombatantState nextTarget = target;
            
            // 防御中の被ダメージに使用する除数
            int guardDamageDivisor = _commandData[BattleCommand.Guard].GuardDamageDivisor;

            switch (request.Command)
            {
                case BattleCommand.Attack:
                case BattleCommand.Special:
                    nextTarget = ApplyDamage(target, commandData.Damage, guardDamageDivisor);
                    break;
                
                case BattleCommand.Guard:
                case BattleCommand.Charge:
                    // 防御とエネルギーの変更はnextActorの生成時に適用済み
                    break;
                
                default:
                    Debug.LogWarning($"指定されていないコマンドです。{request.Command}");
                    return false;
            }

            // 今回の行動で勝敗が決まったか
            bool isFinished = nextTarget.IsDefeated;

            // 決着時は現在の手番とターン番号を維持
            BattleSide nextActionSide = currentState.ActionSide;
            int nextTurnNumber = currentState.TurnNumber;

            if (!isFinished)
            {
                // ターン番号の加算によるオーバーフローを防ぐ
                if (nextTurnNumber == int.MaxValue)
                {
                    Debug.LogWarning("ターン番号を加算するとオーバーフローになります。");
                    return false;
                }

                nextActionSide = target.Side;
                nextTurnNumber++;
                
                // 次の行動者はターン開始時に防御が解除
                nextTarget = CopyCombatant(nextTarget, nextTarget.Hp, nextTarget.Energy, false);
            }
            
            // 参加者の並びをFirst、Secondに戻した次の戦闘状態
            CombatantState nextFirst = request.Actor == BattleSide.First ? nextActor : nextTarget;
            CombatantState nextSecond = request.Actor == BattleSide.Second ? nextActor : nextTarget;
            BattleState nextState = new BattleState(nextFirst, nextSecond, nextActionSide, nextTurnNumber, isFinished);

            result = new BattleResult(request, currentState, nextState);
            return true;
        }

        /// <summary>
        /// 状態と要求を検証し、行動を実行できるか判定
        /// </summary>
        private bool CanExecute(
            BattleState state,
            BattleActionRequest request,
            out BattleCommandDataRecord commandData)
        {
            commandData = null;
            
            if (!IsConfigured || state == null || request == null)
            {
                return false;
            }

            if (state.IsFinished || state.TurnNumber < 1)
            {
                return false;
            }

            if (!IsValidCombatant(state.FirstCombatant, BattleSide.First) ||
                !IsValidCombatant(state.SecondCombatant, BattleSide.Second))
            {
                return false;
            }

            if (state.ActionSide != BattleSide.First && state.ActionSide != BattleSide.Second)
            {
                return false;
            }

            if (request.Actor != state.ActionSide ||
                request.TurnNumber != state.TurnNumber)
            {
                return false;
            }

            if (!_commandData.TryGetValue(request.Command, out commandData))
            {
                return false;
            }
            
            // 現在の手番
            CombatantState actor = GetCombatant(state, request.Actor);

            // 自分のターン開始時には防御が解除されている必要がある
            if (actor.IsGuarding || actor.Energy < commandData.EnergyCost)
            {
                return false;
            }
            
            // チャージはエネルギーが最大の場合に使用できない
            return request.Command != BattleCommand.Charge || actor.Energy < actor.MaxEnergy;
        }

        /// <summary>
        /// 戦闘に使える参加者の状態か判定
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
        /// 識別子に対応する参加者を取得
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
        /// 防御による軽減を適用し、被ダメージ後の状態を生成
        /// </summary>
        private static CombatantState ApplyDamage(CombatantState target, int damage, int guardDamageDivisor)
        {
            // 防御中は半減し、整数除算で端数を切り捨てる
            int actualDamage = target.IsGuarding ? damage / guardDamageDivisor : damage;
            
            // HPが0未満にならないよう補正
            int nextHp = Mathf.Max(0, target.Hp - actualDamage);

            return CopyCombatant(target, nextHp, target.Energy, target.IsGuarding);
        }

        /// <summary>
        /// 識別子と最大値を引き継ぎ、指定された値で状態を生成
        /// </summary>
        private static CombatantState CopyCombatant(CombatantState source, int hp, int energy, bool isGuarding)
        {
            return new CombatantState(source.Side, hp, source.MaxHp, energy, source.MaxEnergy, isGuarding);
        }
    }
}
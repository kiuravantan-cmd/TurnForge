using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TF.Battle.Commands;
using TF.Battle.Models;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TF.UI.Battle
{
    /// <summary>
    /// 戦闘状態の表示と、コマンド操作の通知を担当
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleView : MonoBehaviour, IBattleView
    {
        /// <summary>
        /// 1人目のHP・エネルギー表示
        /// </summary>
        [SerializeField] private TextMeshProUGUI _firstStatus;

        /// <summary>
        /// 2人目のHP・エネルギー表示
        /// </summary>
        [SerializeField] private TextMeshProUGUI _secondStatus;

        /// <summary>
        /// 手番表示
        /// </summary>
        [SerializeField] private TextMeshProUGUI _turnText;

        /// <summary>
        /// 選択中のコマンド表示
        /// </summary>
        [SerializeField] private TextMeshProUGUI _selectionText;

        /// <summary>
        /// 行動結果表示
        /// </summary>
        [SerializeField] private TextMeshProUGUI _resultText;

        /// <summary>
        /// 攻撃コマンドボタン
        /// </summary>
        [SerializeField] private Button _attackButton;

        /// <summary>
        /// 防御コマンドボタン
        /// </summary>
        [SerializeField] private Button _guardButton;
        
        /// <summary>
        /// チャージボタン
        /// </summary>
        [SerializeField] private Button _chargeButton;

        /// <summary>
        /// 必殺技ボタン
        /// </summary>
        [SerializeField] private Button _specialButton;

        /// <summary>
        /// 選択したコマンドを実行するボタン
        /// </summary>
        [SerializeField] private Button _confirmButton;

        /// <summary>
        /// 選択中のコマンドを取り消すボタン
        /// </summary>
        [SerializeField] private Button _cancelButton;

        /// <summary>
        /// Presenterから指定された入力受付状態
        /// </summary>
        private bool _isInputEnabled = false;

        /// <summary>
        /// 選択中のコマンド。未選択の場合はnull
        /// </summary>
        private BattleCommand? _selectedCommand;

        /// <summary>
        /// コマンドが選択されたときに通知
        /// </summary>
        public event UnityAction<BattleCommand> CommandSelected;

        /// <summary>
        /// 決定操作を通知
        /// </summary>
        public event UnityAction ConfirmRequested;

        /// <summary>
        /// 選択中のコマンドの取り消し操作を通知
        /// </summary>
        public event UnityAction CancelRequested;

        /// <summary>
        /// ボタン操作の購読を開始
        /// </summary>
        private void OnEnable ()
        {
            SetButtonListeners(true);
            RefreshButtons();
        }

        /// <summary>
        /// ボタン操作の購読を解除
        /// </summary>
        private void OnDisable()
        {
            SetButtonListeners(false);
        }

        /// <summary>
        /// HP・エネルギー・手番を表示
        /// </summary>
        public void Render(BattleState state)
        {
            if (state == null)
            {
                return;
            }

            SetText(_firstStatus, FormatCombatant(state.FirstCombatant));
            SetText(_secondStatus, FormatCombatant(state.SecondCombatant));

            var turnText = state.IsFinished ? "戦闘終了" : $"ターン {state.TurnNumber}：{state.ActionSide}";
            SetText(_turnText, turnText);

            // 新しい戦闘の初回表示では、前の戦闘の結果文を消す
            if (state.TurnNumber == 1 && !state.IsFinished)
            {
                SetText(_resultText, string.Empty);
            }
        }

        /// <summary>
        /// 戦闘操作全体の受付を切り替える
        /// </summary>
        public void SetInputEnabled(bool enabled)
        {
            _isInputEnabled = enabled;
            RefreshButtons();
        }

        /// <summary>
        /// Presenterが保持する選択を表示へ反映
        /// </summary>
        public void SetSelectedCommand(BattleCommand? command)
        {
            _selectedCommand = command;

            var text = command.HasValue ? $"選択：{GetCommandName(command.Value)}" : "コマンドを選択してください";
            SetText(_selectionText, text);

            RefreshButtons();
        }

        public async UniTask PlayResultAsync(BattleResult result, CancellationToken token)
        {
            if (token.IsCancellationRequested || result == null)
            {
                return;
            }

            SetText(_resultText, $"{result.Request.Actor}:" + GetCommandName(result.Request.Command));

            // 後でアニメーションなどの待機に置き換える
            await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: token);
        }

        /// <summary>
        /// 入力受付と選択状態をボタンへ反映
        /// </summary>
        private void RefreshButtons()
        {
            SetInteractable(_attackButton, _isInputEnabled);
            SetInteractable(_guardButton, _isInputEnabled);
            SetInteractable(_chargeButton, _isInputEnabled);
            SetInteractable(_specialButton, _isInputEnabled);

            // 未選択の場合は決定・取消を受け付けない
            bool canConfirm = _isInputEnabled && _selectedCommand.HasValue;

            SetInteractable(_confirmButton, canConfirm);
            SetInteractable(_cancelButton, canConfirm);
        }

        /// <summary>
        /// 各ボタンの購読を追加または解除
        /// </summary>
        private void SetButtonListeners(bool subscribe)
        {
            SetListener(_attackButton, HandleAttack, subscribe);
            SetListener(_guardButton, HandleGuard, subscribe);
            SetListener(_chargeButton, HandleCharge, subscribe);
            SetListener(_specialButton, HandleSpecial, subscribe);
            SetListener(_confirmButton, HandleConfirm, subscribe);
            SetListener(_cancelButton, HandleCancel, subscribe);
        }

        /// <summary>
        /// 攻撃の選択を通知
        /// </summary>
        private void HandleAttack() => NotifySelection(BattleCommand.Attack);

        /// <summary>
        /// 防御の選択を通知
        /// </summary>
        private void HandleGuard () => NotifySelection(BattleCommand.Guard);

        /// <summary>
        /// チャージの選択を通知
        /// </summary>
        private void HandleCharge () => NotifySelection(BattleCommand.Charge);

        /// <summary>
        /// 必殺技の選択を通知
        /// </summary>
        private void HandleSpecial () => NotifySelection(BattleCommand.Special);

        /// <summary>
        /// 操作可能な場合だけコマンド選択を通知
        /// </summary>
        private void NotifySelection(BattleCommand command)
        {
            if (isActiveAndEnabled && _isInputEnabled)
            {
                CommandSelected?.Invoke(command);
            }
        }

        /// <summary>
        /// 選択済みの場合だけ決定を通知
        /// </summary>
        private void HandleConfirm()
        {
            if (isActiveAndEnabled && _isInputEnabled && _selectedCommand.HasValue)
            {
                ConfirmRequested?.Invoke();
            }
        }

        /// <summary>
        /// 選択済みの場合だけ取消を通知
        /// </summary>
        private void HandleCancel()
        {
            if (isActiveAndEnabled && _isInputEnabled && _selectedCommand.HasValue)
            {
                CancelRequested?.Invoke();
            }
        }

        /// <summary>
        /// 参加者の状態を表示用文字列へ変換
        /// </summary>
        private static string FormatCombatant (CombatantState state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            return $"HP：{state.Hp} / {state.MaxHp}\n"
                + $"エネルギー：{state.Energy} / {state.MaxEnergy}\n"
                + (state.IsGuarding ? "防御中" : string.Empty);
        }

        /// <summary>
        /// コマンドの表示名を取得
        /// </summary>
        private static string GetCommandName (BattleCommand command)
        {
            return command switch
            {
                BattleCommand.Attack => "攻撃",
                BattleCommand.Guard => "防御",
                BattleCommand.Charge => "チャージ",
                BattleCommand.Special => "必殺技",
                _ => "不明"
            };
        }

        /// <summary>
        /// テキストが設定されている場合に表示を更新
        /// </summary>
        private static void SetText(TextMeshProUGUI target, string text)
        {
            if (target != null)
            {
                target.SetText(text);
            }
        }

        /// <summary>
        /// ボタンが設定されている場合に操作可否を更新
        /// </summary>
        private static void SetInteractable (Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        /// <summary>
        /// 指定したボタンのクリック購読を切り替える
        /// </summary>
        private static void SetListener(Button button, UnityAction listener, bool subscribe)
        {
            if (button == null)
            {
                return;
            }

            if (subscribe)
            {

                button.onClick.AddListener(listener);
            }
            else
            {
                button.onClick.RemoveListener(listener);
            }
        }
    }
}

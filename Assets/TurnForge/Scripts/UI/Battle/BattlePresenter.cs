using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using TF.Battle;
using TF.Battle.Commands;
using TF.Battle.Flow;
using TF.Battle.Models;
using TF.GameFlow;
using UnityEngine.Events;

namespace TF.UI.Battle
{
    /// <summary>
    /// 戦闘操作・状態表示・結果演出を接続
    /// </summary>
    public sealed class BattlePresenter : IDisposable
    {
        /// <summary>
        /// 戦闘画面の操作と表示
        /// </summary>
        private readonly IBattleView _view;

        /// <summary>
        /// 現在の戦闘状態
        /// </summary>
        private readonly BattleModel _model;

        /// <summary>
        /// 行動受付と演出待ちの管理
        /// </summary>
        private readonly BattleFlowController _flow;

        /// <summary>
        /// この画面から操作するプレイヤー
        /// </summary>
        private readonly BattleSide _inputSide;

        /// <summary>
        /// Presenterの破棄時に演出を中断する通知元
        /// </summary>
        private readonly CancellationTokenSource _lifeTimeCts = new CancellationTokenSource();

        /// <summary>
        /// 選択中のコマンド。未選択の場合はnull
        /// </summary>
        private BattleCommand? _selectedCommand;

        /// <summary>
        /// 操作イベントを購読しているか
        /// </summary>
        private bool _isInitialized = false;

        /// <summary>
        /// 行動と演出を処理しているか
        /// </summary>
        private bool _isExecuting = false;

        /// <summary>
        /// このPresenterが破棄されたか
        /// </summary>
        private bool _isDisposed = false;

        /// <summary>
        /// 最後の演出まで完了した戦闘状態を通知
        /// </summary>
        public event UnityAction<BattleState> BattleFinished;

        /// <summary>
        /// プレイヤーからの操作を受け付けられるか
        /// </summary>
        private bool CanAcceptPlayerInput => !_isDisposed
                && _isInitialized
                && !_isExecuting
                && _flow.CanAcceptInput
                && _model.CurrentState.ActionSide == _inputSide;

        /// <summary>
        /// 表示・状態・進行管理・プレイヤーの操作側を受け取る
        /// </summary>
        public BattlePresenter (IBattleView view, BattleModel model, BattleFlowController flow, BattleSide inputSide)
        {
            _view = view;
            _model = model;
            _flow = flow;
            _inputSide = inputSide;
        }

        /// <summary>
        /// 開始済みの戦闘へ接続し、初期状態を表示
        /// </summary>
        public bool TryInitialize()
        {
            if (_isDisposed || _view == null || _model?.CurrentState == null || _flow == null || !_flow.CanAcceptInput)
            {
                return false;
            }

            if (_inputSide != BattleSide.First && _inputSide != BattleSide.Second)
            {
                return false;
            }

            if (_isInitialized)
            {
                return true;
            }

            _view.CommandSelected += HandleCommandSelected;
            _view.ConfirmRequested += HandleConfirmRequested;
            _view.CancelRequested += HandleCancelRequested;
            _isInitialized = true;

            _view.Render(_model.CurrentState);
            _view.SetSelectedCommand(_selectedCommand);
            RefreshInput();
            return true;
        }

        /// <summary>
        /// 人間の手番で選択されたコマンドを保持する
        /// </summary>
        private void HandleCommandSelected(BattleCommand command)
        {
            if (!CanAcceptPlayerInput || !Enum.IsDefined(typeof(BattleCommand), command))
            {
                return;
            }

            _selectedCommand = command;
            _view.SetSelectedCommand(command);
        }

        /// <summary>
        /// 選択中のコマンドを、現在のターンの要求として送る
        /// </summary>
        private void HandleConfirmRequested ()
        {
            if (!CanAcceptPlayerInput || !_selectedCommand.HasValue)
            {
                return;
            }

            // 決定した時点の手番情報を使って要求する
            var request = new BattleActionRequest(_inputSide, _model.CurrentState.TurnNumber, _selectedCommand.Value);
            TryExecuteAsync(request).Forget();
        }

        /// <summary>
        /// コマンド選択を取り消す
        /// </summary>
        private void HandleCancelRequested()
        {
            if (!CanAcceptPlayerInput)
            {
                return;
            }

            _selectedCommand = null;
            _view.SetSelectedCommand(null);
        }

        /// <summary>
        /// 行動を実行し、演出と表示更新まで完了する。
        /// </summary>
        public async UniTask<bool> TryExecuteAsync(BattleActionRequest request)
        {
            if (_isDisposed || !_isInitialized || _isExecuting || !_flow.CanAcceptInput)
            {
                return false;
            }

            _isExecuting = true;
            _view.SetInputEnabled(false);

            // 無効な要求では演出を開始しない
            if (!_flow.TryExecute(request, out BattleResult result))
            {
                _isExecuting = false;
                _view.SetInputEnabled(true);
                return false;
            }

            _selectedCommand = null;
            _view.SetSelectedCommand(null);

            bool canceled = await _view.PlayResultAsync(result, _lifeTimeCts.Token).SuppressCancellationThrow();

            _isExecuting = false;

            if (_isDisposed)
            {
                // 実行中の演出が終了してから通知元を解放
                _lifeTimeCts?.Dispose();
                return false;
            }

            // 演出が中断されても、確定済みの状態は維持
            _view.Render(_model.CurrentState);

            if (!_flow.TryCompleteEffects(result))
            {
                RefreshInput();
                return false;
            }

            RefreshInput();

            if (_flow.ActionState == BattleActionState.Finished)
            {
                BattleFinished?.Invoke(_model.CurrentState);
            }

            // falseでも、成立済みの行動は巻き戻さない
            return !canceled;
        }

        /// <summary>
        /// 現在の手番と進行状態を入力受付へ反映
        /// </summary>
        private void RefreshInput()
        {
            _view.SetInputEnabled(CanAcceptPlayerInput);
        }

        /// <summary>
        /// 操作の購読を解除し、実行中の演出に中断を要求
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_isInitialized)
            {
                _view.CommandSelected -= HandleCommandSelected;
                _view.ConfirmRequested -= HandleConfirmRequested;
                _view.CancelRequested -= HandleCancelRequested;
                _isInitialized = false;
            }

            _lifeTimeCts?.Cancel();

            if (!_isExecuting)
            {
                _lifeTimeCts?.Dispose();
            }

            BattleFinished = null;
        }
    }
}

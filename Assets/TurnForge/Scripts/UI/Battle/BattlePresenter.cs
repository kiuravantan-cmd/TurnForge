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
using R3;

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
        /// このPresenterが受け取るR3の購読をまとめる
        /// </summary>
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

        /// <summary>
        /// この画面から今操作できるかを通知する
        /// </summary>
        private readonly ReactiveProperty<bool> _canAcceptInput = new ReactiveProperty<bool>(false);
        
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
        /// 演出後に自分の入力の受け付ける番になった通知
        /// </summary>
        public event UnityAction<BattleState> PlayerTurnReady;

        /// <summary>
        /// プレイヤーからの操作を受け付けられるか
        /// </summary>
        private bool CanAcceptPlayerInput => 
            _isInitialized && !_isDisposed && !_isExecuting &&
            _view != null && _flow != null && _flow.CanAcceptInput &&
            _model?.CurrentState != null &&
            !_model.CurrentState.IsFinished &&
            _model.CurrentState.ActionSide == _inputSide;

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

            // 操作可否の現在値を受け取り、以後の変化を表示へ渡す
            _canAcceptInput.Subscribe(_view.SetInputEnabled).AddTo(_subscriptions);
            
            // 最初に現在の状態を受け取り、その後も変更を受け取る
            _model.StateChanged.Subscribe(HandleStateChanged).AddTo(_subscriptions);

            _model.StateChanged
                .Select(IsLowHp)
                .DistinctUntilChanged()
                .Subscribe(SetLowHpWarning)
                .AddTo(_subscriptions);
            
            _view.SetSelectedCommand(_selectedCommand);
            return true;
        }

        private bool IsLowHp(BattleState state)
        {
            return state.FirstCombatant.Hp > 0 && (long)state.FirstCombatant.Hp * 4 <= state.FirstCombatant.MaxHp;
        }

        private void SetLowHpWarning(bool isLowHp)
        {
            UnityEngine.Debug.Log($"自分のHP警告：{isLowHp}");
            _view.SetLowHpWarning(isLowHp);
        }

        /// <summary>
        /// HP・エネルギー・ターンを表示し、操作の条件を更新する
        /// </summary>
        private void HandleStateChanged(BattleState state)
        {
            if (_isDisposed || state == null)
            {
                return;
            }

            UnityEngine.Debug.Log($"状態通知：ターン{state.TurnNumber}");
            _view.Render(state);
            RefreshInput();
        }

        /// <summary>
        /// プレイヤーの番で選択されたコマンドを保持する
        /// </summary>
        private void HandleCommandSelected(BattleCommand command)
        {
            if (!CanAcceptPlayerInput || !_model.CanExecute(_inputSide, command))
            {
                return;
            }

            _selectedCommand = command;
            _view.SetSelectedCommand(_selectedCommand);
            RefreshInput();
        }

        /// <summary>
        /// 選択中のコマンドを、現在のターンの指示として送る
        /// </summary>
        private void HandleConfirmRequested()
        {
            if (!CanAcceptPlayerInput || !_selectedCommand.HasValue)
            {
                return;
            }

            // 選択後に状態が変わる場合に備え、決定時にも条件を調べる
            BattleCommand command = _selectedCommand.Value;
            if (!_model.CanExecute(_inputSide, command))
            {
                return;
            }

            // 現在の番号を使い、実行処理を開始する
            var request = new BattleActionRequest(_inputSide, _model.CurrentState.TurnNumber, command);
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
            _view.SetSelectedCommand(_selectedCommand);
            RefreshInput();
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
            _canAcceptInput.Value = false;

            // 実行できない指示では演出を開始しない
            if (!_flow.TryExecute(request, out BattleResult result))
            {
                _isExecuting = false;
                RefreshInput();
                return false;
            }

            _selectedCommand = null;
            _view.SetSelectedCommand(null);

            // プレイヤーの陣営を基準に、確定した結果の表示文を作成する
            string message = BattleResultFormatter.Format(result, _inputSide);

            // 表示と演出が完了するまで、次の行動を受け付けない
            bool canceled = await _view.PlayResultAsync(result, message, _lifeTimeCts.Token).SuppressCancellationThrow();

            _isExecuting = false;

            if (_isDisposed)
            {
                // 実行中の演出が終了してから通知元を解放
                _lifeTimeCts?.Dispose();
                return false;
            }

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
            else if (CanAcceptPlayerInput)
            {
                PlayerTurnReady?.Invoke(_model.CurrentState);
            }

            // falseでも、成立済みの行動は巻き戻さない
            return !canceled;
        }

        /// <summary>
        /// 現在の実行条件と行動する番に合わせて、選択内容と操作できるかどうかを更新する
        /// </summary>
        private void RefreshInput()
        {
            if (_isDisposed || !_isInitialized)
            {
                return;
            }

            // 操作全体を受け付けられるか
            bool canAcceptInput = CanAcceptPlayerInput;
            foreach (BattleCommand command in Enum.GetValues(typeof(BattleCommand)))
            {
                // 回復を追加した場合も、同じルールで使用条件を調べる
                bool canExecute = canAcceptInput && _model.CanExecute(_inputSide, command);
                _view.SetCommandEnabled(command, canExecute);
            }

            if (_selectedCommand.HasValue &&
                (!canAcceptInput || !_model.CanExecute(_inputSide, _selectedCommand.Value)))
            {
                _selectedCommand = null;
            }

            _view.SetSelectedCommand(_selectedCommand);

            _canAcceptInput.Value = canAcceptInput;
        }

        /// <summary>
        /// 操作の購読を解除し、実行中の演出に中断を指示
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
            
            // Viewへの受け取りを先に止めてから、通知元を終了する
            _subscriptions.Dispose();
            _canAcceptInput.Dispose();

            _lifeTimeCts?.Cancel();

            if (!_isExecuting)
            {
                _lifeTimeCts?.Dispose();
            }

            BattleFinished = null;
            PlayerTurnReady = null;
        }
    }
}

using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TF.Battle;
using TF.Battle.AI;
using TF.Battle.Factories;
using TF.Battle.Flow;
using TF.Battle.Models;
using TF.Battle.Preparation;
using TF.Battle.Rules;
using TF.Battle.Saving;
using TF.GameFlow;
using TF.Infrastructure.Saving;
using TF.Infrastructure.Updating;
using TF.MasterData;
using TF.UI.Battle;
using TF.UI.Common;
using TF.UI.GameFlow;
using TF.UI.Result;
using TF.UI.Title;

namespace TF.Composition
{
    /// <summary>
    /// ゲームの依存関係を組み立て、終了時に解放する
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameLoopRunner))]
    public class GameCompositionRoot : MonoBehaviour
    {
        /// <summary>
        /// 更新登録を解除するためのハンドル
        /// </summary>
        private readonly List<IDisposable> _registrations = new List<IDisposable>();
        
        /// <summary>
        ///  Unityの更新イベントの受け取り口
        /// </summary>
        [SerializeField] private GameLoopRunner _runner;

        /// <summary>
        /// ゲーム全体の画面切り替え
        /// </summary>
        [SerializeField] private GameScreenView _gameScreenView;

        /// <summary>
        /// タイトル画面の操作受付
        /// </summary>
        [SerializeField] private TitleView _titleView;

        /// <summary>
        /// 戦闘画面の操作受付と表示
        /// </summary>
        [SerializeField] private BattleView _battleView;

        /// <summary>
        /// 結果画面の操作受付と表示
        /// </summary>
        [SerializeField] private ResultView _resultView;

        /// <summary>
        /// 各画面のUI選択を管理するコンポーネント
        /// </summary>
        [SerializeField]
        private UiSelectionController[] _uiSelectionControllers =
             new UiSelectionController[0];

        /// <summary>
        /// 1人目に使用する参加者マスタのID
        /// </summary>
        [SerializeField] private ulong _firstCombatantId = 1;
        
        /// <summary>
        /// 2人目に使用する参加者マスタのID
        /// </summary>
        [SerializeField] private ulong _secondCombatantId = 1;

        /// <summary>
        /// 更新対象と実行順を管理
        /// </summary>
        private UpdateScheduler _scheduler;

        /// <summary>
        /// このRootがRunnerを初期化したか
        /// </summary>
        private bool _ownsRunner;
        
        /// <summary>
        /// 現在の戦闘状態を保持するモデル
        /// </summary>
        private BattleModel _battleModel;

        /// <summary>
        /// 入力受付と演出待ちを管理する進行処理
        /// </summary>
        private BattleFlowController _battleFlow;

        /// <summary>
        /// このRootの解放処理が行われたか
        /// </summary>
        private bool _isReleased = false;

        /// <summary>
        /// ーム全体の進行管理
        /// </summary>
        private GameFlowController _gameFlow;
        
        /// <summary>
        /// 戦闘開始前の準備管理
        /// </summary>
        private BattleLoadingController _battleLoading;
        
        /// <summary>
        /// 戦闘開始処理が進行中か
        /// </summary>
        private bool _isStartingBattle = false;

        /// <summary>
        /// ゲーム状態を画面へ反映するPresenter
        /// </summary>
        private GameScreenPresenter _gameScreenPresenter;

        /// <summary>
        /// タイトル画面の操作受付と、戦闘開始処理を接続するPresenter
        /// </summary>
        private TitlePresenter _titlePresenter;

        /// <summary>
        /// 戦闘結果の表示と再戦・タイトルへの遷移を接続
        /// </summary>
        private ResultPresenter _resultPresenter;

        /// <summary>
        /// 戦闘画面の操作受付・状態表示・結果演出を接続するPresenter
        /// </summary>
        private BattlePresenter _battlePresenter;

        /// <summary>
        /// CPUの行動選択と実行要求を管理
        /// </summary>
        private BattleCpuController _battleCpu;

        /// <summary>
        /// 戦闘終了時にCPUの更新登録を解除するハンドル
        /// </summary>
        private IDisposable _cpuRegistration;

        /// <summary>
        /// 起動中の戦績と保存状態を管理
        /// </summary>
        private BattleRecordService _battleRecordService;

        /// <summary>
        /// 現在のゲーム全体の状態
        /// </summary>
        public GameState CurrentState => _gameFlow?.CurrentState ?? GameState.Inactive;

        private void Awake()
        {
            // 更新基盤とゲームの依存関係を初期化
            if (_runner == null)
            {
                _runner = GetComponent<GameLoopRunner>();
            }
            
            _scheduler = new UpdateScheduler();

            if (_runner != null && _runner.TryInitialize(_scheduler))
            {
                _ownsRunner = true;
            }
            else
            {
                Debug.LogError("更新基盤を初期化できませんでした。", this);
                
                // // 初期化途中で生成したリソースを解放
                Release();
                enabled = false;
            }
        }

        /// <summary>
        /// 更新基盤の初期化後に、戦闘の組み立てを開始する
        /// </summary>
        private void Start()
        {
            if (_isReleased || !_ownsRunner)
            {
                return;
            }

            _gameFlow = new GameFlowController();

            if (_gameScreenView == null)
            {
                Debug.LogError("GameScreenViewが設定されていません。", this);
                Release();
                enabled = false;
                return;
            }

            _gameScreenPresenter = new GameScreenPresenter(_gameFlow, _gameScreenView);

            if (!_gameScreenPresenter.TryInitialize())
            {
                Debug.LogError("GameScreenPresenterを初期化できませんでした。", this);
                Release();
                enabled = false;
                return;
            }

            if (_titleView == null)
            {
                Debug.LogError("TitleViewが設定されていません。", this);
                Release();
                enabled = false;
                return;
            }

            _titlePresenter = new TitlePresenter(_titleView, _gameFlow, TryStartBattleAsync);

            if (!_titlePresenter.TryInitialize())
            {
                Debug.LogError("TitlePresenterを初期化できませんでした。", this);
                Release();
                enabled = false;
                return;
            }

            if (_battleView == null)
            {
                Debug.LogError("BattleViewが設定されていません。", this);
                Release();
                enabled = false;
                return;
            }

            // オフライン用の準備処理を接続
            // TODO LESSON07-01: オンライン用の準備処理を追加し、同一PCの2プロセスを接続する。
            // TODO LESSON08-02: 切断通知と終了処理を接続する。
            var preparation = new OfflineBattlePreparation();
            
            _battleLoading = new BattleLoadingController(_gameFlow, preparation);

            if (_resultView == null)
            {
                Debug.LogError("ResultViewが設定されていません。", this);
                Release();
                enabled = false;
                return;
            }

            // 戦闘画面と同じFirstをプレイヤーの陣営として扱う
            _resultPresenter = new ResultPresenter(
                _resultView,
                _gameFlow,
                TryStartBattleAsync,
                BattleSide.First);

            if (!_resultPresenter.TryInitialize())
            {
                Debug.LogError("ResultPresenterを初期化できませんでした。", this);
                Release();
                enabled = false;
                return;
            }

            if (!TryRegisterUISelections())
            {
                Release();
                enabled = false;
                return;
            }

            InitializeGameAsync().Forget();
        }

        /// <summary>
        /// 各画面の選択管理を表示更新の順序で登録
        /// </summary>
        private bool TryRegisterUISelections()
        {
            if (_uiSelectionControllers == null || _uiSelectionControllers.Length == 0)
            {
                Debug.LogError("UI選択管理が設定されていません。", this);
                return false;
            }

            foreach (UiSelectionController controller in _uiSelectionControllers)
            {
                if (controller == null)
                {
                    Debug.LogError("UI選択管理に未設定の要素があります。", this);
                    return false;
                }

                // 戦闘処理や演出の更新後に、操作可能なUIを確認する
                if (!_scheduler.TryRegisterUpdate(controller, out var registration, UpdateOrder.Input))
                {
                    Debug.LogError("UI選択管理の更新登録に失敗しました。", this);
                    return false;
                }

                // ゲーム終了時にまとめて登録を解除
                _registrations.Add(registration);
            }

            return true;
        }

        private async UniTaskVoid InitializeGameAsync()
        {
            if (!_gameFlow.TryBeginStartup())
            {
                return;
            }

            // TODO LESSON03-07: セーブ機能完成後、読み込みと失敗時の扱いを接続する。
            // 配布時は保存を呼び出さず、タイトルへ進める。

            // マスタ読み込みと戦闘生成の結果
            bool succeeded = await LoadMasterDataAsync();
            
            // 待機中に破棄された場合は、そのまま終了する
            if (_isReleased)
            {
                return;
            }

            if (!succeeded)
            {
                Debug.LogError("マスタを初期化できませんでした。", this);
                _gameFlow.TryFailStartup();
                return;
            }
            
            _gameFlow.TryCompleteStartup();
        }
        
        /// <summary>
        /// 使用するマスタを登録し、読み込み完了まで待機
        /// </summary>
        private async UniTask<bool> LoadMasterDataAsync()
        {
            MasterDataAccessor accessor = MasterDataAccessor.Instance;
            if (accessor == null)
            {
                Debug.LogError("MasterDataAccessorが配置されていません。", this);
                return false;
            }
            
            // 初回だけ、このゲームで使用するマスタを登録
            if (!accessor.IsInitialized)
            {
                bool combatantsRegistered =
                    accessor.Register<BattleCombatantData, BattleCombatantDataRecord>("BattleCombatantData");
                
                bool commandRegistered = 
                    accessor.Register<BattleCommandData, BattleCommandDataRecord>("BattleCommandData");

                if (!combatantsRegistered || !commandRegistered)
                {
                    Debug.LogError("マスタを登録できませんでした。登録・初期化の重複を確認してください。",this);
                    return false;
                }
            }
            
            // 初期化済みの場合も、Accessorが保持する結果を受け取る
            bool initialized = await accessor.InitializeAsync();
            if (_isReleased || accessor == null || !initialized)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 読み込み済みマスタから、1戦分のオブジェクトを生成する。
        /// </summary>
        private bool TryComposeBattle()
        {
            // TODO LESSON05-02: この手動の組み立てをVContainerへ移行する。
            // 戦闘単位の寿命と破棄順を保ち、CPUの実装を登録で差し替える。
            MasterDataAccessor accessor = MasterDataAccessor.Instance;
            if (accessor == null || !accessor.IsInitialized)
            {
                return false;
            }
            
            if (!accessor.TryGetById(_firstCombatantId, out BattleCombatantDataRecord firstCombatant))
            {
                Debug.LogError($"参加者マスタがありません：ID {_firstCombatantId}", this);
                return false;
            }

            if (!accessor.TryGetById(_secondCombatantId, out BattleCombatantDataRecord secondCombatant))
            {
                Debug.LogError($"参加者マスタがありません：ID {_secondCombatantId}", this);
                return false;
            }
            
            // マスタから初期化状態を生成するFactory
            var factory = new BattleStateFactory();
            if (!factory.TryCreateInitialState(firstCombatant, secondCombatant, out var initialState))
            {
                Debug.LogError("参加者マスタの初期能力が不正です。", this);
                return false;
            }

            // コマンドマスタを使用する戦闘ルール
            var rules = new BattleRules(accessor.GetAll<BattleCommandDataRecord>());

            if (!rules.IsConfigured)
            {
                Debug.LogError("コマンドマスタの構成が不正です。", this);
                return false;
            }
            
            _battleModel = new BattleModel(rules, initialState);
            _battleFlow = new BattleFlowController(_battleModel);

            // Presenterの初期化の前に、行動受付状態へ進める
            if (!_battleFlow.TryStartBattle())
            {
                Debug.LogError("戦闘の開始に失敗しました。", this);
                return false;
            }

            // オフラインではFirstをプレイヤーの操作側にする
            _battlePresenter = new BattlePresenter(_battleView, _battleModel, _battleFlow, BattleSide.First);
            _battlePresenter.BattleFinished += HandleBattleFinised;

            if (!_battlePresenter.TryInitialize())
            {
                Debug.LogError("BattlePresenterの初期化に失敗しました。", this);
                return false;
            }

            // CPUは通常攻撃を選択
            IBattleCommandSelector selector = new AttackOnlyCommandSelector();

            // プレイヤーと共通の行動実行・演出処理へ接続する
            _battleCpu = new BattleCpuController(
                _battleModel,
                _battleFlow,
                selector,
                BattleSide.Second,
                _battlePresenter.TryExecuteAsync);

            _battleCpu.Failed += HandleCpuFailed;

            // CPUの更新をゲームロジックの実行順で登録
            if (!_scheduler.TryRegisterUpdate(_battleCpu, out _cpuRegistration, UpdateOrder.Simulation))
            {
                Debug.LogError("CPUの更新登録に失敗しました。", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// タイトルまたは結果画面から、準備を経由して戦闘を開始
        /// </summary>
        public async UniTask<bool> TryStartBattleAsync()
        {
            if (_isReleased || _isStartingBattle || _gameFlow == null || _battleLoading == null)
            {
                return false;
            }

            switch (CurrentState)
            {
                case GameState.Title:
                case GameState.Result:
                    break;

                case GameState.Inactive:
                case GameState.Loading:
                case GameState.Battle:
                case GameState.BattleLoading:
                case GameState.Error:
                default:
                    return false;
            }
            
            _isStartingBattle = true;
            
            // 前の戦闘の利用側を片付けてから、アセットを解放
            ReleaseBattle();

            if (!_battleLoading.ReleasePreparatedResources())
            {
                _isStartingBattle = false;
                return false;
            }

            // ローディングへ移り、戦闘に必要な準備を待つ
            bool isPrepared = await _battleLoading.TryPrepareAsync();

            if (_isReleased || !isPrepared)
            {
                _isStartingBattle = false;
                return false;
            }

            if (!TryComposeBattle())
            {
                ReleaseBattle();
                _battleLoading.CancelPreparation();
                _isStartingBattle = false;
                return false;
            }

            if (!_battleLoading.TryEnterBattle())
            {
                ReleaseBattle();
                _battleLoading.CancelPreparation();
                _isStartingBattle = false;
                return false;
            }

            // 戦闘画面への遷移後に、CPUの行動を許可する
            _battleCpu.SetEnabled(true);

            _isStartingBattle = false;
            return true;
        }
        
        /// <summary>
        /// ローディング中の戦闘準備に中断を要求
        /// </summary>
        public void CancelBattlePreparation()
        {
            if (_isReleased)
            {
                return;
            }

            _battleLoading?.CancelPreparation();
        }

        /// <summary>
        /// 1戦分の演出・購読・オブジェクトを片付ける
        /// </summary>
        private void ReleaseBattle()
        {
            // CPUの更新登録を先に解除
            _cpuRegistration?.Dispose();
            _cpuRegistration = null;

            // CPUの購読と参照を解放
            if (_battleCpu != null)
            {
                _battleCpu.Failed -= HandleCpuFailed;
                _battleCpu.Dispose();
                _battleCpu = null;
            }

            // 新しい画面操作を止める
            if (_battleView != null)
            {
                _battleView.SetInputEnabled(false);
            }

            // モデルの参照を外す前に、演出の中断と購読解除を行う
            if (_battlePresenter != null)
            {
                _battlePresenter.BattleFinished -= HandleBattleFinised;
                _battlePresenter.Dispose();
                _battlePresenter = null;
            }

            _battleFlow = null;
            _battleModel = null;
        }

        /// <summary>
        /// 最後の演出が完了したら、結果画面へ進む
        /// </summary>
        private void HandleBattleFinised(BattleState state)
        {
            if (_isReleased || state == null || !state.IsFinished || _gameFlow.CurrentState != GameState.Battle)
            {
                return;
            }

            // 結果画面ではCPUの行動を停止する
            if (_battleCpu != null)
            {
                _battleCpu.SetEnabled(false);
            }

            // 結果を設定してから画面を切り替える
            if (_resultPresenter == null || !_resultPresenter.TrySetResult(state))
            {
                Debug.LogError("戦闘結果を設定できませんでした。", this);
                return;
            }

            if (!_gameFlow.TryShowResult())
            {
                Debug.LogError("結果画面へ遷移できませんでした。", this);
            }

            // TODO LESSON03-09: 戦績の加算と保存を接続する。重複加算を防ぐ。
        }

        /// <summary>
        /// CPUが停止した原因を出力する
        /// </summary>
        private void HandleCpuFailed(string message)
        {
            if (_isReleased)
            {
                return;
            }

            Debug.LogError($"CPUの行動処理が停止しました：{message}", this);
        }

        /// <summary>
        /// 所有する更新基盤と登録を解放
        /// </summary>
        private void Release()
        {
            if (_isReleased)
            {
                return;
            }
            
            // 非同期処理が完了しても、戦闘を生成し直さないようにする
            _isReleased = true;
            
            // 自分が初期化したRunnerだけを停止する
            if (_ownsRunner && _runner != null)
            {
                _runner.Shutdown();
                _ownsRunner = false;
            }

            for (int i = _registrations.Count - 1; i >= 0; i--)
            {
                _registrations[i].Dispose();
            }
            
            _registrations.Clear();
            
            // 利用側を先に終了させてから、準備処理を破棄する。
            ReleaseBattle();

            _battleLoading?.Dispose();
            _battleLoading = null;

            // 今後、画面Presenterの購読解除もここへ追加する。
            _gameScreenPresenter?.Dispose();
            _gameScreenPresenter = null;
            _titlePresenter?.Dispose();
            _titlePresenter = null;
            _resultPresenter?.Dispose();
            _resultPresenter = null;

            _scheduler?.Dispose();
            _scheduler = null;
        }

        /// <summary>
        /// 保存先を組み立て、戦績を初期化
        /// </summary>
        private bool TryInitializeBattleRecord ()
        {
            // TODO LESSON03-07: 保存先とServiceを組み立て、起動時の読み込みを接続する。
            // 学生用の保存先は講師用と分ける（例：persistentDataPath/TurnForgeLessons）。
            // 戦闘途中の状態・キャラクターID・操作設定の保存データも第3回で追加する。
            return false;
        }

        /// <summary>
        /// 未保存の戦績がある場合に保存を試みる
        /// </summary>
        private void SaveBattleRecordIfNeeded ()
        {
            // TODO LESSON03-08: 変更がある場合だけ保存し、失敗時は後で再試行する。
            // 戦闘途中の保存はターン開始時に接続し、演出途中では保存しない。
        }

        /// <summary>
        /// 第2回のUI接続前に、第1回の通常攻撃をゲーム本体で確認する。
        /// </summary>
        [ContextMenu("授業確認/第1回・プレイヤーの通常攻撃")]
        private void ExecuteLessonAttack()
        {
            if (!Application.isPlaying || _isReleased
                || CurrentState != GameState.Battle
                || _battleModel?.CurrentState == null || _battlePresenter == null)
            {
                Debug.Log("Playモードでタイトルから戦闘画面へ進んでください。", this);
                return;
            }

            // CPUの手番や戦闘終了後の要求は、本体のルールで拒否する。
            var request = new TF.Battle.Commands.BattleActionRequest(
                BattleSide.First, _battleModel.CurrentState.TurnNumber,
                TF.Battle.Commands.BattleCommand.Attack);
            _battlePresenter.TryExecuteAsync(request).Forget();
        }

        /// <summary>
        /// 破棄時に所有するリソースを解放
        /// </summary>
        private void OnDestroy()
        {
            // TODO LESSON03-10: 終了時の未保存データの再保存を接続する。
            Release();
        }
    }   
}

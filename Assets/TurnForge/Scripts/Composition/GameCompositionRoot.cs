using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using TF.Battle;
using TF.Battle.Factories;
using TF.Battle.Flow;
using TF.Battle.Models;
using TF.Battle.Rules;
using TF.MasterData;
using TF.Infrastructure.Updating;

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
        [SerializeField]
        private GameLoopRunner _runner;

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
            
            InitializeBattleAsync().Forget();
        }

        private async UniTaskVoid InitializeBattleAsync()
        {
            // マスタ読み込みと戦闘生成の結果
            bool succeeded = await ComposeAsync();
            
            // 待機中に破棄された場合は、そのまま終了する
            if (_isReleased)
            {
                return;
            }

            if (!succeeded)
            {
                Debug.LogError("戦闘を初期化できませんでした。", this);
                Release();
                enabled = false;
                return;
            }
            
            Debug.Log("戦闘の初期化が完了しました。", this);
        }

        /// <summary>
        /// マスタを読み込み、戦闘に必要なクラスを生成して接続
        /// </summary>
        private async UniTask<bool> ComposeAsync()
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
            
            // ここにView・Presenterの生成と接続を追加する。

            return _battleFlow.TryStartBattle();
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
            
            // Presenterなどの購読解除を行う。

            _battleModel = null;
            _battleFlow = null;
            
            _scheduler?.Dispose();
            _scheduler = null;
        }

        /// <summary>
        /// 破棄時に所有するリソースを解放
        /// </summary>
        private void OnDestroy()
        {
            Release();
        }
    }   
}
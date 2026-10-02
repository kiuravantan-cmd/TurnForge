# 第1回：戦闘処理の実装

## 編集するファイル

パスは `Assets/TurnForge/Scripts/` からの場所です。

| ファイル | 実装する内容 |
|---|---|
| `Battle/Rules/BattleRules.cs` | 状態のコピー、ダメージ計算、実行条件、通常攻撃、回復 |
| `Battle/BattleModel.cs` | 行動が成功したときの状態更新 |
| `Battle/AI/AttackOnlyCommandSelector.cs` | CPUが使う技の選択 |

配布済みの `CombatantState`、`BattleState`、`BattleActionRequest`、`BattleResult` を使います。同じ名前のクラスを新しく作る必要はありません。

## 1コマ目：状態のコピー・ダメージ計算・実行条件

### CopyCombatant（LESSON01-01A）

元のキャラクターの状態と、変更するHP・エネルギー・防御状態を受け取り、新しい `CombatantState` を返します。

- `Side`、`MaxHp`、`MaxEnergy` は元の状態から引き継ぐ。
- `Hp`、`Energy`、`IsGuarding` は引数の値を使う。
- 元の状態を直接書き換えない。

`get / init` のプロパティは、新しい状態を作るときに値を設定します。攻撃前の状態を残すことで、変更前と変更後を比べられます。

#### ライブコーディング

対象のメソッド全体を、次のコードへ置き換えます。

```csharp
/// <summary>
/// 指定された値で、キャラクターの新しい状態を作る
/// </summary>
private static CombatantState CopyCombatant(
    CombatantState source, int hp, int energy, bool isGuarding)
{
    return new CombatantState(
        source.Side, hp, source.MaxHp,
        energy, source.MaxEnergy, isGuarding);
}
```

### ApplyDamage（LESSON01-01B）

攻撃を受けるキャラクターとダメージを受け取り、攻撃後の状態を作ります。

- 元のHPから `damage` を引く。
- HPが0未満にならないようにする。
- `CopyCombatant` に計算後のHPを渡す。
- エネルギーと防御状態は引き継ぐ。

ヒント：`Math.Max(0, 計算後のHP)` で、HPの下限を0にできます。ダメージはマスタデータから渡される値を使います。固定値の20を処理に書き込みません。防御による軽減は後の授業で追加します。

#### ライブコーディング

対象のメソッド全体を、次のコードへ置き換えます。

```csharp
/// <summary>
/// 通常攻撃後のHPを0以上にし、新しい状態を返す
/// </summary>
private static CombatantState ApplyDamage(
    CombatantState target, int damage, int guardDamageDivisor)
{
    // ダメージを受けた後のHP。マスタ側でdamageは0以上であることを確認済み。
    int nextHp = Math.Max(0, target.Hp - damage);
    return CopyCombatant(
        target, nextHp, target.Energy, target.IsGuarding);
    // 第6回でguardDamageDivisorを使った防御の軽減を追加する。
}
```

### private版CanExecute（LESSON01-02）

HPを計算する前に、行動を受け付けてよいか判断します。`out BattleCommandDataRecord` を持つメソッドを編集します。

- マスタが読み込まれているか。
- 状態と指示があり、バトルが終了していないか。
- FirstとSecondの状態がそろい、HPなどが範囲内か。
- 指示を出した側が、今行動する側と一致するか。
- 指示のターン番号が、現在のターン番号と一致するか。
- 指示された技のマスタがあるか。
- この段階では通常攻撃が選ばれているか。

条件を満たさない場合は `false` を返します。条件を満たした場合は、取得した技のマスタを `out` 引数で渡します。

#### ライブコーディング

対象のメソッド全体を、次のコードへ置き換えます。

```csharp
/// <summary>
/// 第1回の通常攻撃を実行できるか確認する
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

    if (!IsValidCombatant(state.FirstCombatant, BattleSide.First)
        || !IsValidCombatant(state.SecondCombatant, BattleSide.Second))
    {
        return false;
    }

    if (state.ActionSide != BattleSide.First
        && state.ActionSide != BattleSide.Second)
    {
        return false;
    }

    if (request.Actor != state.ActionSide
        || request.TurnNumber != state.TurnNumber)
    {
        return false;
    }

    // この時点は通常攻撃のみ。第3コマの課題で回復も許可する。
    if (request.Command != BattleCommand.Attack)
    {
        return false;
    }

    return _commandData.TryGetValue(request.Command, out commandData);
}
```

## 2コマ目：通常攻撃・状態更新・CPUの行動

### BattleRules.TryExecute（LESSON01-03）

次の順に、通常攻撃の結果を作ります。

- `result` を `null` にし、`CanExecute` で実行条件と技のマスタを取得する。
- 指示を出した側と相手の状態を取得する。
- マスタのダメージを `ApplyDamage` へ渡し、相手の攻撃後の状態を作る。
- 相手のHPが0なら終了にする。
- 続く場合は相手の番へ切り替え、ターン番号を1増やす。
- 終了する場合は、行動した側とターン番号を維持する。
- FirstとSecondの位置を保って、新しい `BattleState` を作る。
- 指示・変更前・変更後を `BattleResult` にまとめ、成功を返す。

CPUが攻撃するときもFirstはプレイヤー、SecondはCPUです。「行動した側」を毎回Firstへ入れると、キャラクターが入れ替わってしまいます。

#### ライブコーディング

対象のメソッド全体を、次のコードへ置き換えます。

```csharp
/// <summary>
/// 通常攻撃を計算し、変更前後の戦闘状態を返す
/// </summary>
public bool TryExecute(
    BattleState currentState,
    BattleActionRequest request,
    out BattleResult result)
{
    result = null;

    if (!CanExecute(currentState, request, out var commandData))
    {
        return false;
    }

    // 攻撃するキャラクターと、攻撃を受けるキャラクター。
    CombatantState actor = GetCombatant(currentState, request.Actor);
    CombatantState target = GetCombatant(
        currentState, GetOpponentSide(request.Actor));

    // 第1回は防御による軽減を行わない。
    CombatantState nextTarget = ApplyDamage(
        target, commandData.Damage, 1);

    // 相手のHPが0になったか。
    bool isFinished = nextTarget.IsDefeated;
    // 勝敗が決まったときは現在の行動する番とターン番号をそのままにする。
    BattleSide nextActionSide = currentState.ActionSide;
    int nextTurnNumber = currentState.TurnNumber;

    if (!isFinished)
    {
        // ターン番号が表現できない指示は実行しない。
        if (nextTurnNumber == int.MaxValue)
        {
            return false;
        }

        nextActionSide = target.Side;
        nextTurnNumber++;
    }

    // 攻撃側にかかわらず、キャラクターをFirst、Secondの順に並べる。
    CombatantState nextFirst =
        request.Actor == BattleSide.First ? actor : nextTarget;
    CombatantState nextSecond =
        request.Actor == BattleSide.Second ? actor : nextTarget;
    // 次の時点の戦闘状態。
    BattleState nextState = new BattleState(
        nextFirst, nextSecond, nextActionSide,
        nextTurnNumber, isFinished);

    result = new BattleResult(request, currentState, nextState);
    return true;
}
```

### BattleModel.TryExecute（LESSON01-04）

- 現在の状態と指示を `BattleRules.TryExecute` へ渡す。
- 失敗した場合は現在の状態を変えず、`false` を返す。
- 成功した場合は `NextState` を現在の状態として持つ。
- 状態を置き換えた後、配布済みのイベントで結果を知らせる。

先に状態を更新することで、知らせを受け取った処理が新しいHPを読めます。

#### ライブコーディング

対象のメソッド全体を、次のコードへ置き換えます。

```csharp
/// <summary>
/// 成立した行動の結果を採用し、更新後に通知する
/// </summary>
public bool TryExecute(BattleActionRequest request, out BattleResult result)
{
    result = null;

    if (_rules == null || _currentState == null)
    {
        return false;
    }

    if (!_rules.TryExecute(_currentState, request, out result))
    {
        return false;
    }

    // 通知先が最新の状態を読めるよう、先に置き換える。
    _currentState = result.NextState;
    ActionResolved?.Invoke(result);
    return true;
}
```

### AttackOnlyCommandSelector.TrySelectCommand（LESSON01-05）

- 状態があり、バトルが続いているかを調べる。
- 指定された側がFirstまたはSecondであり、その側の番かを調べる。
- 条件を満たした場合は `BattleCommand.Attack` を選び、成功を返す。

このメソッドは技を選ぶところまでです。HPの計算やターン交代は `BattleRules` に任せます。

#### ライブコーディング

対象のメソッド全体を、次のコードへ置き換えます。

```csharp
/// <summary>
/// 継続中の自分の番なら、通常攻撃を選ぶ
/// </summary>
public bool TrySelectCommand(
    BattleState state, BattleSide actor, out BattleCommand command)
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
```

## 3コマ目の課題：回復処理を作る

`BattleRules` に、自分のHPを回復して相手の番へ進む処理を追加します。回復量には、講師が用意するマスタの値を使います。

- [ ] 回復するキャラクターの状態と回復量を受け取るメソッドを作る。
- [ ] 現在のHPに回復量を足す。
- [ ] 最大HPを超えないようにする。
- [ ] `CopyCombatant` を使い、回復後の状態を作る。
- [ ] HP以外の値を、回復前の状態から引き継ぐ。
- [ ] `CanExecute` で回復の指示も受け付けるようにする。
- [ ] `TryExecute` で通常攻撃と回復を分け、自分の回復後の状態を戦闘全体へ組み込む。
- [ ] 回復後は相手の番へ切り替え、ターン番号を1増やす。

### ヒント

- 攻撃は「相手」のHPを減らし、回復は「自分」のHPを増やします。
- `Math.Min(最大HP, 計算後のHP)` で、HPの上限を決められます。
- 技ごとにHPの計算を分け、計算後のターン交代と結果作成は共通にすると、同じコードを何度も書かずに済みます。
- 回復量はマスタから受け取り、処理に固定値を書き込みません。

## 4コマ目の課題：CPUが攻撃と回復を選ぶ

`AttackOnlyCommandSelector.TrySelectCommand` を変更し、CPU自身のHPに応じて技を選びます。

- [ ] `actor` がFirstかSecondかで、行動するキャラクターの状態を選ぶ。
- [ ] そのキャラクターのHPと最大HPを読む。
- [ ] HPが最大HPの半分以下なら回復を選ぶ。
- [ ] 半分より多ければ通常攻撃を選ぶ。
- [ ] バトル終了後や、自分の番ではないときは技を選ばない。
- [ ] 選んだ技を既存の戦闘処理へ渡し、HP計算は `BattleRules` に任せる。

### ヒント

- 「半分以下」には、ちょうど半分も含みます。
- 最大HPが奇数のとき、整数の割り算でどの値になるか考えましょう。
- 第2コマで作った通常攻撃の選択を、HPによる分岐へ広げます。
- クラス名の変更は必須ではありません。変更する場合は、`GameCompositionRoot` などの参照も合わせます。



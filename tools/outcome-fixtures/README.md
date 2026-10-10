# 勝敗経路の診断データ

## HP0即時終了の修正後検査

現在のサーバーソースを直接検査し、その実通知をUnity判定へ通す:

```powershell
./tools/verify_hp_zero.ps1 -OutputDirectory "$env:TEMP/km-hp-zero-check"
./tools/verify_game_outcomes.ps1 -FixturesPath "$env:TEMP/km-hp-zero-check/server-fixtures.json"
```

サーバー境界検査15件と、清算時HP0による通常勝敗2件は修正後に成功。
賭け金支払い直後のHP0だけでは対局を打ち切らず、局終了地点で判定する。
下の保存済みJSONは修正前の証拠として維持している。既定のverify_game_outcomesはその過去データを診断するため、修正後の確認には上記コマンドを使うこと。累計決着の勝敗逆転は今回の修正対象外。

## 修正前の記録

`server-current-20261006.json` は main `4fa8f17` の実GameEngine/GameSessionを別プロセスで呼び、送信関数に届いたJSONを保存したもの。実サーバーの通信や対局データは使っていない。サーバーのソース変更、判定の差し替えは行っていない。

## 実行

通常の回帰検査: `./tools/verify_phases.ps1`

サーバー通知とUnity勝敗判定の診断: `./tools/verify_game_outcomes.ps1`

後者は確認済みの問題を検出し、現状は終了コード1になる。合格扱いに書き換えない。サーバー修正後は実サーバーコードからデータも採取し直すこと。保存済みJSONの再実行だけで新しいサーバーを検証したとは扱わない。

## 条件

全ケースでプレイヤーIDはself/other。自己視点を反転した2ケースずつ。

- negative: 終局時HPを1000/-1にしてend_round。hp_zero通知の対照検査。
- normal_zero: 勝者HP1000・敗者HP500、双方の掛け金200。実liquidationで勝者1800・敗者0となる。round_end_waitingへ進み、この時点でgame_endは出ない。
- cumulative: 勝者HP1000・累計19000・掛け金3000、敗者HP50000・累計25000・掛け金200。実liquidation後は累計31000/25000、HP13000/49200。game_endには最新累計がなく、Unityの直前statusが19000/25000のままだと勝敗が逆転する。
- carry: end_round(is_draw=true)後、次局の手牌選択完了地点でselected_hand。支払える側HP200・掛け金200、不足側HP500・掛け金1000。サーバーが不足側をHP0にし、unknownのgame_endを出す。

liquidationの牌は `[0,0,0,1,1,1,2,2,2,3,3,3,4,4]`。実HandAnalyzerが四暗刻13翻・4倍と判定し、実清算処理を通した。

normal_zeroのdelayedGameEndは、清算後のHPを引き継いだ手牌選択完了地点で実selected_handを呼んだ結果。配牌生成・手牌操作・WebSocketは省略している。

## 検査の意味

通常回帰には実TryParseGameEndと実DetermineLocalWinを取り込み、HP決着・累計決着・掛け金不足の勝敗6ケースを追加。最新の累計を受信済みなら6ケースとも通る。

診断では「清算でHP0ならその場で終了」を期待値としている。この即時終了検査2件と、累計の勝敗逆転2件が失敗する。通常勝敗は次局手牌選択後の終了通知なら勝敗表示を通るため、即時終了の不一致と永続的な停止を区別すること。

Unity上でも本編シーンに通知を再生し、実清算演出→OK→結果表示、手牌選択中の不足→結果表示を確認した。描画・通信を模擬したCLIテストだけを実機検証とは扱わない。詳細はkm-docsのarchitecture/game-outcomes-audit-20261006.mdを参照。

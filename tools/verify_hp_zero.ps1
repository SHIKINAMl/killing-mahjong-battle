param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $env:TEMP ('km-hp-zero-' + [Guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$taskRepo = Split-Path $PSScriptRoot -Parent
$taskProbe = @'
import asyncio, json, sys
from pathlib import Path
from types import SimpleNamespace
from mahjong_engine.engine.game_engine import GameEngine
from mahjong_engine.engine.game_state import RoundStatus
from mahjong_engine.communication.game_session import GameSession

passed = 0
def check(value, message):
    if not value: raise AssertionError(message)
def done(name):
    global passed
    passed += 1
    print('PASS ' + name)

def engine():
    e = GameEngine()
    e.initialize_players(['self', 'other'])
    e.state.round_state.status = RoundStatus.LIQUIDATION
    e.state.round_state.round_number = 3
    return e

async def capture(e, trigger):
    messages = []
    async def collect(target, payload):
        # Yield once, like an asynchronous transport. Never use a live match.
        await asyncio.sleep(0)
        messages.append(payload)
    s = GameSession(asyncio.Lock(), {'M1': SimpleNamespace(players=['self','other'])},
        {'self':'M1','other':'M1'}, {'M1':e}, [], collect, collect)
    e.on_round_end = s._create_task_callback(s.on_round_end, 'M1')
    e.on_game_end = s._create_task_callback(s.on_game_end, 'M1')
    e.on_phase_change = s._create_task_callback(s.on_phase_change, 'M1')
    trigger()
    tasks = list(s._tasks_by_match.get('M1', set()))
    if tasks: await asyncio.gather(*tasks, return_exceptions=False)
    return messages

async def main():
    fixtures = []
    for index in [0, 1]:
        for hp in [-1, 0, 1]:
            e = engine(); e.state.players[index].health = hp
            messages = await capture(e, lambda: e.end_round())
            ends = [m for m in messages if m['type']=='game_end']
            check(len(ends)==(1 if hp<=0 else 0), 'wrong zero boundary')
            if hp<=0:
                check([m['type'] for m in messages]==['round_end','game_end'], 'next round entered or settlement missing')
                check(ends[0]['victory_method']=='hp_zero', 'wrong reason')
                check(ends[0]['health_zero_players']==[['self','other'][index]], 'wrong exhausted player')
            else:
                check(e.state.round_state.status==RoundStatus.ROUND_END_WAITING, 'positive HP stopped match')
            done('HP boundary player=%d hp=%d' % (index,hp))

    for winner_id in ['self','other']:
        e = engine(); w=e.get_player_by_id(winner_id)
        loser_id='other' if winner_id=='self' else 'self'; loser=e.get_player_by_id(loser_id)
        w.health=1000; loser.health=500; w.bet=loser.bet=200
        def liquidate():
            check(e.liquidation(winner_id,[0,0,0,1,1,1,2,2,2,3,3,3,4,4]), 'real liquidation rejected hand')
        messages=await capture(e,liquidate)
        check([m['type'] for m in messages]==['round_end','game_end'], 'zero HP requires next hand selection')
        end=messages[-1]
        check(end['final_scores'][loser_id]==0 and end['final_scores'][winner_id]==1800, 'wrong final HP')
        check(messages[0]['data']['liquidation']['loser_health']==0, 'settlement missing')
        fixtures.append(dict(name='normal_zero_'+winner_id,kind='normal_zero',expectedWin=winner_id=='self',
            staleCumulative=[0,0],finalCumulative=[p.cumulative_earned_points for p in e.state.players],
            health=[p.health for p in e.state.players],phase=e.state.round_state.status.value,messages=messages,gameEnd=end))
        done('real liquidation ends immediately winner='+winner_id)

    for hp in [0, 1]:
        e=engine(); e.state.players[0].health=hp
        messages=await capture(e,lambda: e.end_round(is_draw=True))
        check(messages[-1]['type']==('game_end' if hp==0 else 'next_round_waiting'), 'draw zero boundary')
        check(next(m for m in messages if m['type']=='round_end')['data']['is_draw'], 'draw flag lost')
        done('draw boundary hp='+str(hp))

    e=engine()
    for p in e.state.players:
        p.health=200
        check(e.place_bet(p,200), 'exact-HP bet rejected')
    messages=await capture(e,e.bet)
    check(not any(m['type']=='game_end' for m in messages) and e.state.round_state.status==RoundStatus.DISCARD,
        'paying exact HP ended game before the round could be played')
    done('zero HP immediately after betting still enters discard')

    for index in [0,1]:
        e=engine(); e._carry_over_bets=True; e.state.round_state.status=RoundStatus.HAND_SELECTION
        for p in e.state.players: p.health=200; p.bet=200
        e.state.players[index].health=500; e.state.players[index].bet=1000
        messages=await capture(e,e.selected_hand)
        check(len(messages)==1 and messages[0]['type']=='game_end', 'carryover shortage failed to end')
        check(messages[0]['victory_method']=='hp_zero' and messages[0]['health_zero_players']==[['self','other'][index]],
            'carryover shortage reason/player inconsistent')
        done('carryover shortage player='+str(index))

    e=engine(); e.state.players[0].cumulative_earned_points=30000
    messages=await capture(e,lambda:e.end_round())
    check(messages[-1]['victory_method']=='cumulative_earned_points', 'positive HP cumulative reason changed')
    done('positive HP cumulative ending preserved')
    e=engine(); e.state.players[0].health=0; e.state.players[1].cumulative_earned_points=30000
    messages=await capture(e,lambda:e.end_round())
    check(messages[-1]['victory_method']=='hp_zero', 'HP reason not consistent at simultaneous threshold')
    done('HP zero reason takes precedence at simultaneous threshold')
    Path(sys.argv[1]).write_text(json.dumps(fixtures,ensure_ascii=True,indent=2),encoding='utf-8')
    print('All %d live-source server regressions passed.' % passed)

asyncio.run(main())
'@
Push-Location $taskRepo
try {
    $taskProbe | python -B - (Join-Path $OutputDirectory 'server-fixtures.json')
    if ($LASTEXITCODE -ne 0) { throw "HP-zero server regression failed ($LASTEXITCODE)" }
}
finally { Pop-Location }
Write-Output "Server fixtures: $(Join-Path $OutputDirectory 'server-fixtures.json')"

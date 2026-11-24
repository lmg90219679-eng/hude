# 사운드 생성 프롬프트 (22개)

AI 소리 생성 도구로 만든 뒤 `Assets/Resources/Audio/<이름>.wav`로 저장하면 코드 수정 없이 교체된다 (`GameAudio.Load`).
형식: wav 44.1kHz 모노 권장 (3D 소리는 모노가 입체음에 맞다). 루프 표시된 것은 끝과 처음이 이어지게.
현재 파일은 `make_audio.py`가 합성한 것이며, 같은 이름으로 덮어쓰면 된다.

| 이름 | 용도 | 길이 | 루프 | 프롬프트 |
|---|---|---|---|---|
| Amb_Drone | 배경 저음 | 12초 | O | dark ambient drone, low sub bass hum, distant metallic creaks, abandoned hospital, unsettling, no melody |
| Heartbeat | 추적자 접근 시 심장 소리 | 1초 | | single human heartbeat lub-dub, deep, muffled, close, horror |
| Footstep1~4 | 발소리 4종 (각각 다르게) | 0.5초 | | footstep on dusty concrete floor, worn shoe, quiet, one step (variation 1~4) |
| DoorSlide | 미닫이 문 | 1.5초 | | heavy metal sliding door rolling on rusty rail, rumble, slight screech |
| DoorCreak | 계단 비상구 삐걱임 | 2초 | | old rusty hinged door slowly creaking open, long wooden-metal groan |
| Drawer | 서랍 열림 | 1초 | | old wooden drawer sliding open, scrape, rattle of items inside |
| MetalClang | 보안카드 금속 소음 (크다) | 3초 | | loud metal clang echoing in empty hallway, dropped metal tray, long reverb |
| Key | 열쇠 줍기 | 1.2초 | | small keys jingle, quick pickup, metallic |
| Pickup | 아이템 획득 | 0.4초 | | soft item pickup, cloth rustle, short |
| Click | 손전등 클릭 | 0.1초 | | flashlight switch click, plastic, crisp |
| Ding | 알림 | 2.5초 | | single soft bell ding, clean, short decay |
| Lever | 차단기 레버 | 1.5초 | | heavy electrical breaker lever thrown, metal clunk, spark buzz |
| PowerUp | 전력 복구 | 3.5초 | | electrical power restoring, rising hum, lights buzz on, relay clicks |
| Alarm | 경보 | 3초 | | emergency siren alarm, harsh, distant, industrial |
| ElevatorHum | 엘리베이터 이동 | 4초 | O | old elevator machinery hum, motor drone, cable creak, low |
| Whisper | 엘리베이터 속삭임 (뒤돌아보면 안 됨) | 8초 | O | creepy whispers behind you, many overlapping voices, unintelligible, close to ear |
| Growl | 추적자 으르렁 | 3.5초 | | monstrous guttural growl, wet throat, low, creature breathing, horror |
| Scream | 잡혔을 때 비명 | 2~3초 | | terrified human scream cut short by a violent impact, horror jump scare |
| Sting | 충격음 (추적자 발견) | 1.5초 | | horror jump scare stinger, sudden loud dissonant hit with sub boom |

눈과 귀가 아프지 않게 피크는 -1dB 안쪽으로, Scream과 Sting만 크게 한다.

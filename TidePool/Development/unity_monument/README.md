# PoolHall_Monument_01

Lighting update: the current scene now uses a closed roof and interior/pool lamps. See `../unity_pool_lighting/README.md` for the current lighting state; the architecture notes below describe the initial daylight variant.

Unity scene: `E:/jianmowenjian/unityproject/TIDEPOOL/TidePool/Assets/_Game/Scenes/PoolHall_Monument_01.unity`

This is an independent architectural version of PoolHall_WaterCycle_01. The previous playable scene remains available.

## Layout and flow

- Hall: 40 × 64 metres, ceiling underside 24 metres. Pool: 22 × 42 metres.
- Eight continuous 2-metre-wide piers; removed the old low side roofs and decorative upper walls in this scene.
- Cold ceramic walls, one long roof aperture, small distant exit.
- E near the console drains the pool over 8 seconds. Descend the existing front-right steps to the letter.
- E at the letter begins flooding immediately; E / Esc closes the letter. Flood duration is 72 seconds: 18 fill, 24 rise over the platform, 30 fill the hall.
- Final water height: 24.5 metres. Dry space: x=6..10, z=49.5..53.5, y=-3..27. Its camera guard changes to eye-height view inside.
- R resets the prototype, including water, letter, seepage, colour and player spawn.
- The character still has no swimming or drowning mechanic. The distant corridor is a spatial placeholder, not a completed level ending.

## Checks and current limitations

- Actual CharacterController escape route measured approximately 65.5 metres; walking estimate 21.8 seconds plus reading fits the current flood window.
- Full sequence checked: exposed letter, roof seepage, complete wet floor after overflow, final water height, no seepage below final water, dark-blue atmosphere, dry-region camera and reset.
- 2026-10-22 intermittent pale-grey world disappearance: user video shows both Scene and Game geometry disappear while GUI continues to update and the editor reports “Compiling shader”. This is a correlation, not a proven cause. No corresponding script exception or device-loss record was found in the inspected log tail.
- Six static waterline/underwater/empty-pool viewpoints and five consecutive real-time drains with idle shaders rendered normally. These checks did not reproduce the user's failure. No definitive white-screen fix is claimed.
- Broad curved wall-light boundaries remain under visual review; they must not be described as a repaired geometry join without verification.

Diagnostic outputs are in this folder: `status.txt`, `shader-status.txt` (on request), `repeat-drain.txt`, `crossing-*.png`, `test-pass.txt`, `camera-pass.txt` (when completed), and scene screenshots. Tools reset Play mode before handoff.

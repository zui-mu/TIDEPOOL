# Current pool hall lighting

Scene: `E:/jianmowenjian/unityproject/TIDEPOOL/TidePool/Assets/_Game/Scenes/PoolHall_Monument_01.unity`

The earlier daylight scene was copied to `BeforeLampLighting.unity` before modifying it. The other playable scene files remain available.

The current hall has a closed roof, eight pool lamps, eight overhead fixtures, eight high side-wall fixtures and eight animated light-cookie projectors. The projectors approximate moving water reflections rather than tracing light through the simulated surface. Existing water simulation, screen-space/planar reflection and underwater fog remain in use.

The opening and the entire drain/search phase now use a brighter cool-white ambient and soft indirect fill. Picking up the real letter starts a 12-second gradual fade of that fill, visible behind the open letter, while actual lamp colours take 24 seconds to reach cyan. Neither draining nor merely approaching the letter dims the hall. The fill reaches zero in the flood, preserving the previous final lamp intensities and blue ambient. Exposure changes by only 0.3 stops. Local point-light scatter in the water is restrained to keep the surface and tiles readable. R restores the bright white opening, lamp colours and exposure.

The 8-second drain and 72-second flood sequence, pool-floor letter, dry air pocket and small passage remain. The second room has not been built. This work adjusts the current hall, not the next level.

See `look.txt`, `pass.txt` and the five PNG images for the latest visual/sequence checks. A controller test pass alone does not establish that the visual reference has been matched. The earlier intermittent white screen remains unproven: five real-time drains with idle shaders did not reproduce it, and the user's video showed shader compilation at the time of failure.

The newer `bright-*.png`, `transition.txt` and `transition-pass.txt` cover the bright opening revision and real letter-reading transition. `BeforeBrightStart.unity` preserves the earlier darker lamp setup.

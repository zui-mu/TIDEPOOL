# WebGpuWater feature tests

The Runtime suite is intentionally deterministic: it validates the C# feature contracts without
depending on a graphics driver, asynchronous GPU readback, or a particular URP renderer setup.

Use Unity's Test Runner with **PlayMode** selected. The test assembly is named
`AbstractOcclusion.WebGpuWater.Tests`. This TidePool checkout does not currently declare a root
`testables` list; verify discovery in the project's Unity version before claiming the suite ran.
Adding or changing test-discovery configuration is a separate configuration change. See the
package's [TidePool integration Spec](../SPEC.md) and the [project testing guide](../../../../docs/testing.md).

Coverage is grouped by feature rather than by implementation file:

- clipmap mesh topology and invalid topology guards;
- height-query allocation/reuse and bilinear field sampling;
- box, sphere, mesh-proxy, nearest-selection, and buffer-validation exclusion behaviour;
- quality limits, Jerlov preset lookup, and interactable submersion;
- mesh builders, wind-wave determinism, and touch pinch tracking;
- FFT ocean spectrum layout, directional swell, and gain normalisation;
- foam profile material-property writes and particle flipbook safety;
- analytic large-wave, surface-kinematics, current-composition, river spline/ribbon, settled
  obstacle-aware fluid/current/foam, and buoyancy maths;
- volume-frame transforms, footprint bounds, and ray-to-surface picking.

The visual and GPU-path validation remains a separate manual pass because it needs the project's
active URP renderer, compute-shader backend, and a human render check. It should not be made a
prerequisite for the deterministic feature suite.

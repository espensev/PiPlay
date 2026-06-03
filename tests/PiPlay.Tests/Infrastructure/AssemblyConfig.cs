using Xunit;

// Run tests serially. The suite is tiny (sub-second) and several tests mutate process-global
// state — PIPLAY_DATA_ROOT (AppPathsTests) and the single WPF Application / STA apartment
// (Layer 3) — which must not race across parallel collections. Determinism over speed.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

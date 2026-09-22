// The modules under test keep process-wide static state (regex object, DeString table, dir stack, CWD).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

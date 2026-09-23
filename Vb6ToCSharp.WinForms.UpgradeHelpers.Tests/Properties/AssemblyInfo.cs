// The UI stacks are process-wide (Application.OpenForms, Application.Current, the registered IVbUiBridge):
// test classes must not run against each other.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

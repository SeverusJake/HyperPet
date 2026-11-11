namespace HyperPet.App.Tests.TestSupport;

// WPF's shared BAML/package cache is not safe to load from concurrent STA
// dispatchers. Match the application's single UI-thread execution model.
[CollectionDefinition("WPF UI", DisableParallelization = true)]
public sealed class WpfUiCollection;

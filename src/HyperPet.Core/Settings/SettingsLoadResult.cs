namespace HyperPet.Core.Settings;

public sealed record SettingsLoadResult(HyperPetSettings Settings, bool CanPersist, bool CanApplyStartup, string? Warning);

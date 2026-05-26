namespace HyperPet.Core.Pet;

public sealed record PetAlert(
    string AppName,
    string Title,
    string Body,
    DateTimeOffset Timestamp,
    bool CanActivate,
    string AppUserModelId = "");

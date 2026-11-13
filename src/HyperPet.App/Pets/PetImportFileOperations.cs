using System.IO;
namespace HyperPet.App.Pets;
internal class PetImportFileOperations
{
    public virtual void MoveDirectory(string source, string destination) => Directory.Move(source, destination);
}

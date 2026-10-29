using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(SuperFill.Acad.Plugin))]
[assembly: CommandClass(typeof(SuperFill.Acad.Commands.HelloCommands))]

namespace SuperFill.Acad;

public sealed class Plugin : IExtensionApplication
{
    public void Initialize() =>
        Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nSuperFill loaded. Type SFHELLO to test.\n");

    public void Terminate()
    {
    }
}

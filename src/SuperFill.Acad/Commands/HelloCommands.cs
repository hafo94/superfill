using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.Runtime;
using SuperFill.Core.Geometry;

namespace SuperFill.Acad.Commands;

public sealed class HelloCommands
{
    /// <summary>Smoke test: proves the plugin loads (also in accoreconsole) and can call into Core.</summary>
    [CommandMethod("SFHELLO")]
    public static void Hello()
    {
        var ed = Application.DocumentManager.MdiActiveDocument.Editor;
        var tol = Tolerances.Metres;
        ed.WriteMessage($"\nSFHELLO: SuperFill is alive. Core chord error = {tol.ChordError} m.\n");
    }
}

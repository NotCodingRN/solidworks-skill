using System;
using SolidWorks.Interop.sldworks;

// Read-only. Shows version, licence and every open document with its unsaved flag. Run first.
class Status
{
    static void Main()
    {
        var sw = Kit.Connect();
        Console.WriteLine("SOLIDWORKS " + Kit.Year() + " rev " + sw.RevisionNumber() + " licence " + sw.GetCurrentLicenseType());
        var docs = Kit.OpenDocs();
        Console.WriteLine("open documents: " + docs.Count);
        foreach (var m in docs)
            Console.WriteLine("  " + m.GetTitle() + " | unsaved=" + m.GetSaveFlag() + " | " + m.GetPathName());
    }
}

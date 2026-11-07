using System;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

// Read-only report on a part or assembly. Pass a copy, not an original: opening can update file versions on save.
// Args: <path to .SLDPRT or .SLDASM>
class Inspect
{
    static void Main(string[] a)
    {
        var sw = Kit.Connect();
        string path = Path.GetFullPath(a[0]);
        bool already = Kit.OpenDocs().Any(d => string.Equals(d.GetPathName(), path, StringComparison.OrdinalIgnoreCase));
        var m = Kit.Open(path);
        var mp = m.Extension.CreateMassProperty();
        Console.WriteLine(Path.GetFileName(path));
        Console.WriteLine("  mass kg=" + mp.Mass.ToString("F5") + " volume mm3=" + (mp.Volume * 1e9).ToString("F1"));
        var cg = (double[])mp.CenterOfMass;
        Console.WriteLine("  centre of mass mm=" + string.Join(", ", cg.Select(x => (x * 1000).ToString("F3"))));
        Console.WriteLine("  configurations=" + string.Join(", ", (string[])m.GetConfigurationNames()));
        Console.WriteLine("  feature types=" + string.Join(", ", Kit.Feats(m).Select(f => f.GetTypeName2()).Distinct()));
        if (path.ToUpper().EndsWith("PRT"))
        {
            string db;
            Console.WriteLine("  material=" + ((PartDoc)m).GetMaterialPropertyName2("", out db));
        }
        else
        {
            var comps = (object[])((AssemblyDoc)m).GetComponents(false);
            Console.WriteLine("  components=" + (comps == null ? 0 : comps.Length));
        }
        Kit.Check(m, "  rebuild");
        if (!already) sw.CloseDoc(m.GetTitle());   // only closes what this recipe opened
    }
}

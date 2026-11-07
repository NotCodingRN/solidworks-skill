// SwKit: helpers for driving SOLIDWORKS from C#. swrun.ps1 compiles this into every recipe.
// Every helper here was run against SOLIDWORKS 2023 SP5. See references/ for what each one proved.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

public static class Kit
{
    public static SldWorks Sw;
    public const double MM = 0.001;                                    // API lengths are metres
    public static double Deg(double d) { return d * Math.PI / 180.0; } // API angles are radians

    public static SldWorks Connect()
    {
        // CreateInstance joins a running session or starts one.
        // Marshal.GetActiveObject failed even with SOLIDWORKS open, so it is not used.
        Sw = (SldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application"));
        Sw.Visible = true;
        Sw.UserControl = true;   // keeps SOLIDWORKS open after this program exits
        return Sw;
    }

    // RevisionNumber "31.5.0" means 2023: year = 1992 + major.
    public static int Year() { return 1992 + int.Parse(Sw.RevisionNumber().Split('.')[0]); }

    public static string Template(string ext)   // ext: prtdot, asmdot, drwdot
    {
        int kind = ext == "prtdot" ? (int)swUserPreferenceStringValue_e.swDefaultTemplatePart
                 : ext == "asmdot" ? (int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly
                 : (int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing;
        string t = Sw.GetUserPreferenceStringValue(kind);   // often empty on a fresh install
        if (!string.IsNullOrEmpty(t) && File.Exists(t)) return t;
        string pd = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData);
        string data = Path.Combine(Sw.GetExecutablePath(), @"data\templates");
        foreach (var root in new[] { Path.Combine(pd, @"SolidWorks\SOLIDWORKS " + Year() + @"\templates"), data })
        {
            if (!Directory.Exists(root)) continue;
            var f = Directory.GetFiles(root, "*." + ext, SearchOption.AllDirectories);
            if (f.Length == 0) continue;
            var bsi = f.FirstOrDefault(x => Path.GetFileName(x).ToLower() == "bsi.drwdot");   // UK default
            return bsi ?? f.First();
        }
        throw new Exception("No ." + ext + " template found");
    }

    public static string SheetFormat(string name)   // for example "a3 - bsi.slddrt"
    {
        string pd = System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData);
        string f = Path.Combine(pd, @"SolidWorks\SOLIDWORKS " + Year() + @"\lang\english\sheetformat", name);
        if (!File.Exists(f)) throw new Exception("Sheet format not found: " + f);
        return f;
    }

    // Templates can carry inch units with no warning, so every new document is forced to MMGS.
    public static void Mmgs(ModelDoc2 m)
    {
        m.Extension.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitSystem, 0, (int)swUnitSystem_e.swUnitSystem_MMGS);
    }
    public static ModelDoc2 NewPart() { var m = (ModelDoc2)Sw.NewDocument(Template("prtdot"), 0, 0, 0); Mmgs(m); return m; }
    public static ModelDoc2 NewAsm() { var m = (ModelDoc2)Sw.NewDocument(Template("asmdot"), 0, 0, 0); Mmgs(m); return m; }
    public static ModelDoc2 NewDrawing(int paper) { var m = (ModelDoc2)Sw.NewDocument(Template("drwdot"), paper, 0, 0); Mmgs(m); return m; }

    public static ModelDoc2 Open(string path)
    {
        string full = Path.GetFullPath(path);   // SOLIDWORKS resolves relative paths from its own folder
        string u = full.ToUpper();
        int t = u.EndsWith("ASM") ? (int)swDocumentTypes_e.swDocASSEMBLY
              : u.EndsWith("DRW") ? (int)swDocumentTypes_e.swDocDRAWING
              : (int)swDocumentTypes_e.swDocPART;
        int e = 0, w = 0;
        var m = Sw.OpenDoc6(full, t, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w) as ModelDoc2;
        if (m == null) throw new Exception("Open failed err=" + e + " warn=" + w + " : " + full);
        return m;
    }

    // The target decides the format: .SLDPRT .STEP .pdf .dxf .png
    public static void Save(ModelDoc2 m, string path)
    {
        int e = 0, w = 0;
        bool ok = m.Extension.SaveAs(Path.GetFullPath(path), (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref e, ref w);
        if (!ok) throw new Exception("Save failed err=" + e + " warn=" + w + " : " + path);
    }

    public static bool Sel(ModelDoc2 m, string name, string type, bool append = false, int mark = 0)
    {
        return m.Extension.SelectByID2(name, type, 0, 0, 0, append, mark, null, 0);
    }

    public static IEnumerable<Feature> Feats(ModelDoc2 m)
    {
        var f = (Feature)m.FirstFeature();
        while (f != null) { yield return f; f = (Feature)f.GetNextFeature(); }
    }

    public static Feature Last(ModelDoc2 m, string type)
    {
        return Feats(m).Where(x => x.GetTypeName2() == type).LastOrDefault();
    }

    public static double Mass(ModelDoc2 m) { return m.Extension.CreateMassProperty().Mass; }
    public static double VolMm3(ModelDoc2 m) { return m.Extension.CreateMassProperty().Volume * 1e9; }

    // Runs a block with the dimension-entry popup off, then restores the user's setting.
    public static void NoDimPopup(Action a)
    {
        int k = (int)swUserPreferenceToggle_e.swInputDimValOnCreate;
        bool old = Sw.GetUserPreferenceToggle(k);
        Sw.SetUserPreferenceToggle(k, false);
        try { a(); } finally { Sw.SetUserPreferenceToggle(k, old); }
    }

    // Sketch on a named plane. draw() adds geometry. Leaves the finished sketch feature as the return value.
    public static Feature Sketch(ModelDoc2 m, string plane, Action<SketchManager> draw)
    {
        Sel(m, plane, "PLANE");
        var sm = m.SketchManager;
        sm.InsertSketch(true);
        draw(sm);
        m.ClearSelection2(true);
        sm.InsertSketch(true);
        return Last(m, "ProfileFeature");
    }

    public static Feature Extrude(ModelDoc2 m, Feature sketch, double depth)
    {
        sketch.Select2(false, 0);
        return m.FeatureManager.FeatureExtrusion2(true, false, false, 0, 0, depth, 0, false, false, false, false, 0, 0,
            false, false, false, false, true, true, true, 0, 0, false);
    }

    // Through-all cut. Direction depends on the sketch plane and which side the body is on, so both are tried.
    // Dir (third argument) reverses direction. Flip (second) swaps which side of the profile is removed: leave it false.
    // A failed call still uses up a feature number, so hold the returned Feature and never assume "Cut-Extrude1".
    public static Feature CutThrough(ModelDoc2 m, Feature sketch)
    {
        foreach (var dir in new[] { false, true })
        {
            sketch.Select2(false, 0);
            var c = m.FeatureManager.FeatureCut4(true, false, dir, 1, 0, 0, 0, false, false, false, false, 0, 0,
                false, false, false, false, false, true, true, true, true, false, 0, 0, false, false);
            if (c != null) return c;
        }
        return null;
    }

    public static Body2 Body(ModelDoc2 part)
    {
        return ((object[])((PartDoc)part).GetBodies2((int)swBodyType_e.swSolidBody, true))[0] as Body2;
    }

    // Faces and edges are found by geometry because their names are not stable.
    public static Face2 CylFace(Body2 b, double r)
    {
        foreach (Face2 f in (object[])b.GetFaces())
        {
            var s = (Surface)f.GetSurface();
            if (s.IsCylinder() && Math.Abs(((double[])s.CylinderParams)[6] - r) < 1e-6) return f;
        }
        return null;
    }

    // Planar face normal to Z at the lowest or highest position.
    public static Face2 ZFace(Body2 b, bool high)
    {
        Face2 best = null;
        double bz = high ? -1e9 : 1e9;
        foreach (Face2 f in (object[])b.GetFaces())
        {
            var s = (Surface)f.GetSurface();
            if (!s.IsPlane()) continue;
            if (Math.Abs(Math.Abs(((double[])s.PlaneParams)[2]) - 1) > 1e-6) continue;
            double z = ((double[])f.GetBox())[2];
            if (high ? z > bz : z < bz) { bz = z; best = f; }
        }
        return best;
    }

    // Rebuilds and reports feature errors. Returns true if the model is clean.
    public static bool Check(ModelDoc2 m, string label)
    {
        m.ForceRebuild3(false);
        bool ok = true;
        foreach (var f in Feats(m))
        {
            bool w;
            int c = f.GetErrorCode2(out w);
            if (c != 0) { ok = false; Console.WriteLine("  FEATURE ERROR " + f.Name + " code=" + c + " warn=" + w); }
        }
        Console.WriteLine(label + ": " + (ok ? "rebuild clean" : "HAS ERRORS"));
        return ok;
    }

    // Lists open documents. Run before acting, so work the user already has open is never touched.
    public static List<ModelDoc2> OpenDocs()
    {
        var list = new List<ModelDoc2>();
        var d = (ModelDoc2)Sw.GetFirstDocument();
        while (d != null) { list.Add(d); d = (ModelDoc2)d.GetNext(); }
        return list;
    }
}

using System;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swmotionstudy;

// Rebuilds small models and compares each result with a hand calculation.
// Run after a SOLIDWORKS upgrade, or on a new machine, to learn which kit behaviour still holds.
// Compile with -Sim -Motion. Arg: an empty scratch folder. Touches nothing outside it.
class SelfTest
{
    const double MM = 0.001;
    static int pass, fail;

    static void Near(string what, double got, double want, double tolFrac)
    {
        bool ok = Math.Abs(got - want) <= Math.Abs(want) * tolFrac;
        if (ok) pass++; else fail++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + what + ": got " + got.ToString("G6") + ", hand " + want.ToString("G6"));
    }

    static void Yes(string what, bool ok)
    {
        if (ok) pass++; else fail++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + what);
    }

    static void Main(string[] a)
    {
        string dir = Path.GetFullPath(a[0]);
        Directory.CreateDirectory(dir);
        var sw = Kit.Connect();
        Console.WriteLine("SOLIDWORKS " + Kit.Year() + " rev " + sw.RevisionNumber());

        string hub = Path.Combine(dir, "Hub.SLDPRT"), shaft = Path.Combine(dir, "Shaft.SLDPRT");
        BuildHub(hub);
        BuildShaft(shaft);
        Interference(dir, hub, shaft);
        Drawing(dir, hub);
        Fea(dir);
        Motion(dir, hub, shaft);
        Console.WriteLine("RESULT: " + pass + " passed, " + fail + " failed");
        System.Environment.ExitCode = fail == 0 ? 0 : 3;
    }

    static void BuildHub(string path)
    {
        var p = Kit.NewPart();
        Kit.NoDimPopup(() =>
        {
            var sk = Kit.Sketch(p, "Front Plane", sm =>
            {
                sm.CreateCircleByRadius(0, 0, 0, 30 * MM);
                sm.CreateCircleByRadius(0, 0, 0, 8 * MM);
            });
            Kit.Extrude(p, sk, 12 * MM);
            Near("extrude volume mm3", Kit.VolMm3(p), Math.PI * (900 - 64) * 12, 1e-4);

            var hole = Kit.Sketch(p, "Front Plane", sm => sm.CreateCircleByRadius(22 * MM, 0, 0, 2.5 * MM));
            var cut = Kit.CutThrough(p, hole);
            Yes("through-all cut made", cut != null);

            Kit.Sel(p, "Top Plane", "PLANE");
            Kit.Sel(p, "Right Plane", "PLANE", true);
            p.InsertAxis2(true);
            var axis = Kit.Last(p, "RefAxis");
            p.ClearSelection2(true);
            Kit.Sel(p, axis.Name, "AXIS", false, 1);
            cut.Select2(true, 4);
            p.FeatureManager.FeatureCircularPattern5(6, 2 * Math.PI, false, "NULL", false, true, false, false, false, false, 1, 0, "NULL", false);
            Near("pattern volume mm3", Kit.VolMm3(p), Math.PI * (836 - 6 * 6.25) * 12, 1e-4);
        });
        ((PartDoc)p).SetMaterialPropertyName2("", "SOLIDWORKS Materials", "AISI 1045 Steel, cold drawn");
        Near("density kg/m3", Kit.Mass(p) / (Kit.VolMm3(p) * 1e-9), 7850, 1e-3);
        Yes("metric units", p.Extension.GetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitSystem, 0) == (int)swUnitSystem_e.swUnitSystem_MMGS);
        Yes("hub rebuild clean", Kit.Check(p, "hub"));
        Kit.Save(p, path);
    }

    static void BuildShaft(string path)
    {
        var p = Kit.NewPart();
        var sk = Kit.Sketch(p, "Front Plane", sm => sm.CreateCircleByRadius(0, 0, 0, 8 * MM));
        Kit.Extrude(p, sk, 50 * MM);
        ((PartDoc)p).SetMaterialPropertyName2("", "SOLIDWORKS Materials", "AISI 1045 Steel, cold drawn");
        Kit.Save(p, path);
    }

    static void Interference(string dir, string hub, string shaft)
    {
        // AddComponent5 places the bounding-box centre, not the part origin. Offset 5 mm radially: a known overlap.
        var m = Kit.NewAsm();
        var ad = (AssemblyDoc)m;
        int cfg = (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig;
        var c1 = ad.AddComponent5(hub, cfg, "", false, "", 0, 0, 0);
        var c2 = ad.AddComponent5(shaft, cfg, "", false, "", 5 * MM, 0, 0);
        m.EditRebuild3();
        var idm = ad.InterferenceDetectionManager;
        idm.TreatCoincidenceAsInterference = false;
        idm.TreatSubAssembliesAsComponents = false;
        idm.IncludeMultibodyPartInterferences = false;
        idm.UseTransform = false;
        int n = idm.GetInterferenceCount();
        double vol = 0;
        if (n > 0) foreach (IInterference i in (object[])idm.GetInterferences()) vol += i.Volume * 1e9;
        idm.Done();
        Yes("interference control found a clash", n > 0);
        double r = 8, d = 5;
        double lens = 2 * r * r * Math.Acos(d / (2 * r)) - (d / 2) * Math.Sqrt(4 * r * r - d * d);
        Near("overlap volume mm3", vol, (Math.PI * r * r - lens) * 12, 0.005);
        sw_close(m);
    }

    static void sw_close(ModelDoc2 m) { Kit.Sw.CloseDoc(m.GetTitle()); }

    static void Drawing(string dir, string hub)
    {
        int a3 = (int)swDwgPaperSizes_e.swDwgPaperA3size;
        var d = Kit.NewDrawing(a3);
        var dd = (DrawingDoc)d;
        var sheet = (Sheet)dd.GetCurrentSheet();
        Yes("A3 BSI sheet format", dd.SetupSheet5(sheet.GetName(), a3, (int)swDwgTemplates_e.swDwgTemplateCustom, 1, 1, true,
            Kit.SheetFormat("a3 - bsi.slddrt"), 0.42, 0.297, "Default", false));
        Yes("first-angle views", dd.Create1stAngleViews(hub));
        Kit.Save(d, Path.Combine(dir, "Hub.SLDDRW"));
        Kit.Save(d, Path.Combine(dir, "Hub.pdf"));
        Kit.Save(d, Path.Combine(dir, "Hub.png"));
        Yes("PDF written", new FileInfo(Path.Combine(dir, "Hub.pdf")).Length > 1000);
        sw_close(d);
    }

    static void Fea(string dir)
    {
        // 10 x 10 x 100 mm steel bar fixed at z=0.
        var p = Kit.NewPart();
        var sk = Kit.Sketch(p, "Front Plane", sm => sm.CreateCenterRectangle(0, 0, 0, 5 * MM, 5 * MM, 0));
        Kit.Extrude(p, sk, 100 * MM);
        ((PartDoc)p).SetMaterialPropertyName2("", "SOLIDWORKS Materials", "AISI 1045 Steel, cold drawn");
        Kit.Sel(p, "Front Plane", "PLANE");
        Kit.Sel(p, "Top Plane", "PLANE", true);
        p.InsertAxis2(true);
        var axis = Kit.Last(p, "RefAxis");
        Kit.Save(p, Path.Combine(dir, "Bar.SLDPRT"));

        var cw = Sim.Connect();
        var body = Kit.Body(p);
        Face2 root = Kit.ZFace(body, false), end = Kit.ZFace(body, true);

        var t = Sim.NewStatic(cw, "Tension");
        Sim.Fix(t, root);
        Sim.Pressure(t, end, -1e7);     // 10 MPa tension
        Sim.MeshAndRun(t, 2.5);
        Near("bar end displacement mm", Sim.MaxDispMm(t), 10e6 / 205e9 * 0.1 * 1000, 0.01);
        var mid = Sim.Nodes(t).Where(n => Math.Abs(n.Z - 0.05) < 0.003).Select(n => n.SZ).ToList();
        Near("bar mid-section stress MPa (positive is tension)", mid.Average(), 10, 0.01);

        var s = Sim.NewStatic(cw, "Spin");
        Sim.Fix(s, root);
        Sim.Spin(s, axis, 1000.0);
        Sim.MeshAndRun(s, 2.5);
        var ms = Sim.Nodes(s).Where(n => Math.Abs(n.Z - 0.05) < 0.003).Select(n => n.SZ).ToList();
        Near("spinning bar mid-section stress MPa", ms.Average(), 7850 * 1e6 * (0.01 - 0.0025) / 2 / 1e6, 0.01);
        sw_close(p);
    }

    static void Motion(string dir, string hub, string shaft)
    {
        Mot.LoadAddIn();
        var h = Kit.Open(hub);
        var sh = Kit.Open(shaft);
        var m = Kit.NewAsm();
        var ad = (AssemblyDoc)m;
        int cfg = (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig;
        var c1 = ad.AddComponent5(hub, cfg, "", false, "", 0, 0, 0);
        var c2 = ad.AddComponent5(shaft, cfg, "", false, "", 0, 0, 0.04);
        var b1 = (Body2)c1.GetBody();
        var b2 = (Body2)c2.GetBody();
        int err = 0;
        m.ClearSelection2(true);
        ((Entity)Kit.CylFace(b1, 8 * MM)).Select4(false, null);
        ((Entity)Kit.CylFace(b2, 8 * MM)).Select4(true, null);
        ad.AddMate5((int)swMateType_e.swMateCONCENTRIC, (int)swMateAlign_e.swMateAlignALIGNED, false, 0, 0, 0, 0, 0, 0, 0, 0, false, false, 0, out err);
        Yes("mate added (err 1 means no error)", err == 1);
        m.ClearSelection2(true);
        m.EditRebuild3();
        Kit.Save(m, Path.Combine(dir, "SpringMass.SLDASM"));

        double mass = Kit.Mass(sh), k = 1000.0;
        var ms = Mot.Study(m, 0.3, 0.0002, 1000);
        Mot.Gravity(ms, 2);
        Mot.Spring(ms, Kit.ZFace(b1, true), Kit.ZFace(b2, true), k, 0.060);
        Mot.RotaryRamp(ms, Kit.CylFace(b2, 8 * MM), "6000*TIME");
        Yes("motion calculate", ms.Calculate());

        string line = Path.Combine(dir, "timeline.csv");
        Mot.TimelineCsv(ms, c2, line, 0.3, 0.002);
        var rows = File.ReadAllLines(line).Skip(1).Select(l => l.Split(',').Select(double.Parse).ToArray()).ToList();
        var t = rows.Select(r => r[0]).ToList();
        var z = rows.Select(r => r[3] * 1000).ToList();
        var peaks = Enumerable.Range(1, z.Count - 2).Where(i => z[i] > z[i - 1] && z[i] >= z[i + 1]).Select(i => t[i]).ToList();
        Near("spring period s", (peaks.Last() - peaks.First()) / (peaks.Count - 1), 2 * Math.PI * Math.Sqrt(mass / k), 0.02);
        Near("centre of oscillation mm", (z.Min() + z.Max()) / 2, z[0] + 1.0 - mass * 9.80665 / k * 1000, 0.002);

        string csv = Path.Combine(dir, "omega.csv");
        Mot.PlotCsv(ms, Kit.CylFace(b2, 8 * MM), swMotionPlotAxisType_e.swMotionPlotAxisType_ANGULAR_VELOCITY,
            swMotionPlotAxisComponent_e.swMotionPlotAxisComponent_MAGNITUDE, csv);
        var last = File.ReadAllLines(csv).Last().Replace("\"", "").Split(',').Select(double.Parse).ToArray();
        Near("motor speed at end deg/s", last[1], 6000 * last[0], 0.01);

        Kit.Save(m, Path.Combine(dir, "SpringMass.SLDASM"));
        sw_close(m);
        sw_close(sh);
        sw_close(h);
    }
}

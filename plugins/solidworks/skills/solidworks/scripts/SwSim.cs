// Simulation (FEA) helpers. Compiled in by: swrun.ps1 -Sim
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.cosworks;

public static class Sim
{
    // Results below are requested in N/mm2 (MPa) and mm.
    public const int MPA = (int)swsStrengthUnit_e.swsStrengthUnitNewtonPerSquareMillimeter;
    public const int MMU = (int)swsLinearUnit_e.swsLinearUnitMillimeters;

    // A second program in the same SOLIDWORKS session faults on GetAddInObject unless the add-in is reloaded first.
    public static CosmosWorks Connect()
    {
        string dll = Path.Combine(Kit.Sw.GetExecutablePath(), @"Simulation\cosworks.dll");
        Kit.Sw.UnloadAddIn(dll);
        Kit.Sw.LoadAddIn(dll);
        return ((CwAddincallback)Kit.Sw.GetAddInObject("SldWorks.Simulation")).CosmosWorks;
    }

    public static CWStudy NewStatic(CosmosWorks cw, string name)
    {
        int err;
        var mgr = cw.ActiveDoc.StudyManager;
        var s = mgr.CreateNewStudy3(name, (int)swsAnalysisStudyType_e.swsAnalysisStudyTypeStatic, 0, out err);
        if (s == null) throw new Exception("Study failed err=" + err);
        mgr.ActiveStudy = mgr.StudyCount - 1;
        return s;
    }

    // Immovable returned error 15 on a solid. Fixed works.
    public static void Fix(CWStudy s, object entity)
    {
        int err;
        var r = s.LoadsAndRestraintsManager.AddRestraint((int)swsRestraintType_e.swsRestraintTypeFixed, new object[] { entity }, null, out err);
        if (r == null) throw new Exception("Restraint failed err=" + err);
    }

    // value in Pa. Positive pushes into the face (compression). Negative pulls (tension).
    // Unit stays 0 (Pa): setting the N/mm2 unit was ignored and the number was applied as Pa, a million times too small.
    // ReverseDirection and ReverseDirection2 had no effect, so the sign of the value is what sets direction.
    public static void Pressure(CWStudy s, object face, double pascals)
    {
        int err;
        var p = s.LoadsAndRestraintsManager.AddPressure((int)swsPressureType_e.swsPressureTypeNormal, new object[] { face }, null, out err);
        if (p == null) throw new Exception("Pressure failed err=" + err);
        p.PressureBeginEdit();
        p.Unit = 0;
        p.Value = pascals;
        int e = p.PressureEndEdit();
        if (e != 0 || p.Value != pascals) throw new Exception("Pressure edit failed " + e);
    }

    // axis: a reference axis Feature or a straight edge. omega in rad/s (unit 0).
    public static void Spin(CWStudy s, object axis, double radPerSec)
    {
        int err;
        var c = s.LoadsAndRestraintsManager.AddCentrifugalForce(axis, out err);
        if (c == null) throw new Exception("Centrifugal failed err=" + err);
        c.CFForceBeginEdit();
        c.AngularVelocity = radPerSec;
        int e = c.CFForceEndEdit();
        if (e != 0) throw new Exception("Centrifugal edit failed " + e);
    }

    public static void MeshAndRun(CWStudy s, double sizeMm)
    {
        s.Mesh.Quality = (int)swsMeshQuality_e.swsMeshQualityHigh;
        int m = s.CreateMesh(MMU, sizeMm, sizeMm * 0.05);
        if (m != 0) throw new Exception("Mesh failed err=" + m);
        int r = s.RunAnalysis();
        // 30 = swsRunAnalysisErrorInvalidLBC: loads or restraints are missing or invalid.
        if (r != 0) throw new Exception("Run failed err=" + r + " " + Enum.GetName(typeof(swsRunAnalysisError_e), r));
    }

    // GetMinMax* return [node, min, node, max].
    public static double MaxVon(CWStudy s)
    {
        int err;
        var a = (object[])s.Results.GetMinMaxStress((int)swsStressComponent_e.swsStressComponentVON, 0, 1, null, MPA, out err);
        return Convert.ToDouble(a[3]);
    }

    public static double MaxDispMm(CWStudy s)
    {
        int err;
        var a = (object[])s.Results.GetMinMaxDisplacement((int)swsDisplacementComponent_e.swsDisplacementComponentURES, 1, null, MMU, out err);
        return Convert.ToDouble(a[3]);
    }

    public static double MinFos(CWStudy s)
    {
        int err;
        var a = (object[])s.Results.GetMinMaxFactorOfSafety(true, null, 0, 0, out err);
        return Convert.ToDouble(a[0]);
    }

    // One row per node: id, SX, SY, SZ, TXY, TXZ, TYZ, P1, P2, P3, VON, INT, plus location in metres.
    public class NodeRow { public int Id; public double X, Y, Z, SX, SY, SZ, Von, Intensity; }

    public static List<NodeRow> Nodes(CWStudy s)
    {
        int err;
        var a = (object[])s.Results.GetStress(0, 1, null, MPA, out err);
        int n = s.Mesh.NodeCount, w = a.Length / n;
        var rows = new List<NodeRow>(n);
        for (int i = 0; i < n; i++)
        {
            var r = new NodeRow { Id = Convert.ToInt32(a[i * w]) };
            s.Mesh.GetNodeLocation(r.Id, out r.X, out r.Y, out r.Z);
            r.SX = Convert.ToDouble(a[i * w + 1]);
            r.SY = Convert.ToDouble(a[i * w + 2]);
            r.SZ = Convert.ToDouble(a[i * w + 3]);
            r.Von = Convert.ToDouble(a[i * w + 10]);        // not the last column: that is stress intensity
            r.Intensity = Convert.ToDouble(a[i * w + 11]);
            rows.Add(r);
        }
        return rows;
    }

    // Peaks sit on restraint corners and move with the mesh. A high percentile is steadier.
    public static double VonPercentile(CWStudy s, double fraction)
    {
        var v = Nodes(s).Select(r => r.Von).OrderBy(x => x).ToList();
        return v[(int)(fraction * (v.Count - 1))];
    }
}

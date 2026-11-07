// Motion Analysis helpers. Compiled in by: swrun.ps1 -Motion
using System;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swmotionstudy;

public static class Mot
{
    public const int KIND = (int)swMotionStudyType_e.swMotionStudyTypeCosmosMotion;

    public static void LoadAddIn()
    {
        // 0 = loaded, 2 = already loaded.
        Kit.Sw.LoadAddIn(Path.Combine(Kit.Sw.GetExecutablePath(), "cmotionsw.dll"));
    }

    public static MotionStudy Study(ModelDoc2 asm, double seconds, double maxStep, int frameRate)
    {
        var msm = (MotionStudyManager)asm.Extension.GetMotionStudyManager();
        var ms = msm.GetMotionStudy((string)((object[])msm.GetMotionStudyNames())[0]);
        ms.Activate();
        ms.StudyType = KIND;
        ms.SetDuration(seconds);
        var p = (CosmosMotionStudyProperties)ms.GetProperties(KIND);
        p.MaxStepSize = maxStep;
        ((MotionStudyProperties)ms.GetProperties(KIND)).SetFrameRate(frameRate);
        return ms;
    }

    // Default strength 9.80665 m/s2. Axis 2 acted along -Z in testing.
    public static void Gravity(MotionStudy ms, int axis)
    {
        var g = (SimulationGravityFeatureData)ms.CreateDefinition((int)swFeatureNameID_e.swFmAEMGravity);
        g.Axis = axis;
        Need(ms.CreateFeature(g), "gravity");
    }

    // k in N/m, free length in m. swFmAEMLinearMotionSpring does not cast to this type; swFmAEMLinearSpring does.
    public static void Spring(MotionStudy ms, object end1, object end2, double k, double freeLength)
    {
        var s = (SimulationLinearSpringFeatureData)ms.CreateDefinition((int)swFeatureNameID_e.swFmAEMLinearSpring);
        s.SetEndPoints(end1, end2);
        s.SpringConstant = k;
        s.FreeLength = freeLength;
        Need(ms.CreateFeature(s), "spring");
    }

    // expression in deg/s, for example "6000*TIME" is 1000 rpm/s. face: a cylinder on the driven part.
    public static void RotaryRamp(MotionStudy ms, object face, string expression)
    {
        var m = (SimulationMotorFeatureData)ms.CreateDefinition((int)swFeatureNameID_e.swFmAEMRotationalMotor);
        m.Location = face;
        m.DirectionReference = face;
        m.MotionType = (int)swSimulationMotorMotionType_e.swSimulationMotorMotion_Function;
        m.Expression = expression;
        Need(ms.CreateFeature(m), "motor");
    }

    public static void Contact(MotionStudy ms, Component2 a, Component2 b, double stiffness, double exponent, double damping,
        double penetration, double muStatic, double muDynamic, double vStatic, double vDynamic)
    {
        var c = (Simulation3DContactFeatureData)ms.CreateDefinition((int)swFeatureNameID_e.swFmAEM3DContact);
        c.ContactComponents = new DispatchWrapper[] { new DispatchWrapper(a), new DispatchWrapper(b) };   // wrapper is required
        c.SpecifyMaterial = false;
        c.UseRestitutionCoefficient = false;
        c.Stiffness = stiffness;
        c.Exponent = exponent;
        c.MaxDamping = damping;
        c.Penetration = penetration;
        c.FrictionOption = 1;
        c.StaticFrictionCoefficient = muStatic;
        c.DynamicFrictionCoefficient = muDynamic;
        c.StaticFrictionVelocity = vStatic;
        c.DynamicFrictionVelocity = vDynamic;
        Need(ms.CreateFeature(c), "contact");
    }

    // Plot y against time and export it. The face must be wrapped in a DispatchWrapper or no plot is made.
    // GetValues returned empty axes in testing, so data comes out through the CSV.
    public static void PlotCsv(MotionStudy ms, object face, swMotionPlotAxisType_e type, swMotionPlotAxisComponent_e comp, string csv)
    {
        var res = (CosmosMotionStudyResults)ms.GetResults(KIND);
        var pd = (MotionPlotFeatureData)res.CreatePlotFeatureData();
        var x = (MotionPlotAxisFeatureData)res.CreatePlotXAxisFeatureData();
        var y = (MotionPlotAxisFeatureData)res.CreatePlotYAxisFeatureData();
        x.Type = (int)swMotionPlotAxisType_e.swMotionPlotAxisType_XAXISTIME;
        y.Type = (int)type;
        y.Component = (int)comp;
        y.Entities = new DispatchWrapper[] { new DispatchWrapper(face) };
        var f = res.InsertPlotFeature(pd, x, y);
        Need(f, "plot " + type);
        if (!res.ExportCSVFile(f, Path.GetFullPath(csv))) throw new Exception("CSV export failed: " + csv);
    }

    // No plot needed: after Calculate, step the timeline and read the part position (metres).
    public static void TimelineCsv(MotionStudy ms, Component2 c, string csv, double seconds, double dt)
    {
        using (var w = new StreamWriter(Path.GetFullPath(csv)))
        {
            w.WriteLine("t,tx,ty,tz");
            int n = (int)Math.Round(seconds / dt);
            for (int i = 0; i <= n; i++)
            {
                ms.SetTime(i * dt);
                var d = (double[])c.Transform2.ArrayData;
                w.WriteLine((i * dt) + "," + d[9] + "," + d[10] + "," + d[11]);
            }
        }
    }

    static void Need(object o, string what)
    {
        if (o == null) throw new Exception("Motion feature failed: " + what);
    }
}

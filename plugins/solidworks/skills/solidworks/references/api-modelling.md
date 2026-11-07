# Parts: sketches, features, materials

**Tested** means run on SOLIDWORKS 2023 SP5 with the result checked by hand. **Not tested** means general knowledge: try it on a scratch model first.

## Tested

### Sketching

`Kit.Sketch` selects a plane, opens a sketch, runs your drawing code and closes it. Coordinates are in the sketch's own plane, in metres.

```csharp
var sk = Kit.Sketch(p, "Front Plane", sm => sm.CreateCircleByRadius(0, 0, 0, 30 * Kit.MM));
var sk2 = Kit.Sketch(p, "Front Plane", sm => sm.CreateCenterRectangle(0, 0, 0, 5 * Kit.MM, 5 * Kit.MM, 0));
```

The three default planes are `Front Plane`, `Top Plane` and `Right Plane`.

When adding many lines, set `sm.AddToDB = true` first and back to `false` after. This stops the entities snapping to each other or picking up relations. It is how the revolve profile below was drawn.

### Named dimensions

Named dimensions let equations and configurations find them later. Run inside `Kit.NoDimPopup`, or the dimension dialog blocks the program.

```csharp
var circ = (SketchSegment)sm.CreateCircleByRadius(0, 0, 0, 30 * Kit.MM);
circ.Select4(false, null);
var dd = (DisplayDimension)p.AddDimension2(40 * Kit.MM, 40 * Kit.MM, 0);   // where the text sits
dd.GetDimension2(0).Name = "OD";
```

Read one back later with `(Dimension)p.Parameter("OD@Sketch1")`. A sketch circle with no dimension has nothing to find, so `Parameter` returns null.

### Features

```csharp
Kit.Extrude(p, sk, 12 * Kit.MM);                       // blind, grows along +Z from Front Plane
Kit.CutThrough(p, sk2);                                // tries both directions
```

Revolve. Select the sketch, which must contain a centreline:

```csharp
sm.CreateCenterLine(0, 0, 0, 0, 50 * Kit.MM, 0);       // inside the sketch
sk.Select2(false, 0);
p.FeatureManager.FeatureRevolve2(true, true, false, false, false, false, 0, 0, 2 * Math.PI, 0,
    false, false, 0, 0, 0, 0, 0, true, true, true);
```

`FeatureCut4` flags: `Flip` (second) swaps which side of the profile is removed, and `Dir` (third) reverses the direction. To reverse a cut, change `Dir`. Setting `Flip` by mistake returned null in testing.

Circular pattern. Axis takes selection mark 1, the feature takes mark 4:

```csharp
Kit.Sel(p, "Top Plane", "PLANE");
Kit.Sel(p, "Right Plane", "PLANE", true);
p.InsertAxis2(true);                                   // axis from two planes
var axis = Kit.Last(p, "RefAxis");
p.ClearSelection2(true);
Kit.Sel(p, axis.Name, "AXIS", false, 1);
cut.Select2(true, 4);
p.FeatureManager.FeatureCircularPattern5(6, 2 * Math.PI, false, "NULL", false, true, false, false, false, false, 1, 0, "NULL", false);
```

Fillet. Find edges by geometry, because edge names are not stable. `CircleParams[6]` is the radius:

```csharp
foreach (Edge e in (object[])body.GetEdges())
{
    var cv = (Curve)e.GetCurve();
    if (cv.IsCircle() && Math.Abs(((double[])cv.CircleParams)[6] - 30 * Kit.MM) < 1e-6)
        ((Entity)e).Select4(true, null);
}
p.FeatureManager.FeatureFillet3(195, 2 * Kit.MM, 0, 0, 0, 0, 0, null, null, null, null, null, null, null);
```

Selecting a face the same way: `Kit.CylFace(body, radius)`, `Kit.ZFace(body, high)`. For a face in another direction, copy `ZFace` and change the normal index in `PlaneParams`.

### Material

```csharp
((PartDoc)p).SetMaterialPropertyName2("", "SOLIDWORKS Materials", "AISI 1045 Steel, cold drawn");
```

Use the exact library name, then confirm by dividing mass by volume: this one gave 7850 kg/m³, the right density for steel.

### Equations

```csharp
var eq = p.GetEquationMgr();
eq.Add2(-1, "\"THK\" = 12", true);                     // global variable
eq.Add2(-1, "\"D1@Boss-Extrude1\" = \"THK\"", true);   // drives the extrude depth
```

### Configurations

```csharp
p.ConfigurationManager.AddConfiguration2("Large", "", "", 0, "", "", true);    // becomes active
((Dimension)p.Parameter("OD@Sketch1")).SetSystemValue3(70 * Kit.MM,
    (int)swSetValueInConfiguration_e.swSetValue_InThisConfiguration, null);
p.ForceRebuild3(false);
p.ShowConfiguration2("Default");
```

Check by volume in each configuration: in testing the two differed by the expected amount.

### Measuring and checking

`Kit.Mass`, `Kit.VolMm3`, and `m.Extension.CreateMassProperty().CenterOfMass` (metres, world coordinates). `Kit.Check` rebuilds and lists any feature with an error code. Run it before saving.

## Not tested

Design tables through the API (`InsertFamilyTableNew`, needs Excel), Hole Wizard, cosmetic threads, helix and sweep, lofts, sheet metal, weldments, surfaces, Toolbox. The interface steps for design tables are in `gui-guide.md`.

## Practice worth following

- Fully define sketches. An under-defined sketch can shift when a dimension changes, and rebuilds then give different geometry.
- Put driving sizes in global variables, so a design change is one edit.
- Name features and dimensions for what they are. `Spring_Pocket` beats `Cut-Extrude7` when a model is reopened months later.
- Sketch on planes where you can. Sketching on model faces ties later features to earlier ones and breaks more easily.
- Check mass and centre of mass against the design calculation after each major change, not only at the end.

# Simulation (FEA)

Compile with `-Sim`. Helpers are in `scripts/SwSim.cs`. **Tested** means solved on SOLIDWORKS 2023 SP5 and matched to a hand value.

## Validated against hand calculations

| Case | SOLIDWORKS | Hand | Difference |
|---|---|---|---|
| 10 × 10 × 100 mm steel bar, 10 MPa tension, end displacement | 0.004865 mm | 0.004878 mm | -0.3% |
| Same bar, stress at mid-length | 10.0 MPa | 10 MPa | 0% |
| Same bar spun at 1000 rad/s about the root, mean stress at mid-length | 29.41 MPa | 29.44 MPa | -0.1% |
| Factor of safety at 169.6 MPa peak | 3.125 | 530 / 169.6 = 3.125 | 0% |

Before modelling anything hard, a case like this tells you the set-up is right.

## Standard flow

```csharp
var cw = Sim.Connect();                  // part must be the active document
var body = Kit.Body(p);
var s = Sim.NewStatic(cw, "Tension");
Sim.Fix(s, Kit.ZFace(body, false));
Sim.Pressure(s, Kit.ZFace(body, true), -1e7);    // 10 MPa tension
Sim.MeshAndRun(s, 2.5);                  // element size in mm
Console.WriteLine(Sim.MaxDispMm(s));
```

If the part is already open behind another document, activate it first: `sw.ActivateDoc3(p.GetTitle(), false, 0, 0)`.

## Traps found in testing

**Pressure unit is ignored.** Setting `Unit` to N/mm² and `Value` to 10 applied 10 Pa. The solve succeeded and every stress was a million times too small. Leave `Unit` at 0 and give the value in pascals. `Sim.Pressure` does this.

**Pressure direction comes from the sign.** `ReverseDirection` and `ReverseDirection2` read back as set but changed nothing. Positive pushes into the face (compression). Negative pulls (tension).

**Use `swsRestraintTypeFixed`.** `Immovable` on a solid returned null with error 15, and the run then failed with error 30.

**Reload the add-in in every program.** A second program in the same session faults on `GetAddInObject` with `RPC_E_SERVERFAULT`. `UnloadAddIn` then `LoadAddIn` clears it. `Sim.Connect` does this.

**Set the active study** when a part has several: `mgr.ActiveStudy = mgr.StudyCount - 1`. `Sim.NewStatic` does this.

**Run error 30** is `swsRunAnalysisErrorInvalidLBC`: loads or restraints are missing or invalid. Check the object returned by each `Add` call, because a failure there is only reported through its `out` error value.

## Reading stress

`GetMinMaxStress` and `GetMinMaxDisplacement` return `[node, min, node, max]`.

`Sim.Nodes(study)` gives every node. The raw `GetStress(0, 1, null, unit, out err)` array has 12 columns per node:

`id, SX, SY, SZ, TXY, TXZ, TYZ, P1, P2, P3, VON, INT`

The last column is **stress intensity** (P1 - P3), not von Mises. Reading it as von Mises gave 55.9 MPa where the true value was 52.3 MPa. Node locations are in metres from `Mesh.GetNodeLocation`.

## Peaks are not the answer

In the tension bar, peak SZ was 17.2 MPa beside the fixed end, on a true 10 MPa. The fixed face stops the bar contracting sideways, which makes a local spike that grows as the mesh gets finer. It is a modelling artefact, not a real stress.

So for any model with sharp restraints:

- Report a **section mean** where a hand value exists: filter `Sim.Nodes` by position and average.
- Report a **high percentile** alongside the peak: `Sim.VonPercentile(s, 0.999)`.
- Run a **convergence study**: solve at several element sizes and tabulate displacement, percentile stress and peak. Displacement and percentile should settle. A peak that keeps moving is a singularity, and the report should say so.

## Rotation

```csharp
Kit.Sel(p, "Front Plane", "PLANE");
Kit.Sel(p, "Top Plane", "PLANE", true);
p.InsertAxis2(true);
var axis = Kit.Last(p, "RefAxis");
Sim.Spin(s, axis, 1000.0);               // rad/s
```

The axis object is the feature itself. Place it deliberately: spinning about a straight edge offset from the centreline added bending, and the peak read 170 MPa instead of about 40.

## Not tested

Force and torque (`AddForce3`), gravity (`AddGravity`), contact sets between bodies, assembly studies, thermal, frequency, buckling, fatigue, mesh controls, plot images, report generation. Add them on a case with a hand answer.

## Choosing a set-up

General guidance, apart from the yield figures.

- Restrain against what really holds the part, not whatever is convenient.
- Check reactions equal the applied load. Support reactions near zero on stabilising restraints confirm equilibrium.
- Assign the real material and state whether yield is hot-rolled or cold-drawn. The library's cold-drawn 1045 yields at 530 MPa, against about 310 MPa hot-rolled, which nearly doubles a factor of safety.
- Linear static assumes small strain and stress below yield. If results exceed yield, the result is not valid and the answer is not simply the number shown.

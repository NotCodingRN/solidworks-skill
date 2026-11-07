# Assemblies, drawings, exports

**Tested** means run on SOLIDWORKS 2023 SP5 with the result checked. **Not tested** means try it on a scratch model first.

## Assemblies (tested)

The part must be open before it is added, so open it first and keep it open until the assembly closes.

```csharp
var hub = Kit.Open(hubPath);
var m = Kit.NewAsm();
var ad = (AssemblyDoc)m;
int cfg = (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig;
var c1 = ad.AddComponent5(hubPath, cfg, "", false, "", 0, 0, 0);
var c2 = ad.AddComponent5(shaftPath, cfg, "", false, "", 0, 0, 0.04);
m.EditRebuild3();
```

- X, Y, Z place the **centre of the part's bounding box, not its origin**. A 12 mm disc added at 0 sat at -6 mm. Read `c.Transform2.ArrayData` to see the real position: items 9, 10 and 11 are the translation in metres.
- The first component added comes in **fixed**. Check with `c.IsFixed()`.
- The tested install shipped only MBD assembly templates. `Kit.Template` finds whichever exist. Units are forced to millimetres.

### Mates

Select with component faces, taken from `c.GetBody()`, so they are in assembly context:

```csharp
int err = 0;
var b1 = (Body2)c1.GetBody();
var b2 = (Body2)c2.GetBody();
m.ClearSelection2(true);
((Entity)Kit.CylFace(b1, 8 * Kit.MM)).Select4(false, null);
((Entity)Kit.CylFace(b2, 8 * Kit.MM)).Select4(true, null);
var mate = ad.AddMate5((int)swMateType_e.swMateCONCENTRIC, (int)swMateAlign_e.swMateAlignALIGNED,
    false, 0, 0, 0, 0, 0, 0, 0, 0, false, false, 0, out err);
```

`err == 1` is `swAddMateError_NoError`. Other values are failures.

Component planes work too. The selection name has the form `Front Plane@Hub-1@Assem1`, and the last part is the assembly title without its extension.

Distance mate: `swMateDISTANCE`, with the distance repeated in the next three arguments (`20 * MM, 20 * MM, 20 * MM`).

To move a mated part, change its mates. Setting `Transform2` on a mated component did not produce the expected position change in testing, even with one mate suppressed.

### Interference

```csharp
var idm = ad.InterferenceDetectionManager;
idm.TreatCoincidenceAsInterference = false;
idm.TreatSubAssembliesAsComponents = false;
idm.IncludeMultibodyPartInterferences = false;
idm.UseTransform = false;
int n = idm.GetInterferenceCount();
foreach (IInterference i in (object[])idm.GetInterferences())
    Console.WriteLine(i.Volume * 1e9);   // mm3
idm.Done();
```

This reported 944.1 mm³ for an overlap of 944.1 mm³ by hand. Two warnings:

- **Do not use `ToolsCheckInterference2`.** It crashed the call with `RPC_E_SERVERFAULT`.
- A count of zero is only meaningful once a deliberate overlap has been caught. Do this at least once per model type.

Check every position that matters: retracted, engaged and any extreme. A clash can exist only at one end of travel.

## Drawings (tested)

```csharp
int a3 = (int)swDwgPaperSizes_e.swDwgPaperA3size;
var d = Kit.NewDrawing(a3);                // forced to MMGS
var dd = (DrawingDoc)d;
var sheet = (Sheet)dd.GetCurrentSheet();
dd.SetupSheet5(sheet.GetName(), a3, (int)swDwgTemplates_e.swDwgTemplateCustom, 1, 1,
    true,                                  // true = first angle, false = third angle
    Kit.SheetFormat("a3 - bsi.slddrt"), 0.42, 0.297, "Default", false);
dd.Create1stAngleViews(partPath);
dd.InsertModelAnnotations3((int)swImportModelItemsSource_e.swImportModelItemsFromEntireModel,
    (int)swInsertAnnotation_e.swInsertDimensionsMarkedForDrawing, true, false, false, false);
d.ForceRebuild3(false);
```

- Without `Kit.Mmgs` the dimensions came out in **inches** (Ø2.36 for a 60 mm disc) with no warning.
- Without `SetupSheet5` the sheet has no title block.
- `InsertModelAnnotations3` brings in only dimensions that exist in the model. Undimensioned sketches give gaps: in testing a hole with no sketch dimension got no callout.
- Formats in `ProgramData\SolidWorks\SOLIDWORKS <year>\lang\english\sheetformat` include `a3 - bsi`, `a3 - iso`, `a3 - din`, `a3 - jis` and `a3 - landscape`.

Always save a PNG and read it with the Read tool. It shows units, title block and missing views.

## Exports (tested)

`Kit.Save` picks the format from the extension:

| From | Extensions |
|---|---|
| Part or assembly | `.STEP` |
| Drawing | `.pdf`, `.dxf`, `.png` |
| Native | `.SLDPRT`, `.SLDASM`, `.SLDDRW` |

## Not tested

Section views (`CreateSectionViewAt5`), detail views, BOM tables, balloons, tolerances and GD&T, custom property links into title blocks, exploded views, Pack and Go. A good drawing still needs a person to place dimensions, so expect the import to be a starting point. Tell the user so.

---
name: solidworks
description: "Work with SOLIDWORKS end to end: drive it directly on Windows through its API to build and inspect parts, assemblies and drawings, run Simulation FEA and Motion studies, and export data, or guide a person step by step through the interface. Use this whenever the user mentions SOLIDWORKS, SolidWorks, .SLDPRT, .SLDASM or .SLDDRW files, sketches, mates, design tables, configurations, FEA, von Mises stress, factor of safety, mesh convergence, Motion Analysis, rotary motors, contact, or mechanical CAD work such as a clutch, gearbox or bracket, even if they only say \"model this\", \"check this part\" or \"why won't this mate\"."
---

# SOLIDWORKS

Two ways to help, and most requests need one of them:

- **Drive it.** If SOLIDWORKS is installed on this Windows machine, it can be fully controlled from compiled C#. Use this when the user wants something built, measured, simulated or exported.
- **Guide it.** Use `references/gui-guide.md` when the user wants to learn, is doing the clicking, or is working on a computer you cannot reach.

Ask only if the request could go either way and it changes the work. "Model a hub" means drive. "How do I add a rotary motor?" means guide.

## Rules that protect the user's work

1. **Run `status.cs` first.** It lists every open document and whether it has unsaved changes. The user may be working in the same session.
2. **Work on copies.** Copy source files to a scratch folder before opening them. Save results under new names. Opening and saving an old file upgrades it, and that cannot be undone: a file saved in a newer version will not open in an older one.
3. **Close only what you opened**, by title with `CloseDoc`. Never use `CloseAllDocuments`, because it can discard the user's unsaved work. Only call `ExitApp` if you started SOLIDWORKS and `status.cs` shows nothing but your own files.
4. **Restore settings you change.** `Kit.NoDimPopup` does this for the dimension popup. Follow that pattern for any other preference.
5. **Never write a licence key or activation code** into a file, script or reply, even if you find one in the project.

## Rules that protect the results

SOLIDWORKS reports success on wrong answers. In testing, a load was applied a million times too small with no error, and a drawing came out in inches with no warning. So:

- **Check every result against a hand calculation** before reporting it. If you cannot estimate the answer, say so.
- **Prove a check can fail.** Zero interferences means nothing until a deliberate overlap has been detected. See `selftest.cs` for the pattern.
- **Read values back after setting them.** A property that accepts a value may not have used it.
- **Look at output.** Save drawings as PNG and read the image. File size and return codes do not show inches, missing title blocks or empty views.
- **Never report a number from a run that failed partway.** Say which step failed and what the error was.

Report a result with its hand value and the difference, for example "0.00486 mm against 0.00488 mm by hand, 0.3% low".

## How to run code

Write a recipe as a `.cs` file with the Write tool, not a shell heredoc, because C# strings break shell quoting. Use a scratch folder for recipes and output, and full paths everywhere. Then run it with the PowerShell tool:

```powershell
$s = "<base directory of this skill>\scripts"
powershell -NoProfile -ExecutionPolicy Bypass -File "$s\swrun.ps1" -Source "C:\full\path\recipe.cs" -Args2 "C:\full\path\work"
```

The base directory is shown when this skill loads. Add `-Sim` for FEA and `-Motion` for Motion. They compile in `SwSim.cs` and `SwMotion.cs`. Use a timeout of at least 5 minutes: SOLIDWORKS takes about a minute to start, and a study takes longer.

A minimal recipe:

```csharp
using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

class Recipe
{
    static void Main(string[] a)
    {
        var sw = Kit.Connect();
        var p = Kit.NewPart();                         // forced to MMGS
        var sk = Kit.Sketch(p, "Front Plane", sm => sm.CreateCircleByRadius(0, 0, 0, 30 * Kit.MM));
        Kit.Extrude(p, sk, 12 * Kit.MM);
        Console.WriteLine(Kit.VolMm3(p));              // check against pi r2 h
        Kit.Check(p, "disc");
        Kit.Save(p, System.IO.Path.Combine(a[0], "Disc.SLDPRT"));
        sw.CloseDoc(p.GetTitle());
    }
}
```

Three API facts catch almost everyone: lengths are **metres**, angles are **radians**, and `AddMate5` returns error code **1 for success**.

## Versions and licences

The kit was tested on SOLIDWORKS 2023 SP5, English install. It finds other years automatically, but behaviour can change between releases, so run `selftest.cs` once before trusting it on another version, and tell the user about any failures. Simulation and Motion need a licence that includes them. Premium has both. If one is missing, the self-test stops at that stage and the earlier results still stand.

## Ready-made recipes

| Recipe in `scripts/recipes/` | Use |
|---|---|
| `status.cs` | Version, licence, open documents. Read-only. Run first. |
| `inspect_model.cs <path>` | Mass, centre of mass, configurations, features, material, rebuild state. |
| `selftest.cs <empty folder>` (`-Sim -Motion`) | Builds small models and checks 19 results against hand values. Run after any SOLIDWORKS upgrade, or on a new machine, before trusting the kit. Also the best worked example of everything. |

## Where to look next

Read only the file the task needs.

| Task | Read |
|---|---|
| Sketches, features, patterns, fillets, materials, equations, configurations | `references/api-modelling.md` |
| Assemblies, mates, interference, drawings, exports | `references/api-assembly-drawing.md` |
| FEA: loads, mesh, stress results, factor of safety | `references/api-simulation.md` |
| Motion: motors, springs, contact, plots, CSV | `references/api-motion.md` |
| Teaching someone through the interface | `references/gui-guide.md` |
| Centrifugal clutch design, sizing and checks | `references/clutch-design.md` |

Each `api-*` file marks what has actually been run. If a feature is marked not tested, or is not listed, try it on a scratch model, check the result by hand, and tell the user it is new ground. Exact signatures can be read from `api\redist\SolidWorks.Interop.*.dll` in the install folder by reflection, which is faster and safer than guessing argument order. Help files are in `api\` too.

## When something fails

| Symptom | Cause and fix |
|---|---|
| `OpenDoc6` error 2 | Relative path. SOLIDWORKS resolves it from its own folder. Use a full path. |
| Feature call returns null | Nothing selected, or wrong direction. Check the selection, then see `Kit.CutThrough`. |
| Feature named `Cut-Extrude2` when you expected 1 | A failed call used up the number. Keep the returned `Feature` object and never assume a name. |
| `0x80010105` `RPC_E_SERVERFAULT` | SOLIDWORKS rejected the call. List documents to see what is left open. `ToolsCheckInterference2` and a second `GetAddInObject` both do this, and each has a replacement in the references. |
| `Environment` is ambiguous | Write `System.Environment`, because the interop has its own type of that name. |
| `dynamic` errors on a `GetModelDoc2()` result | Cast it: `(ModelDoc2)c.GetModelDoc2()`. |
| Dialog appears to hang the run | Open with `swOpenDocOptions_Silent` and tell the user a popup may need clicking. |

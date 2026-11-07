# SOLIDWORKS skill for Claude Code

Lets Claude Code work in SOLIDWORKS on your Windows PC: build and inspect parts, assemblies and drawings, run FEA and Motion studies, and export the results. It can also walk you through the interface step by step if you'd rather do the clicking yourself.

SOLIDWORKS often reports success on wrong answers, so the skill checks results against hand calculations before reporting them.

## Install

In a terminal:

```bash
claude plugin marketplace add NotCodingRN/solidworks-skill
```

```bash
claude plugin install solidworks@notcodingrn-solidworks
```

Or copy `plugins/solidworks/skills/solidworks` to `%USERPROFILE%\.claude\skills\solidworks`.

Then ask for what you need, for example "model a 60 mm flanged hub with six M5 holes" or "run FEA on this bracket and check it by hand". The skill loads by itself when you mention SOLIDWORKS or a related task.

## Requirements

- Windows with SOLIDWORKS installed and licensed. The API code runs locally, so nothing else needs installing.
- A licence that includes Simulation and Motion for those studies. Premium has both.
- Guidance through the interface works without SOLIDWORKS on the same machine.

## What it does

| Area | Covers |
|---|---|
| Parts | Sketches, extrudes, revolves, cuts, patterns, fillets, materials, equations, configurations |
| Assemblies | Components, mates, interference detection |
| Drawings | First-angle views, A3 title block, metric units, PDF, DXF, PNG and STEP export |
| FEA | Static studies, pressure, centrifugal load, stress, displacement, factor of safety |
| Motion | Motors, springs, gravity, contact, plots, CSV export |
| Interface help | Step-by-step guidance and common fixes |

## How far it has been tested

It was developed and tested on **SOLIDWORKS 2023 SP5, English install**. Other versions should work, but run the self-test before relying on them. It builds small models and compares 19 results with hand calculations:

```powershell
$s = "<skill folder>\scripts"
powershell -NoProfile -ExecutionPolicy Bypass -File "$s\swrun.ps1" -Source "$s\recipes\selftest.cs" -Args2 "C:\empty\scratch\folder" -Sim -Motion
```

| Check | SOLIDWORKS | Hand |
|---|---|---|
| Bar in tension, end displacement | 0.004865 mm | 0.004878 mm |
| Spinning bar, section stress | 29.41 MPa | 29.44 MPa |
| Deliberate clash, overlap volume | 944.1 mm³ | 944.1 mm³ |
| Spring-mass period | 0.056 s | 0.0558 s |
| Time to impact (separate test, not in the self-test) | 0.032 s | 0.0319 s |

**Not tested yet:** friction torque through contact measured against a hand value, design tables through the API, section views, bills of materials, sheet metal, weldments, surfaces and Toolbox. The reference files mark what has and hasn't been run. Menu names in the interface guide weren't checked click by click.

## Keeping your work safe

The skill lists open documents first, works on copies, closes only files it opened, and restores any setting it changes. Keep backups anyway, because a file saved in a newer SOLIDWORKS version won't open in an older one.

## Licence

MIT. See [LICENSE](LICENSE).

This is an independent project, not affiliated with or endorsed by Dassault Systèmes. SOLIDWORKS is a trademark of Dassault Systèmes. No SOLIDWORKS files are included, and you need your own licence.

# Motion Analysis

Compile with `-Motion`. Helpers are in `scripts/SwMotion.cs`. **Tested** means run on SOLIDWORKS 2023 SP5 and matched to a hand value.

## Validated against hand calculations

| Case | SOLIDWORKS | Hand |
|---|---|---|
| Spring-mass period, 0.0789 kg on 1000 N/m | 0.056 s | 0.0558 s |
| Centre of oscillation, spring offset 1 mm minus gravity sag 0.774 mm | 15.226 mm | 15.226 mm |
| Motor ramp `6000*TIME` at 0.3 s | 1800 deg/s | 1800 deg/s |
| First contact, part dropped 5 mm | 0.032 s | 0.0319 s |

## Standard flow

```csharp
Mot.LoadAddIn();
var ms = Mot.Study(asm, 0.3, 0.0002, 1000);   // duration s, max step s, frames per second
Mot.Gravity(ms, 2);                           // axis 2 acted along -Z
Mot.Spring(ms, faceA, faceB, 1000.0, 0.060);  // N/m and metres
Mot.RotaryRamp(ms, cylinderFace, "6000*TIME");
ms.Calculate();
Mot.TimelineCsv(ms, component, csvPath, 0.3, 0.002);
Kit.Save(asm, path);
```

Faces come from `c.GetBody()`, so they are in assembly context. The Motion add-in loads from `cmotionsw.dll` in the install folder. `LoadAddIn` returns 0 when loaded and 2 when already loaded.

The study type must be `swMotionStudyTypeCosmosMotion`. The default is plain Animation, which does no physics. `Mot.Study` sets it.

## Getting data out

Three ways were tried:

- **`Mot.TimelineCsv`**, which steps the study with `SetTime` and reads the part's transform, gives position in metres at any spacing. It needs no plot and is the most dependable way to get displacement. Only valid after `Calculate`. It reads the part origin, not its centre of mass.
- **`Mot.PlotCsv`** makes a real plot and exports a CSV. The header row is quoted and the file begins with the plot name, so skip the first two lines and strip quotes. Position plots are in mm, speed in deg/s.
- **`res.GetValues`** returned empty axes. Do not use it.

Plot targets must be wrapped, or no plot is made: `y.Entities = new DispatchWrapper[] { new DispatchWrapper(face) }`. A bare `object[]` or a component target returned null. Contact components need the same wrapper.

## Traps found in testing

**Spring type.** `swFmAEMLinearSpring` casts to `SimulationLinearSpringFeatureData`. `swFmAEMLinearMotionSpring` does not.

**Spring length** is measured between the chosen faces. If the free length differs from the current gap, the part starts displaced, which is useful for a test and a mistake otherwise.

**Rigid-body spring stress is not modelled.** Motion treats a spring as a force law. Spring stress is a separate hand calculation.

**Contact needs distrust after impact.** A part dropped 5 mm with stiffness 1e8 N/m, exponent 1.1, damping 2000 N·s/m and a 0.2 ms step hit at the correct time, then rebounded to about 34 mm: far more energy than it fell with. So:

- Contact **timing** was accurate. Rebound **height** was not.
- Plot a position trace after every contact model, and check nothing gains energy.
- Report contact force as a mean over a window, not as a peak. Single windows scatter.
- If bounce matters, tune damping, step size and contact settings against a hand case first, and state what was used.

Precise contact needs `UsePreciseContact = true` on the study properties.

## Contact set-up

```csharp
Mot.Contact(ms, c1, c2, 1e8, 1.1, 2000, 1e-4, 0.40, 0.35, 0.01, 0.05);
//                    stiffness exp damping penetration muS muD vS vD
```

Contact is expensive. Only pair the components that touch.

## Not tested

Friction-driven torque transfer through contact, measured against a hand value. The contact feature was created and solved with friction set, but only impact timing was checked. Also linear motors, forces, dampers, torsional springs, flexible mates, and AVI export.

## Choosing settings

General guidance, apart from the frame-rate figure and the expression units, which were tested.

- Maximum step: start at 0.2 ms for contact, and halve it until the answer stops changing.
- Frame rate sets output spacing, not accuracy. Too few frames hide events: at 100 frames per second a 1000 rpm/s ramp has 10 rpm resolution.
- Gravity needs adding as a feature. It is not on by default.
- Motor expressions use `TIME` in seconds, and `6000*TIME` means degrees per second, so 1000 rpm/s.
- Motion uses the mass the parts report, so assign real materials and check mass properties first.
- Redundant mates can give wrong reaction forces. Use the fewest mates that define the motion.

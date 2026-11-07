# Guiding someone through the interface

Use this when a person is doing the clicking. Give one step at a time for anything fiddly, and say what they should see, so they can tell when it has gone wrong.

These steps come from general SOLIDWORKS practice. Menu names were not checked click by click. If a screen differs, ask what they can see. Later versions move things.

## Before starting

- **Check units first:** bottom-right of the status bar, or Tools > Options > Document Properties > Units. Choose MMGS.
- **Check the version** against the computer the work will be opened on. A file saved in a newer version will not open in an older one.
- Save often, in one folder, with short names. Assemblies find parts by path, so moving or renaming files outside SOLIDWORKS breaks them. Use Pack and Go to move a whole project.

## Part modelling

1. New > Part. Pick the plane that matches how the part sits, so the origin is useful later.
2. Sketch. Black lines are fully defined and blue are not. Aim for all black.
3. Smart Dimension each size. Add relations (coincident, tangent, equal) before dimensions.
4. Features tab: Extruded Boss/Base, Revolved Boss/Base, Extruded Cut. Use Sweep for springs.
5. Use Linear or Circular Pattern for repeats instead of drawing each one.
6. Edit Material: right-click Material in the feature tree.
7. Evaluate > Mass Properties. Compare with the design calculation.

## Equations and design tables

- Tools > Equations for global variables that drive sizes.
- Insert > Tables > Design Table runs configurations from a spreadsheet, with one row per configuration and one column per dimension.
- Name dimensions first: right-click a dimension > Properties > Name. Column headers must match the form `R_shoe_in@Sketch1`.
- Choosing From file loads a prepared spreadsheet.
- Change a value in a cell and confirm the model updates.

## Assembly

1. New > Assembly and insert the first part. It is fixed at the origin.
2. Insert Components for the rest.
3. Mate: select two faces, then pick concentric, coincident or distance. Leave free exactly the motion the mechanism needs.
4. Drag a part to confirm it moves only as intended.
5. Evaluate > Interference Detection > Calculate, in every position that matters.

## Motion Analysis

1. Build the assembly with mates that allow the motion.
2. Add-ins: tick SOLIDWORKS Motion.
3. Open the Motion Study tab at the bottom. In the top-left dropdown change Animation to **Motion Analysis**. Animation and Basic Motion do not give the same physics.
4. Gravity: pick a direction.
5. Motor: Rotary Motor, select a cylindrical face, set direction and speed. For a ramp use an expression, such as `6000*TIME` for 1000 rpm/s.
6. Contact: Solid Bodies, select the parts that touch. Set friction if it matters.
7. Springs: select the two ends, then give the stiffness and free length.
8. Calculate.
9. Results and Plots: pick a category and component, then select a face. Right-click a plot to export a CSV.

Use the Study Properties to set frame rate and step. A short key-point drag on the timeline sets duration.

## FEA

1. Add-ins: tick SOLIDWORKS Simulation. New Study > Static.
2. Apply Material if it is not set.
3. Fixtures, then External Loads. Include centrifugal load when the part spins.
4. Mesh with high-quality elements, then Run.
5. Results: von Mises, displacement, factor of safety. Use Probe for values.
6. Repeat at several element sizes for a convergence table.
7. Red at a sharp corner or fixture is often a singularity, not a real stress. If it keeps rising as the mesh gets finer, treat it as one.

## Drawings

1. New > Drawing, A3, first-angle for BS 8888: Sheet Properties.
2. Add the model views, then a section where inside features matter.
3. Annotation > Model Items imports dimensions, then tidy by hand.
4. Add tolerances and notes. Fill in the title block.
5. Check units in Document Properties. The default can be inches.

Standard parts such as nuts, bolts and springs normally do not get detail drawings, but do appear in the assembly's bill of materials.

## Common problems

| They say | Likely cause |
|---|---|
| Sketch won't extrude | Open contour or overlapping lines. Use Tools > Sketch Tools > Check Sketch for Feature. |
| Mate fails or part jumps | Over-defined. Check the Mates folder for red marks and remove the conflict. |
| Assembly can't find a part | File moved. Use the dialog to browse to it, and Pack and Go in future. |
| Simulation won't mesh | Tiny faces or slivers. Try a finer mesh, or a curvature-based mesher. |
| Results look huge or tiny | Units on the load, or the wrong material. |
| Motion runs but nothing moves | Animation selected, or a mate is locking it, or the motor is on the wrong part. |
| Part spins with the motor | The motor's reference is wrong, or nothing is fixed. |
| Parts pass through each other | No contact defined. Mates do not stop bodies colliding. |

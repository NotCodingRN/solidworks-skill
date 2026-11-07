# Centrifugal clutch design

A worked method for a shoe-type centrifugal clutch on a small engine. Treat it as a pattern for any speed-dependent mechanism: size by hand, model, simulate, compare, revise.

## Sizing equations

| Quantity | Equation |
|---|---|
| Angular speed | ω = 2πN/60 |
| Centrifugal force on one shoe | F_c = m r ω² |
| Power | P = T ω |
| Normal force above contact | N_n = m r₂ (ω² - ω_c²) |
| Torque capacity | T_cap = n μ R_d m r₂ (ω² - ω_c²) |
| Shoe size for lock-up | m r₂ = T / [n μ R_d (ω_L² - ω_c²)] |
| Spring force at contact | F_s = m r₂ ω_c² |
| Preload | F₀ = F_s - k δ |
| Start of movement | ω = √[F₀ / (m r₁)] |
| Travel below contact | x = (m r₁ ω² - F₀) / (k - m ω²) |
| Uniform lining pressure | p = N_n / (2 R_d w sin(θ/2)) |
| Engagement ODE | J dω_d/dt = T_cap(ω_in) - T_L |

r₁ is the shoe centre-of-mass radius when retracted, and r₂ = r₁ + δ at contact. Use the CAD values, not the estimates, because an arc-shaped shoe's centre of mass sits well inside its mean radius.

## Order of work

1. Turn the requirements into numbers: idle, contact, lock-up, design torque, lining pressure limit, safety factors.
2. Compare concepts with weighted scoring.
3. Size m·r₂ and springs by hand, and integrate the ODE.
4. Model, then read real mass and centre of mass from CAD and rerun the sums.
5. FEA the shoe at rated speed and overspeed.
6. Motion study of engagement, compared with the ODE.
7. Revise and repeat. Expect more than one revision.

## Things that catch people

- **Static and dynamic lock-up differ.** The drum has to accelerate too, so dynamic lock-up comes later than the static speed where capacity equals torque. Design to the dynamic figure.
- **Fixing one requirement can break another.** More m·r₂ brings lock-up down but raises lining pressure at overspeed. Lower contact speed can lose idle clearance.
- **Check lift-off, not just contact.** Shoes start moving well below contact speed. Idle clearance depends on spring rate and preload together.
- **Some targets conflict.** If a load case can only be met by exceeding a limit, show that by calculation and say so, rather than tuning past it.
- **Match the lining radius to the drum at contact.** A lining ground to its retracted radius touches on a line until it beds in.
- **Check how the hub locates.** A threaded shaft with a journal needs a clearance bore ahead of the thread, and the thread and shoulder do the locating.

## What simulation tends to show

- Shoes pressed against a drum are loaded mainly in compression, so stresses are often low and mass is set by the torque requirement.
- Compare motion results with theory in a table: contact speed, lock-up, clearance, mean contact force.
- Undamped models bounce. Report bounce as a limitation and use mean contact force over a window. Shoes do not strike together, so one shoe's force can read zero after others have hit.
- Define lock-up before reading results, for example the first time slip falls below a set value.

## Limits to state

Constant friction, assumed load and inertia, no spring or guide damping, and no fatigue, wear or heat analysis unless done. Friction varies with temperature and wear, so include a sensitivity table across μ.

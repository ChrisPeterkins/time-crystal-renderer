# Time Crystal Renderer — Code Standards

## Guiding Principles

1. **Clarity over cleverness** — Code should read like well-written prose. If a reader needs to pause and re-read a line, simplify it.
2. **One responsibility per unit** — Each class does one thing. Each method does one thing. If you can't name it clearly, it's doing too much.
3. **Explicit over implicit** — Prefer named constants over magic numbers. Prefer clear parameter names over positional ambiguity. Prefer visible flow over hidden side effects.

## Naming

- **Classes**: `PascalCase` nouns describing what the thing *is* — `VoxelVolume`, `MeshVertex`, `OrbitCamera`
- **Methods**: `PascalCase` verbs describing what the method *does* — `ExtractSurface`, `UploadToGpu`, `ApplyPattern`
- **Local variables**: `camelCase`, descriptive enough to read without scrolling up — `triangleCount` not `tc`, `neighborCount` not `n`
- **Private fields**: `_camelCase` with underscore prefix — `_vertices`, `_currentBuffer`
- **Constants**: `PascalCase` — `MaxGridSize`, `DefaultGenerations`
- **Booleans**: Should read as a yes/no question — `isAlive`, `hasNeighbors`, `shouldCull`
- **No abbreviations** unless universally understood (`Vbo`, `Vao`, `Stl`, `Obj` are fine; `genCnt`, `vtx`, `buf` are not)

## Structure and Organization

- **One type per file**, file name matches type name
- **Namespace matches folder path** — `TimeCrystalRenderer.Core.MarchingCubes` lives in `Core/MarchingCubes/`
- **Dependencies flow inward** — `Renderer` depends on `Core`, never the reverse. `Core` has zero external dependencies.
- **Group members by purpose**, not by access modifier. Related fields, properties, and methods should live together.

## Methods

- **Short methods** — If a method exceeds ~30 lines, it likely has an extractable sub-step. Extract it with a clear name.
- **No more than 3-4 parameters** — If you need more, introduce a configuration struct or object.
- **Early returns** for guard clauses — Check preconditions at the top and return/throw immediately. Don't nest the happy path.
- **Pure functions where possible** — Methods that take input and return output with no side effects are easier to test, read, and trust.

## Comments

- **Don't comment *what*** — The code should say what it does through clear naming.
- **Do comment *why*** — Explain non-obvious decisions, algorithm choices, and workarounds.
- **Do comment algorithm steps** — For complex algorithms like marching cubes, use comments to label the logical steps so readers can follow the flow.
- **XML docs on public API only** — `<summary>` on public classes and methods. Keep them to one sentence.

## Error Handling

- **Fail fast, fail loud** — Throw `ArgumentException` / `InvalidOperationException` on bad inputs rather than silently producing garbage.
- **No empty catch blocks** — Every catch must either handle the error meaningfully or rethrow.
- **Validate at boundaries** — Check user input and file I/O. Trust internal data passed between our own classes.

## Performance

- **Correctness first, optimize second** — Get it working and readable, then profile, then optimize the hot path only.
- **Use `Span<T>` and `ReadOnlySpan<T>`** for bulk data transfer (mesh vertices, voxel data) to avoid unnecessary copies.
- **Pre-allocate collections** when the size is known or estimable — `new List<T>(capacity)`.
- **Document performance-critical sections** — If code is intentionally written for speed over clarity, explain why with a comment.

## Formatting

- **4-space indentation**, no tabs
- **Braces on their own line** (Allman style) for type and method declarations
- **Single blank line** between methods; no double blanks
- **Max line length ~120 characters** — Break long lines at logical points

## Testing and Verification

- Each phase has a concrete verification step (see plan). Verify before moving on.
- When debugging, add temporary console output — remove it before considering the phase complete.

## Audit Checklist

When reviewing new code, check each item:

- [ ] Can I understand what this code does in one read-through?
- [ ] Are all names descriptive and unambiguous?
- [ ] Does each class/method have a single clear responsibility?
- [ ] Are magic numbers replaced with named constants?
- [ ] Are public APIs documented with XML summary comments?
- [ ] Do complex algorithms have step-by-step comments explaining the *why*?
- [ ] Are guard clauses used instead of deep nesting?
- [ ] Is `Span<T>` used where bulk data is passed without ownership transfer?
- [ ] Does the code follow the dependency direction (Renderer → Core, never reverse)?
- [ ] Are there any abbreviations that would confuse a new reader?

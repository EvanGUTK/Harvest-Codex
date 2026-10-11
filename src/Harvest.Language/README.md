# Language continuation boundary

Application supplies immutable tick-start sensor scalars, current capabilities, the prior matching terminal result, and `NextRequestId`. IDs belong to Application and must increase across reruns. Language emits at most one action proposal per resume and never submits to Domain itself. Application maps API names to published Domain command kinds, rechecks mode capabilities, submits once, and routes rejection/completion on the next pass.

`Compile` returns a program or source diagnostics; `Start` binds actor/private state; `Resume` returns a bounded slice. Pause omits Resume; Stop discards execution at the Application/Domain boundary. Wait is interpreter-only. UTF-16 offsets and 1-based line/column match editor indexing. Valid infinite loops yield indefinitely; there is no lifetime instruction limit.

Sensor scalar keys: x/y/fuel/cargo (int64), heading/crop/implement (bounded strings), ripe (bool). Application converts Domain values to these immutable language scalars. `position()` exposes x/y; `sense()` exposes the documented sensor keys. Action result fields are ok/reason. Host objects and arbitrary members are rejected.

## Executable entry signatures

```csharp
CompileResult LanguageCompiler.Compile(string source, Capabilities capabilities, RuntimeLimits? limits = null);
CompiledProgram? CompileResult.Program { get; }
IReadOnlyList<Diagnostic> CompileResult.Diagnostics { get; }
Execution CompiledProgram.Start(long actorId);
ExecutionStatus Execution.Status { get; }
RunSlice Execution.Resume(TickInput input);
void Execution.Stop();
```

Start is on CompiledProgram (not a static Execution factory). All public boundary types are in Harvest.Language. Call Resume at most once per actor per strictly increasing tick; waiting slices consume zero instructions until their terminal result/wake tick arrives. At most one proposal is emitted. RuntimeLimits only permits a per-tick instruction budget of 1..256; all other Decision 001 caps are fixed. Snapshot keys may be omitted if unused, but a queried missing field faults with a source diagnostic. Terminal reason codes are lower-case letters/underscores, at most 64 characters, with success exactly `none`. Sensor coordinates/fuel/cargo are nonnegative int64; ripe is bool; heading/crop/implement are bounded strings.

Tests are a package-free executable harness: `dotnet run --project tests/Language/Harvest.Language.Tests.csproj`. A failing check returns nonzero. There is no claim of Domain or Unity integration coverage here.

# Language continuation boundary

Application supplies immutable tick-start sensor scalars, current capabilities, the prior matching terminal result, and `NextRequestId`. IDs belong to Application and must increase across reruns. Language emits at most one action proposal per resume and never submits to Domain itself. Application maps API names to published Domain command kinds, rechecks mode capabilities, submits once, and routes rejection/completion on the next pass.

`Compile` returns a program or source diagnostics; `Start` binds actor/private state; `Resume` returns a bounded slice. Pause omits Resume; Stop discards execution at the Application/Domain boundary. Wait is interpreter-only. UTF-16 offsets and 1-based line/column match editor indexing. Valid infinite loops yield indefinitely; there is no lifetime instruction limit.

Sensor scalar keys: x/y/fuel/cargo (int64), heading/crop/implement (bounded strings), ripe (bool). Application converts Domain values to these immutable language scalars. `position()` exposes x/y; `sense()` exposes the documented sensor keys. Action result fields are ok/reason. Host objects and arbitrary members are rejected.

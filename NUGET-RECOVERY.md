# NuGet Dependency Recovery

The debugging exercise includes a deliberately broken NuGet dependency. This document
records the failure and the exact recovery steps.

## The broken scenario

`EventBoard.Api/EventBoard.Api.csproj` was changed to request a **non-existent version**
of a package:

```xml
<!-- broken -->
<PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.99" />
```

Version `8.0.99` does not exist on nuget.org. The rest of the project pins the EF Core
`8.0.0` stack (`Microsoft.EntityFrameworkCore`, `.Design`, `.Tools`, all `8.0.0`).

## The failure (before)

```
$ dotnet restore EventBoard.sln

warning NU1603: EventBoard.Api depends on Microsoft.EntityFrameworkCore.SqlServer
  (>= 8.0.99) but Microsoft.EntityFrameworkCore.SqlServer 8.0.99 was not found.
  Microsoft.EntityFrameworkCore.SqlServer 9.0.0 was resolved instead.

error NU1605: Warning As Error: Detected package downgrade:
  Microsoft.EntityFrameworkCore from 9.0.0 to 8.0.0. Reference the package directly
  from the project to select a different version.
  EventBoard.Api -> Microsoft.EntityFrameworkCore.Design 8.0.0
                 -> Microsoft.EntityFrameworkCore.Relational 9.0.0
                 -> Microsoft.EntityFrameworkCore (>= 9.0.0)
  EventBoard.Api -> Microsoft.EntityFrameworkCore (>= 8.0.0)

restore exit=1
```

Full log: [`debug-artifacts/before-restore.log`](debug-artifacts/before-restore.log).

### Why it fails

1. NuGet can't find `SqlServer 8.0.99`, so it **floats up** to the next available
   version, `9.0.0` (`NU1603`).
2. `SqlServer 9.0.0` transitively requires `EF Core >= 9.0.0`, but the project also
   directly references `EF Core 8.0.0`.
3. Resolving the direct `8.0.0` reference against the transitive `9.0.0` requirement is a
   **package downgrade**, which NuGet reports as `NU1605` and treats as an error.

The compile never starts — this is a restore-time failure, so the whole build (and every
test) is blocked until it is fixed.

## Recovery steps

1. **Read the error, not just "restore failed."** The offending package and version are
   named directly: `Microsoft.EntityFrameworkCore.SqlServer (>= 8.0.99)`.

2. **Check what versions actually exist** and what the sibling packages use:

   ```bash
   dotnet package search Microsoft.EntityFrameworkCore.SqlServer --exact-match
   # or browse https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer
   ```

   The other EF Core packages in the csproj are all pinned to `8.0.0`, so the SqlServer
   provider must match that line to avoid a mixed 8.x/9.x graph.

3. **Correct the version** in `EventBoard.Api/EventBoard.Api.csproj`:

   ```diff
   - <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.99" />
   + <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="8.0.0" />
   ```

4. **(If a bad version was cached)** clear NuGet caches so a stale/failed resolution is
   not reused:

   ```bash
   dotnet nuget locals all --clear
   ```

5. **Restore and build to confirm:**

   ```bash
   dotnet restore EventBoard.sln   # -> Restored ... (in ~0.5s), exit 0
   dotnet build   EventBoard.sln   # -> 0 Warning(s), 0 Error(s)
   ```

## After

```
$ dotnet restore EventBoard.sln
  Determining projects to restore...
  Restored /.../EventBoard.Api/EventBoard.Api.csproj (in 531 ms).
restore exit=0
```

## Takeaways

- `NU1603` (version not found → floated) and `NU1605` (package downgrade) usually travel
  together when someone bumps **one** package in a version-locked family.
- Keep a package family (here, all `Microsoft.EntityFrameworkCore.*`) on a **single
  aligned version**.
- A non-existent version does not fail loudly as "not found"; NuGet silently substitutes
  the nearest higher version, and the real error surfaces one hop later as a downgrade
  conflict. Read the whole error chain.

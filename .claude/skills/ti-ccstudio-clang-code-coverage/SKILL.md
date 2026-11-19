---
name: ti-ccstudio-clang-code-coverage
description: Generate a code coverage or profiling report for a TI Clang project. Use when the user asks for code coverage, coverage report, profiling instrumentation, or wants to measure test coverage on an embedded target. Supports both TI Arm Clang (TMS470) and TI C29 Clang (C2000) compilers.
allowed-tools: Bash, Read, Glob, Grep
---

# Code Coverage for TI Clang Projects (Arm and C29)

Generate a code coverage or profiling report for a project compiled with the TI Arm Clang compiler
or the TI C29 Clang compiler. Both compilers use LLVM-based coverage instrumentation with
identical option names and the same LLVM profiling runtime, so the flow is the same; only the
coverage tool names differ.

Read `CCS.md` if not already read.
Run this skill's scripts (in its `scripts/` folder) with the bundled Node.js (see Node.js in the
central guide).

## High-Level Flow

1. Add the coverage compiler options to the project
2. Build the project
3. Debug/run the application on target for a user-determined amount of time
4. Extract profiling counters from target memory (produces a `.cnt` file)
5. Generate the .profdata file
6. Generate the coverage report
7. Ask user if the coverage options should be removed

## Step 1 - Add Compiler Options

Mandatory options: `-fprofile-instr-generate` and `-fcoverage-mapping`.

Ask the user if they want any optional options:

| Option | Purpose |
|------|---------|
| `-fmcdc` | Enable MC/DC (Modified Condition/Decision Coverage). Increases instrumentation cost. |
| `-ffunction-coverage-only` | Function-level coverage only. Decreases instrumentation cost. |
| `-fprofile-counter-size=32` | Use 32-bit instead of default 64-bit counters. Saves memory on target. |

Add the options to the compiler flags of **every** build configuration of the project, as
Project Settings in the central guide describes. Do not add an option that is already present.
Note which options you added; Step 7 removes only those.

**Reference**: Chapter 12 of the TI Arm Clang Compiler Tools User's Guide (and the equivalent
section of the TI C29 Clang Compiler Tools User's Guide) describes the full code coverage tools
and collection process.

### Resolving Tool Paths

- **Compiler tools path** (`<compiler tools path>`): the install path of the compiler version the
  project is configured to use (see Installed SDKs and Tools in the central guide).
  - TI Arm Clang: tools are `tiarmprofdata` / `tiarmcov`.
  - TI C29 Clang (install directory starts with `ti-cgt-c29_`): tools are `c29profdata` /
    `c29cov`.
- **Executable path**: the `.out` file the build produces, normally
  `<project location>/<active build configuration>/<project name>.out`.

## Step 2 - Build the Project

Do a full rebuild so every file is instrumented (see Building a Project in the central guide).
Verify the build succeeds and that the coverage options appear in the compiler command lines
before proceeding.

## Step 3 - Run the Application

The application must be loaded and run on the target for a sufficient duration to exercise the
code paths the user wants to measure. The user determines when enough execution has occurred.

- Start a debug session and run the application (see Debug in the central guide)
- Wait for the user to indicate the application has run sufficiently
- Halt the target before extracting data

## Step 4 - Extract Coverage Data from Target Memory

**CRITICAL — Do NOT end the debug session before reading counters.** Ending a session can resume
the CPU before it disconnects (for 10–30 seconds on some devices), adding spurious execution
counts. Read the counters from the halted target in the active debug session.

1. **Find the section bounds** from the linker symbols (identical for Arm and C29):
   - Counters: `__start___llvm_prf_cnts` to `__stop___llvm_prf_cnts` (end exclusive).
   - MC/DC bitmap: `__start___llvm_prf_bits` to `__stop___llvm_prf_bits`. These symbols exist
     only when built with `-fmcdc`; if they are absent, the bitmap is empty.
2. **Read both ranges' bytes** from target memory through the debug session (see Debug in the
   central guide). If a memory read is limited in size, read the range in contiguous chunks.
3. **Write the `.cnt` file**: the raw counter bytes followed immediately by the MC/DC bytes, with
   no header. `scripts/write_cnt.js` assembles it from hex files:

   ```
   write_cnt.js --output <output_path>.cnt --chunk <file> [--chunk <file>]... [--mcdc <file>]
   ```

   Each file holds either a JSON array of two-digit hex strings (`["01","00","ff",...]`) or plain
   hex text (`0100ff...`, whitespace ignored). `--chunk` files are concatenated in the order
   given, so list them in address order; omit `--mcdc` when there is no bitmap. Create the files
   with the Write tool rather than embedding the bytes in a shell command, which can exceed the
   command-line length limit.
4. **End the debug session** only after the `.cnt` file has been written.

## Step 5 - Generate the .profdata

The tool name depends on compiler family:

- **TI Arm Clang:** `<compiler tools path>/bin/tiarmprofdata`
- **TI C29 Clang:** `<compiler tools path>/bin/c29profdata`

Execute:
```
<profdata tool> merge -sparse -obj-file=<.out file> <.cnt file> -o <.profdata file>
```

Multiple coverage runs can be combined with the profdata tool and a single report generated
from the merged data.

## Step 6 - Generate the Report

The tool name depends on compiler family:

- **TI Arm Clang:** `<compiler tools path>/bin/tiarmcov`
- **TI C29 Clang:** `<compiler tools path>/bin/c29cov`

Execute:
```
<cov tool> show --format=html --show-instantiations --show-branches=count \
    --object=<.out file> -instr-profile=<.profdata file> \
    --output-dir=<cov report directory>
```

Place the coverage report directory in the folder holding the `.out` file, e.g.
`<project location>/Debug/coverage_report/`.

If the MCDC option was selected, add `--show-mcdc-summary` to the command.

Present the final report path to the user.

## Step 6.5 - Patch the HTML Report

Run `scripts/patch_coverage_report.js` to apply TI branding, fix CCStudio webview usability issues,
and add project metadata. It is idempotent.

```
patch_coverage_report.js <cov report directory>
    --project <project name>
    --compiler <compiler display string>
    --coverage-type <coverage type>
    --toolchain <toolchain name>
    --ti-css {ccs-resources-ai-dir}/styles/ti-brand.css
```

- `--compiler`: e.g. `"TI Clang v4.0.4.LTS"` for Arm, `"TI Clang v2.2.0.LTS"` for C29
- `--coverage-type`: describe the collection mode based on options enabled. Examples:
  `"Source Coverage"` (default), `"Function Coverage"` (with `-ffunction-coverage-only`),
  `"Source Coverage + MC/DC"` (with `-fmcdc`)
- `--toolchain`: `"TI Arm Clang"` or `"TI C29 Clang"`; used in the report footer
- `--ti-css`: the shared TI stylesheet; the script falls back to built-in styles if it can't be
  read

## Step 7 - Ask user if the coverage compiler options should be removed

If the user wants them removed, remove every option noted in Step 1 from every build
configuration:

- **In CCS**, run `scripts/remove_coverage_options.js --cproject <path> --option <option>...`.
  Do not use Project Settings or add `-fno-` flags.
- **Otherwise**, as Project Settings in the central guide describes.

Then do a full rebuild (see Building a Project in the central guide) and confirm the options no
longer appear in the compiler command lines. Do not report them as removed until the rebuild shows
them gone. If the options are kept, tell the user the build output stays instrumented.

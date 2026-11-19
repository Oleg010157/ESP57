/*
 * TEXAS INSTRUMENTS TEXT FILE LICENSE
 *
 * Copyright (c) 2026 Texas Instruments Incorporated
 *
 * All rights reserved not granted herein.
 *
 * Limited License.
 *
 * Texas Instruments Incorporated grants a world-wide, royalty-free, non-exclusive
 * license under copyrights and patents it now or hereafter owns or controls to
 * make, have made, use, import, offer to sell and sell ("Utilize") this software
 * subject to the terms herein. With respect to the foregoing patent license, such
 * license is granted solely to the extent that any such patent is necessary to
 * Utilize the software alone. The patent license shall not apply to any
 * combinations which include this software, other than combinations with devices
 * manufactured by or for TI ("TI Devices"). No hardware patent is licensed
 * hereunder.
 *
 * Redistributions must preserve existing copyright notices and reproduce this
 * license (including the above copyright notice and the disclaimer and (if
 * applicable) source code license limitations below) in the documentation and/or
 * other materials provided with the distribution
 *
 *
 * Redistribution and use in binary form, without modification, are permitted
 * provided that the following conditions are met:
 *
 * * No reverse engineering, decompilation, or disassembly of this software is
 * permitted with respect to any software provided in binary form.
 *
 * * any redistribution and use are licensed by TI for use only with TI Devices.
 *
 * * Nothing shall obligate TI to provide you with source code for the software
 * licensed and provided to you in object code.
 *
 * If software source code is provided to you, modification and redistribution of
 * the source code are permitted provided that the following conditions are met:
 *
 * * any redistribution and use of the source code, including any resulting
 * derivative works, are licensed by TI for use only with TI Devices.
 *
 * * any redistribution and use of any object code compiled from the source code
 * and any resulting derivative works, are licensed by TI for use only with TI
 * Devices.
 *
 * Neither the name of Texas Instruments Incorporated nor the names of its suppliers
 * may be used to endorse or promote products derived from this software without
 * specific prior written permission.
 *
 * DISCLAIMER.
 *
 * THIS SOFTWARE IS PROVIDED BY TI AND TI"S LICENSORS "AS IS" AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO
 * EVENT SHALL TI AND TI"S LICENSORS BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
 * PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE
 * OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

// write_cnt.js
//
// Combines memory chunks read from the target through a debug session into a
// binary .cnt file for use with tiarmprofdata / c29profdata merge.
//
// Background:
//   When LLVM coverage instrumentation is active, the compiler places two
//   sections in the binary's RAM:
//     __llvm_prf_cnts  — execution counter array (one 64-bit counter per
//                        instrumented region: function entries, branches, etc.)
//     __llvm_prf_bits  — MC/DC bitmap (only present when -fmcdc is used;
//                        one bit per condition per branch decision)
//
//   After the application has run, these raw bytes must be extracted from the
//   target and fed to the profdata tool, which correlates them with the
//   compile-time mapping metadata embedded in the .out file to produce a
//   .profdata file.  The .profdata file is then used by the coverage report
//   tool to generate an HTML report.
//
//   The .cnt file is simply the raw bytes of __llvm_prf_cnts followed
//   immediately by the raw bytes of __llvm_prf_bits (if present), with no
//   header or framing.  The profdata tool uses the .out file's symbol table
//   to know how many bytes belong to each section.
//
// Why this script exists:
//   A debug session's memory read may be limited in size per call, so large
//   counter sections must be read in multiple chunks.  Rather than embedding
//   hundreds of hex values inline in a shell command (which risks truncation
//   at the shell's command-line length limit), the agent writes each chunk to
//   a small JSON file using the Write tool and then calls this script to
//   assemble all the pieces into the final binary .cnt file.
//
// Input format:
//   Each file holds either
//     - a JSON array of hex byte strings, e.g. a CCS readMemory "values" field:
//         ["01","00","ff","3a","00","00","00","00",...]
//       The strings are two hex digits each (no "0x" prefix); or
//     - plain hex text, e.g. a GDB/MI -data-read-memory-bytes "contents" field:
//         0100ff3a00000000...
//       Whitespace is ignored.
//
// Usage:
//   node write_cnt.js --output <output.cnt> \
//       --chunk <chunk1.json> [--chunk <chunk2.json>]... \
//       [--mcdc <mcdc.json>]
//
//   --output  Required. Path for the output .cnt binary file.
//   --chunk   Path to a file holding one chunk's bytes (see Input format).
//             Repeat this flag for each chunk, in address order.
//             All chunks are concatenated in the order they are listed.
//   --mcdc    Path to a file holding the __llvm_prf_bits section bytes.
//             This is appended after all counter chunks.  Omit if -fmcdc was
//             not used (the symbols __start___llvm_prf_bits /
//             __stop___llvm_prf_bits will not exist in that case).

"use strict";

const fs   = require("fs");
const path = require("path");

// =============================================================================
// Argument parsing
// =============================================================================

function parseArgs() {
    // process.argv = ["node", "write_cnt.js", arg1, arg2, ...]
    // slice(2) gives us only the user-supplied arguments
    const args = process.argv.slice(2);

    const opts = {
        output: null,   // path for the output .cnt binary file
        chunks: [],     // ordered list of counter section chunk JSON files
        mcdc:   null,   // optional path to the MC/DC section JSON file
    };

    for (let i = 0; i < args.length; i++) {
        const arg = args[i];

        if (arg === "--output" && i + 1 < args.length) {
            // Destination path for the assembled .cnt binary
            opts.output = args[++i];

        } else if (arg === "--chunk" && i + 1 < args.length) {
            // Each --chunk argument names one file from a single memory read.
            // Multiple chunks cover a counter section larger than one read
            // allows.  They are appended in listing order,
            // so the caller must list them in ascending address order.
            opts.chunks.push(args[++i]);

        } else if (arg === "--mcdc" && i + 1 < args.length) {
            // Optional: the __llvm_prf_bits (MC/DC bitmap) section bytes.
            // The profdata tool expects these to follow immediately after the
            // counter bytes in the .cnt file.
            opts.mcdc = args[++i];

        } else {
            console.error("Unknown argument: " + arg);
            process.exit(1);
        }
    }

    // Both --output and at least one --chunk are required
    if (!opts.output || opts.chunks.length === 0) {
        console.error(
            "Usage: node write_cnt.js --output <output.cnt> " +
            "--chunk <chunk.json> [--chunk <chunk2.json>]... [--mcdc <mcdc.json>]",
        );
        process.exit(1);
    }

    return opts;
}

// =============================================================================
// Hex file reader
// =============================================================================
//
// Reads a file produced by the agent and converts it to a Node.js Buffer of
// raw bytes.  Two formats are accepted (see Input format above):
//   ["01","00","ff","3a"]   — JSON array, one two-digit hex string per byte
//   0100ff3a                — plain hex text, whitespace ignored

function readHexFile(filePath) {
    const raw = fs.readFileSync(filePath, "utf-8").trim();

    if (raw.startsWith("[")) {
        // JSON array of hex-string elements
        const arr = JSON.parse(raw);
        if (!Array.isArray(arr)) {
            throw new Error(filePath + ": expected a JSON array of hex strings");
        }
        // Convert each "ff" hex string to the integer 255, then wrap in a Buffer.
        return Buffer.from(arr.map((h) => { return parseInt(h, 16); }));
    }

    // Plain hex text: strip whitespace, then decode two digits per byte
    const hex = raw.replace(/\s+/g, "");
    if (!/^([0-9a-fA-F]{2})*$/.test(hex)) {
        throw new Error(filePath + ": expected a JSON array or an even-length hex string");
    }
    return Buffer.from(hex, "hex");
}

// =============================================================================
// Main entry point
// =============================================================================

const opts = parseArgs();

// We collect all byte buffers here in order, then concatenate them once at the
// end.  This is more efficient than repeatedly concatenating growing buffers.
const buffers = [];

// Track counter byte total separately from MC/DC bytes for the summary message
let totalCntBytes = 0;

// -----------------------------------------------------------------------
// Process counter section chunks (--chunk flags)
// Each chunk corresponds to one memory read that covered part of the
// __llvm_prf_cnts section.  Chunks must be listed in
// ascending address order so the bytes are concatenated correctly.
// -----------------------------------------------------------------------
for (let i = 0; i < opts.chunks.length; i++) {
    const buf = readHexFile(opts.chunks[i]);
    buffers.push(buf);
    totalCntBytes += buf.length;
    // Print a summary line for each chunk so the user can verify sizes
    console.log("  chunk " + path.basename(opts.chunks[i]) + ": " + buf.length + " bytes");
}

// -----------------------------------------------------------------------
// Process MC/DC section (--mcdc flag, optional)
// The __llvm_prf_bits section contains a compact bitmap of MC/DC results.
// It is only present when -fmcdc was used at compile time.  The profdata
// tool expects these bytes to follow the counter bytes in the .cnt file.
// -----------------------------------------------------------------------
let mcdcBytes = 0;
if (opts.mcdc) {
    const mcdcBuf = readHexFile(opts.mcdc);
    buffers.push(mcdcBuf);
    mcdcBytes = mcdcBuf.length;
    console.log("  mcdc  " + path.basename(opts.mcdc) + ": " + mcdcBytes + " bytes");
}

// -----------------------------------------------------------------------
// Concatenate all buffers and write the binary .cnt file
// Buffer.concat efficiently merges all the individual chunk buffers into
// one contiguous binary blob without any intermediate copies.
// -----------------------------------------------------------------------
const output = Buffer.concat(buffers);
fs.writeFileSync(opts.output, output);

// Print a summary so the user can verify the total byte count against
// the counter section size reported by the debug session
console.log(
    "Wrote " + output.length + " bytes " +
    "(" + totalCntBytes + " counter + " + mcdcBytes + " MC/DC) to " +
    opts.output,
);

# CAN Logging Documentation

## Overview

The CAN logging system captures all CAN bus communication in real-time and generates detailed TSV (Tab-Separated Values) reports with timing analysis.

## Features

- **Real-time event capture** with high-precision Stopwatch timestamps
- **Automatic delay calculations** for system response time analysis
- **UTC timestamp tracking** with microsecond precision
- **Multi-command support** - correctly correlates TX/RX pairs by command ID

## Logged Events

The logger captures four types of CAN events:

| Event | Label | Description |
|-------|-------|-------------|
| **TX** | `TX` | Command transmitted to CAN bus |
| **RX** | `RX` | Response received (intermediate acknowledgment) |
| **RX13** | `RX13` | Mailbox 13 response received (detailed data) |
| **RX_done** | `RX_done` | Final response processed (command complete) |

## Output Format

Each logging session creates a single `.tsv` file in `Documents\CanLogTools\`:

### File Format
```
N;Time_ms;dt_us;Dir;ID;cmd;d1;d2;d3;TxRx_us;Rx13_Rx_us;RxDone_Rx13_us;RxDone_Tx_us
```

### Column Descriptions

| Column | Type | Description |
|--------|------|-------------|
| **N** | Integer | Sequential event number |
| **Time_ms** | Decimal (3 decimals) | Absolute time since logging start (milliseconds) |
| **dt_us** | Integer | Time delta from previous event (microseconds) |
| **Dir** | String | Event type: `TX`, `RX`, `RX13`, or `RX_done` |
| **ID** | Decimal | CAN message ID |
| **cmd** | Decimal | Command code (0-65535) |
| **d1, d2, d3** | Decimal | Data parameters (empty if not applicable) |
| **TxRx_us** | Integer | Delay from TX to RX for this command (μs) - **RX rows only** |
| **Rx13_Rx_us** | Integer | Delay from RX to RX13 for this command (μs) - **RX13 rows only** |
| **RxDone_Rx13_us** | Integer | Delay from RX13 to RX_done for this command (μs) - **RX_done rows only** |
| **RxDone_Tx_us** | Integer | **Total response time** from TX to RX_done (μs) - **RX_done rows only** |

## Delay Calculations

All delays are calculated **by matching command IDs** to ensure accurate round-trip timing:

### TxRx_us (RX row)
```
TxRx_us = time(RX) - time(TX with same cmd)
```
Measures the time from command transmission to first acknowledgment.

### Rx13_Rx_us (RX13 row)
```
Rx13_Rx_us = time(RX13) - time(RX with same cmd)
```
Measures additional processing delay between first and detailed response.

### RxDone_Rx13_us (RX_done row)
```
RxDone_Rx13_us = time(RX_done) - time(RX13 with same cmd)
```
Measures final processing step duration.

### RxDone_Tx_us (RX_done row)
```
RxDone_Tx_us = time(RX_done) - time(TX with same cmd)
```
**Total system response time** - measures complete request/response cycle.

## Usage

### Enable Logging

Check the **"CAN логирование: ВЫКЛ"** checkbox in the main application window to start capturing events.

### Stop Logging

Uncheck the checkbox to stop capturing and automatically write the `.tsv` file.

### View Logs

All log files are saved in: `Documents\CanLogTools\can_YYYYMMDD_HHmmss_fff.tsv`

## Implementation Details

### Timing Precision

- **Timestamp source**: `Stopwatch.GetTimestamp()` (high-resolution counter)
- **Conversion to microseconds**: `ticks * 1,000,000 / Stopwatch.Frequency`
- **Time base**: All timestamps relative to logging session start
- **Frequency**: Depends on system timer resolution (typically 10 MHz on modern systems = 0.1 μs per tick)

### Event Correlation

Events are correlated by **command ID (cmd)** to ensure:
- TX and its corresponding RX/RX13/RX_done are linked
- Multi-command scenarios correctly match responses to requests
- No mixing of delays across different command sequences

### Lock-Free Performance

- Events logged to in-memory `List<LogEntry>` under lock
- File writing deferred until logging stops
- No performance impact on CAN communication thread

## Example Analysis

```
Line 10: TX   cmd=75   
         TxRx_us = [empty]      (delay shown in RX row, not here)

Line 11: RX   cmd=75   
         TxRx_us = 234          (TX→RX delay = 234 μs)
         Rx13_Rx_us = [empty]   (delay shown in RX13 row)

Line 12: RX13 cmd=75   
         Rx13_Rx_us = 145       (RX→RX13 delay = 145 μs)
         RxDone_Rx13_us = [empty]

Line 13: RX_done cmd=75   
         RxDone_Rx13_us = 89    (RX13→RX_done delay = 89 μs)
         RxDone_Tx_us = 468     (Total: TX→RX_done = 468 μs)
```

**Verification**: 234 + 145 + 89 = 468 ✓

## Troubleshooting

### Missing Delay Values

If delay columns are empty:
- Check that event labels match exactly: `TX`, `RX`, `RX13`, `RX_done` (case-sensitive)
- Verify command IDs (cmd) are present and match between related events
- Ensure events are in chronological order (time always increases)

### Incorrect Timing

If delays don't match manual calculations:
- Verify Excel formulas use `=B_row - B_previous_row` format
- Check that all timestamps use the same time base (from logging start)
- Confirm Stopwatch.Frequency is correctly applied

## Technical Notes

- **Build symbol**: Logging is compiled only when `CAN_LOG` preprocessor symbol is defined
- **Thread safety**: All logging operations are locked to prevent corruption
- **File encoding**: UTF-8 (compatible with Excel and standard text editors)
- **Separator**: Semicolon (`;`) - locale-independent format suitable for European Excel versions

---

**Last Updated**: 2024-02-24  
**Version**: 1.0  
**Author**: ICDIBasic CAN Logging System

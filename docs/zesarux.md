This document describes the interaction with ZEsarUX

* any response of "`command>·`" indicates CPU is running
* any response of "`command@cpu-step>·`" indicates CPU is stopped and in step mode

Note: · = space


# Connect()

## Version

| Send | `get-version` |
| --- | --- |
| Receive | `5.1-SN` |
| | `command>·` |


## Count breakpoints available

| Send | `get-breakpoints` |
| --- | --- |
| Receive | `Breakpoints: On` |
| | `Disabled 1: PC=000Dh` | 
| | `Disabled 2: None` | 
| | ... | 
| | `Disabled 99: None` | 
| | `Disabled 100: None` | 
| | `command>·` |


## Set debug settings

| Send | `set-debug-settings 40` |
| --- | --- |
| Receive | `command>·` |

```
Bit 0: show all cpu registers on cpu stepping or only pc+opcode.
Bit 1: show 8 next opcodes on cpu stepping.
Bit 2: Do not add a L preffix when searching source code labels.
Bit 3: Show bytes when debugging opcodes.
Bit 4: Repeat last command only by pressing enter.
Bit 5: Step over interrupt when running cpu-step, cpu-step-over and run verbose. It's the same setting as Step Over Interrupt on menu
```


## Disable each individual breakpoint

| Send | `disable-breakpoint 1` | (repeats for each) |
| --- | --- | --- |
| Receive | `command>·` |
| or | `Error. You must enable breakpoints first` | 


## Set memory zone to the 64k Spectrum memory

| Send | `set-memory-zone -1` |
| --- | --- |
| Receive | `command>·` |

```
Zone: -1 Name: Mapped memory
Zone: 0 Name: Machine RAM Size: 131072 R/W: 3
Zone: 1 Name: Machine ROM Size: 32768 R/W: 1
Zone: 2 Name: Diviface eeprom Size: 8192 R/W: 3
Zone: 3 Name: Diviface ram Size: 131072 R/W: 3
```

## Enable breakpoints

| Send | `enable-breakpoints` |
| --- | --- |
| Receive | `command>·` |
| or | `Error. Already enabled` | 


# Disconnect()

## Disable breakpoints

| Send | `disable-breakpoints` |
| --- | --- | --- |
| Receive | `command>·` |
| or | `Error. Already disabled` |


## Exit

| Send | `exit` |
| --- | --- | --- |
| Receive | `Sayonara baby` |


# Pause()

| Send | `enter-cpu-step` |
| --- | --- | --- |
| Receive | `command@cpu-step>·` |


# Continue()

| Send | `run` |
| --- | --- | --- |
| Receive | (nothing until breakpoint hit) |


# StepOver()

| Send | `cpu-step-over` |
| --- | --- | --- |
| Receive | `command@cpu-step>·` |


# Step()

| Send | `cpu-step` |
| --- | --- | --- |
| Receive | `command@cpu-step>·` |


# RefreshMemoryPages()

| Send | `get-memory-pages` |
| --- | --- | --- |
| Receive (48k) | `command@cpu-step>·` |
| Receive (128k) | `RO1 RA5 RA2 RA0 SCR5 PEN` | 
| | `command@cpu-step>·` |

* First four entries represent each 16k slot.  RO# = ROM number, RA# = RAM bank.
* SCR indicates which bank is currently in the screen slot.
* PEN indicates paging is enabled.  Alternatively it would be PDI when disabled.


# ReadMemory( address, length )

| Send | `read-memory 5625 10` |
| --- | --- | --- |
| Receive | `56EBCD2C16E1D9C987C6` |
| | `command@cpu-step>·` |

Address & length are sent as decimal.


# RefreshRegisters()

| Send | `get-registers` |
| --- | --- | --- |
| Receive | `PC=15f9 SP=ff4a BC=0921 A=00 HL=5cb9 DE=5ca8 IX=ffff IY=5c3a A'=00 BC'=174b HL'=107f DE'=0006 I=3f R=59  F= Z P3H   F'= Z P     MEMPTR=15f7 EI IM1 VPS: 0` |
| | `command@cpu-step>·` |

Registers parsed by regex: `(?i)([a-z']*)=([0-9a-f].*?)(?:\s)`
Flags parsed by regex: `(?i)(F'?)=(.{8})\s`

Results are always hex.


# SetRegister()

| Send | `set-register PC=1234` (decimal) | 
| --- | --- | --- |
| Receive | `command@cpu-step>·` |



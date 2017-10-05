The following requests should be supported by the remote device (emulator, physical machine, etc):

Command| Suggested arguments | Description
--- | --- | ---
Get registers | | Retrieve the current values of all registers
Set register | Register, Value | Set a single register to the specified value
Read memory | Address, Size | Read
Get memory pages | | Retrieve the current memory configuration, i.e. which memory pages are currently mapped into the 64k address range
Pause | | Pause CPU execution and enter single-step mode
Continue | | Continue CPU execution
Step | | Execute one instruction and pause
Step over | | Execute one instruction and pause when the instruction returns (e.g. for CALL, DJNZ etc)
Step out | | Optional<br/>Return from current function call - does not always make sense on a Z80 platform but available
Set breakpoints | List of addresses | Optional<br/>Clear all current breakpoints and set the ones provided in the list
Set single breakpoint | Address | Optional<br/>Set a breakpoint at the specified address
Remove breakpoints | | Optional<br/>Remove all breakpoints
Remove single breakpoint | | Optional<br/>Remove a specified breakpoint
Custom command | | Optional<br/>Support an arbitrary text command which may return one or more lines of text



Additionally, the remote device should support sending the following events individually (i.e. not prompted by one of the above requests)

Event | Suggested arguments | Description
--- | --- | ---
Continued | | Tell the debugger that the CPU has entered the running state.<br/>This should be triggered when the device starts the 'Continue', 'Step', and 'Step over' requests.
Paused | | Tell the debugger that the CPU has become paused.<br/>This should be triggered after the CPU completes the 'Pause', 'Step', 'Step over' requests<br/>Additionally it will be triggered when a breakpoint is hit.

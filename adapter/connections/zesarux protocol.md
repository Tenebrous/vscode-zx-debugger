# Information on the ZEsarUX remote protocol ZRCP

# command> help

    Available commands:
    about                    Shows about message
    cpu-step                 Run single opcode cpu step
    cpu-step-over            Runs until returning from the current opcode
    disable-breakpoint       Disable specific breakpoint
    disable-breakpoints      Disable all breakpoints
    disassemble              Disassemble at address
    dump-nested-functions    Shows internal nested core functions
    enable-breakpoint        Enable specific breakpoint
    enable-breakpoints       Enable breakpoints
    enter-cpu-step           Enter cpu step to step mode
    evaluate                 Evaluate expression, can be more than one register separated by spaces
    exit-cpu-step            Exit cpu step to step mode
    exit-emulator            Ends emulator
    find-label               Finds label on source code
    generate-nmi             Generates a NMI
    get-audio-buffer-info    Get audio buffer information
    get-breakpoints          Get breakpoints list
    get-breakpointsactions   Get breakpoints actions list
    get-cpu-core-name        Get emulation cpu core name
    get-current-machine      Returns current machine name
    get-current-memory-zone  Returns current memory zone
    get-debug-settings       Get debug settings on remote command protocol
    get-io-ports             Returns currently i/o ports used
    get-machines             Returns list of emulated machines
    get-memory-pages         Returns current state of memory pages
    get-memory-zones         Returns list of memory zones of this machine
    get-ocr                  Get OCR output text
    get-os                   Shows emulator operating system
    get-registers            Get CPU registers
    get-stack-backtrace      Get last 5 16-bit values from the stack
    get-version              Shows emulator version
    get-visualmem-dump       Dumps all the visual memory written positions
    hard-reset-cpu           Hard resets the machine
    help                     Shows help screen or command help
    hexdump                  Dumps memory at address, showing hex and ascii
    hexdump-internal         Dumps internal memory (hexadecimal and ascii) for a given memory pointer
    load-source-code         Load source file to be used on disassemble opcode functions
    ls                       Minimal command list
    noop                     This command does nothing
    quit                     Closes connection
    read-memory              Dumps memory at address
    reset-cpu                Resets CPU
    run                      Run cpu when on cpu step mode
    save-binary-internal     Dumps internal memory to file for a given memory pointer
    send-keys-ascii          Simulates sending some ascii keys on parameters asciichar, separated by spaces
    send-keys-string         Simulates sending some keys on parameter string
    set-breakpoint           Sets a breakpoint at desired index entry with condition
    set-breakpointaction     Sets a breakpoint action at desired index entry with condition
    set-cr                   Sends carriage return to every command output received, useful on Windows environments
    set-debug-settings       Set debug settings on remote command protocol
    set-machine              Set machine
    set-memory-zone          Set memory zone number
    set-register             Changes register value
    set-verbose-level        Sets verbose level for console output
    set-window-zoom          Sets window zoom
    smartload                Smart-loads a file
    tbblue-get-palette       Get palette colour at index
    tbblue-get-pattern       Get patterns at index, if not specified items parameters, returns only one
    tbblue-get-sprite        Get sprites at index, if not specified items parameters, returns only one
    tsconf-get-af-port       Get TSConf XXAF port value
    tsconf-get-nvram         Get TSConf NVRAM value at index
    view-basic               Gets Basic program listing
    write-memory             Writes a sequence of bytes starting at desired address on memory
    write-memory-raw         Writes a sequence of bytes starting at desired address on memory

    You can get descriptive help for every command with: help command
    Note: When help shows an argument in brackets [], it means the argument is optional, and when written, you must not write these brackets



# command> help set-debug-settings

    Syntax: set-debug-settings|sds setting

    Description
    Set debug settings on remote command protocol. It's a numeric value with bitmask with different meaning:
    Bit 0: show all cpu registers on cpu stepping or only pc+opcode.
    Bit 1: show 8 next opcodes on cpu stepping.
    Bit 2: Do not add a L preffix when searching source code labels.
    Bit 3: Show bytes when debugging opcodes.
    Bit 4: Repeat last command only by pressing enter.
    Bit 5: Step over interrupt when running cpu-step, cpu-step-over and run verbose. It's the same setting as Step Over Interrupt on menu


# command> help tbblue-get-palette

    Syntax: tbblue-get-palette index

    Description
    Get palette colour at index. Returned values are in hexadecimal format. Only allowed on machine TBBlue

    command> tbblue-get-palette 1 10
    01

    command> tbblue-get-palette 1
    01


# command> help tbblue-get-pattern

    Syntax: tbblue-get-pattern index [items]

    Description
    Get patterns at index, if not specified items parameters, returns only one. Returned values are in hexadecimal format. Only allowed on machine TBBlue

    command> tbblue-get-pattern 1
    E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 24 24 24 24 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 24 FB FB FB 69 24 E3 E3 E3 E3 E3 E3 E3 E3 E3 24 FB 92 24 69 FB 6E 24 E3 E3 E3 E3 E3 E3 E3 24 FB 6E 24 E3 24 D6 92 24 E3 E3 E3 E3 E3 E3 E3 24 FB 6E 24 24 69 D6 6E 24 E3 24 24 24 E3 E3 E3 E3 24 FB 92 24 D6 69 69 24 24 92 FB FB 24 E3 E3 E3 E3 24 FB FB 92 24 24 92 FB FB 92 92 FB 24 E3 E3 E3 E3 24 92 FB FB FB FB 92 24 24 69 92 FB 24 E3 E3 24 24 24 24 24 24 24 24 E3 E3 24 6E FB 24 E3 24 6E D6 FB FB FB 92 24 24 E3 E3 24 92 FB 24 E3 24 D6 6E 24 24 92 FB FB 92 24 24 92 FB 24 E3 E3 24 92 69 69 6E 24 24 92 FB FB FB FB 24 E3 E3 E3 E3 24 92 6E 69 24 E3 24 24 24 24 24 E3 E3 E3 E3 E3 E3 24 24 24 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3 E3



# command> help tbblue-get-sprite

    Syntax: tbblue-get-sprite index [items]

    Description
    Get sprites at index, if not specified items parameters, returns only one. Returned values are in hexadecimal format. Only allowed on machine TBBlue

    command> tbblue-get-sprite 1 10
    9E 7E 00 81
    F4 28 00 82
    92 28 00 83
    E4 0A 00 84
    82 9F 00 85
    90 91 00 86
    84 19 00 87
    E4 0A 00 88
    02 17 01 89
    78 12 00 8A


# command> help disable-breakpoints
    Syntax: disable-breakpoints

    Description
    Disable all breakpoints


# command> help set-breakpoint
    Syntax: set-breakpoint|sb index [condition]

    Description
    Sets a breakpoint at desired index entry with condition. If no condition set, breakpoint will be handled as disabled
    A condition breakpoint has the following format:
    [VARIABLE][CONDITION][VALUE] [OPERATOR] [VARIABLE][CONDITION][VALUE] [OPERATOR] .... where:
    [VARIABLE] can be a CPU register or some pseudo variables: A,B,C,D,E,F,H,L,AF,BC,DE,HL,A',B',C',D',E',F',H',L',AF',BC',DE',HL',I,R,SP,PC,IX,IY
    FS,FZ,FP,FV,FH,FN,FC: Flags
    (BC),(DE),(HL),(SP),(PC),(IX),(IY), (NN), IFF1, IFF2, OPCODE,
    RAM: RAM mapped on 49152-65535 on Spectrum 128 or Prism,
    ROM: ROM mapped on 0-16383 on Spectrum 128,
    SEG0, SEG1, SEG2, SEG3: memory banks mapped on each 4 memory segments on Z88
    MRV: value returned on read memory operation
    MWV: value written on write memory operation
    MRA: address used on read memory operation
    MWA: address used on write memory operation
    PRV: value returned on read port operation
    PWV: value written on write port operation
    PRA: address used on read port operation
    PWA: address used on write port operation

    ENTERROM: returns 1 the first time PC register is on ROM space (0-16383)
    EXITROM: returns 1 the first time PC register is out ROM space (16384-65535)
    Note: The last two only return 1 the first time the breakpoint is fired, or a watch is shown, it will return 1 again only exiting required space address and entering again

    TSTATES: t-states total in a frame
    TSTATESL: t-states in a scanline
    TSTATESP: t-states partial
    SCANLINE: scanline counter

    [CONDITION] must be one of: <,>,=,/  (/ means not equal)
    [VALUE] must be a numeric value
    [OPERATOR] must be one of the following: and, or, xor
    Examples of conditions:
    SP<32768 : it will match when SP register is below 32768
    FS=1: it will match when flag S is set
    A=10 and BC<33 : it will match when A register is 10 and BC is below 33
    OPCODE=ED4AH : it will match when running opcode ADC HL,BC
    OPCODE=21H : it will match when running opcode LD HL,NN
    OPCODE=210040H : it will match when running opcode LD HL,4000H
    SEG2=40H : when memory bank 40H is mapped to memory segment 2 (49152-65535 range) on Z88
    MWA<16384 : it will match when attempting to write in ROM
    ENTERROM=1 : it will match when entering ROM space address
    TSTATESP>69888 : it will match when partial counter has executed a 48k full video frame (you should reset it before)

    Note: Any condition in the whole list can trigger a breakpoint



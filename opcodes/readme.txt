Format of .tbl is:

    START>
      | 0        | 1        | 2 
    0 | instr 01 |          | instr 02
    1 |          | instr 11 | >NEXT
    2 |          | instr 21 |
    5 |          |          | instr 52

    NEXT>
      | 0           | 1           | 2 
    0 | instr 12-01 |             | instr 12-02
    1 |             | instr 12-11 | 
    2 |             | instr 12-21 |

"START>" "NEXT>" - these are the names of the tables

Column is high part of byte, row is low part of byte

Any rows/columns may be left out for clarity, so the row/column headings must be present

Disassembler always starts with table named "START"


each "entry" can be:

    ">NEXT"     further bytes are interpreted by the NEXT table

    ">NEXT {b}" as above, but the next byte is saved for use later

    "text"      the opcode text to be used  
                {b} immediate byte            e.g. LD A, {b}
                {w} immediate word            e.g. LD HL, {w}
                {data} 16-bit data address    e.g. LD HL, ({data})
                {code} 16-bit code address    e.g. CALL {code}
                {+b} 8-bit +/- code offset    e.g. JR {+b}
                {+i} 8-bit +/- indexer offset e.g. LD (IX+{+i}), A
               
    blank       invalid opcode



This layout is easy to read and follows the layout from http://clrhome.org/table/
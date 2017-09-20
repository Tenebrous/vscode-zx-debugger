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
                {b} immediate byte
                {w} immediate word
                {data} 16-bit data address
                {code} 16-bit code address
                {+b} 8-bit +/- offset
               
    blank       invalid opcode



This layout is easy to read and follows the layout from http://clrhome.org/table/
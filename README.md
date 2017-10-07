Source for the ZX Debugger extension:
* https://marketplace.visualstudio.com/items?itemName=Tenebrous.vscode-zx-debugger


# Notes

During initialization, vscode provides the 'linesStartAt1' setting.

This only applies to lines sent/received through standard vscode debug requests/responses/events.

Any lines sent/received through _custom_ debug requests/events (listed below) are unaffected and are always be 0-based.


# Custom requests

## getDisassemblyForSource

arg | description
--- | ---
file | file system path to source file
line | 0-based line number within souce file

## getSourceFromDisassembly

arg | description
--- | ---
file | file system path to disassembly file
line | 0-based line number within disassembly file

## setNextStatement

arg | description
--- | ---
file | file system path to file
line | 0-based line number within file

# Custom events

## setDisassemblyLine

arg | description
--- | ---
line | 0-based line number to be highlighted within the disassembly file

Source for the ZX Debugger extension:
* https://marketplace.visualstudio.com/items?itemName=Tenebrous.vscode-zx-debugger


See also:
* [Capabilities](docs/capabilities.md)<br/>list of capabilities that can/should be provided by remote connections
* [Debugger](docs/debugger.md)<br/>guide to subclassing Debugger to provide a device-specific connection
* [ZEsarUX](docs/zesarux.md)<br/>overview of how Debugger is subclassed to connect to ZEsarUX


# Notes

During initialization, vscode provides the 'linesStartAt1' setting.

This setting is only used with values sent/received through standard vscode debug requests/responses/events - Any line & column numbers used in _custom_ debug requests/events (listed below) are unaffected and are always be 0-based.


# Custom requests

## getDefinition

arg | description
--- | ---
file | file system path to source file
line | 0-based line number of cursor within file
column | 0-based column of cursor within file
text | whole line from file
symbol | symbol at cursor position

## getHover

arg | description
--- | ---
file | file system path to source file
line | 0-based line number of cursor within file
column | 0-based column of cursor within file
text | whole line from file
symbol | symbol at cursor position

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

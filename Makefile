ZESARUX_DEBUG = bin/vscode-zesarux-debug.exe

all: $ZESARUX_DEBUG

clean:
	rm -rf bin/
	xbuild /p:Configuration=Release /t:Clean

$ZESARUX_DEBUG:
	xbuild /p:Configuration=Release

zip: $ZESARUX_DEBUG
	rm -f vscode-zesarux-debug.zip
	zip -r9 vscode-zesarux-debug.zip bin/ attach.js package.json -x "*.DS_Store"

vsix: clean $ZESARUX_DEBUG
	rm -f *.vsix
	vsce package
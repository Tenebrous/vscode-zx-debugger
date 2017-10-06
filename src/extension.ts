'use strict';

import * as vscode from 'vscode';

export function activate( context: vscode.ExtensionContext ) {

    context.subscriptions.push(
        vscode.debug.registerDebugConfigurationProvider(
            'zxdebug',
            new ZXDebugConfigurationProvider()
        )
    )

	context.subscriptions.push(
		vscode.commands.registerCommand('extension.zxdebug.setNextStatement',
			args => setNextStatement(args)
		)	
    );
    
	context.subscriptions.push(
        vscode.debug.onDidReceiveDebugSessionCustomEvent(e => {
            if( e.event == 'setDisassemblyLine' ) {
                setDisassemblyLine( e.body );
            }
        })
    );

	context.subscriptions.push(
            vscode.languages.registerHoverProvider(
            '*',
            new HoverProvider()
        )
    );

    context.subscriptions.push(
        vscode.languages.registerDefinitionProvider(
            '*',
            new DefinitionProvider()
        )
    );
}

function setNextStatement( args ) : Thenable<any> {
    
    if( vscode.debug.activeDebugSession === undefined 
     || vscode.window.activeTextEditor === undefined
     || args === undefined )
        return Promise.resolve(null);

    return vscode.debug.activeDebugSession.customRequest(
        "setNextStatement",
        { 
            file: args.fsPath, 
            line: vscode.window.activeTextEditor.selection.start.line 
        }
    ).then( reply => {
        // ok
    }, err => {
        throw err;
    });
}

var decorators : vscode.TextEditorDecorationType[] = [];
function setDisassemblyLine( args ) {

    decorators.forEach(decorator => {
        decorator.dispose();
    });

    vscode.window.visibleTextEditors.forEach( editor => {
        
        var document = editor.document;
        
        if( document.fileName.endsWith('disasm.zdis') )
        {
            var decorator = vscode.window.createTextEditorDecorationType(
            {
                isWholeLine: true,
                backgroundColor: new vscode.ThemeColor('debugExceptionWidget.background')
            });
                
            var decRange = new vscode.Range(args.line,0,args.line,0);
            var revealRange = new vscode.Range(args.line-3,0,args.line+3,0);
            
            editor.setDecorations(
                decorator,
                [decRange]
            );

            editor.revealRange( revealRange );

            var pos = new vscode.Position(args.line, 0);
            editor.selection = new vscode.Selection(pos, pos);

            decorators.push( decorator );
        }

    });
}

class DefinitionProvider implements vscode.DefinitionProvider {
    provideDefinition(
        document: vscode.TextDocument,
        position: vscode.Position,
        token: vscode.CancellationToken) : vscode.ProviderResult<vscode.Definition>
    {
        if( vscode.debug.activeDebugSession === undefined )
            return undefined;
       
        let request = "getDisassemblyForSource";
        
        if( document.fileName.endsWith( ".zdis" ) )
            request = "getSourceFromDisassembly";
        
        return vscode.debug.activeDebugSession.customRequest(
            request,
            { file: document.fileName, line: position.line }
        ).then( reply => {
            return new vscode.Location(
                vscode.Uri.file( reply.file ),
                new vscode.Range(
                    reply.startLine, 0,
                    reply.endLine, 9999
                )
            );
        }, err => {
            throw err;
        });
    }
}

class HoverProvider implements vscode.HoverProvider {
    provideHover(
        document: vscode.TextDocument, 
        position: vscode.Position, 
        token: vscode.CancellationToken): vscode.ProviderResult<vscode.Hover> {
        
            // console.log( document.fileName + " " + position.line + " " );
            //return new vscode.Hover("hello");
            return undefined;

    }
}

class ZXDebugConfigurationProvider implements vscode.DebugConfigurationProvider {
    
      /**
       * returns initial debug configurations.
       */
      provideDebugConfigurations(
          folder: vscode.WorkspaceFolder | undefined, 
          token?: vscode.CancellationToken): vscode.ProviderResult<vscode.DebugConfiguration[]> {
    
        const config = {
            "type": "zxdebug",
            "name": "ZX Debugger",
            "request": "attach",
            "projectFolder": "${workspaceRoot}",
            "sourceMaps": [
                "48k_rom.dbg"
            ],
            "opcodeTables": [
                "z80.tbl"
            ],
            "stopOnEntry": true
        }
        
        return [ config ];
      }
    
      /**
       * "massage" launch configuration before starting the session.
       */
      resolveDebugConfiguration(
          folder: vscode.WorkspaceFolder | undefined, 
          config: vscode.DebugConfiguration, 
          token?: vscode.CancellationToken): vscode.ProviderResult<vscode.DebugConfiguration> {

        console.log( config );
        
        config.workspaceConfiguration = vscode.workspace.getConfiguration('zxdebug');

        console.log( config );
        
        return config;
      }
    }
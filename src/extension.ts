'use strict';

import * as vscode from 'vscode';
import * as fs     from 'fs';
import * as path   from 'path';

export function activate(context: vscode.ExtensionContext) {

	context.subscriptions.push(
		vscode.commands.registerCommand('extension.zxdebug.startSession',
			config => startSession(config)
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

function startSession(config) {

    // provide all the workspace config items to the adapter by
    // adding them to the launch/attach config it already gets
    config.workspaceConfiguration = vscode.workspace.getConfiguration('zxdebug');

    vscode.commands.executeCommand('vscode.startDebug', config);

    return { status: 'ok' };
}

var decorators=[];
function setDisassemblyLine( data ) {

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
                borderWidth: '1px',
                borderStyle: 'solid',
                light: {
                    borderColor: 'black'
                },
                dark: {
                    borderColor: 'white'
                }
            });
                
            var decRange = new vscode.Range(data.line-1,0,data.line-1,0);
            var revealRange = new vscode.Range(data.line-3,0,data.line+2,0);
            
            editor.setDecorations(
                decorator,
                [decRange]
            );

            editor.revealRange( revealRange );

            var pos = new vscode.Position(data.line-1, 0);
            editor.selection = new vscode.Selection(pos, pos);

            decorators.push( decorator );
        }

    });
}

class DefinitionProvider implements vscode.DefinitionProvider {
    provideDefinition(
        document: vscode.TextDocument,
        position: vscode.Position,
        token: vscode.CancellationToken): Thenable<vscode.Location>
    {
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

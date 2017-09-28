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

	// context.subscriptions.push(
    //     vscode.debug.onDidReceiveDebugSessionCustomEvent(e => {
    //         if( e.event == 'showHeatMap' ) {
    //             showHeatMap( e.body );
    //         }
    //     })
    // );

	context.subscriptions.push(
        vscode.debug.onDidReceiveDebugSessionCustomEvent(e => {
            if( e.event == 'showDisassembly' ) {
                showDisassembly( e.body );
            }
        })
    );

    // vscode.languages.registerDefinitionProvider(
    //     {pattern: "*.{c,asm}"},
    //     new DefinitionProvider()
    // );
}

function startSession(config) {

    // provide all the workspace config items to the adapter by
    // adding them to the launch/attach config it already gets
    config.workspaceConfiguration = vscode.workspace.getConfiguration('zxdebug');

    vscode.commands.executeCommand('vscode.startDebug', config);

    return { status: 'ok' };
}

// var decs=[];
// function showHeatMap( data ) {

//     decs.forEach(dec => {
//         dec.dispose();
//     });

//     vscode.window.visibleTextEditors.forEach( editor => {
        
//         var document = editor.document;

//         if( document.fileName.endsWith('disasm.zdis') )
//         {
//             for (var lineNumber = 0; lineNumber < document.lineCount; lineNumber++) {
                
//                 var annotation = '';

//                 if( (lineNumber+1) in data )
//                     annotation = '' + data[lineNumber+1];

//                 var dec = vscode.window.createTextEditorDecorationType({isWholeLine:true, before:{contentText: annotation, width: '150px'}});
                
//                 editor.setDecorations(
//                     dec,
//                     [new vscode.Range(lineNumber,0,lineNumber,0)]
//                 );

//                 decs.push( dec );
//             }
//         }

//     });
// }

function showDisassembly( data ) {
    console.log( "showDisassembly: " + data );
}

// class DefinitionProvider implements vscode.DefinitionProvider {
//     provideDefinition(
//         document: vscode.TextDocument,
//         position: vscode.Position,
//         token: vscode.CancellationToken): vscode.Definition
//     {
//         return new vscode.Location( 
//             vscode.Uri.file(".zxdbg\\disasm.zdis"), 
//             new vscode.Position( 10,10 )
//         );
//     }
// }
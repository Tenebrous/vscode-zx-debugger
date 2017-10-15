'use strict';

import * as vscode from 'vscode';

export default class DefinitionProvider implements vscode.DefinitionProvider {
    provideDefinition(
        document: vscode.TextDocument,
        position: vscode.Position,
        token: vscode.CancellationToken) : vscode.ProviderResult<vscode.Definition>
    {
        if( vscode.debug.activeDebugSession === undefined )
            return undefined;
       
        let wordRange = document.getWordRangeAtPosition(position);
        let lineRange = new vscode.Range( position.line, 0, position.line, 9999 );
        
        return vscode.debug.activeDebugSession.customRequest(
            "getDefinition",
            { 
                file:   document.fileName, 
                line:   position.line,
                column: position.character,
                text:   document.getText( lineRange ),
                symbol: document.getText( wordRange ),
            }
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

